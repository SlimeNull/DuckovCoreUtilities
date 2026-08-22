using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class BulletCountCrosshairColorFeature : FeatureBase
    {
        private readonly List<CrosshairColorControllerBase> _controllers = new List<CrosshairColorControllerBase>();

        public override string Name => "Bullet count crosshair color";

        public float WarnRatio { get; set; } = 0.5f;

        protected override void OnEnable()
        {
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            AttachToCrosshairs();
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            DetachControllers();
        }

        private void OnAfterLevelInitialized()
        {
            AttachToCrosshairs();
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
                RegisterController(aimMarker.gameObject.GetOrAddComponent<AimMarkerColorController>().Initialize(this, aimMarker));
            }
        }

        private void AttachToAdsAimMarkers()
        {
            var adsAimMarkers = GameObject.FindObjectsOfType<ADSAimMarker>();
            foreach (var adsAimMarker in adsAimMarkers)
            {
                RegisterController(adsAimMarker.gameObject.GetOrAddComponent<AdsAimMarkerColorController>().Initialize(this, adsAimMarker));
            }
        }

        private void RegisterController(CrosshairColorControllerBase controller)
        {
            if (!_controllers.Contains(controller))
            {
                _controllers.Add(controller);
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

        private static Color GetCrosshairColor(ItemAgent_Gun gun, float warnRatio)
        {
            var capacity = gun.Capacity;
            if (capacity <= 0)
            {
                return Color.white;
            }

            var ratio = Mathf.Clamp01(gun.BulletCount / (float)capacity);
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

            protected void LateUpdate()
            {
                if (OwnerFeature is null)
                {
                    return;
                }

                var gun = LevelManager.Instance?.MainCharacter?.GetGun();
                var color = gun == null ? Color.white : GetCrosshairColor(gun, OwnerFeature.WarnRatio);
                ApplyColor(color);
            }

            protected abstract void ApplyColor(Color color);

            protected static void ApplyGraphicColor(Graphic? graphic, Color color)
            {
                if (graphic == null)
                {
                    return;
                }

                var currentColor = graphic.color;
                graphic.color = new Color(color.r, color.g, color.b, currentColor.a);
            }
        }

        private sealed class AimMarkerColorController : CrosshairColorControllerBase
        {
            private AimMarker? _aimMarker;

            public AimMarkerColorController Initialize(BulletCountCrosshairColorFeature ownerFeature, AimMarker aimMarker)
            {
                InitializeOwner(ownerFeature);
                _aimMarker = aimMarker;
                return this;
            }

            protected override void ApplyColor(Color color)
            {
                if (_aimMarker?.aimMarkerImages is null)
                {
                    return;
                }

                foreach (var image in _aimMarker.aimMarkerImages)
                {
                    ApplyGraphicColor(image, color);
                }
            }
        }

        private sealed class AdsAimMarkerColorController : CrosshairColorControllerBase
        {
            private ADSAimMarker? _adsAimMarker;

            public AdsAimMarkerColorController Initialize(BulletCountCrosshairColorFeature ownerFeature, ADSAimMarker adsAimMarker)
            {
                InitializeOwner(ownerFeature);
                _adsAimMarker = adsAimMarker;
                return this;
            }

            protected override void ApplyColor(Color color)
            {
                if (_adsAimMarker == null)
                {
                    return;
                }

                if (_adsAimMarker.crosshairs is not null)
                {
                    foreach (var crosshair in _adsAimMarker.crosshairs)
                    {
                        if (crosshair != null)
                        {
                            foreach (var graphic in crosshair.GetComponentsInChildren<Graphic>(true))
                            {
                                ApplyGraphicColor(graphic, color);
                            }
                        }
                    }
                }

                ApplyGraphicColor(_adsAimMarker.sniperRoundRenderer, color);
                ApplyGraphicColor(_adsAimMarker.followSniperRoundRenderer, color);
            }
        }
    }
}
