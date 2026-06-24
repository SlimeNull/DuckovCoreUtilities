using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovInterop.Infrastructure
{
    internal sealed class FeatureHost
    {
        private readonly FeatureContext _context;
        private readonly List<FeatureBase> _features = new List<FeatureBase>();


        public FeatureHost(GameObject hostObject)
        {
            _context = new FeatureContext(hostObject, new Harmony("slimenull.duckov.core-utilities"));
        }

        public void Register(FeatureBase feature)
        {
            if (feature is null)
            {
                throw new ArgumentNullException(nameof(feature));
            }

            _features.Add(feature);
        }

        public void EnableAll()
        {
            foreach (var feature in _features)
            {
                if (feature.IsEnabled)
                {
                    continue;
                }

                try
                {
                    feature.Enable(_context);
                    Debug.Log($"enabled {feature.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"failed to enable {feature.Name}, {ex}");
                }
            }
        }

        public void DisableAll()
        {
            foreach (var feature in _features)
            {
                try
                {
                    feature.Disable();
                    Debug.Log($"disabled {feature.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"failed to disable {feature.Name}, {ex}");
                }
            }
        }

        public void Tick()
        {
            foreach (var featureKV in _features)
            {
                try
                {
                    if (featureKV.IsEnabled)
                    {
                        featureKV.Tick();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"tick failed in {featureKV.Name}, {ex}");
                }
            }
        }

        public void OnGUI()
        {
            foreach (var featureKV in _features)
            {
                try
                {
                    if (featureKV.IsEnabled)
                    {
                        featureKV.OnGUI();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"OnGUI failed in {featureKV.Name}, {ex}");
                }
            }
        }
    }
}
