using Duckov;
using Duckov.UI;
using Duckov.UI.MainMenu;
using HarmonyLib;
using ItemStatsSystem.Data;
using ItemStatsSystem;
using SlimeNull.DockovParty.Configuration;
using SlimeNull.DockovParty.Networking;
using SlimeNull.DockovParty.Networking.Protocol;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class PartyRuntime : MonoBehaviour
    {
        private const string JoinButtonObjectName = "DockovParty_JoinGame";
        private const string JoinButtonText = "加入游戏";
        private const string ClientSavePrefix = "DockovParty/Players/";
        private const string ClientHealthSuffix = "/Health";

        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private readonly PartySession _session = new PartySession();
        private PartySettings? _settings;
        private Button? _joinButton;
        private TextMeshProUGUI? _joinButtonLabel;
        private bool _needsJoinButton;
        private bool _handshakeComplete;
        private string _sessionId = string.Empty;
        private string _localPlayerId = string.Empty;
        private string _remotePlayerId = string.Empty;
        private string _remotePlayerName = string.Empty;
        private string _assignedClientCharacterJson = string.Empty;
        private string _hostCharacterJson = string.Empty;
        private string _lastFailure = string.Empty;
        private bool _suppressClientSave;
        private bool _returnClientToMainMenu;
        private SceneCoordinator? _scenes;
        private PlayerReplicator? _players;
        private NpcReplicator? _npcs;
        private ContainerReplicator? _containers;
        private GroundItemReplicator? _groundItems;

        public static PartyRuntime? Instance { get; private set; }

        public PartyRole Role => _session.Role;
        public bool Connected => _handshakeComplete && _session.HasPeer;
        public bool IsHost => Role == PartyRole.Host;
        public bool IsClient => Role == PartyRole.Client;
        public string LocalPlayerId => _localPlayerId;
        public string RemotePlayerId => _remotePlayerId;
        public string AssignedClientCharacterJson => _assignedClientCharacterJson;
        public float AssignedClientHealth { get; private set; } = -1f;
        public string InitialHostSceneId { get; private set; } = string.Empty;
        public string InitialHostSceneName { get; private set; } = string.Empty;
        public bool LocalPlayerAlive => CharacterMainControl.Main == null || !CharacterMainControl.Main.Health.IsDead;
        public bool RemotePlayerAlive { get; internal set; } = true;
        public SceneCoordinator? Scenes => _scenes;
        public SpectatorController? Spectator { get; internal set; }
        public CharacterMainControl? RemoteCharacter { get; internal set; }
        public string CurrentSceneId => Duckov.Scenes.MultiSceneCore.MainScene.HasValue ?
            Duckov.Scenes.MultiSceneCore.MainSceneID ?? string.Empty :
            SceneInfoCollection.GetSceneID(SceneManager.GetActiveScene().buildIndex) ?? string.Empty;
        public string HostCharacterJson => _hostCharacterJson;
        public int StateRate => _settings?.StateRate ?? 15;
        public float InterpolationDelay => _settings?.InterpolationDelay ?? 0.1f;
        public NpcReplicator? Npcs => _npcs;
        public GroundItemReplicator? GroundItems => _groundItems;
        public bool SuppressClientSave => _suppressClientSave;

        public void Initialize(PartySettings settings)
        {
            if (Instance != null && Instance != this)
            {
                throw new InvalidOperationException("Only one DockovParty runtime may exist.");
            }

            Instance = this;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _localPlayerId = GetStablePlayerId();

            _session.PeerConnected += endpoint => Enqueue(() => OnPeerConnected(endpoint));
            _session.MessageReceived += message => Enqueue(() => OnMessage(message));
            _session.PeerClosed += failure => Enqueue(() => OnPeerClosed(failure));
            _session.Failed += failure => Enqueue(() => OnNetworkFailure(failure));

            MainMenu.OnMainMenuAwake += OnMainMenuAwake;
            MainMenu.OnMainMenuDestroy += OnMainMenuDestroy;
            _scenes = new SceneCoordinator(this);
            Spectator = new SpectatorController(this);
            _players = new PlayerReplicator(this);
            _npcs = new NpcReplicator(this);
            _containers = new ContainerReplicator(this);
            _groundItems = new GroundItemReplicator(this);
            _needsJoinButton = IsMainMenuLoaded();
        }

        public void BeginHostFromContinue()
        {
            if (_settings == null || Role != PartyRole.None)
            {
                return;
            }

            try
            {
                _lastFailure = string.Empty;
                if (!IPAddress.TryParse(_settings.ListenAddress, out var address))
                {
                    throw new InvalidOperationException($"无效监听地址: {_settings.ListenAddress}");
                }

                _sessionId = Guid.NewGuid().ToString("N");
                _session.StartHost(new TcpStreamListener(address, _settings.Port));
                Debug.Log($"[DockovParty] 正在监听 {_settings.ListenAddress}:{_settings.Port}");
            }
            catch (Exception ex)
            {
                OnNetworkFailure(ex);
            }
        }

        public void StopSession()
        {
            _players?.FlushForShutdown();
            _containers?.FlushForShutdown();
            _handshakeComplete = false;
            _remotePlayerId = string.Empty;
            _remotePlayerName = string.Empty;
            _assignedClientCharacterJson = string.Empty;
            _hostCharacterJson = string.Empty;
            AssignedClientHealth = -1f;
            InitialHostSceneId = string.Empty;
            InitialHostSceneName = string.Empty;
            RemotePlayerAlive = true;
            _session.Stop();
            SetJoinButtonState(JoinButtonText, interactable: true);
        }

        public void Send(PartyMessage message)
        {
            if (_session.HasPeer)
            {
                LogProtocol("send", message);
                _session.Send(message);
            }
        }

        private void Update()
        {
            var processed = 0;
            while (processed < 512 && _mainThreadActions.TryDequeue(out var action))
            {
                processed++;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DockovParty] 主线程任务失败: {ex}");
                }
            }

            if (_needsJoinButton && _joinButton == null)
            {
                TryCreateJoinButton();
            }

            if (_returnClientToMainMenu && !SceneLoader.IsSceneLoading && SceneLoader.Instance != null)
            {
                _returnClientToMainMenu = false;
                if (!IsMainMenuLoaded())
                {
                    SceneLoader.LoadMainMenu();
                }
            }

            Spectator?.Tick();
            _players?.Tick();
            _npcs?.Tick();
            _containers?.Tick();
            _groundItems?.Tick();
        }

        private void OnDestroy()
        {
            MainMenu.OnMainMenuAwake -= OnMainMenuAwake;
            MainMenu.OnMainMenuDestroy -= OnMainMenuDestroy;
            _scenes?.Dispose();
            _scenes = null;
            Spectator?.Dispose();
            Spectator = null;
            _players?.Dispose();
            _players = null;
            _npcs?.Dispose();
            _npcs = null;
            _containers?.Dispose();
            _containers = null;
            _groundItems?.Dispose();
            _groundItems = null;
            _session.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnMainMenuAwake()
        {
            _returnClientToMainMenu = false;
            var restoreLocalSave = _suppressClientSave;
            if (Role != PartyRole.None)
            {
                StopSession();
            }

            if (restoreLocalSave)
            {
                try
                {
                    Saves.SavesSystem.SetFile(Saves.SavesSystem.CurrentSlot);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DockovParty] 恢复客户端本地存档缓存失败: {ex}");
                }
                finally
                {
                    _suppressClientSave = false;
                }
            }

            _joinButton = null;
            _joinButtonLabel = null;
            _needsJoinButton = true;
        }

        private void OnMainMenuDestroy()
        {
            _needsJoinButton = false;
            _joinButton = null;
            _joinButtonLabel = null;
        }

        private void TryCreateJoinButton()
        {
            var existing = GameObject.Find(JoinButtonObjectName);
            if (existing != null)
            {
                _joinButton = existing.GetComponentInChildren<Button>(true);
                _joinButtonLabel = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                _needsJoinButton = false;
                return;
            }

            var source = Resources.FindObjectsOfTypeAll<ContinueButton>()
                .FirstOrDefault(button => button != null && button.gameObject.scene.isLoaded);
            if (source == null || source.transform.parent == null)
            {
                return;
            }

            var clone = Instantiate(source.gameObject, source.transform.parent, false);
            clone.name = JoinButtonObjectName;
            clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            _joinButton = clone.GetComponentInChildren<Button>(true);
            _joinButtonLabel = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_joinButton == null || _joinButtonLabel == null)
            {
                Destroy(clone);
                return;
            }

            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(JoinConfiguredHost);
            _joinButtonLabel.text = JoinButtonText;

            var clonedContinue = clone.GetComponent<ContinueButton>();
            if (clonedContinue != null)
            {
                Destroy(clonedContinue);
            }

            _needsJoinButton = false;
        }

        private void JoinConfiguredHost()
        {
            if (_settings == null || Role != PartyRole.None)
            {
                return;
            }

            SetJoinButtonState("连接中...", interactable: false);
            _lastFailure = string.Empty;
            var hello = CreateHelloMessage();
            var connector = new TcpStreamConnector(_settings.JoinAddress, _settings.Port);
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            _ = _session.StartClientAsync(connector, timeout.Token).ContinueWith(task =>
            {
                timeout.Dispose();
                if (!task.IsCompletedSuccessfully)
                {
                    var failure = task.IsCanceled ?
                        new TimeoutException("连接超时。") :
                        task.Exception?.GetBaseException() ?? new InvalidOperationException("连接失败。");
                    Enqueue(() =>
                    {
                        StopSession();
                        OnNetworkFailure(failure);
                    });
                    return;
                }

                try
                {
                    _session.Send(hello);
                }
                catch (Exception ex)
                {
                    Enqueue(() =>
                    {
                        StopSession();
                        OnNetworkFailure(ex);
                    });
                }
            }, TaskScheduler.Default);
        }

        private HelloMessage CreateHelloMessage()
        {
            return new HelloMessage
            {
                ProtocolVersion = PartyWireCodec.ProtocolVersion,
                ModVersion = typeof(PartyRuntime).Assembly.GetName().Version?.ToString() ?? "0.0.0.0",
                GameVersion = GameMetaData.Instance.Version.ToString(),
                PlayerId = _localPlayerId,
                PlayerName = _settings?.PlayerName ?? "玩家",
            };
        }

        private void OnPeerConnected(string endpoint)
        {
            Debug.Log($"[DockovParty] Stream 长连接已建立: {endpoint}");
            if (IsHost)
            {
                NotifyUser("玩家正在加入...");
            }
        }

        private void OnMessage(PartyMessage message)
        {
            LogProtocol("receive", message);
            switch (message)
            {
                case HelloMessage hello when IsHost:
                    HandleHello(hello);
                    break;
                case WelcomeMessage welcome when IsClient:
                    HandleWelcome(welcome);
                    break;
                case ErrorMessage error:
                    NotifyUser($"联机错误: {error.Description}");
                    Debug.LogError($"[DockovParty/{error.Code}] {error.Description}");
                    if (IsClient)
                    {
                        StopSession();
                    }

                    break;
                case NoticeMessage notice:
                    NotifyUser(notice.Text);
                    break;
                default:
                    MessageReceived?.Invoke(message);
                    break;
            }
        }

        public event Action<PartyMessage>? MessageReceived;
        public event Action? HandshakeCompleted;
        public event Action? Disconnected;

        private void HandleHello(HelloMessage hello)
        {
            var reason = ValidateHello(hello);
            if (reason != null)
            {
                Send(new WelcomeMessage { Accepted = false, Reason = reason });
                return;
            }

            _remotePlayerId = hello.PlayerId;
            _remotePlayerName = hello.PlayerName;
            var scene = GetCurrentScene();
            var hostCharacter = CaptureMainCharacterSnapshot();
            var clientCharacter = LoadClientCharacter(hello.PlayerId);
            var clientHealth = LoadClientHealth(hello.PlayerId);
            _assignedClientCharacterJson = clientCharacter;
            AssignedClientHealth = clientHealth;
            RemotePlayerAlive = clientHealth != 0f;

            Send(new WelcomeMessage
            {
                Accepted = true,
                SessionId = _sessionId,
                HostPlayerId = _localPlayerId,
                HostPlayerName = _settings?.PlayerName ?? "玩家",
                SceneId = scene.SceneId,
                SceneName = scene.SceneName,
                HostAlive = CharacterMainControl.Main == null || !CharacterMainControl.Main.Health.IsDead,
                ClientHealth = clientHealth,
                ClientAlive = clientHealth != 0f,
                HostCharacterJson = hostCharacter,
                ClientCharacterJson = clientCharacter,
            });

            _handshakeComplete = true;
            NotifyUser($"{_remotePlayerName} 已加入游戏");
            HandshakeCompleted?.Invoke();
        }

        private void HandleWelcome(WelcomeMessage welcome)
        {
            if (!welcome.Accepted)
            {
                var reason = string.IsNullOrWhiteSpace(welcome.Reason) ? "服主拒绝了连接。" : welcome.Reason;
                SetJoinButtonState(JoinButtonText, interactable: true);
                NotifyUser(reason);
                StopSession();
                return;
            }

            _sessionId = welcome.SessionId;
            _remotePlayerId = welcome.HostPlayerId;
            _remotePlayerName = welcome.HostPlayerName;
            _assignedClientCharacterJson = welcome.ClientCharacterJson;
            AssignedClientHealth = welcome.ClientHealth;
            _hostCharacterJson = welcome.HostCharacterJson;
            InitialHostSceneId = welcome.SceneId;
            InitialHostSceneName = welcome.SceneName;
            RemotePlayerAlive = welcome.HostAlive;
            _suppressClientSave = true;
            _handshakeComplete = true;
            SetJoinButtonState("已连接", interactable: false);
            NotifyUser($"已连接到 {_remotePlayerName} 的游戏");
            HandshakeCompleted?.Invoke();
        }

        private string? ValidateHello(HelloMessage hello)
        {
            if (hello.ProtocolVersion != PartyWireCodec.ProtocolVersion)
            {
                return $"协议版本不一致（服主 {PartyWireCodec.ProtocolVersion}，客户端 {hello.ProtocolVersion}）。";
            }

            if (string.IsNullOrWhiteSpace(hello.PlayerId))
            {
                return "客户端没有有效玩家标识。";
            }

            if (hello.PlayerId == _localPlayerId)
            {
                return "不能使用同一个 Steam 账号加入自己的游戏。";
            }

            var gameVersion = GameMetaData.Instance.Version.ToString();
            if (!string.Equals(gameVersion, hello.GameVersion, StringComparison.Ordinal))
            {
                return $"游戏版本不一致（服主 {gameVersion}，客户端 {hello.GameVersion}）。";
            }

            return null;
        }

        private void OnPeerClosed(Exception? failure)
        {
            var wasConnected = _handshakeComplete;
            _handshakeComplete = false;
            if (failure != null)
            {
                Debug.LogWarning($"[DockovParty] 连接断开: {failure.Message}");
            }

            if (wasConnected)
            {
                NotifyUser(IsHost ? "另一名玩家已断开" : "与服主的连接已断开");
            }

            if (wasConnected && _suppressClientSave && !IsMainMenuLoaded())
            {
                _returnClientToMainMenu = true;
            }

            SetJoinButtonState(JoinButtonText, interactable: true);

            Disconnected?.Invoke();
            _remotePlayerId = string.Empty;
            _remotePlayerName = string.Empty;
        }

        private void OnNetworkFailure(Exception failure)
        {
            var message = failure.GetBaseException().Message;
            if (_lastFailure == message)
            {
                return;
            }

            _lastFailure = message;
            Debug.LogError($"[DockovParty] 网络错误: {failure}");
            NotifyUser($"联机失败: {message}");
            if (IsClient)
            {
                SetJoinButtonState(JoinButtonText, interactable: true);
            }
        }

        public string CaptureMainCharacterSnapshot()
        {
            var characterItem = CharacterMainControl.Main?.CharacterItem;
            return characterItem == null ? string.Empty :
                GameDataSerializer.SerializeItem(ItemTreeData.FromItem(characterItem));
        }

        public void UpdateAssignedClientCharacter(string json)
        {
            if (IsClient && !string.IsNullOrWhiteSpace(json))
            {
                _assignedClientCharacterJson = json;
            }
        }

        public void UpdateAssignedClientHealth(float health)
        {
            if (IsClient)
            {
                AssignedClientHealth = health;
            }
        }

        public bool ShouldLoadAssignedClientCharacter(string key)
        {
            return IsClient && Connected && key == LevelManager.MainCharacterItemSaveKey;
        }

        public async Cysharp.Threading.Tasks.UniTask<Item> InstantiateAssignedClientCharacterAsync()
        {
            var data = GameDataSerializer.DeserializeItem(_assignedClientCharacterJson);
            var item = await ItemTreeData.InstantiateAsync(data);
            if (item != null)
            {
                return item;
            }

            return await ItemStatsSystem.ItemAssetsCollection.InstantiateAsync(
                Duckov.Utilities.GameplayDataSettings.ItemAssets.DefaultCharacterItemTypeID);
        }

        private static string LoadClientCharacter(string playerId)
        {
            try
            {
                return Saves.SavesSystem.Load<string>(ClientSavePrefix + SanitizeSaveKey(playerId)) ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockovParty] 读取客户端角色存档失败: {ex.Message}");
                return string.Empty;
            }
        }

        public void StoreClientCharacter(string playerId, string json, float health = -1f)
        {
            if (!IsHost || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            Saves.SavesSystem.Save(ClientSavePrefix + SanitizeSaveKey(playerId), json);
            if (health >= 0f)
            {
                Saves.SavesSystem.Save(
                    ClientSavePrefix + SanitizeSaveKey(playerId) + ClientHealthSuffix,
                    health);
            }
        }

        private static float LoadClientHealth(string playerId)
        {
            try
            {
                var key = ClientSavePrefix + SanitizeSaveKey(playerId) + ClientHealthSuffix;
                return Saves.SavesSystem.KeyExisits(key) ? Saves.SavesSystem.Load<float>(key) : -1f;
            }
            catch
            {
                return -1f;
            }
        }

        private static (string SceneId, string SceneName) GetCurrentScene()
        {
            var active = SceneManager.GetActiveScene();
            var sceneId = Duckov.Scenes.MultiSceneCore.MainScene.HasValue ?
                Duckov.Scenes.MultiSceneCore.MainSceneID ?? string.Empty :
                SceneInfoCollection.GetSceneID(active.buildIndex) ?? string.Empty;
            var sceneName = SceneInfoCollection.GetSceneInfo(sceneId)?.SceneReference?.Name;

            return (sceneId, sceneName ?? active.name ?? string.Empty);
        }

        private void SetJoinButtonState(string text, bool interactable)
        {
            if (_joinButtonLabel != null)
            {
                _joinButtonLabel.text = text;
            }

            if (_joinButton != null)
            {
                _joinButton.interactable = interactable;
            }
        }

        private static bool IsMainMenuLoaded()
        {
            return SceneManager.GetActiveScene().name == "MainMenu" ||
                Resources.FindObjectsOfTypeAll<MainMenu>().Any(menu => menu != null && menu.gameObject.scene.isLoaded);
        }

        private static string GetStablePlayerId()
        {
            var id = PlatformInfo.GetID();
            return string.IsNullOrWhiteSpace(id) ? SystemInfo.deviceUniqueIdentifier : id.Trim();
        }

        private static string SanitizeSaveKey(string value)
        {
            var chars = value.Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_').ToArray();
            return chars.Length == 0 ? "unknown" : new string(chars);
        }

        public static void NotifyUser(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                NotificationText.Push(text);
            }
        }

        private void Enqueue(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        private void LogProtocol(string direction, PartyMessage message)
        {
            if (_settings?.DiagnosticLogging != true ||
                message is PlayerStateMessage || message is NpcStateBatchMessage ||
                message is PingMessage || message is PongMessage)
            {
                return;
            }

            Debug.Log($"[DockovParty] {direction} {message.Kind}");
        }
    }
}
