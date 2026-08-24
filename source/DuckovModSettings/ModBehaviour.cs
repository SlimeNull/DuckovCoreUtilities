using HarmonyLib;
using SlimeNull.DuckovModSettings.Core;
using SlimeNull.DuckovModSettings.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovModSettings
{
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "SlimeNull.DuckovModSettings";
        private readonly HashSet<GameObject> _editedObjects = new HashSet<GameObject>();
        private SettingsCatalog? _catalog;
        private SettingsPanelInjector? _injector;
        private Harmony? _harmony;
        private float _nextCatalogRefresh;
        private float _nextPanelRefresh;
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
            _injector = new SettingsPanelInjector(_catalog, FlushUpdatedMessages);
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(ModBehaviour).Assembly);

            _catalog.Refresh();
            _injector.Refresh();
            Debug.Log("[DuckovModSettings] Loaded reflection-based mod settings.");
        }

        protected override void OnBeforeDeactivate()
        {
            FlushUpdatedMessages();
            SettingsStore.SaveNow();

            if (_catalog != null)
            {
                _catalog.UserEdited -= OnUserEdited;
            }
            _injector?.Dispose();
            _harmony?.UnpatchAll(HarmonyId);
            _injector = null;
            _catalog = null;
            _harmony = null;
            _editedObjects.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (_catalog != null && now >= _nextCatalogRefresh)
            {
                _nextCatalogRefresh = now + 0.75f;
                _catalog.Refresh();
            }
            if (_injector != null && now >= _nextPanelRefresh)
            {
                _nextPanelRefresh = now + 0.75f;
                _injector.Refresh();
            }
            if (_catalog != null && now >= _nextValuePoll)
            {
                _nextValuePoll = now + 0.5f;
                _catalog.ObserveExternalChanges();
            }
            SettingsStore.SaveIfDue(now);
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

        internal void RefreshSettings()
        {
            _catalog?.Refresh();
            _injector?.Refresh();
        }
    }

    [HarmonyPatch(typeof(Duckov.Modding.ModBehaviour), nameof(Duckov.Modding.ModBehaviour.Setup))]
    internal static class ModSetupPatch
    {
        private static void Postfix()
        {
            ModBehaviour.Instance?.RefreshSettings();
        }
    }
}
