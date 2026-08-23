using Duckov.UI;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal class AutoFadeHudWhenAimingFeature : FeatureBase
    {
        public override string Name => "Fade HUD when aiming";

        public float TargetAlpha { get; set; } = .3f;
        public float SmoothTime { get; set; } = .1f;

        protected override void OnEnable()
        {
            LevelManager.OnAfterLevelInitialized += LevelManager_OnAfterLevelInitialized;
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= LevelManager_OnAfterLevelInitialized;
        }

        private void LevelManager_OnAfterLevelInitialized()
        {
            var hudCanvasObject = GameObject.Find("HUDCanvas");
            var hudCanvas = hudCanvasObject.GetComponent<Canvas>();

            List<CanvasGroup> groups = new();

            for (var i = 0; i < hudCanvas.transform.childCount; i++)
            {
                var child = hudCanvas.transform.GetChild(i);
                if (child.GetComponentInChildren<AimMarker>(true) != null ||
                    child.GetComponentInChildren<ADSAimMarker>(true) != null ||
                    child.GetComponentInChildren<EvacuationCountdownUI>(true) != null ||
                    child.GetComponentInChildren<HealthBarManager>(true) != null ||
                    child.GetComponentInChildren<EvacuationCountdownUIProxy>(true) != null)
                {
                    continue;
                }

                var canvasGroup = child.gameObject.GetOrAddComponent<CanvasGroup>();
                groups.Add(canvasGroup);
            }

            hudCanvas.gameObject.GetOrAddComponent<AutoFadeWhenAiming>().Initialize(this, groups);
        }

        private class AutoFadeWhenAiming : MonoBehaviour
        {
            private AutoFadeHudWhenAimingFeature? _ownerFeature;
            private List<CanvasGroup>? _canvasGroups;

            private float _currentAlpha = 1;
            private float _currentVelocity;

            public void Initialize(AutoFadeHudWhenAimingFeature ownerFeature, List<CanvasGroup> canvasGroups)
            {
                _ownerFeature = ownerFeature;
                _canvasGroups = canvasGroups;
            }

            void Update()
            {
                if (_ownerFeature is null ||
                    _canvasGroups is null)
                {
                    return;
                }

                var isAiming = IsAiming();
                var targetAlpha = isAiming ? _ownerFeature.TargetAlpha : 1f;
                var smoothAlpha = UnityEngine.Mathf.SmoothDamp(_currentAlpha, targetAlpha, ref _currentVelocity, _ownerFeature.SmoothTime);
                _currentAlpha = smoothAlpha;

                foreach (var canvasGroup in _canvasGroups)
                {
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = smoothAlpha;
                    }
                }
            }

            static bool IsAiming()
            {
                var mainCharacter = LevelManager.Instance?.MainCharacter;
                if (mainCharacter == null)
                {
                    return false;
                }
                return mainCharacter.IsInAdsInput;
            }
        }
    }
}
