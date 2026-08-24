using Cysharp.Threading.Tasks;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using SlimeNull.DockovParty.Networking.Protocol;
using System;
using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class PlayerReplicator : IDisposable
    {
        private const float SnapshotInterval = 1.5f;

        private readonly PartyRuntime _runtime;
        private PlayerStateMessage? _latestRemoteState;
        private string _remoteCharacterJson = string.Empty;
        private string _lastLocalCharacterJson = string.Empty;
        private float _nextStateTime;
        private float _nextSnapshotTime;
        private float _hostSaveAt;
        private uint _sequence;
        private bool _hostSavePending;
        private bool _creatingRemote;
        private bool _disposed;

        public PlayerReplicator(PartyRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.MessageReceived += OnMessage;
            _runtime.HandshakeCompleted += OnHandshakeCompleted;
            _runtime.Disconnected += OnDisconnected;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            Health.OnHurt += OnHealthHurt;
            Health.OnDead += OnHealthDead;
        }

        public void Tick()
        {
            if (!_runtime.Connected || !LevelManager.LevelInited || CharacterMainControl.Main == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextStateTime)
            {
                _nextStateTime = Time.unscaledTime + 1f / Mathf.Max(5, _runtime.StateRate);
                SendLocalState();
            }

            if (Time.unscaledTime >= _nextSnapshotTime)
            {
                _nextSnapshotTime = Time.unscaledTime + SnapshotInterval;
                SendCharacterSnapshotIfChanged();
            }

            if (_runtime.RemoteCharacter == null && !_creatingRemote && _latestRemoteState != null &&
                SameScene(_latestRemoteState.SceneId, _runtime.CurrentSceneId))
            {
                EnsureRemoteCharacterAsync().Forget();
            }

            if (_runtime.IsHost && _hostSavePending && Time.unscaledTime >= _hostSaveAt)
            {
                FlushHostClientSave();
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
            _runtime.HandshakeCompleted -= OnHandshakeCompleted;
            _runtime.Disconnected -= OnDisconnected;
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            Health.OnHurt -= OnHealthHurt;
            Health.OnDead -= OnHealthDead;
            FlushForShutdown();
            DestroyRemoteCharacter();
        }

        public void FlushForShutdown()
        {
            PersistRemoteCharacter();
            FlushHostClientSave();
        }

        private void OnHandshakeCompleted()
        {
            _remoteCharacterJson = _runtime.IsClient ?
                _runtime.HostCharacterJson : _runtime.AssignedClientCharacterJson;
            if (LevelManager.LevelInited)
            {
                OnAfterLevelInitialized();
            }
        }

        private void OnAfterLevelInitialized()
        {
            DestroyRemoteCharacter();
            _latestRemoteState = null;
            _nextStateTime = 0f;
            _nextSnapshotTime = 0f;

            if (!_runtime.Connected)
            {
                return;
            }

            if (_runtime.IsClient && _runtime.AssignedClientHealth >= 0f &&
                LevelManager.Instance != null && !LevelManager.Instance.IsBaseLevel)
            {
                ApplyHealth(
                    CharacterMainControl.Main?.Health,
                    _runtime.AssignedClientHealth,
                    _runtime.AssignedClientHealth > 0f);
            }

            SendCharacterSnapshot(force: true);
        }

        private void OnMessage(PartyMessage message)
        {
            switch (message)
            {
                case PlayerStateMessage state when
                    string.Equals(state.PlayerId, _runtime.RemotePlayerId, StringComparison.Ordinal):
                    HandleRemoteState(state);
                    break;
                case CharacterSnapshotMessage snapshot when
                    string.Equals(snapshot.PlayerId, _runtime.RemotePlayerId, StringComparison.Ordinal):
                    var characterChanged = !string.Equals(
                        _remoteCharacterJson,
                        snapshot.ItemTreeJson,
                        StringComparison.Ordinal);
                    _remoteCharacterJson = snapshot.ItemTreeJson;
                    _runtime.RemotePlayerAlive = snapshot.Alive;
                    if (_runtime.IsHost)
                    {
                        _runtime.StoreClientCharacter(snapshot.PlayerId, snapshot.ItemTreeJson, snapshot.Health);
                        ScheduleHostSave();
                    }

                    if (characterChanged && _runtime.RemoteCharacter != null)
                    {
                        DestroyRemoteCharacter();
                    }

                    if (_runtime.RemoteCharacter == null && _latestRemoteState != null)
                    {
                        EnsureRemoteCharacterAsync().Forget();
                    }

                    break;
                case PlayerHealthAuthorityMessage authority when _runtime.IsClient &&
                    string.Equals(authority.PlayerId, _runtime.LocalPlayerId, StringComparison.Ordinal):
                    _runtime.UpdateAssignedClientHealth(authority.Health);
                    ApplyHealth(CharacterMainControl.Main?.Health, authority.Health, authority.Alive);
                    break;
                case PeerDeathMessage death when
                    string.Equals(death.PlayerId, _runtime.RemotePlayerId, StringComparison.Ordinal):
                    _runtime.RemotePlayerAlive = false;
                    break;
            }
        }

        private void HandleRemoteState(PlayerStateMessage state)
        {
            _latestRemoteState = state;
            _runtime.RemotePlayerAlive = state.Alive;
            var remote = _runtime.RemoteCharacter;
            if (remote == null)
            {
                EnsureRemoteCharacterAsync().Forget();
                return;
            }

            if (!SameScene(state.SceneId, _runtime.CurrentSceneId))
            {
                DestroyRemoteCharacter();
                return;
            }

            remote.GetComponent<RemotePlayerReplica>()?.Apply(state);

            if (_runtime.IsHost)
            {
                if (!state.Alive)
                {
                    ApplyHealth(remote.Health, 0f, alive: false);
                }
                else if (state.Health > remote.Health.CurrentHealth)
                {
                    remote.Health.SetHealth(state.Health);
                    StoreRemoteHealthAndScheduleSave();
                }

                SendAuthoritativeRemoteHealth();
            }
            else
            {
                ApplyHealth(remote.Health, state.Health, state.Alive);
            }
        }

        private async UniTask EnsureRemoteCharacterAsync()
        {
            if (_creatingRemote || _runtime.RemoteCharacter != null || _latestRemoteState == null ||
                !LevelManager.LevelInited || LevelManager.Instance?.MainCharacter == null)
            {
                return;
            }

            if (!SameScene(_latestRemoteState.SceneId, _runtime.CurrentSceneId))
            {
                return;
            }

            _creatingRemote = true;
            try
            {
                Item? characterItem = null;
                var data = GameDataSerializer.DeserializeItem(_remoteCharacterJson);
                if (data != null)
                {
                    characterItem = await ItemTreeData.InstantiateAsync(data);
                }

                if (characterItem == null)
                {
                    characterItem = await ItemAssetsCollection.InstantiateAsync(
                        Duckov.Utilities.GameplayDataSettings.ItemAssets.DefaultCharacterItemTypeID);
                }

                if (characterItem == null || !_runtime.Connected || !LevelManager.LevelInited ||
                    LevelManager.Instance?.MainCharacter == null)
                {
                    characterItem?.DestroyTree();
                    return;
                }

                var state = _latestRemoteState;
                if (state == null || !SameScene(state.SceneId, _runtime.CurrentSceneId))
                {
                    characterItem.DestroyTree();
                    return;
                }

                var main = LevelManager.Instance.MainCharacter;
                var position = state.Transform.Position.ToUnity();
                var rotation = state.Transform.Rotation.ToUnity();
                var remote = await LevelManager.Instance.CharacterCreator.CreateCharacter(
                    characterItem,
                    main.defaultCharacterModelPrefab,
                    position,
                    rotation);
                if (remote == null)
                {
                    characterItem.DestroyTree();
                    return;
                }

                if (!_runtime.Connected || !LevelManager.LevelInited)
                {
                    UnityEngine.Object.Destroy(remote.gameObject);
                    return;
                }

                remote.gameObject.name = $"DockovParty_RemotePlayer_{_runtime.RemotePlayerId}";
                remote.SetTeam(Teams.player);
                remote.CharacterItem.Inventory.AcceptSticky = true;
                remote.Health.showHealthBar = true;
                remote.Health.SetHealth(Mathf.Max(1f, state.Health));

                var replica = remote.gameObject.AddComponent<RemotePlayerReplica>();
                replica.Initialize(_runtime.RemotePlayerId, remote, _runtime.InterpolationDelay);
                replica.Apply(state);

                DisableAutonomousControl(remote);
                _runtime.RemoteCharacter = remote;
                _runtime.RemotePlayerAlive = state.Alive;

            }
            catch (Exception ex)
            {
                Debug.LogError($"[DockovParty] 创建远端玩家失败: {ex}");
            }
            finally
            {
                _creatingRemote = false;
            }
        }

        private void SendLocalState()
        {
            var main = CharacterMainControl.Main;
            if (main == null || main.Health == null)
            {
                return;
            }

            var visualRotation = main.modelRoot != null ? main.modelRoot.rotation : main.transform.rotation;
            var heldItem = main.agentHolder?.CurrentHoldItemAgent?.Item;
            _runtime.Send(new PlayerStateMessage
            {
                Sequence = ++_sequence,
                PlayerId = _runtime.LocalPlayerId,
                SceneId = _runtime.CurrentSceneId,
                Transform = main.transform.ToNetwork(visualRotation),
                Velocity = main.Velocity.ToNetwork(),
                AimPoint = main.GetCurrentAimPoint().ToNetwork(),
                Health = main.Health.CurrentHealth,
                MaxHealth = main.Health.MaxHealth,
                Alive = !main.Health.IsDead,
                Running = main.Running,
                Aiming = main.IsAiming(),
                HeldItemTypeId = heldItem?.TypeID ?? 0,
            });

            if (_runtime.IsHost && _runtime.RemoteCharacter != null)
            {
                SendAuthoritativeRemoteHealth();
            }
        }

        private void SendCharacterSnapshotIfChanged()
        {
            var json = _runtime.CaptureMainCharacterSnapshot();
            if (!string.Equals(json, _lastLocalCharacterJson, StringComparison.Ordinal))
            {
                SendCharacterSnapshot(json, force: false);
            }
        }

        private void SendCharacterSnapshot(bool force)
        {
            SendCharacterSnapshot(_runtime.CaptureMainCharacterSnapshot(), force);
        }

        private void SendCharacterSnapshot(string json, bool force)
        {
            if (string.IsNullOrWhiteSpace(json) || (!force && json == _lastLocalCharacterJson))
            {
                return;
            }

            _lastLocalCharacterJson = json;
            var health = CharacterMainControl.Main?.Health;
            if (_runtime.IsClient)
            {
                _runtime.UpdateAssignedClientCharacter(json);
                _runtime.UpdateAssignedClientHealth(health?.CurrentHealth ?? -1f);
            }

            _runtime.Send(new CharacterSnapshotMessage
            {
                PlayerId = _runtime.LocalPlayerId,
                ItemTreeJson = json,
                Health = health?.CurrentHealth ?? 0f,
                Alive = health == null || !health.IsDead,
            });
        }

        private void OnHealthHurt(Health health, DamageInfo damageInfo)
        {
            if (!_runtime.IsHost || !_runtime.Connected || _runtime.RemoteCharacter == null ||
                health != _runtime.RemoteCharacter.Health)
            {
                return;
            }

            SendAuthoritativeRemoteHealth();
            StoreRemoteHealthAndScheduleSave();
        }

        private void OnHealthDead(Health health, DamageInfo damageInfo)
        {
            if (!_runtime.IsHost || !_runtime.Connected || _runtime.RemoteCharacter == null ||
                health != _runtime.RemoteCharacter.Health)
            {
                return;
            }

            _runtime.RemotePlayerAlive = false;
            SendAuthoritativeRemoteHealth();
            StoreRemoteHealthAndScheduleSave();
            _runtime.Send(new PeerDeathMessage
            {
                PlayerId = _runtime.RemotePlayerId,
                SceneId = _runtime.CurrentSceneId,
            });
        }

        private void SendAuthoritativeRemoteHealth()
        {
            var health = _runtime.RemoteCharacter?.Health;
            if (health == null)
            {
                return;
            }

            _runtime.Send(new PlayerHealthAuthorityMessage
            {
                PlayerId = _runtime.RemotePlayerId,
                Health = health.CurrentHealth,
                Alive = !health.IsDead,
            });
        }

        private void OnDisconnected()
        {
            PersistRemoteCharacter();
            FlushHostClientSave();
            _latestRemoteState = null;
            _remoteCharacterJson = string.Empty;
            _lastLocalCharacterJson = string.Empty;
            DestroyRemoteCharacter();
        }

        private void PersistRemoteCharacter()
        {
            var remote = _runtime.RemoteCharacter;
            if (!_runtime.IsHost || remote?.CharacterItem == null ||
                string.IsNullOrWhiteSpace(_runtime.RemotePlayerId))
            {
                return;
            }

            try
            {
                _runtime.StoreClientCharacter(
                    _runtime.RemotePlayerId,
                    GameDataSerializer.SerializeItem(ItemTreeData.FromItem(remote.CharacterItem)),
                    remote.Health?.CurrentHealth ?? 0f);
                _hostSavePending = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockovParty] 保存断线玩家角色失败: {ex.Message}");
            }
        }

        private void StoreRemoteHealthAndScheduleSave()
        {
            var health = _runtime.RemoteCharacter?.Health;
            if (!_runtime.IsHost || health == null || string.IsNullOrWhiteSpace(_remoteCharacterJson))
            {
                return;
            }

            _runtime.StoreClientCharacter(
                _runtime.RemotePlayerId,
                _remoteCharacterJson,
                health.CurrentHealth);
            ScheduleHostSave();
        }

        private void ScheduleHostSave()
        {
            if (!_runtime.IsHost)
            {
                return;
            }

            if (!_hostSavePending)
            {
                _hostSavePending = true;
                _hostSaveAt = Time.unscaledTime + 2f;
            }
        }

        private void FlushHostClientSave()
        {
            if (!_runtime.IsHost || !_hostSavePending)
            {
                return;
            }

            _hostSavePending = false;
            try
            {
                Saves.SavesSystem.SaveFile();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockovParty] 写入客户端角色存档失败: {ex.Message}");
                _hostSavePending = true;
                _hostSaveAt = Time.unscaledTime + 2f;
            }
        }

        private void DestroyRemoteCharacter()
        {
            var remote = _runtime.RemoteCharacter;
            _runtime.RemoteCharacter = null;
            if (remote != null)
            {
                UnityEngine.Object.Destroy(remote.gameObject);
            }
        }

        private static void DisableAutonomousControl(CharacterMainControl remote)
        {
            foreach (var behaviour in remote.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour is Health || behaviour is Animator ||
                    behaviour is RemotePlayerReplica)
                {
                    continue;
                }

                var name = behaviour.GetType().Name;
                if (name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("AICharacter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("PathControl", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    behaviour.enabled = false;
                }
            }

            remote.enabled = false;
        }

        private static void ApplyHealth(Health? health, float targetHealth, bool alive)
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            if (alive && targetHealth > 0f)
            {
                health.SetHealth(targetHealth);
                return;
            }

            var damage = new DamageInfo(null);
            damage.damageType = DamageTypes.realDamage;
            damage.damageValue = Mathf.Max(health.CurrentHealth + 1f, 1f);
            damage.ignoreArmor = true;
            damage.ignoreDifficulty = true;
            damage.damagePoint = health.transform.position;
            damage.damageNormal = Vector3.up;
            health.Hurt(damage);
        }

        private static bool SameScene(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
