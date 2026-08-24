using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class BulletCountCrosshairColorFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(BulletCountCrosshairColorFeature);
        private static BulletCountCrosshairColorFeature? ActiveFeature;
        private readonly List<CrosshairColorControllerBase> _controllers = new List<CrosshairColorControllerBase>();
        private CrosshairColorDriver? _driver;
        private ItemAgent_Gun? _currentGun;
        private bool _colorDirty = true;
        private int _lastBulletCount = -1;
        private int _lastCapacity = -1;
        private float _lastWarnRatio = -1f;
        private float _nextFallbackPollTime;

        public override string Name => "Bullet count crosshair color";

        public float WarnRatio { get; set; } = 0.5f;

        protected override void OnEnable()
        {
            ActiveFeature = this;
            _colorDirty = true;
            Context.Harmony.PatchCategory(HarmonyCategory);
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            CharacterMainControl.OnMainCharacterChangeHoldItemAgentEvent += OnMainCharacterChangeHoldItemAgent;
            ItemAgent_Gun.OnMainCharacterShootEvent += OnMainCharacterShoot;
            _driver = Context.HostObject.GetComponent<CrosshairColorDriver>() ??
                Context.HostObject.AddComponent<CrosshairColorDriver>();
            _driver.Initialize(this);
            SetCurrentGun(LevelManager.Instance?.MainCharacter?.GetGun());
            AttachToCrosshairs();
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            CharacterMainControl.OnMainCharacterChangeHoldItemAgentEvent -= OnMainCharacterChangeHoldItemAgent;
            ItemAgent_Gun.OnMainCharacterShootEvent -= OnMainCharacterShoot;
            SetCurrentGun(null);
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            DetachControllers();
            if (_driver != null)
            {
                _driver.ClearOwner(this);
                UnityEngine.Object.Destroy(_driver);
                _driver = null;
            }
            if (ReferenceEquals(ActiveFeature, this))
            {
                ActiveFeature = null;
            }
        }

        private void OnAfterLevelInitialized()
        {
            SetCurrentGun(LevelManager.Instance?.MainCharacter?.GetGun());
            AttachToCrosshairs();
        }

        private void OnMainCharacterChangeHoldItemAgent(CharacterMainControl character, DuckovItemAgent itemAgent)
        {
            SetCurrentGun(itemAgent as ItemAgent_Gun);
        }

        private void OnMainCharacterShoot(ItemAgent_Gun gun)
        {
            if (gun == _currentGun)
            {
                _colorDirty = true;
            }
        }

        private void SetCurrentGun(ItemAgent_Gun? gun)
        {
            if (_currentGun == gun)
            {
                return;
            }

            if (_currentGun != null)
            {
                _currentGun.OnLoadedEvent -= OnCurrentGunLoaded;
            }

            _currentGun = gun;
            if (_currentGun != null)
            {
                _currentGun.OnLoadedEvent += OnCurrentGunLoaded;
            }

            _colorDirty = true;
        }

        private void OnCurrentGunLoaded()
        {
            _colorDirty = true;
        }

        private void AttachToCrosshairs()
        {
            AttachToAimMarkers();
            AttachToAdsAimMarkers();
            PruneControllers();
        }

        private void AttachToAimMarkers()
        {
            var aimMarkers = GameObject.FindObjectsOfType<AimMarker>();
            foreach (var aimMarker in aimMarkers)
            {
                AttachToAimMarker(aimMarker);
            }
        }

        private void AttachToAdsAimMarkers()
        {
            var adsAimMarkers = GameObject.FindObjectsOfType<ADSAimMarker>();
            foreach (var adsAimMarker in adsAimMarkers)
            {
                AttachToAdsAimMarker(adsAimMarker);
            }
        }

        private void AttachToAdsAimMarker(ADSAimMarker? adsAimMarker)
        {
            if (adsAimMarker != null)
            {
                RegisterController(adsAimMarker.gameObject.GetOrAddComponent<AdsAimMarkerColorController>().Initialize(this, adsAimMarker));
            }

            PruneControllers();
        }

        private void AttachToAimMarker(AimMarker? aimMarker)
        {
            if (aimMarker != null)
            {
                RegisterController(aimMarker.gameObject.GetOrAddComponent<AimMarkerColorController>().Initialize(this, aimMarker));
            }

            PruneControllers();
        }

        private void RegisterController(CrosshairColorControllerBase controller)
        {
            if (!_controllers.Contains(controller))
            {
                _controllers.Add(controller);
                _colorDirty = true;
            }
        }

        private void DetachControllers()
        {
            foreach (var controller in _controllers)
            {
                if (controller != null)
                {
                    controller.ClearOwner();
                    controller.ApplyDefaultColor();
                }
            }

            _controllers.Clear();
        }

        private void LateUpdateColors()
        {
            var actualGun = LevelManager.Instance?.MainCharacter?.GetGun();
            if (actualGun != _currentGun)
            {
                SetCurrentGun(actualGun);
            }

            var now = Time.unscaledTime;
            if (!_colorDirty && now < _nextFallbackPollTime)
            {
                return;
            }

            _nextFallbackPollTime = now + 0.5f;
            var bulletCount = _currentGun != null ? _currentGun.BulletCount : 0;
            var capacity = _currentGun != null ? _currentGun.Capacity : 0;
            var warnRatio = WarnRatio;
            if (!_colorDirty &&
                bulletCount == _lastBulletCount &&
                capacity == _lastCapacity &&
                Mathf.Approximately(warnRatio, _lastWarnRatio))
            {
                return;
            }

            _colorDirty = false;
            _lastBulletCount = bulletCount;
            _lastCapacity = capacity;
            _lastWarnRatio = warnRatio;

            var color = GetCrosshairColor(bulletCount, capacity, warnRatio);
            PruneControllers();
            foreach (var controller in _controllers)
            {
                controller.ApplyColor(color);
            }
        }

        private void PruneControllers()
        {
            for (var i = _controllers.Count - 1; i >= 0; i--)
            {
                if (_controllers[i] == null)
                {
                    _controllers.RemoveAt(i);
                }
            }
        }

        private static Color GetCrosshairColor(int bulletCount, int capacity, float warnRatio)
        {
            if (capacity <= 0)
            {
                return Color.white;
            }

            var ratio = Mathf.Clamp01(bulletCount / (float)capacity);
            var clampedWarnRatio = Mathf.Clamp01(warnRatio);

            if (ratio >= clampedWarnRatio)
            {
                return Color.white;
            }

            if (clampedWarnRatio <= 0f)
            {
                return ratio <= 0f ? Color.red : Color.white;
            }

            return Color.Lerp(Color.red, Color.yellow, ratio / clampedWarnRatio);
        }

        private abstract class CrosshairColorControllerBase : MonoBehaviour
        {
            protected BulletCountCrosshairColorFeature? OwnerFeature { get; private set; }

            protected void InitializeOwner(BulletCountCrosshairColorFeature ownerFeature)
            {
                OwnerFeature = ownerFeature;
            }

            public void ClearOwner()
            {
                OwnerFeature = null;
            }

            public void ApplyDefaultColor()
            {
                ApplyColor(Color.white);
            }

            public abstract void ApplyColor(Color color);

            protected static void ApplyGraphicTint(Graphic? graphic, Color color)
            {
                if (graphic == null)
                {
                    return;
                }

                var tintEffect = graphic.GetComponent<CrosshairTintEffect>();
                if (tintEffect == null)
                {
                    if (CrosshairTintEffect.IsDefaultTint(color))
                    {
                        return;
                    }

                    tintEffect = graphic.gameObject.AddComponent<CrosshairTintEffect>();
                }

                tintEffect.SetTint(color);
            }
        }

        private sealed class AimMarkerColorController : CrosshairColorControllerBase
        {
            [SerializeField]
            private AimMarker? _aimMarker;

            public AimMarkerColorController Initialize(BulletCountCrosshairColorFeature ownerFeature, AimMarker aimMarker)
            {
                InitializeOwner(ownerFeature);
                _aimMarker = aimMarker;
                return this;
            }

            public override void ApplyColor(Color color)
            {
                if (_aimMarker?.aimMarkerImages is null)
                {
                    return;
                }

                foreach (var image in _aimMarker.aimMarkerImages)
                {
                    ApplyGraphicTint(image, color);
                }
            }
        }

        private sealed class AdsAimMarkerColorController : CrosshairColorControllerBase
        {
            [SerializeField]
            private ADSAimMarker? _adsAimMarker;
            private readonly List<Graphic> _graphics = new List<Graphic>();

            public AdsAimMarkerColorController Initialize(BulletCountCrosshairColorFeature ownerFeature, ADSAimMarker adsAimMarker)
            {
                InitializeOwner(ownerFeature);
                _adsAimMarker = adsAimMarker;
                _graphics.Clear();
                if (_adsAimMarker.crosshairs is not null)
                {
                    foreach (var crosshair in _adsAimMarker.crosshairs)
                    {
                        var graphic = crosshair != null ? crosshair.GetComponent<Graphic>() : null;
                        if (graphic != null)
                        {
                            _graphics.Add(graphic);
                        }
                    }
                }

                if (_adsAimMarker.sniperRoundRenderer != null)
                {
                    _graphics.Add(_adsAimMarker.sniperRoundRenderer);
                }

                if (_adsAimMarker.followSniperRoundRenderer != null)
                {
                    _graphics.Add(_adsAimMarker.followSniperRoundRenderer);
                }
                return this;
            }

            public override void ApplyColor(Color color)
            {
                if (_adsAimMarker == null)
                {
                    return;
                }

                foreach (var graphic in _graphics)
                {
                    ApplyGraphicTint(graphic, color);
                }
            }
        }

        [DisallowMultipleComponent]
        [RequireComponent(typeof(Graphic))]
        private sealed class CrosshairTintEffect : BaseMeshEffect
        {
            private Color _tint = Color.white;

            public static bool IsDefaultTint(Color color)
            {
                return Mathf.Approximately(color.r, 1f) &&
                    Mathf.Approximately(color.g, 1f) &&
                    Mathf.Approximately(color.b, 1f);
            }

            public void SetTint(Color color)
            {
                var nextTint = new Color(color.r, color.g, color.b, 1f);
                var shouldEnable = !IsDefaultTint(nextTint);
                if (Approximately(_tint, nextTint) && enabled == shouldEnable)
                {
                    return;
                }

                _tint = nextTint;
                if (enabled != shouldEnable)
                {
                    enabled = shouldEnable;
                    return;
                }

                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }

            public override void ModifyMesh(VertexHelper vertexHelper)
            {
                if (!IsActive())
                {
                    return;
                }

                var tint = (Color32)_tint;
                var vertex = default(UIVertex);
                for (var i = 0; i < vertexHelper.currentVertCount; i++)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    vertex.color = new Color32(
                        Multiply(vertex.color.r, tint.r),
                        Multiply(vertex.color.g, tint.g),
                        Multiply(vertex.color.b, tint.b),
                        vertex.color.a);
                    vertexHelper.SetUIVertex(vertex, i);
                }
            }

            private static byte Multiply(byte value, byte tint)
            {
                return (byte)((value * tint + 127) / 255);
            }

            private static bool Approximately(Color left, Color right)
            {
                return Mathf.Approximately(left.r, right.r) &&
                    Mathf.Approximately(left.g, right.g) &&
                    Mathf.Approximately(left.b, right.b);
            }
        }

        private sealed class CrosshairColorDriver : MonoBehaviour
        {
            private BulletCountCrosshairColorFeature? _owner;

            public void Initialize(BulletCountCrosshairColorFeature owner)
            {
                _owner = owner;
            }

            public void ClearOwner(BulletCountCrosshairColorFeature owner)
            {
                if (ReferenceEquals(_owner, owner))
                {
                    _owner = null;
                }
            }

            private void LateUpdate()
            {
                _owner?.LateUpdateColors();
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(AimMarker), "Awake")]
        private static class AimMarkerAwakePatch
        {
            private static void Postfix(AimMarker __instance)
            {
                try
                {
                    ActiveFeature?.AttachToAimMarker(__instance);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BulletCountCrosshairColorFeature] Failed to attach to AimMarker: {ex}");
                }
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(AimMarker), "SwitchAdsAimMarker")]
        private static class SwitchAdsAimMarkerPatch
        {
            private static void Postfix(ADSAimMarker? ___currentAdsAimMarker)
            {
                try
                {
                    ActiveFeature?.AttachToAdsAimMarker(___currentAdsAimMarker);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BulletCountCrosshairColorFeature] Failed to attach to ADSAimMarker: {ex}");
                }
            }
        }
    }
}
