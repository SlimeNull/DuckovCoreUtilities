using Cysharp.Threading.Tasks;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using SlimeNull.DockovParty.Networking.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class GroundItemReplicator : IDisposable
    {
        private const float ScanInterval = 0.25f;

        private readonly PartyRuntime _runtime;
        private readonly Dictionary<string, InteractablePickup> _hostItems =
            new Dictionary<string, InteractablePickup>();
        private readonly Dictionary<int, string> _hostIdsByInstance = new Dictionary<int, string>();
        private readonly Dictionary<string, InteractablePickup> _clientItems =
            new Dictionary<string, InteractablePickup>();
        private readonly Dictionary<string, PendingPickup> _pendingPickups =
            new Dictionary<string, PendingPickup>();
        private readonly HashSet<string> _pendingSpawns = new HashSet<string>();
        private readonly HashSet<string> _despawnedClientIds = new HashSet<string>();
        private readonly List<InteractablePickup> _pendingClientDrops = new List<InteractablePickup>();
        private int _nextGroundId = 1;
        private float _nextScanTime;
        private bool _applyingSpawn;
        private bool _allowClientPickup;
        private bool _processingRemotePickup;
        private bool _disposed;

        public GroundItemReplicator(PartyRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.MessageReceived += OnMessage;
            _runtime.HandshakeCompleted += OnHandshakeCompleted;
            _runtime.Disconnected += OnDisconnected;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            InteractablePickup.OnPickupSuccess += OnPickupSuccess;
        }

        public bool ApplyingSpawn => _applyingSpawn;

        public void Tick()
        {
            if (!_runtime.Connected || !_runtime.IsHost || !LevelManager.LevelInited ||
                Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + ScanInterval;
            DiscoverHostItems();
        }

        public void RegisterHostDrop(DuckovItemAgent? agent)
        {
            if (!_runtime.IsHost || !_runtime.Connected || agent == null)
            {
                return;
            }

            var pickup = agent.GetComponent<InteractablePickup>();
            if (pickup != null)
            {
                RegisterHostItem(pickup, broadcast: true);
            }
        }

        public void ReportClientDrop(Item item, DuckovItemAgent? agent)
        {
            if (!_runtime.IsClient || !_runtime.Connected || _applyingSpawn || item == null || agent == null)
            {
                return;
            }

            _runtime.Send(new GroundSpawnMessage
            {
                Item = CreateGroundState(string.Empty, item, agent.transform),
            });

            var health = CharacterMainControl.Main?.Health;
            var characterJson = _runtime.CaptureMainCharacterSnapshot();
            _runtime.UpdateAssignedClientCharacter(characterJson);
            _runtime.UpdateAssignedClientHealth(health?.CurrentHealth ?? -1f);
            _runtime.Send(new CharacterSnapshotMessage
            {
                PlayerId = _runtime.LocalPlayerId,
                ItemTreeJson = characterJson,
                Health = health?.CurrentHealth ?? 0f,
                Alive = health == null || !health.IsDead,
            });

            var pickup = agent.GetComponent<InteractablePickup>();
            if (pickup != null)
            {
                _pendingClientDrops.Add(pickup);
                pickup.gameObject.SetActive(false);
            }
        }

        public bool TryRequestClientPickup(
            CharacterMainControl character,
            Item item,
            out bool result)
        {
            result = false;
            if (_allowClientPickup || !_runtime.IsClient || !_runtime.Connected ||
                character != CharacterMainControl.Main || item?.ActiveAgent == null)
            {
                return false;
            }

            var pickup = item.ActiveAgent.GetComponent<InteractablePickup>();
            var tag = pickup?.GetComponent<NetworkGroundItem>();
            if (pickup == null || tag == null || string.IsNullOrEmpty(tag.GroundId))
            {
                return false;
            }

            if (_pendingPickups.Values.Any(value => value.GroundId == tag.GroundId))
            {
                result = false;
                return true;
            }

            var requestId = Guid.NewGuid().ToString("N");
            _pendingPickups.Add(requestId, new PendingPickup(tag.GroundId, character, item, pickup));
            _runtime.Send(new GroundPickupRequestMessage
            {
                RequestId = requestId,
                GroundId = tag.GroundId,
                ItemTypeId = item.TypeID,
                Position = pickup.transform.position.ToNetwork(),
            });
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
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            InteractablePickup.OnPickupSuccess -= OnPickupSuccess;
            Clear();
        }

        private void OnMessage(PartyMessage message)
        {
            switch (message)
            {
                case GroundSpawnMessage spawn when _runtime.IsHost &&
                    string.IsNullOrEmpty(spawn.Item.GroundId):
                    CreateHostDropFromClientAsync(spawn.Item).Forget();
                    break;
                case GroundSpawnMessage spawn when _runtime.IsClient:
                    EnsureClientGroundItemAsync(spawn.Item).Forget();
                    break;
                case GroundSnapshotMessage snapshot when _runtime.IsClient:
                    ApplyClientSnapshotAsync(snapshot).Forget();
                    break;
                case GroundPickupRequestMessage request when _runtime.IsHost:
                    HandleHostPickupRequest(request);
                    break;
                case GroundPickupResultMessage result when _runtime.IsClient:
                    HandleClientPickupResult(result);
                    break;
                case GroundDespawnMessage despawn when _runtime.IsClient:
                    _despawnedClientIds.Add(despawn.GroundId);
                    DespawnClientItem(despawn.GroundId);
                    break;
            }
        }

        private void OnHandshakeCompleted()
        {
            if (_runtime.IsHost && LevelManager.LevelInited)
            {
                DiscoverHostItems();
                SendHostSnapshot();
            }
        }

        private void OnAfterLevelInitialized()
        {
            Clear();
            _nextScanTime = 0f;
            if (_runtime.IsHost && _runtime.Connected)
            {
                DiscoverHostItems();
                SendHostSnapshot();
            }
        }

        private void DiscoverHostItems()
        {
            foreach (var pickup in UnityEngine.Object.FindObjectsOfType<InteractablePickup>(true))
            {
                RegisterHostItem(pickup, broadcast: true);
            }
        }

        private void RegisterHostItem(InteractablePickup? pickup, bool broadcast)
        {
            var item = pickup?.ItemAgent?.Item;
            if (pickup == null || item == null)
            {
                return;
            }

            var instanceId = pickup.GetInstanceID();
            if (_hostIdsByInstance.ContainsKey(instanceId))
            {
                return;
            }

            var groundId = $"{_runtime.CurrentSceneId}:g:{_nextGroundId++}";
            _hostIdsByInstance.Add(instanceId, groundId);
            _hostItems.Add(groundId, pickup);
            var tag = pickup.GetComponent<NetworkGroundItem>() ?? pickup.gameObject.AddComponent<NetworkGroundItem>();
            tag.Initialize(groundId, pickup);

            if (broadcast)
            {
                _runtime.Send(new GroundSpawnMessage
                {
                    Item = CreateGroundState(groundId, item, pickup.transform),
                });
            }
        }

        private void SendHostSnapshot()
        {
            var snapshot = new GroundSnapshotMessage();
            foreach (var pair in _hostItems.ToArray())
            {
                var pickup = pair.Value;
                var item = pickup?.ItemAgent?.Item;
                if (pickup == null || item == null)
                {
                    _hostItems.Remove(pair.Key);
                    continue;
                }

                snapshot.Items.Add(CreateGroundState(pair.Key, item, pickup.transform));
            }

            _runtime.Send(snapshot);
        }

        private async UniTask CreateHostDropFromClientAsync(GroundItemState state)
        {
            var data = GameDataSerializer.DeserializeItem(state.ItemTreeJson);
            var item = await ItemTreeData.InstantiateAsync(data);
            if (item == null || !LevelManager.LevelInited)
            {
                item?.DestroyTree();
                return;
            }

            item.Drop(
                state.Transform.Position.ToUnity(),
                createRigidbody: false,
                Vector3.forward,
                0f);
        }

        private async UniTask EnsureClientGroundItemAsync(GroundItemState state)
        {
            if (string.IsNullOrEmpty(state.GroundId) || _clientItems.ContainsKey(state.GroundId) ||
                _despawnedClientIds.Contains(state.GroundId) || !_pendingSpawns.Add(state.GroundId))
            {
                return;
            }

            try
            {
                var match = FindUntaggedClientMatch(state);
                if (match != null)
                {
                    _pendingClientDrops.Remove(match);
                    DestroyPickup(match);
                }

                var data = GameDataSerializer.DeserializeItem(state.ItemTreeJson);
                var item = await ItemTreeData.InstantiateAsync(data);
                if (item == null || !_runtime.Connected || !_runtime.IsClient ||
                    !LevelManager.LevelInited ||
                    _despawnedClientIds.Contains(state.GroundId))
                {
                    item?.DestroyTree();
                    return;
                }

                _applyingSpawn = true;
                DuckovItemAgent? agent;
                try
                {
                    agent = item.Drop(
                        state.Transform.Position.ToUnity(),
                        createRigidbody: false,
                        Vector3.forward,
                        0f);
                }
                finally
                {
                    _applyingSpawn = false;
                }

                var pickup = agent?.GetComponent<InteractablePickup>();
                if (pickup == null)
                {
                    item.DestroyTree();
                    return;
                }

                pickup.transform.rotation = state.Transform.Rotation.ToUnity();
                var tag = pickup.gameObject.AddComponent<NetworkGroundItem>();
                tag.Initialize(state.GroundId, pickup);
                _clientItems[state.GroundId] = pickup;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DockovParty] 同步地面物品失败: {ex}");
            }
            finally
            {
                _pendingSpawns.Remove(state.GroundId);
            }
        }

        private async UniTask ApplyClientSnapshotAsync(GroundSnapshotMessage snapshot)
        {
            var authoritativeIds = new HashSet<string>(snapshot.Items.Select(item => item.GroundId));
            foreach (var item in snapshot.Items)
            {
                await EnsureClientGroundItemAsync(item);
            }

            foreach (var pair in _clientItems.ToArray())
            {
                if (!authoritativeIds.Contains(pair.Key))
                {
                    DespawnClientItem(pair.Key);
                }
            }

            foreach (var pickup in UnityEngine.Object.FindObjectsOfType<InteractablePickup>(true))
            {
                if (pickup != null && pickup.GetComponent<NetworkGroundItem>() == null)
                {
                    DestroyPickup(pickup);
                }
            }
        }

        private void HandleHostPickupRequest(GroundPickupRequestMessage request)
        {
            var accepted = false;
            var reason = "物品已被其他玩家取走";
            if (_hostItems.TryGetValue(request.GroundId, out var pickup) && pickup != null &&
                pickup.ItemAgent?.Item != null && pickup.ItemAgent.Item.TypeID == request.ItemTypeId &&
                _runtime.RemoteCharacter != null && _runtime.RemotePlayerAlive)
            {
                var item = pickup.ItemAgent.Item;
                _processingRemotePickup = true;
                try
                {
                    accepted = _runtime.RemoteCharacter.PickupItem(item);
                }
                finally
                {
                    _processingRemotePickup = false;
                }

                if (accepted)
                {
                    _hostItems.Remove(request.GroundId);
                    _hostIdsByInstance.Remove(pickup.GetInstanceID());
                    var remote = _runtime.RemoteCharacter;
                    if (remote?.CharacterItem != null)
                    {
                        _runtime.StoreClientCharacter(
                            _runtime.RemotePlayerId,
                            GameDataSerializer.SerializeItem(ItemTreeData.FromItem(remote.CharacterItem)),
                            remote.Health?.CurrentHealth ?? 0f);
                    }

                    reason = string.Empty;
                }
                else
                {
                    reason = "背包没有可用空间";
                }
            }

            _runtime.Send(new GroundPickupResultMessage
            {
                RequestId = request.RequestId,
                Accepted = accepted,
                GroundId = request.GroundId,
                Reason = reason,
            });
        }

        private void HandleClientPickupResult(GroundPickupResultMessage result)
        {
            if (!_pendingPickups.TryGetValue(result.RequestId, out var pending))
            {
                return;
            }

            _pendingPickups.Remove(result.RequestId);
            if (!result.Accepted || pending.Item == null || pending.Character == null)
            {
                PartyRuntime.NotifyUser(string.IsNullOrWhiteSpace(result.Reason) ?
                    "物品已被取走" : result.Reason);
                return;
            }

            _allowClientPickup = true;
            try
            {
                pending.Character.PickupItem(pending.Item);
            }
            finally
            {
                _allowClientPickup = false;
            }

            _clientItems.Remove(pending.GroundId);
        }

        private void OnPickupSuccess(InteractablePickup pickup, CharacterMainControl character)
        {
            if (!_runtime.IsHost || !_runtime.Connected || pickup == null)
            {
                return;
            }

            var tag = pickup.GetComponent<NetworkGroundItem>();
            if (tag == null || string.IsNullOrEmpty(tag.GroundId))
            {
                return;
            }

            _hostItems.Remove(tag.GroundId);
            _hostIdsByInstance.Remove(pickup.GetInstanceID());
            if (_processingRemotePickup)
            {
                return;
            }

            _runtime.Send(new GroundDespawnMessage
            {
                GroundId = tag.GroundId,
                ItemTypeId = pickup.ItemAgent?.Item?.TypeID ?? 0,
                Position = pickup.transform.position.ToNetwork(),
            });
        }

        private void DespawnClientItem(string groundId)
        {
            if (_clientItems.TryGetValue(groundId, out var pickup))
            {
                _clientItems.Remove(groundId);
                DestroyPickup(pickup);
            }
        }

        private InteractablePickup? FindUntaggedClientMatch(GroundItemState state)
        {
            InteractablePickup? closest = null;
            var closestDistance = float.MaxValue;
            var target = state.Transform.Position.ToUnity();
            foreach (var pickup in UnityEngine.Object.FindObjectsOfType<InteractablePickup>(true))
            {
                if (pickup == null || pickup.GetComponent<NetworkGroundItem>() != null ||
                    pickup.ItemAgent?.Item?.TypeID != state.ItemTypeId)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(pickup.transform.position - target);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = pickup;
                }
            }

            return closestDistance <= 4f ? closest : null;
        }

        private static GroundItemState CreateGroundState(string groundId, Item item, Transform transform)
        {
            return new GroundItemState
            {
                GroundId = groundId,
                ItemTypeId = item.TypeID,
                Transform = transform.ToNetwork(transform.rotation),
                ItemTreeJson = GameDataSerializer.SerializeItem(ItemTreeData.FromItem(item)),
            };
        }

        private static void DestroyPickup(InteractablePickup? pickup)
        {
            if (pickup == null)
            {
                return;
            }

            var item = pickup.ItemAgent?.Item;
            if (item != null)
            {
                item.AgentUtilities.ReleaseActiveAgent();
                item.Detach();
                item.DestroyTree();
            }

            if (pickup != null)
            {
                UnityEngine.Object.Destroy(pickup.gameObject);
            }
        }

        private void OnDisconnected()
        {
            foreach (var pickup in _pendingClientDrops)
            {
                if (pickup != null)
                {
                    pickup.gameObject.SetActive(true);
                }
            }

            Clear();
        }

        private void Clear()
        {
            _hostItems.Clear();
            _hostIdsByInstance.Clear();
            _clientItems.Clear();
            _pendingPickups.Clear();
            _pendingSpawns.Clear();
            _despawnedClientIds.Clear();
            _pendingClientDrops.Clear();
            _nextGroundId = 1;
            _applyingSpawn = false;
            _allowClientPickup = false;
            _processingRemotePickup = false;
        }

        private sealed class PendingPickup
        {
            public PendingPickup(
                string groundId,
                CharacterMainControl character,
                Item item,
                InteractablePickup pickup)
            {
                GroundId = groundId;
                Character = character;
                Item = item;
                Pickup = pickup;
            }

            public string GroundId { get; }
            public CharacterMainControl Character { get; }
            public Item Item { get; }
            public InteractablePickup Pickup { get; }
        }
    }
}
