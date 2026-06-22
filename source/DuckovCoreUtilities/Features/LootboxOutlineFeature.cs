using EPOOutline;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class LootboxOutlineFeature : FeatureBase
    {
        private const string HarmonyCatagory = nameof(LootboxOutlineFeature);
        private static readonly List<SpriteRenderer> PickupSpriteRenderers = new List<SpriteRenderer>();

        public override string Name => "Loot outline";

        public int ActivationDistance { get; set; } = 10;
        public bool UseQualityColor { get; set; } = true;

        protected override void OnEnable()
        {
            Context.Harmony.PatchCategory(HarmonyCatagory);
            RenderPipelineManager.endCameraRendering += ResetPickupBillboards;
        }

        protected override void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= ResetPickupBillboards;
            Context.Harmony.UnpatchCategory(HarmonyCatagory);
            PickupSpriteRenderers.Clear();
        }

        private static void EnsureOutlineAttached(InteractableLootbox instance)
        {
            var outline = instance.GetOrAddComponent<LootOutlinable>();
        }

        private static void EnsureOutlineAttached(InteractablePickup instance)
        {
            var outline = instance.GetOrAddComponent<GroundItemOutlinable>();
        }

        private static void RegisterPickupSpriteRenderer(SpriteRenderer spriteRenderer)
        {
            if (!PickupSpriteRenderers.Contains(spriteRenderer))
            {
                PickupSpriteRenderers.Add(spriteRenderer);
            }
        }

        private static void OrientPickupBillboards()
        {
            PickupSpriteRenderers.RemoveAll(static renderer => renderer == null);

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            var cameraTransform = mainCamera.transform;
            foreach (var spriteRenderer in PickupSpriteRenderers)
            {
                var rendererTransform = spriteRenderer.transform;
                var direction = rendererTransform.position - cameraTransform.position;
                rendererTransform.rotation = Quaternion.LookRotation(direction.normalized, cameraTransform.rotation * Vector3.up);
                rendererTransform.localScale = new Vector3(0.14f, 0.14f, 1f);
            }
        }

        private static void ResetPickupBillboards(ScriptableRenderContext context, Camera camera)
        {
            PickupSpriteRenderers.RemoveAll(static renderer => renderer == null);

            foreach (var spriteRenderer in PickupSpriteRenderers)
            {
                spriteRenderer.transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }

        private abstract class OutlinableBehaviourBase : MonoBehaviour
        {
            protected Outlinable? Outlinable { get; set; }

            private CharacterMainControl? _player;

            public int ActivationDistance { get; set; } = 10;

            protected virtual void Update()
            {
                if (Outlinable is null)
                {
                    return;
                }

                _player ??= LevelManager.Instance.MainCharacter;
                if (_player is null)
                {
                    return;
                }

                Outlinable.enabled = Vector3.Distance(transform.position, _player.transform.position) < ActivationDistance;
            }

            protected static void ConfigureOutline(Outlinable outlinable, Color color)
            {
                outlinable.OutlineParameters.Enabled = true;
                outlinable.OutlineParameters.Color = color;
                outlinable.OutlineParameters.DilateShift = 3f;
                outlinable.enabled = false;
            }

            protected static bool ShouldOutlineRenderer(Renderer renderer)
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.GetComponentInParent<InteractMarker>() != null ||
                    renderer.GetComponentInParent<Canvas>() != null ||
                    renderer.GetComponent<SodaPointLight>() != null ||
                    renderer.name == "SodaPointLight")
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
                outlinable.OutlineParameters.DilateShift = 3f;

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
                    renderer.GetComponentInParent<Canvas>() != null ||
                    renderer.GetComponent<SodaPointLight>() != null ||
                    renderer.name == "SodaPointLight")
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

        private class GroundItemOutlinable : OutlinableBehaviourBase
        {
            private InteractablePickup? _pickup;

            private void Start()
            {
                _pickup = GetComponent<InteractablePickup>();
                if (ShouldApplyOutline(_pickup))
                {
                    AttachOutline();
                }
            }

            private void AttachOutline()
            {
                if (_pickup is null)
                {
                    return;
                }

                var spriteRenderer = _pickup.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    Outlinable = spriteRenderer.gameObject.GetComponent<Outlinable>() ??
                        spriteRenderer.gameObject.AddComponent<Outlinable>();
                    RegisterPickupSpriteRenderer(spriteRenderer);
                    Outlinable.AddRenderer(spriteRenderer);
                    ConfigureOutline(Outlinable, Color.white);
                    return;
                }

                Outlinable = gameObject.GetComponent<Outlinable>() ?? gameObject.AddComponent<Outlinable>();
                foreach (var renderer in GetComponentsInChildren<Renderer>(false))
                {
                    if (ShouldOutlineRenderer(renderer) &&
                        renderer.name != "Quad")
                    {
                        Outlinable.AddRenderer(renderer);
                    }
                }

                if (Outlinable.OutlineTargetsCount > 0)
                {
                    ConfigureOutline(Outlinable, Color.white);
                }
                else
                {
                    Destroy(Outlinable);
                    Outlinable = null;
                }
            }

            private static bool ShouldApplyOutline(InteractablePickup? pickup)
            {
                return pickup != null &&
                    pickup.isActiveAndEnabled &&
                    pickup.ItemAgent != null &&
                    pickup.ItemAgent.Item != null;
            }
        }

        [HarmonyPatchCategory(HarmonyCatagory)]
        [HarmonyPatch(typeof(InteractableBase), "Start")]
        private static class InteractableBaseStartPatch
        {
            private static void Postfix(InteractableBase __instance)
            {
                if (__instance is InteractableLootbox lootbox)
                {
                    EnsureOutlineAttached(lootbox);
                }
                else if (__instance is InteractablePickup pickup)
                {
                    EnsureOutlineAttached(pickup);
                }
            }
        }

        [HarmonyPatchCategory(HarmonyCatagory)]
        [HarmonyPatch]
        private static class SRPOutlineExecutePatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("EPOOutline.URPOutlineFeature+SRPOutline:Execute");
            }

            private static void Prefix()
            {
                OrientPickupBillboards();
            }
        }
    }
}
