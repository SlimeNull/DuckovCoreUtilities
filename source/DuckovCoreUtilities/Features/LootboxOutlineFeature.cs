using EPOOutline;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class LootboxOutlineFeature : FeatureBase
    {
        private const string HarmonyCatagory = nameof(LootboxOutlineFeature);

        public override string Name => "Loot outline";

        public int ActivationDistance { get; set; } = 10;
        public bool UseQualityColor { get; set; } = true;

        protected override void OnEnable()
        {
            Context.Harmony.PatchCategory(HarmonyCatagory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCatagory);
        }

        private static void EnsureOutlineAttached(InteractableLootbox instance)
        {
            var outline = instance.GetOrAddComponent<LootOutlinable>();
        }

        private class LootOutlinable : MonoBehaviour
        {
            private InteractableLootbox? lootBox;
            private Outlinable? outlinable;
            private CharacterMainControl? _player;

            public int ActivationDistance { get; set; } = 10;
            public bool UseQualityColor { get; set; } = true;

            void Start()
            {
                lootBox = GetComponent<InteractableLootbox>();
                if (ShouldApplyOutline(lootBox))
                {
                    AttachOutline();
                }
            }

            void Update()
            {
                if (outlinable is null)
                {
                    return;
                }

                _player ??= LevelManager.Instance.MainCharacter;
                if (_player is null)
                {
                    return;
                }

                outlinable.enabled = Vector3.Distance(transform.position, _player.transform.position) < ActivationDistance;
            }

            private void AttachOutline()
            {
                outlinable = gameObject.AddComponent<Outlinable>();
                Debug.Log($"[LootboxOutlineFeature] Added outline to {gameObject.name}");

                AddLootboxRenderers(outlinable);
                outlinable.OutlineParameters.Enabled = true;
                outlinable.OutlineParameters.Color = Color.white;

                if (UseQualityColor && lootBox is not null)
                {
                    int maxQuality = 0;
                    foreach (var item in lootBox.Inventory)
                    {
                        if (item.StackCount == 0)
                        {
                            continue;
                        }

                        maxQuality = Math.Max(maxQuality, item.Quality);
                    }

                    var color = QualityColor.Get(maxQuality);
                    color.a = 1;
                    outlinable.OutlineParameters.Color = color;
                }

                outlinable.enabled = false;
            }

            private void AddLootboxRenderers(Outlinable outlinable)
            {
                var renderers = GetComponentsInChildren<Renderer>(false);
                foreach (var renderer in renderers)
                {
                    if (ShouldOutlineRenderer(renderer))
                    {
                        outlinable.AddRenderer(renderer);
                        Debug.Log($"[LootboxOutlineFeature] Added renderer {renderer.GetType().Name} to outline of {gameObject.name}");
                    }
                }
            }

            private static bool ShouldOutlineRenderer(Renderer renderer)
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.GetComponentInParent<InteractMarker>() != null ||
                    renderer.GetComponentInParent<Canvas>() != null)
                {
                    return false;
                }

                if (renderer is LineRenderer ||
                    renderer is TrailRenderer ||
                    renderer is ParticleSystemRenderer)
                {
                    return false;
                }

                if (renderer is MeshRenderer)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    return meshFilter != null && meshFilter.sharedMesh != null;
                }

                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    return skinnedMeshRenderer.sharedMesh != null;
                }

                return false;
            }

            private static bool ShouldApplyOutline(InteractableLootbox lootbox)
            {
                if (lootbox == null ||
                    !lootbox.isActiveAndEnabled ||
                    lootbox.Inventory.IsEmpty())
                {
                    return false;
                }

                if (lootbox.transform.Find("Prfb_Groundpile3") == null &&
                    lootbox.transform.Find("InteractMarker(Clone)") == null)
                {
                    return false;
                }

                if (lootbox.transform.Find("Storage") != null ||
                    lootbox.transform.Find("Inventory") != null)
                {
                    return false;
                }

                if (lootbox.GetComponentInChildren<InteractMarker>() == null)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatchCategory(HarmonyCatagory)]
        [HarmonyPatch(typeof(InteractableLootbox), "Start")]
        private static class LootBoxStartPatch
        {
            private static void Postfix(InteractableLootbox __instance)
            {
                EnsureOutlineAttached(__instance);
            }
        }
    }
}
