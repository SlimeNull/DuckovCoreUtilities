using Duckov.Modding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckovModBehaviour = Duckov.Modding.ModBehaviour;

namespace SlimeNull.DuckovModSettings.Core
{
    internal sealed class SettingsCatalog
    {
        private readonly Dictionary<string, Snapshot> _snapshots = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        private ModSettingsModel[] _mods = Array.Empty<ModSettingsModel>();
        private bool _batching;
        private readonly HashSet<ComponentSettingsModel> _batchedValidation = new HashSet<ComponentSettingsModel>();

        public IReadOnlyList<ModSettingsModel> Mods => _mods;

        public event Action? StructureChanged;
        public event Action<SettingNode, SettingChangeOrigin>? ValueChanged;
        public event Action<ComponentSettingsModel>? UserEdited;

        public IEnumerator RefreshIncrementally(Action<int, int>? reportProgress = null)
        {
            var roots = GetActiveRoots();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var changed = false;
            reportProgress?.Invoke(0, roots.Count);

            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];
                if (root == null || string.IsNullOrWhiteSpace(root.info.name) || !root.gameObject.scene.IsValid())
                {
                    reportProgress?.Invoke(index + 1, roots.Count);
                    yield return null;
                    continue;
                }

                var key = GetRootKey(root);
                if (!seen.Add(key))
                {
                    reportProgress?.Invoke(index + 1, roots.Count);
                    yield return null;
                    continue;
                }

                var fingerprint = BuildFingerprint(root);
                if (_snapshots.TryGetValue(key, out var existing) && existing.Fingerprint == fingerprint)
                {
                    reportProgress?.Invoke(index + 1, roots.Count);
                    yield return null;
                    continue;
                }

                var model = ReflectionSettingsScanner.Scan(root);
                if (model != null)
                {
                    AttachAndLoad(model);
                }
                _snapshots[key] = new Snapshot(root, fingerprint, model);
                changed = true;
                reportProgress?.Invoke(index + 1, roots.Count);
                yield return null;
            }

            foreach (var staleKey in _snapshots.Keys.Where(key => !seen.Contains(key)).ToArray())
            {
                _snapshots.Remove(staleKey);
                changed = true;
            }

            _mods = _snapshots.Values
                .Select(snapshot => snapshot.Model)
                .Where(model => model != null)
                .Cast<ModSettingsModel>()
                .OrderBy(model => model.DisplayName, StringComparer.Create(LocalizedText.Culture, ignoreCase: true))
                .ToArray();
            if (changed)
            {
                StructureChanged?.Invoke();
            }
        }

        public void ObserveExternalChanges()
        {
            foreach (var mod in _mods)
            {
                foreach (var component in mod.Components)
                {
                    foreach (var node in component.Leaves)
                    {
                        node.TryObserveExternalChange();
                    }
                }
            }
        }

        public void RefreshLocalization()
        {
            _mods = _mods
                .OrderBy(model => model.DisplayName, StringComparer.Create(LocalizedText.Culture, ignoreCase: true))
                .ToArray();
            StructureChanged?.Invoke();
        }

        private static List<DuckovModBehaviour> GetActiveRoots()
        {
            var roots = new List<DuckovModBehaviour>(ModManager.modInfos.Count);
            foreach (var info in ModManager.modInfos)
            {
                if (ModManager.IsModActive(info, out var root) && root != null)
                {
                    roots.Add(root);
                }
            }
            return roots;
        }

        public void Reset(ModSettingsModel mod)
        {
            _batching = true;
            try
            {
                foreach (var node in mod.Components.SelectMany(component => component.Leaves))
                {
                    node.Reset();
                }
            }
            finally
            {
                _batching = false;
                foreach (var component in _batchedValidation)
                {
                    component.InvokeOnValidate();
                    foreach (var node in component.Leaves)
                    {
                        node.ObserveCurrentValue();
                        SettingsStore.Set(node.Owner.Mod.Info, node.StoreKey, node.GetValue(), node.ValueType);
                    }
                    UserEdited?.Invoke(component);
                }
                _batchedValidation.Clear();
            }
        }

        private void AttachAndLoad(ModSettingsModel mod)
        {
            foreach (var component in mod.Components)
            {
                var loadedAny = false;
                foreach (var node in component.Leaves)
                {
                    node.ValueChanged += HandleValueChanged;
                    if (SettingsStore.TryGet(mod.Info, node.StoreKey, node.FormerKeys, node.ValueType, out var value) &&
                        node.TrySetValue(value, SettingChangeOrigin.Load))
                    {
                        loadedAny = true;
                    }
                    node.ObserveCurrentValue();
                }

                if (loadedAny)
                {
                    component.InvokeOnValidate();
                }
            }
        }

        private void HandleValueChanged(SettingNode node, SettingChangeOrigin origin)
        {
            if (origin == SettingChangeOrigin.User || origin == SettingChangeOrigin.Reset)
            {
                if (_batching)
                {
                    _batchedValidation.Add(node.Owner);
                }
                else
                {
                    node.Owner.InvokeOnValidate();
                    node.ObserveCurrentValue();
                    UserEdited?.Invoke(node.Owner);
                }
            }

            if (origin != SettingChangeOrigin.Load)
            {
                SettingsStore.Set(node.Owner.Mod.Info, node.StoreKey, node.GetValue(), node.ValueType);
            }

            ValueChanged?.Invoke(node, origin);
        }

        private static string GetRootKey(DuckovModBehaviour root)
        {
            return root.gameObject.GetInstanceID() + ":" + (root.GetType().Assembly.FullName ?? root.GetType().Assembly.GetName().Name);
        }

        private static string BuildFingerprint(DuckovModBehaviour root)
        {
            try
            {
                return string.Join(",", root.GetComponents<MonoBehaviour>()
                    .Where(component => component != null && component.GetType().Assembly == root.GetType().Assembly)
                    .Select(component => component.GetInstanceID().ToString())
                    .OrderBy(value => value, StringComparer.Ordinal));
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class Snapshot
        {
            public Snapshot(DuckovModBehaviour root, string fingerprint, ModSettingsModel? model)
            {
                Root = root;
                Fingerprint = fingerprint;
                Model = model;
            }

            public DuckovModBehaviour Root { get; }
            public string Fingerprint { get; }
            public ModSettingsModel? Model { get; }
        }
    }
}
