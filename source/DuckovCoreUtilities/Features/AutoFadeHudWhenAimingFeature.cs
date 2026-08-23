using Duckov.UI;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal class AutoFadeHudWhenAimingFeature : FeatureBase
    {
        private AutoFadeWhenAiming? _controller;

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

            if (_controller != null)
            {
                _controller.RestoreAndClearOwner();
                Object.Destroy(_controller);
                _controller = null;
            }
        }

        private void LevelManager_OnAfterLevelInitialized()
        {
            var hudCanvasObject = GameObject.Find("HUDCanvas");
            var hudCanvas = hudCanvasObject != null ? hudCanvasObject.GetComponent<Canvas>() : null;
            if (hudCanvas == null)
            {
                return;
            }

            List<CanvasGroup> groups = new();

            for (var i = 0; i < hudCanvas.transform.childCount; i++)
            {
                var child = hudCanvas.transform.GetChild(i);
                if (child.GetComponentInChildren<AimMarker>(true) != null ||
                    child.GetComponentInChildren<ADSAimMarker>(true) != null ||
                    child.GetComponentInChildren<EvacuationCountdownUI>(true) != null ||
                    child.GetComponentInChildren<EvacuationCountdownUIProxy>(true) != null ||
                    child.GetComponent<HealthBarManager>() != null ||
                    child.GetComponent<BulletTypeHUD>() != null)
                {
                    continue;
                }

                var canvasGroup = child.gameObject.GetOrAddComponent<CanvasGroup>();
                groups.Add(canvasGroup);
            }

            _controller = hudCanvas.gameObject.GetOrAddComponent<AutoFadeWhenAiming>();
            _controller.Initialize(this, groups);
        }

        private class AutoFadeWhenAiming : MonoBehaviour
        {
            private AutoFadeHudWhenAimingFeature? _ownerFeature;
            private List<CanvasGroup>? _canvasGroups;

            private float _currentAlpha = 1;
            private float _currentVelocity;
            private float _lastAppliedAlpha = 1f;
            private bool _lastAiming;
            private bool _stateInitialized;

            public void Initialize(AutoFadeHudWhenAimingFeature ownerFeature, List<CanvasGroup> canvasGroups)
            {
                _ownerFeature = ownerFeature;
                _canvasGroups = canvasGroups;
                _currentAlpha = 1f;
                _currentVelocity = 0f;
                _lastAppliedAlpha = 1f;
                _stateInitialized = false;
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
                if (_stateInitialized &&
                    isAiming == _lastAiming &&
                    Mathf.Abs(_currentAlpha - targetAlpha) < 0.001f)
                {
                    return;
                }

                _stateInitialized = true;
                _lastAiming = isAiming;
                _currentAlpha = Mathf.SmoothDamp(_currentAlpha, targetAlpha, ref _currentVelocity, _ownerFeature.SmoothTime);
                if (Mathf.Abs(_currentAlpha - targetAlpha) < 0.001f)
                {
                    _currentAlpha = targetAlpha;
                    _currentVelocity = 0f;
                }

                if (Mathf.Abs(_lastAppliedAlpha - _currentAlpha) < 0.0001f)
                {
                    return;
                }

                foreach (var canvasGroup in _canvasGroups)
                {
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = _currentAlpha;
                    }
                }

                _lastAppliedAlpha = _currentAlpha;
            }

            public void RestoreAndClearOwner()
            {
                if (_canvasGroups != null)
                {
                    foreach (var canvasGroup in _canvasGroups)
                    {
                        if (canvasGroup != null)
                        {
                            canvasGroup.alpha = 1f;
                        }
                    }
                }

                _ownerFeature = null;
                _canvasGroups = null;
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
