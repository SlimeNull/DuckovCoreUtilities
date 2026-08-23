using Duckov;
using Duckov.UI;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class MuteAndPauseWhenUnfocusedFeature : FeatureBase
    {
        private FocusWatcher? _focusWatcher;
        private AudioManager.Bus? _masterBus;
        private bool _capturedMuteState;
        private bool _muteStateBeforeFocusLoss;
        private bool _focused;
        private bool _evacuationStarted;
        private float _nextUnfocusedRetryTime;

        public override string Name => "Mute and pause when unfocused";

        public bool MuteWhenUnfocused { get; set; } = true;

        public bool PauseWhenUnfocused { get; set; } = true;

        protected override void OnEnable()
        {
            _evacuationStarted = false;
            LevelManager.OnEvacuated += OnEvacuated;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            _focusWatcher = Context.HostObject.GetComponent<FocusWatcher>() ?? Context.HostObject.AddComponent<FocusWatcher>();
            _focusWatcher.Initialize(this);
            SetFocused(Application.isFocused, force: true);
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            LevelManager.OnEvacuated -= OnEvacuated;
            _focusWatcher?.ClearOwner(this);
            _focusWatcher = null;
            _evacuationStarted = false;
            RestoreMuteState();
        }

        public override void Tick()
        {
            var focused = Application.isFocused;
            if (focused != _focused)
            {
                SetFocused(focused);
            }
            else if (!focused && Time.unscaledTime >= _nextUnfocusedRetryTime)
            {
                ApplyUnfocusedState();
            }
        }

        private void SetFocused(bool focused, bool force = false)
        {
            if (!force && _focused == focused)
            {
                return;
            }

            _focused = focused;
            if (focused)
            {
                RestoreMuteState();
            }
            else
            {
                ApplyUnfocusedState();
            }
        }

        private void ApplyUnfocusedState()
        {
            _nextUnfocusedRetryTime = Time.unscaledTime + 0.5f;

            if (MuteWhenUnfocused)
            {
                EnsureMuted();
            }

            if (PauseWhenUnfocused)
            {
                EnsurePauseMenuShown();
            }
        }

        private void EnsureMuted()
        {
            var masterBus = AudioManager.GetBus("Master");
            if (masterBus == null)
            {
                return;
            }

            if (!_capturedMuteState || !ReferenceEquals(_masterBus, masterBus))
            {
                _masterBus = masterBus;
                _muteStateBeforeFocusLoss = masterBus.Mute;
                _capturedMuteState = true;
            }

            if (!masterBus.Mute)
            {
                masterBus.Mute = true;
            }
        }

        private void RestoreMuteState()
        {
            if (_capturedMuteState && _masterBus != null && _masterBus.Mute != _muteStateBeforeFocusLoss)
            {
                _masterBus.Mute = _muteStateBeforeFocusLoss;
            }

            _masterBus = null;
            _capturedMuteState = false;
        }

        private void EnsurePauseMenuShown()
        {
            if (GameManager.Instance == null ||
                LevelManager.Instance == null ||
                !LevelManager.LevelInited ||
                SceneLoader.IsSceneLoading ||
                PauseMenu.Instance == null ||
                PauseMenu.Instance.Shown ||
                !CanPauseCurrentGameplay())
            {
                return;
            }

            PauseMenu.Show();
        }

        private bool CanPauseCurrentGameplay()
        {
            if (_evacuationStarted || !InputManager.InputActived)
            {
                return false;
            }

            var levelManager = LevelManager.Instance;
            var mainCharacter = levelManager?.MainCharacter;
            var controllingCharacter = levelManager?.ControllingCharacter;
            if (mainCharacter == null ||
                controllingCharacter == null ||
                !mainCharacter.gameObject.activeInHierarchy ||
                !controllingCharacter.gameObject.activeInHierarchy ||
                mainCharacter.Health == null ||
                mainCharacter.Health.IsDead)
            {
                return false;
            }

            return controllingCharacter.CanMove();
        }

        private void OnEvacuated(EvacuationInfo _)
        {
            _evacuationStarted = true;
        }

        private void OnAfterLevelInitialized()
        {
            _evacuationStarted = false;
        }

        private sealed class FocusWatcher : MonoBehaviour
        {
            private MuteAndPauseWhenUnfocusedFeature? _owner;

            public void Initialize(MuteAndPauseWhenUnfocusedFeature owner)
            {
                _owner = owner;
            }

            public void ClearOwner(MuteAndPauseWhenUnfocusedFeature owner)
            {
                if (ReferenceEquals(_owner, owner))
                {
                    _owner = null;
                }
            }

            private void OnApplicationFocus(bool hasFocus)
            {
                _owner?.SetFocused(hasFocus);
            }
        }
    }
}
