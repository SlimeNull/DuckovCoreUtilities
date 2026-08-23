using EPOOutline;
using FOW;
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
        private static readonly List<SpriteRenderer> VisiblePickupSpriteRenderers = new List<SpriteRenderer>();
        private static LootboxOutlineFeature? ActiveFeature;
        private readonly List<OutlinableBehaviourBase> _groundItemControllers = new List<OutlinableBehaviourBase>();
        private readonly List<LootOutlinable> _lootboxControllers = new List<LootOutlinable>();
        private FogOfWarRevealer3D? _playerRevealer;
        private VisibilityContext _visibilityContext;
        private float _nextControllerPruneTime;

        public override string Name => "Loot outline";

        public bool EnableLootboxOutline { get; set; } = true;
        public bool EnableGroundItemOutline { get; set; } = true;
        public bool UseQualityColor { get; set; } = true;
        public bool LootboxBreathingEffect { get; set; } = true;
        public bool GroundItemBreathingEffect { get; set; } = true;
        public float BreathingPeriod { get; set; } = 1.5f;
        public float BreathingMinAlpha { get; set; } = 0.35f;
        private float CurrentBreathingAlpha { get; set; } = 1f;

        protected override void OnEnable()
        {
            ActiveFeature = this;
            Context.Harmony.PatchCategory(HarmonyCatagory);
            RenderPipelineManager.endCameraRendering += ResetPickupBillboards;
            ReactivateControllers();
        }

        protected override void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= ResetPickupBillboards;
            Context.Harmony.UnpatchCategory(HarmonyCatagory);
            foreach (var controller in _groundItemControllers)
            {
                if (controller != null)
                {
                    controller.Deactivate();
                }
            }

            foreach (var controller in _lootboxControllers)
            {
                if (controller != null)
                {
                    controller.Deactivate();
                }
            }

            VisiblePickupSpriteRenderers.Clear();
            _playerRevealer = null;
            ActiveFeature = null;
        }

        public override void Tick()
        {
            _playerRevealer = LevelManager.Instance?.FogOfWarManager?.mainVis;
            _visibilityContext = VisibilityContext.Create(_playerRevealer);

            var period = Mathf.Max(0.01f, BreathingPeriod);
            var normalized = (Mathf.Sin(Time.time / period * Mathf.PI * 2f) + 1f) * 0.5f;
            CurrentBreathingAlpha = Mathf.Lerp(Mathf.Clamp01(BreathingMinAlpha), 1f, normalized);

            if (Time.unscaledTime >= _nextControllerPruneTime)
            {
                _nextControllerPruneTime = Time.unscaledTime + 2f;
                _groundItemControllers.RemoveAll(static controller => controller == null);
                _lootboxControllers.RemoveAll(static controller => controller == null);
            }

            foreach (var controller in _groundItemControllers)
            {
                if (controller != null)
                {
                    controller.Tick();
                }
            }

            foreach (var controller in _lootboxControllers)
            {
                if (controller != null)
                {
                    controller.Tick();
                }
            }
        }

        private void ReactivateControllers()
        {
            for (var i = _groundItemControllers.Count - 1; i >= 0; i--)
            {
                var controller = _groundItemControllers[i];
                if (controller == null)
                {
                    _groundItemControllers.RemoveAt(i);
                }
                else
                {
                    controller.Activate(this);
                }
            }

            for (var i = _lootboxControllers.Count - 1; i >= 0; i--)
            {
                var controller = _lootboxControllers[i];
                if (controller == null)
                {
                    _lootboxControllers.RemoveAt(i);
                }
                else
                {
                    controller.Initialize(this);
                }
            }
        }

        private static void EnsureOutlineAttached(InteractableLootbox instance)
        {
            if (ActiveFeature is null ||
                !ActiveFeature.EnableLootboxOutline)
            {
                return;
            }

            var controller = instance.GetOrAddComponent<LootOutlinable>().Initialize(ActiveFeature);
            if (!ActiveFeature._lootboxControllers.Contains(controller))
            {
                ActiveFeature._lootboxControllers.Add(controller);
            }
        }

        private static void EnsureOutlineAttached(InteractablePickup instance)
        {
            if (ActiveFeature is null ||
                !ActiveFeature.EnableGroundItemOutline)
            {
                return;
            }

            var controller = instance.GetOrAddComponent<GroundItemOutlinable>().Initialize(ActiveFeature);
            if (!ActiveFeature._groundItemControllers.Contains(controller))
            {
                ActiveFeature._groundItemControllers.Add(controller);
            }
        }

        private static void SetPickupSpriteRendererVisible(SpriteRenderer spriteRenderer, bool visible)
        {
            if (visible)
            {
                if (!VisiblePickupSpriteRenderers.Contains(spriteRenderer))
                {
                    VisiblePickupSpriteRenderers.Add(spriteRenderer);
                }
            }
            else
            {
                VisiblePickupSpriteRenderers.Remove(spriteRenderer);
            }
        }

        private static void OrientPickupBillboards()
        {
            VisiblePickupSpriteRenderers.RemoveAll(static renderer => renderer == null);

            if (VisiblePickupSpriteRenderers.Count == 0)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            var cameraTransform = mainCamera.transform;
            foreach (var spriteRenderer in VisiblePickupSpriteRenderers)
            {
                var rendererTransform = spriteRenderer.transform;
                var direction = rendererTransform.position - cameraTransform.position;
                rendererTransform.rotation = Quaternion.LookRotation(direction.normalized, cameraTransform.rotation * Vector3.up);
                rendererTransform.localScale = new Vector3(0.14f, 0.14f, 1f);
            }
        }

        private static void ResetPickupBillboards(ScriptableRenderContext context, Camera camera)
        {
            VisiblePickupSpriteRenderers.RemoveAll(static renderer => renderer == null);

            foreach (var spriteRenderer in VisiblePickupSpriteRenderers)
            {
                spriteRenderer.transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }

        private static bool IsVisibleToPlayer(Vector3 position)
        {
            var feature = ActiveFeature;
            var revealer = feature?._playerRevealer;
            if (feature is null || revealer == null)
            {
                return false;
            }

            return feature._visibilityContext.MayBeVisible(position) &&
                revealer.TestPoint(position);
        }

        private readonly struct VisibilityContext
        {
            private readonly FogOfWarWorld.GamePlane _gamePlane;
            private readonly Vector2 _eyePosition;
            private readonly Vector2 _forward;
            private readonly float _viewRadiusSquared;
            private readonly float _senseRadiusSquared;
            private readonly float _halfViewAngleCos;
            private readonly bool _hasSenseRadius;
            private readonly bool _fullCircle;
            private readonly bool _valid;

            private VisibilityContext(
                FogOfWarWorld.GamePlane gamePlane,
                Vector2 eyePosition,
                Vector2 forward,
                float viewRadius,
                float senseRadius,
                float viewAngle)
            {
                _gamePlane = gamePlane;
                _eyePosition = eyePosition;
                _forward = forward.normalized;
                _viewRadiusSquared = viewRadius * viewRadius;
                _senseRadiusSquared = senseRadius * senseRadius;
                _halfViewAngleCos = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
                _hasSenseRadius = senseRadius > 0f;
                _fullCircle = viewAngle >= 359.999f;
                _valid = true;
            }

            public static VisibilityContext Create(FogOfWarRevealer3D? revealer)
            {
                var world = FogOfWarWorld.instance;
                if (revealer == null || world == null)
                {
                    return default;
                }

                var viewRadius = revealer.ViewRadius;
                if (world.UsingSoftening)
                {
                    viewRadius += revealer.RevealHiderInFadeOutZonePercentage * revealer.SoftenDistance;
                }

                var forward = world.gamePlane == FogOfWarWorld.GamePlane.XZ
                    ? revealer.transform.forward
                    : revealer.transform.up;
                return new VisibilityContext(
                    world.gamePlane,
                    Project(world.gamePlane, revealer.GetEyePosition()),
                    Project(world.gamePlane, forward),
                    Mathf.Max(0f, viewRadius),
                    revealer.UnobscuredRadius,
                    revealer.ViewAngle);
            }

            public bool MayBeVisible(Vector3 worldPosition)
            {
                if (!_valid)
                {
                    return false;
                }

                var delta = Project(_gamePlane, worldPosition) - _eyePosition;
                var distanceSquared = delta.sqrMagnitude;
                if (_hasSenseRadius && distanceSquared < _senseRadiusSquared)
                {
                    return true;
                }

                if (distanceSquared >= _viewRadiusSquared)
                {
                    return false;
                }

                if (_fullCircle || distanceSquared <= Mathf.Epsilon)
                {
                    return true;
                }

                return Vector2.Dot(delta, _forward) >
                    _halfViewAngleCos * Mathf.Sqrt(distanceSquared);
            }

            private static Vector2 Project(FogOfWarWorld.GamePlane gamePlane, Vector3 value)
            {
                return gamePlane switch
                {
                    FogOfWarWorld.GamePlane.XY => new Vector2(value.x, value.y),
                    FogOfWarWorld.GamePlane.ZY => new Vector2(value.z, value.y),
                    _ => new Vector2(value.x, value.z),
                };
            }
        }

        private abstract class OutlinableBehaviourBase : MonoBehaviour
        {
            protected Outlinable? Outlinable { get; set; }
            protected LootboxOutlineFeature? OwnerFeature { get; private set; }
            private bool _outlineVisible;
            private bool _lastBreathingEnabled;

            protected void InitializeOwner(LootboxOutlineFeature ownerFeature)
            {
                OwnerFeature = ownerFeature;
                enabled = true;
            }

            public void Activate(LootboxOutlineFeature ownerFeature)
            {
                InitializeOwner(ownerFeature);
            }

            public void Deactivate()
            {
                SetOutlineVisible(visible: false);
                OwnerFeature = null;
                enabled = false;
            }

            public void Tick()
            {
                if (Outlinable is null)
                {
                    return;
                }

                SetOutlineVisible(IsVisibleToPlayer(transform.position));

                if (!_outlineVisible)
                {
                    return;
                }

                var breathingEnabled = UseBreathingEffect();
                if (breathingEnabled)
                {
                    Outlinable.OutlineParameters.Color = ApplyBreathing(GetOutlineColor());
                }
                else if (_lastBreathingEnabled)
                {
                    Outlinable.OutlineParameters.Color = GetOutlineColor();
                }

                _lastBreathingEnabled = breathingEnabled;
            }

            private void SetOutlineVisible(bool visible)
            {
                if (_outlineVisible == visible || Outlinable is null)
                {
                    return;
                }

                _outlineVisible = visible;
                Outlinable.enabled = visible;
                OnOutlineVisibilityChanged(visible);
            }

            protected virtual void OnOutlineVisibilityChanged(bool visible)
            {
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

            protected Color ApplyBreathing(Color color)
            {
                if (OwnerFeature is null)
                {
                    return color;
                }

                color.a *= OwnerFeature.CurrentBreathingAlpha;
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
            private bool _outlineVisible;
            private bool _lastBreathingEnabled;

            public bool UseQualityColor { get; set; } = true;

            public LootOutlinable Initialize(LootboxOutlineFeature ownerFeature)
            {
                _ownerFeature = ownerFeature;
                UseQualityColor = ownerFeature.UseQualityColor;
                enabled = true;
                return this;
            }

            public void Deactivate()
            {
                _outlineVisible = false;
                if (outlinable != null)
                {
                    outlinable.enabled = false;
                }

                _ownerFeature = null;
                enabled = false;
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

            public void Tick()
            {
                if (outlinable is null)
                {
                    return;
                }

                var visible = IsVisibleToPlayer(transform.position);
                if (_outlineVisible != visible)
                {
                    _outlineVisible = visible;
                    outlinable.enabled = visible;
                }

                if (!_outlineVisible)
                {
                    return;
                }

                var breathingEnabled = _ownerFeature?.LootboxBreathingEffect == true;
                if (breathingEnabled)
                {
                    outlinable.OutlineParameters.Color = ApplyBreathing(_outlineColor);
                }
                else if (_lastBreathingEnabled)
                {
                    outlinable.OutlineParameters.Color = _outlineColor;
                }

                _lastBreathingEnabled = breathingEnabled;
            }

            private void AttachOutline()
            {
                outlinable = gameObject.AddComponent<Outlinable>();
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

                color.a *= _ownerFeature.CurrentBreathingAlpha;
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
            private SpriteRenderer? _spriteRenderer;

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

                _spriteRenderer = _pickup.GetComponentInChildren<SpriteRenderer>();
                if (_spriteRenderer != null)
                {
                    Outlinable = _spriteRenderer.gameObject.GetComponent<Outlinable>() ??
                        _spriteRenderer.gameObject.AddComponent<Outlinable>();
                    Outlinable.AddRenderer(_spriteRenderer);
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

            protected override void OnOutlineVisibilityChanged(bool visible)
            {
                if (_spriteRenderer != null)
                {
                    SetPickupSpriteRendererVisible(_spriteRenderer, visible);
                }
            }

            private void OnDestroy()
            {
                if (_spriteRenderer != null)
                {
                    SetPickupSpriteRendererVisible(_spriteRenderer, visible: false);
                }
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
