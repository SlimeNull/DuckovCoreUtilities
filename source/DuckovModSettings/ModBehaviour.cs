using Duckov.Modding;
using Duckov.Options.UI;
using HarmonyLib;
using SlimeNull.DuckovModSettings.Core;
using SlimeNull.DuckovModSettings.UI;
using SodaCraft.Localizations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckovModBehaviour = Duckov.Modding.ModBehaviour;

namespace SlimeNull.DuckovModSettings
{
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "SlimeNull.DuckovModSettings";
        private readonly HashSet<GameObject> _editedObjects = new HashSet<GameObject>();
        private readonly HashSet<int> _hydratedRoots = new HashSet<int>();
        private SettingsCatalog? _catalog;
        private SettingsPanelInjector? _injector;
        private Harmony? _harmony;
        private readonly HashSet<SettingsPage> _openPages = new HashSet<SettingsPage>();
        private Coroutine? _catalogRefreshCoroutine;
        private Coroutine? _attachPanelsCoroutine;
        private bool _catalogRefreshRequested;
        private float _nextValuePoll;

        internal static ModBehaviour? Instance { get; private set; }

        protected override void OnAfterSetup()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _catalog = new SettingsCatalog();
            _catalog.UserEdited += OnUserEdited;
            _injector = new SettingsPanelInjector(_catalog, OnSettingsPageOpened, OnSettingsPageClosed);
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(ModBehaviour).Assembly);
            ModManager.OnModActivated += OnModActivated;
            ModManager.OnModWillBeDeactivated += OnModWillBeDeactivated;
            LocalizedText.SetLanguage(
                LocalizationManager.Initialized ? LocalizationManager.CurrentLanguage : Application.systemLanguage);
            LocalizationManager.OnSetLanguage += OnLanguageChanged;

            _catalogRefreshRequested = true;
            _attachPanelsCoroutine = StartCoroutine(AttachExistingPanelsAfterStart());
            Debug.Log("[DuckovModSettings] Loaded reflection-based mod settings.");
        }

        protected override void OnBeforeDeactivate()
        {
            ModManager.OnModActivated -= OnModActivated;
            ModManager.OnModWillBeDeactivated -= OnModWillBeDeactivated;
            LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            StopCatalogRefresh();
            if (_attachPanelsCoroutine != null)
            {
                StopCoroutine(_attachPanelsCoroutine);
                _attachPanelsCoroutine = null;
            }

            _injector?.Dispose();
            FlushUpdatedMessages();
            SettingsStore.SaveNow();

            if (_catalog != null)
            {
                _catalog.UserEdited -= OnUserEdited;
            }
            _harmony?.UnpatchAll(HarmonyId);
            _injector = null;
            _catalog = null;
            _harmony = null;
            _editedObjects.Clear();
            _hydratedRoots.Clear();
            _openPages.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (_openPages.Count > 0 && _catalog != null && _catalogRefreshCoroutine == null && now >= _nextValuePoll)
            {
                _nextValuePoll = now + 0.5f;
                _catalog.ObserveExternalChanges();
            }
            SettingsStore.SaveIfDue(now);
        }

        internal void AttachOptionsPanel(OptionsPanel panel)
        {
            try
            {
                _injector?.Attach(panel);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovModSettings] Could not attach to OptionsPanel: {ex}");
            }
        }

        private IEnumerator AttachExistingPanelsAfterStart()
        {
            yield return null;
            try
            {
                _injector?.AttachExistingPanels();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovModSettings] Could not find existing OptionsPanel instances: {ex}");
            }

            foreach (var info in ModManager.modInfos.ToArray())
            {
                if (ModManager.IsModActive(info, out var root) && root != null)
                {
                    HydrateRoot(root);
                    yield return null;
                }
            }
            _attachPanelsCoroutine = null;
        }

        private void OnSettingsPageOpened(SettingsPage page)
        {
            _openPages.Add(page);
            RequestCatalogRefresh();
        }

        private void OnSettingsPageClosed(SettingsPage page)
        {
            _openPages.Remove(page);
            FlushUpdatedMessages();
            if (_openPages.Count == 0)
            {
                StopCatalogRefresh();
                _catalogRefreshRequested = true;
            }
        }

        private void OnModActivated(ModInfo info, DuckovModBehaviour root)
        {
            HydrateRoot(root);
            RequestCatalogRefreshIfOpen();
        }

        private void OnModWillBeDeactivated(ModInfo info, DuckovModBehaviour root)
        {
            if (root != null)
            {
                _hydratedRoots.Remove(root.GetInstanceID());
            }
            RequestCatalogRefreshIfOpen();
        }

        private void OnLanguageChanged(SystemLanguage _)
        {
            LocalizedText.SetLanguage(LocalizationManager.CurrentLanguage);
            _catalog?.RefreshLocalization();
            _injector?.RefreshLocalization();
            ColorPickerDialog.RefreshCurrentLocalization();
        }

        private void HydrateRoot(DuckovModBehaviour root)
        {
            if (root == null || !_hydratedRoots.Add(root.GetInstanceID()))
            {
                return;
            }

            try
            {
                SettingsStore.ApplyPersistedValues(root);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovModSettings] Could not restore settings for '{root.info.name}': {ex}");
            }
        }

        private void RequestCatalogRefreshIfOpen()
        {
            _catalogRefreshRequested = true;
            if (_openPages.Count > 0)
            {
                foreach (var page in _openPages)
                {
                    if (page != null)
                    {
                        page.BeginLoading();
                    }
                }
                StartCatalogRefresh();
            }
        }

        private void RequestCatalogRefresh()
        {
            _catalogRefreshRequested = true;
            StartCatalogRefresh();
        }

        private void StartCatalogRefresh()
        {
            if (!_catalogRefreshRequested || _catalogRefreshCoroutine != null || _catalog == null || _openPages.Count == 0)
            {
                return;
            }

            _catalogRefreshRequested = false;
            _catalogRefreshCoroutine = StartCoroutine(RefreshCatalogIncrementally());
        }

        private IEnumerator RefreshCatalogIncrementally()
        {
            yield return null;

            var refresh = _catalog!.RefreshIncrementally(ReportLoadingProgress);
            Exception? failure = null;
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = refresh.MoveNext();
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }

                if (!hasNext)
                {
                    break;
                }
                yield return refresh.Current;
            }
            (refresh as IDisposable)?.Dispose();

            _catalogRefreshCoroutine = null;
            if (failure != null)
            {
                Debug.LogError($"[DuckovModSettings] Settings scan failed: {failure}");
            }

            if (_catalogRefreshRequested && _openPages.Count > 0)
            {
                StartCatalogRefresh();
                yield break;
            }

            foreach (var page in _openPages.ToArray())
            {
                if (page != null)
                {
                    page.CompleteLoading();
                }
            }
            _nextValuePoll = Time.unscaledTime + 0.5f;
        }

        private void ReportLoadingProgress(int processed, int total)
        {
            foreach (var page in _openPages)
            {
                if (page != null)
                {
                    page.ReportLoadingProgress(processed, total);
                }
            }
        }

        private void StopCatalogRefresh()
        {
            if (_catalogRefreshCoroutine != null)
            {
                StopCoroutine(_catalogRefreshCoroutine);
                _catalogRefreshCoroutine = null;
            }
        }

        private void OnUserEdited(ComponentSettingsModel component)
        {
            if (component.Target != null)
            {
                _editedObjects.Add(component.Target.gameObject);
            }
        }

        internal void FlushUpdatedMessages()
        {
            if (_editedObjects.Count == 0)
            {
                return;
            }

            SettingsStore.SaveNow();
            foreach (var target in _editedObjects)
            {
                if (target == null)
                {
                    continue;
                }

                try
                {
                    target.SendMessage("DuckovModSettingsUpdated", SendMessageOptions.DontRequireReceiver);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DuckovModSettings] DuckovModSettingsUpdated failed for '{target.name}': {ex}");
                }
            }
            _editedObjects.Clear();
        }

    }
}
