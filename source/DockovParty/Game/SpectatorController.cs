using Cysharp.Threading.Tasks;
using SlimeNull.DockovParty.Networking.Protocol;
using System;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class SpectatorController : IDisposable
    {
        private readonly PartyRuntime _runtime;
        private UniTaskCompletionSource? _returnCompletion;
        private bool _returnCommittedBeforeClosure;
        private bool _disposed;

        public SpectatorController(PartyRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.MessageReceived += OnMessage;
            _runtime.Disconnected += OnDisconnected;
            LevelManager.OnMainCharacterDead += OnLocalPlayerDead;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
        }

        public bool Active { get; private set; }

        public bool TryInterceptDeathClosure(DamageInfo damageInfo, out UniTask result)
        {
            result = default;
            if (!_runtime.Connected || (!_runtime.RemotePlayerAlive && !_returnCommittedBeforeClosure))
            {
                return false;
            }

            if (_returnCommittedBeforeClosure)
            {
                _returnCommittedBeforeClosure = false;
                result = UniTask.CompletedTask;
                return true;
            }

            result = SpectateUntilReturnAsync();
            return true;
        }

        public void Tick()
        {
            if (!Active || LevelManager.Instance == null)
            {
                return;
            }

            var remote = _runtime.RemoteCharacter;
            if (remote != null && remote.gameObject.activeInHierarchy &&
                LevelManager.Instance.ControllingCharacter != remote)
            {
                LevelManager.Instance.SetControllingCharacter(remote);
            }
        }

        public void ReleaseForSceneCommit(SceneCommitMessage commit)
        {
            var key = !string.IsNullOrWhiteSpace(commit.SceneId) ? commit.SceneId : commit.SceneName;
            if (string.Equals(key, "Base", StringComparison.OrdinalIgnoreCase))
            {
                if (_returnCompletion != null)
                {
                    _returnCompletion.TrySetResult();
                }
                else
                {
                    _returnCommittedBeforeClosure = true;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _runtime.MessageReceived -= OnMessage;
            _runtime.Disconnected -= OnDisconnected;
            LevelManager.OnMainCharacterDead -= OnLocalPlayerDead;
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            _returnCompletion?.TrySetResult();
            _returnCompletion = null;
            _returnCommittedBeforeClosure = false;
        }

        private async UniTask SpectateUntilReturnAsync()
        {
            if (_returnCompletion == null)
            {
                _returnCompletion = new UniTaskCompletionSource();
            }

            Active = true;
            PartyRuntime.NotifyUser("你已阵亡，正在观战另一名玩家");
            Tick();
            await _returnCompletion.Task;
            Active = false;
            _returnCompletion = null;
        }

        private void OnLocalPlayerDead(DamageInfo damageInfo)
        {
            if (!_runtime.Connected)
            {
                return;
            }

            _runtime.Send(new PeerDeathMessage
            {
                PlayerId = _runtime.LocalPlayerId,
                SceneId = _runtime.CurrentSceneId,
            });

            if (_runtime.IsHost && !_runtime.RemotePlayerAlive)
            {
                _runtime.Scenes?.CommitPartyWipeToBase();
            }
        }

        private void OnMessage(PartyMessage message)
        {
            if (!(message is PeerDeathMessage death) ||
                !string.Equals(death.PlayerId, _runtime.RemotePlayerId, StringComparison.Ordinal))
            {
                return;
            }

            _runtime.RemotePlayerAlive = false;
            if (_runtime.IsHost && !_runtime.LocalPlayerAlive)
            {
                _runtime.Scenes?.CommitPartyWipeToBase();
            }
        }

        private void OnAfterLevelInitialized()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
            {
                Active = false;
                _returnCompletion?.TrySetResult();
                _returnCompletion = null;
                _returnCommittedBeforeClosure = false;
            }
        }

        private void OnDisconnected()
        {
            Active = false;
            _returnCompletion?.TrySetResult();
            _returnCompletion = null;
            _returnCommittedBeforeClosure = false;
        }
    }
}
