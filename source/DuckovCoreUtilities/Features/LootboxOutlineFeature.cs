using EPOOutline;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class LootboxOutlineFeature : FeatureBase
    {
        private const string HarmonyCatagory = nameof(LootboxOutlineFeature);
        private static readonly List<SpriteRenderer> PickupSpriteRenderers = new List<SpriteRenderer>();
        private static LootboxOutlineFeature? ActiveFeature;

        public override string Name => "Loot outline";

        public bool EnableLootboxOutline { get; set; } = true;
        public bool EnableGroundItemOutline { get; set; } = true;
        public bool UseQualityColor { get; set; } = true;
        public bool LootboxBreathingEffect { get; set; } = true;
        public bool GroundItemBreathingEffect { get; set; } = true;
        public float BreathingPeriod { get; set; } = 1.5f;
        public float BreathingMinAlpha { get; set; } = 0.35f;

        protected override void OnEnable()
        {
            ActiveFeature = this;
            Context.Harmony.PatchCategory(HarmonyCatagory);
            RenderPipelineManager.endCameraRendering += ResetPickupBillboards;
        }

        protected override void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= ResetPickupBillboards;
            Context.Harmony.UnpatchCategory(HarmonyCatagory);
            PickupSpriteRenderers.Clear();
            ActiveFeature = null;
        }

        private static void EnsureOutlineAttached(InteractableLootbox instance)
        {
            if (ActiveFeature is null ||
                !ActiveFeature.EnableLootboxOutline)
            {
                return;
            }

            instance.GetOrAddComponent<LootOutlinable>().Initialize(ActiveFeature);
        }

        private static void EnsureOutlineAttached(InteractablePickup instance)
        {
            if (ActiveFeature is null ||
                !ActiveFeature.EnableGroundItemOutline)
            {
                return;
            }

            instance.GetOrAddComponent<GroundItemOutlinable>().Initialize(ActiveFeature);
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

        private static bool IsVisibleToPlayer(Vector3 position)
        {
            var levelManager = LevelManager.Instance;
            var revealer = levelManager != null
                ? levelManager.FogOfWarManager?.mainVis
                : null;

            return revealer != null && revealer.TestPoint(position);
        }

        private abstract class OutlinableBehaviourBase : MonoBehaviour
        {
            protected Outlinable? Outlinable { get; set; }
            protected LootboxOutlineFeature? OwnerFeature { get; private set; }

            protected void InitializeOwner(LootboxOutlineFeature ownerFeature)
            {
                OwnerFeature = ownerFeature;
            }

            protected virtual void Update()
            {
                if (Outlinable is null)
                {
                    return;
                }

                Outlinable.enabled = IsVisibleToPlayer(transform.position);

                if (Outlinable.enabled)
                {
                    Outlinable.OutlineParameters.Color = ApplyBreathing(GetOutlineColor(), UseBreathingEffect());
                }
            }

            protected static void ConfigureOutline(Outlinable outlinable, Color color)
            {
                outlinable.OutlineParameters.Enabled = true;
                outlinable.OutlineParameters.Color = color;
                outlinable.OutlineParameters.DilateShift = 3f;
                outlinable.enabled = false;
            }

            protected virtual Color GetOutlineColor()
            {
                return Color.white;
            }

            protected virtual bool UseBreathingEffect()
            {
                return false;
            }

            protected Color ApplyBreathing(Color color, bool enabled)
            {
                if (!enabled || OwnerFeature is null)
                {
                    return color;
                }

                var period = Mathf.Max(0.01f, OwnerFeature.BreathingPeriod);
                var normalized = (Mathf.Sin(Time.time / period * Mathf.PI * 2f) + 1f) * 0.5f;
                var minAlpha = Mathf.Clamp01(OwnerFeature.BreathingMinAlpha);
                color.a *= Mathf.Lerp(minAlpha, 1f, normalized);
                return color;
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
            private LootboxOutlineFeature? _ownerFeature;
            private Color _outlineColor = Color.white;

            public bool UseQualityColor { get; set; } = true;

            public LootOutlinable Initialize(LootboxOutlineFeature ownerFeature)
            {
                _ownerFeature = ownerFeature;
                UseQualityColor = ownerFeature.UseQualityColor;
                return this;
            }

            void Start()
            {
                lootBox = GetComponent<InteractableLootbox>();
                if (_ownerFeature is not null &&
                    _ownerFeature.EnableLootboxOutline &&
                    ShouldApplyOutline(lootBox))
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

                outlinable.enabled = IsVisibleToPlayer(transform.position);
                if (outlinable.enabled)
                {
                    outlinable.OutlineParameters.Color = ApplyBreathing(_outlineColor);
                }
            }

            private void AttachOutline()
            {
                outlinable = gameObject.AddComponent<Outlinable>();
                Debug.Log($"[LootboxOutlineFeature] Added outline to {gameObject.name}");

                AddLootboxRenderers(outlinable);
                outlinable.OutlineParameters.Enabled = true;
                _outlineColor = Color.white;
                outlinable.OutlineParameters.Color = _outlineColor;
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
                    _outlineColor = color;
                    outlinable.OutlineParameters.Color = _outlineColor;
                }

                outlinable.enabled = false;
            }

            private Color ApplyBreathing(Color color)
            {
                if (_ownerFeature is null ||
                    !_ownerFeature.LootboxBreathingEffect)
                {
                    return color;
                }

                var period = Mathf.Max(0.01f, _ownerFeature.BreathingPeriod);
                var normalized = (Mathf.Sin(Time.time / period * Mathf.PI * 2f) + 1f) * 0.5f;
                color.a *= Mathf.Lerp(Mathf.Clamp01(_ownerFeature.BreathingMinAlpha), 1f, normalized);
                return color;
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

            public GroundItemOutlinable Initialize(LootboxOutlineFeature ownerFeature)
            {
                InitializeOwner(ownerFeature);
                return this;
            }

            private void Start()
            {
                _pickup = GetComponent<InteractablePickup>();
                if (OwnerFeature is not null &&
                    OwnerFeature.EnableGroundItemOutline &&
                    ShouldApplyOutline(_pickup))
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
                    ConfigureOutline(Outlinable, GetOutlineColor());
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
                    ConfigureOutline(Outlinable, GetOutlineColor());
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

            protected override Color GetOutlineColor()
            {
                if (OwnerFeature is null ||
                    !OwnerFeature.UseQualityColor ||
                    _pickup?.ItemAgent?.Item is null)
                {
                    return Color.white;
                }

                var color = QualityColor.Get(_pickup.ItemAgent.Item.Quality);
                color.a = 1f;
                return color;
            }

            protected override bool UseBreathingEffect()
            {
                return OwnerFeature?.GroundItemBreathingEffect == true;
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
