using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Infrastructure
{
    internal sealed class FeatureHost
    {
        private readonly FeatureContext _context;
        private readonly Dictionary<FeatureBase, FeatureStatus> _features = new Dictionary<FeatureBase, FeatureStatus>();

        private record class FeatureStatus
        {
            public bool IsEnabled { get; set; }
        }

        public FeatureHost(GameObject hostObject)
        {
            _context = new FeatureContext(hostObject, new Harmony("slimenull.duckov.core-utilities"));
        }

        public void Register(FeatureBase feature)
        {
            _features.Add(feature, new FeatureStatus());
        }

        public void EnableAll()
        {
            foreach (var featureKV in _features)
            {
                if (featureKV.Value.IsEnabled)
                {
                    continue;
                }

                try
                {
                    featureKV.Key.Enable(_context);
                    featureKV.Value.IsEnabled = true;
                    Debug.Log($"enabled {featureKV.Key.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"failed to enable {featureKV.Key.Name}, {ex}");
                }
            }
        }

        public void DisableAll()
        {
            foreach (var featureKV in _features)
            {
                if (!featureKV.Value.IsEnabled)
                {
                    continue;
                }

                try
                {
                    featureKV.Key.Disable();
                    featureKV.Value.IsEnabled = false;
                    Debug.Log($"disabled {featureKV.Key.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"failed to disable {featureKV.Key.Name}, {ex}");
                }
            }
        }

        public void Tick()
        {
            foreach (var featureKV in _features)
            {
                try
                {
                    featureKV.Key.Tick();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"tick failed in {featureKV.Key.Name}, {ex}");
                }
            }
        }

        public void OnGUI()
        {
            foreach (var featureKV in _features)
            {
                try
                {
                    featureKV.Key.OnGUI();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"OnGUI failed in {featureKV.Key.Name}, {ex}");
                }
            }
        }
    }
}
