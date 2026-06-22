using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
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
            var allCanvas = GameObject.FindObjectsOfType<Canvas>();
            foreach (var canvas in allCanvas)
            {
                if (canvas.GetComponentInChildren<AimMarker>(true) != null ||
                    canvas.GetComponentInChildren<ADSAimMarker>(true) != null)
                {
                    continue;
                }

                canvas.gameObject.GetOrAddComponent<AutoFadeWhenAiming>().Initialize(this);
            }
        }

        [RequireComponent(typeof(CanvasGroup))]
        private class AutoFadeWhenAiming : MonoBehaviour
        {
            private AutoFadeHudWhenAimingFeature? _ownerFeature;
            private CanvasGroup? _canvasGroup;

            private float _currentVelocity;

            public void Initialize(AutoFadeHudWhenAimingFeature ownerFeature)
            {
                _ownerFeature = ownerFeature;
            }

            void Start()
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            void Update()
            {
                if (_ownerFeature is null ||
                    _canvasGroup == null)
                {
                    return;
                }

                var targetAlpha = IsAiming() ? _ownerFeature.TargetAlpha : 1f;
                var smoothAlpha = UnityEngine.Mathf.SmoothDamp(_canvasGroup.alpha, targetAlpha, ref _currentVelocity, _ownerFeature.SmoothTime);

                _canvasGroup.alpha = smoothAlpha;
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
