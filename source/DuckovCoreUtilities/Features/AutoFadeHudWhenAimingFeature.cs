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
                List<CanvasGroup> groups = new();

                for (var i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i);
                    if (child.GetComponentInChildren<AimMarker>(true) != null ||
                        child.GetComponentInChildren<ADSAimMarker>(true) != null)
                    {
                        continue;
                    }

                    var canvasGroup = child.gameObject.GetOrAddComponent<CanvasGroup>();
                    groups.Add(canvasGroup);
                }

                canvas.gameObject.GetOrAddComponent<AutoFadeWhenAiming>().Initialize(this, groups);
            }
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
            }

            void Start()
            {

            }

            void Update()
            {
                if (_ownerFeature is null ||
                    _canvasGroups is null)
                {
                    return;
                }

                var targetAlpha = IsAiming() ? _ownerFeature.TargetAlpha : 1f;
                var smoothAlpha = UnityEngine.Mathf.SmoothDamp(_currentAlpha, targetAlpha, ref _currentVelocity, _ownerFeature.SmoothTime);

                foreach (var canvasGroup in _canvasGroups)
                {
                    canvasGroup.alpha = smoothAlpha;
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
