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

        public override string Name => "Mute and pause when unfocused";

        public bool MuteWhenUnfocused { get; set; } = true;

        public bool PauseWhenUnfocused { get; set; } = true;

        protected override void OnEnable()
        {
            _focusWatcher = Context.HostObject.GetComponent<FocusWatcher>() ?? Context.HostObject.AddComponent<FocusWatcher>();
            _focusWatcher.Initialize(this);
            SetFocused(Application.isFocused, force: true);
        }

        protected override void OnDisable()
        {
            _focusWatcher?.ClearOwner(this);
            _focusWatcher = null;
            RestoreMuteState();
        }

        public override void Tick()
        {
            var focused = Application.isFocused;
            if (focused != _focused)
            {
                SetFocused(focused);
            }
            else if (!focused)
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

        private static void EnsurePauseMenuShown()
        {
            if (GameManager.Instance == null ||
                LevelManager.Instance == null ||
                SceneLoader.IsSceneLoading ||
                PauseMenu.Instance == null ||
                PauseMenu.Instance.Shown)
            {
                return;
            }

            View.ActiveView?.Close();
            PauseMenu.Show();
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
