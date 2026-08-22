using Duckov.UI;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Collections;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using UnityEngine;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features.Abstraction
{
    public abstract class ItemDecorateFeature : FeatureBase
    {
        private const string PatchCategory = nameof(ItemDecorateFeature);
        private static readonly WeakCollection<ItemDecorateFeature> _createdFeatures = new WeakCollection<ItemDecorateFeature>();

        public ItemDecorateFeature()
        {
            _createdFeatures.Add(this);
        }

        protected override void OnEnable()
        {
            Context.Harmony.PatchCategory(PatchCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(PatchCategory);
        }

        protected abstract void DecorateItemDisplay(ItemDisplay itemDisplay);

        private class ItemDecorateComponent : MonoBehaviour
        {
            private ItemDecorateFeature? _feature;
            private ItemDisplay? _decorateTarget;

            public void Initialize(ItemDecorateFeature feature, ItemDisplay decorateTarget)
            {
                _feature = feature;
                _decorateTarget = decorateTarget;
            }

            void Start()
            {
                if (_feature is not null &&
                    _decorateTarget is not null)
                {
                    _feature.DecorateItemDisplay(_decorateTarget);
                }
            }
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyLib.HarmonyPatch(typeof(ItemDisplay), "OnEnable")]
        private static class HarmonyPatch
        {
            private static void Postfix(ItemDisplay __instance)
            {
                foreach (var feature in _createdFeatures)
                {
                    try
                    {
                        __instance.GetOrAddComponent<ItemDecorateComponent>().Initialize(feature, __instance);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error while decorating ItemDisplay in {feature.GetType().Name}: {ex}");
                    }
                }
            }
        }
    }
}
