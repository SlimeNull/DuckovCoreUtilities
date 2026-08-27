using Cysharp.Threading.Tasks;
using Duckov.Scenes;
using Eflatun.SceneReference;
using ItemStatsSystem;
using SlimeNull.DockovParty.Networking.Protocol;
using SlimeNull.DockovParty.Localization;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class SceneCoordinator : IDisposable
    {
        private readonly PartyRuntime _runtime;
        private PendingSceneLoad? _pendingLoad;
        private SceneReadyMessage? _hostReady;
        private SceneReadyMessage? _clientReady;
        private string _activeTransitionId = string.Empty;
        private string _preapprovedSceneKey = string.Empty;
        private bool _bypass;
        private bool _initialClientLoadStarted;
        private bool _partyWipeCommitted;
        private bool _disposed;

        public SceneCoordinator(PartyRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.MessageReceived += OnMessage;
            _runtime.HandshakeCompleted += OnHandshakeCompleted;
            _runtime.Disconnected += OnDisconnected;
            SceneLoader.onFinishedLoadingScene += OnFinishedLoadingScene;
        }

        public bool TryIntercept(
            SceneLoader loader,
            SceneReference sceneReference,
            SceneReference overrideCurtainScene,
            bool clickToContinue,
            bool notifyEvacuation,
            bool doCircleFade,
            bool useLocation,
            MultiSceneLocation location,
            bool saveToFile,
            bool hideTips,
            out UniTask result)
        {
            result = default;
            if (_bypass || !_runtime.Connected || sceneReference == null)
            {
                return false;
            }

            var sceneKey = GetSceneKey(sceneReference);
            if (IsMainMenu(sceneReference))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_preapprovedSceneKey) &&
                string.Equals(_preapprovedSceneKey, sceneKey, StringComparison.Ordinal))
            {
                _preapprovedSceneKey = string.Empty;
                return false;
            }

            result = CoordinateLoadAsync(new PendingSceneLoad(
                loader,
                sceneReference,
                overrideCurtainScene,
                clickToContinue,
                notifyEvacuation,
                doCircleFade,
                useLocation,
                location,
                saveToFile,
                hideTips,
                sceneKey));
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _runtime.MessageReceived -= OnMessage;
            _runtime.HandshakeCompleted -= OnHandshakeCompleted;
            _runtime.Disconnected -= OnDisconnected;
            SceneLoader.onFinishedLoadingScene -= OnFinishedLoadingScene;
            ReleasePendingForSoloPlay();
        }

        public void CommitPartyWipeToBase()
        {
            if (!_runtime.IsHost || !_runtime.Connected || _partyWipeCommitted)
            {
                return;
            }

            var baseInfo = SceneInfoCollection.GetSceneInfo("Base");
            if (baseInfo?.SceneReference == null)
            {
                return;
            }

            var commit = new SceneCommitMessage
            {
                TransitionId = Guid.NewGuid().ToString("N"),
                SceneId = "Base",
                SceneName = baseInfo.SceneReference.Name ?? string.Empty,
            };
            _partyWipeCommitted = true;
            _activeTransitionId = commit.TransitionId;
            _runtime.Send(commit);
            HandleCommit(commit);
        }

        private async UniTask CoordinateLoadAsync(PendingSceneLoad request)
        {
            if (_pendingLoad != null)
            {
                if (string.Equals(_pendingLoad.SceneKey, request.SceneKey, StringComparison.Ordinal))
                {
                    PartyRuntime.NotifyUser(SettingsText.WaitingForScene);
                    return;
                }

                _pendingLoad.Cancel();
                _pendingLoad = null;
            }

            _pendingLoad = request;
            var ready = CreateReadyMessage(request);
            if (_runtime.IsHost)
            {
                _hostReady = ready;
                TryCommitOnHost();
            }
            else
            {
                _runtime.Send(ready);
            }

            PartyRuntime.NotifyUser(SettingsText.ReadyWaiting);

            SceneCommitMessage commit;
            try
            {
                commit = await request.Completion.Task;
            }
            finally
            {
                if (ReferenceEquals(_pendingLoad, request))
                {
                    _pendingLoad = null;
                }
            }

            if (request.Cancelled)
            {
                return;
            }

            if (!string.Equals(request.SceneKey, GetSceneKey(commit), StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"[DockovParty] 场景提交与本地请求不一致: {request.SceneKey} != {GetSceneKey(commit)}");
                return;
            }

            _runtime.UpdateAssignedClientCharacter(ready.CharacterJson);
            _runtime.UpdateAssignedClientHealth(ready.Health);
            await ExecuteOriginalAsync(request);
        }

        private SceneReadyMessage CreateReadyMessage(PendingSceneLoad request)
        {
            var snapshot = _runtime.CaptureMainCharacterSnapshot();
            var health = CharacterMainControl.Main?.Health;
            var sceneId = SceneInfoCollection.GetSceneID(request.SceneReference) ?? string.Empty;
            return new SceneReadyMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                PlayerId = _runtime.LocalPlayerId,
                SceneId = sceneId,
                SceneName = request.SceneReference.Name ?? string.Empty,
                IsBase = string.Equals(sceneId, "Base", StringComparison.Ordinal) ||
                    string.Equals(request.SceneReference.Name, "Base", StringComparison.OrdinalIgnoreCase),
                CharacterJson = snapshot,
                Health = health?.CurrentHealth ?? 0f,
                Alive = health == null || !health.IsDead,
            };
        }

        private void OnMessage(PartyMessage message)
        {
            switch (message)
            {
                case SceneReadyMessage ready when _runtime.IsHost:
                    if (!string.Equals(ready.PlayerId, _runtime.RemotePlayerId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _clientReady = ready;
                    _runtime.StoreClientCharacter(ready.PlayerId, ready.CharacterJson, ready.Health);
                    TryCommitOnHost();
                    break;
                case SceneCommitMessage commit:
                    HandleCommit(commit);
                    break;
                case SceneLoadedMessage loaded when _runtime.IsClient:
                    if (!_initialClientLoadStarted && !LevelManager.LevelInited)
                    {
                        BeginInitialClientLoad(loaded.SceneId, string.Empty);
                    }

                    break;
            }
        }

        private void TryCommitOnHost()
        {
            if (!_runtime.IsHost || !_runtime.Connected)
            {
                return;
            }

            var hostReady = _hostReady;
            var clientReady = _clientReady;
            if (hostReady == null && clientReady == null)
            {
                return;
            }

            SceneReadyMessage? target = null;
            if (hostReady != null && clientReady != null)
            {
                if (SameScene(hostReady, clientReady))
                {
                    target = hostReady;
                }
                else
                {
                    _runtime.Send(new NoticeMessage { Text = SettingsText.DestinationMismatchRemote });
                    PartyRuntime.NotifyUser(SettingsText.DestinationMismatchLocal);
                    return;
                }
            }
            else if (hostReady != null && hostReady.IsBase && !_runtime.RemotePlayerAlive)
            {
                target = hostReady;
            }
            else if (clientReady != null && clientReady.IsBase && !_runtime.LocalPlayerAlive)
            {
                target = clientReady;
            }

            if (target == null)
            {
                return;
            }

            var commit = new SceneCommitMessage
            {
                TransitionId = Guid.NewGuid().ToString("N"),
                SceneId = target.SceneId,
                SceneName = target.SceneName,
            };
            _activeTransitionId = commit.TransitionId;
            _hostReady = null;
            _clientReady = null;

            _runtime.Send(commit);
            HandleCommit(commit);
        }

        private void HandleCommit(SceneCommitMessage commit)
        {
            _activeTransitionId = commit.TransitionId;
            var pending = _pendingLoad;
            if (pending != null &&
                string.Equals(pending.SceneKey, GetSceneKey(commit), StringComparison.Ordinal))
            {
                pending.Completion.TrySetResult(commit);
                return;
            }

            _preapprovedSceneKey = GetSceneKey(commit);
            _runtime.Spectator?.ReleaseForSceneCommit(commit);

            if (_runtime.IsClient && !LevelManager.LevelInited)
            {
                BeginInitialClientLoad(commit.SceneId, commit.SceneName);
            }
        }

        private void OnHandshakeCompleted()
        {
            if (_runtime.IsClient)
            {
                BeginInitialClientLoad(_runtime.InitialHostSceneId, _runtime.InitialHostSceneName);
            }
        }

        private void BeginInitialClientLoad(string sceneId, string sceneName)
        {
            if (_initialClientLoadStarted || SceneLoader.Instance == null)
            {
                return;
            }

            var info = string.IsNullOrWhiteSpace(sceneId) ? null : SceneInfoCollection.GetSceneInfo(sceneId);
            var reference = info?.SceneReference;
            if (reference == null || string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _initialClientLoadStarted = true;
            ForceInitialLoadAsync(reference).Forget();
        }

        private async UniTask ForceInitialLoadAsync(SceneReference target)
        {
            try
            {
                _bypass = true;
                UniTask operation;
                try
                {
                    operation = SceneLoader.Instance.LoadScene(
                        target,
                        null,
                        clickToConinue: false,
                        notifyEvacuation: false,
                        doCircleFade: true,
                        useLocation: false,
                        default,
                        saveToFile: false,
                        hideTips: false);
                }
                finally
                {
                    _bypass = false;
                }

                await operation;
            }
            catch (Exception ex)
            {
                _initialClientLoadStarted = false;
                Debug.LogError($"[DockovParty] 加载服主场景失败: {ex}");
                PartyRuntime.NotifyUser(SettingsText.HostSceneLoadFailed);
            }
        }

        private async UniTask ExecuteOriginalAsync(PendingSceneLoad request)
        {
            _bypass = true;
            UniTask operation;
            try
            {
                operation = request.Loader.LoadScene(
                    request.SceneReference,
                    request.OverrideCurtainScene,
                    request.ClickToContinue,
                    request.NotifyEvacuation,
                    request.DoCircleFade,
                    request.UseLocation,
                    request.Location,
                    request.SaveToFile,
                    request.HideTips);
            }
            finally
            {
                _bypass = false;
            }

            await operation;
        }

        private void OnFinishedLoadingScene(SceneLoadingContext context)
        {
            if (!_runtime.Connected)
            {
                return;
            }

            var sceneId = SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex) ?? string.Empty;
            if (string.Equals(sceneId, "Base", StringComparison.Ordinal))
            {
                _partyWipeCommitted = false;
            }

            _runtime.Send(new SceneLoadedMessage
            {
                PlayerId = _runtime.LocalPlayerId,
                TransitionId = _activeTransitionId,
                SceneId = sceneId,
            });
        }

        private void OnDisconnected()
        {
            _initialClientLoadStarted = false;
            _hostReady = null;
            _clientReady = null;
            _activeTransitionId = string.Empty;
            _preapprovedSceneKey = string.Empty;
            _partyWipeCommitted = false;
            if (_runtime.SuppressClientSave)
            {
                _pendingLoad?.Cancel();
                _pendingLoad = null;
            }
            else
            {
                ReleasePendingForSoloPlay();
            }
        }

        private void ReleasePendingForSoloPlay()
        {
            var pending = _pendingLoad;
            if (pending == null)
            {
                return;
            }

            pending.Completion.TrySetResult(new SceneCommitMessage
            {
                TransitionId = string.Empty,
                SceneId = SceneInfoCollection.GetSceneID(pending.SceneReference) ?? string.Empty,
                SceneName = pending.SceneReference.Name ?? string.Empty,
            });
        }

        private static bool SameScene(SceneReadyMessage left, SceneReadyMessage right)
        {
            return string.Equals(GetSceneKey(left.SceneId, left.SceneName),
                GetSceneKey(right.SceneId, right.SceneName), StringComparison.Ordinal);
        }

        private static string GetSceneKey(SceneReference reference)
        {
            return GetSceneKey(SceneInfoCollection.GetSceneID(reference), reference.Name);
        }

        private static string GetSceneKey(SceneCommitMessage commit)
        {
            return GetSceneKey(commit.SceneId, commit.SceneName);
        }

        private static string GetSceneKey(string? sceneId, string? sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneId) ? sceneId! : sceneName ?? string.Empty;
        }

        private static bool IsMainMenu(SceneReference reference)
        {
            var mainMenu = Duckov.Utilities.GameplayDataSettings.SceneManagement?.MainMenuScene;
            return string.Equals(reference.Name, mainMenu?.Name, StringComparison.Ordinal) ||
                string.Equals(reference.Name, "MainMenu", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class PendingSceneLoad
        {
            public PendingSceneLoad(
                SceneLoader loader,
                SceneReference sceneReference,
                SceneReference overrideCurtainScene,
                bool clickToContinue,
                bool notifyEvacuation,
                bool doCircleFade,
                bool useLocation,
                MultiSceneLocation location,
                bool saveToFile,
                bool hideTips,
                string sceneKey)
            {
                Loader = loader;
                SceneReference = sceneReference;
                OverrideCurtainScene = overrideCurtainScene;
                ClickToContinue = clickToContinue;
                NotifyEvacuation = notifyEvacuation;
                DoCircleFade = doCircleFade;
                UseLocation = useLocation;
                Location = location;
                SaveToFile = saveToFile;
                HideTips = hideTips;
                SceneKey = sceneKey;
            }

            public SceneLoader Loader { get; }
            public SceneReference SceneReference { get; }
            public SceneReference OverrideCurtainScene { get; }
            public bool ClickToContinue { get; }
            public bool NotifyEvacuation { get; }
            public bool DoCircleFade { get; }
            public bool UseLocation { get; }
            public MultiSceneLocation Location { get; }
            public bool SaveToFile { get; }
            public bool HideTips { get; }
            public string SceneKey { get; }
            public bool Cancelled { get; private set; }
            public UniTaskCompletionSource<SceneCommitMessage> Completion { get; } =
                new UniTaskCompletionSource<SceneCommitMessage>();

            public void Cancel()
            {
                Cancelled = true;
                Completion.TrySetResult(new SceneCommitMessage());
            }
        }
    }
}
