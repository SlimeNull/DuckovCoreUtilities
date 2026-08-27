using Cysharp.Threading.Tasks;
using Duckov.UI;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using Saves;
using SlimeNull.DockovParty.Networking.Protocol;
using SlimeNull.DockovParty.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class ContainerReplicator : IDisposable
    {
        private const string StorageContainerId = "storage";

        private readonly PartyRuntime _runtime;
        private readonly Dictionary<string, long> _versions = new Dictionary<string, long>();
        private readonly Dictionary<string, HostLease> _hostLeases = new Dictionary<string, HostLease>();
        private readonly Dictionary<string, PendingLease> _pendingClientLeases =
            new Dictionary<string, PendingLease>();
        private readonly HashSet<int> _knownHostContainers = new HashSet<int>();
        private readonly SemaphoreSlim _inventoryApplyGate = new SemaphoreSlim(1, 1);
        private ClientLease? _clientLease;
        private int _applyingInventoryDepth;
        private bool _closingDeniedView;
        private bool _clientDirty;
        private bool _clientCommitInFlight;
        private bool _clientReleaseRequested;
        private float _clientCommitAt;
        private float _nextHostScanTime;
        private bool _disposed;

        public ContainerReplicator(PartyRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.MessageReceived += OnMessage;
            _runtime.Disconnected += OnDisconnected;
            InteractableLootbox.OnStartLoot += OnStartLoot;
            InteractableLootbox.OnStopLoot += OnStopLoot;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            PlayerStorage.OnPlayerStorageChange += OnPlayerStorageChanged;
        }

        public void Tick()
        {
            if (_runtime.IsHost && _runtime.Connected && LevelManager.LevelInited &&
                Time.unscaledTime >= _nextHostScanTime)
            {
                _nextHostScanTime = Time.unscaledTime + 0.25f;
                DiscoverHostContainers();
            }

            if (_runtime.IsClient && _clientLease != null && _clientDirty &&
                !_clientCommitInFlight && !_clientReleaseRequested &&
                Time.unscaledTime >= _clientCommitAt)
            {
                SendClientCommit();
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
            InteractableLootbox.OnStartLoot -= OnStartLoot;
            InteractableLootbox.OnStopLoot -= OnStopLoot;
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            PlayerStorage.OnPlayerStorageChange -= OnPlayerStorageChanged;
            Clear();
        }

        public void FlushForShutdown()
        {
            if (_runtime.IsHost && _hostLeases.Values.Any(lease => lease.RemoteOwned))
            {
                SaveHostData();
            }
        }

        private void OnStartLoot(InteractableLootbox lootbox)
        {
            if (!_runtime.Connected || lootbox == null || _closingDeniedView)
            {
                return;
            }

            var inventory = lootbox.Inventory;
            if (inventory == null)
            {
                return;
            }

            var containerId = GetContainerId(lootbox);
            if (_runtime.IsHost)
            {
                BeginHostLocalLease(containerId, lootbox, inventory);
            }
            else
            {
                BeginClientLease(containerId, lootbox, inventory);
            }
        }

        private void OnStopLoot(InteractableLootbox lootbox)
        {
            if (!_runtime.Connected || lootbox == null || _closingDeniedView)
            {
                return;
            }

            var containerId = GetContainerId(lootbox);
            if (_runtime.IsHost)
            {
                if (_hostLeases.TryGetValue(containerId, out var lease) && !lease.RemoteOwned)
                {
                    lease.Inventory.onContentChanged -= OnHostInventoryChanged;
                    _hostLeases.Remove(containerId);
                    BroadcastContainer(lease.Lootbox, lease.Inventory, lease.Version);
                }
            }
            else if (_clientLease != null && _clientLease.ContainerId == containerId)
            {
                _clientReleaseRequested = true;
                if (!_clientCommitInFlight)
                {
                    SendClientRelease();
                }
            }
        }

        private void BeginHostLocalLease(
            string containerId,
            InteractableLootbox lootbox,
            Inventory inventory)
        {
            if (_hostLeases.TryGetValue(containerId, out var existing))
            {
                if (existing.RemoteOwned)
                {
                    DenyCurrentView(lootbox, SettingsText.ContainerInUse);
                }

                return;
            }

            var lease = new HostLease(containerId, lootbox, inventory, remoteOwned: false, GetVersion(containerId));
            _hostLeases.Add(containerId, lease);
            inventory.onContentChanged += OnHostInventoryChanged;
        }

        private void BeginClientLease(
            string containerId,
            InteractableLootbox lootbox,
            Inventory inventory)
        {
            if (_clientLease != null || _pendingClientLeases.Count > 0)
            {
                DenyCurrentView(lootbox, SettingsText.ContainerSyncInProgress);
                return;
            }

            inventory.Loading = true;
            var requestId = Guid.NewGuid().ToString("N");
            _pendingClientLeases.Add(requestId, new PendingLease(containerId, lootbox, inventory));
            _runtime.Send(new ContainerLeaseRequestMessage
            {
                RequestId = requestId,
                ContainerId = containerId,
            });
        }

        private void OnMessage(PartyMessage message)
        {
            switch (message)
            {
                case ContainerLeaseRequestMessage request when _runtime.IsHost:
                    HandleHostLeaseRequest(request);
                    break;
                case ContainerLeaseResultMessage result when _runtime.IsClient:
                    HandleClientLeaseResultAsync(result).Forget();
                    break;
                case ContainerCommitMessage commit when _runtime.IsHost:
                    HandleHostCommitAsync(commit).Forget();
                    break;
                case ContainerReleaseMessage release when _runtime.IsHost:
                    HandleHostReleaseAsync(release).Forget();
                    break;
                case ContainerReleaseResultMessage result when _runtime.IsClient:
                    HandleClientReleaseResultAsync(result).Forget();
                    break;
                case ContainerSpawnMessage spawn when _runtime.IsClient:
                    ApplyContainerSpawnAsync(spawn).Forget();
                    break;
            }
        }

        private void HandleHostLeaseRequest(ContainerLeaseRequestMessage request)
        {
            if (_hostLeases.ContainsKey(request.ContainerId))
            {
                SendLeaseResult(request, null, granted: false, SettingsText.ContainerInUse);
                return;
            }

            var target = FindContainer(request.ContainerId);
            var inventory = target?.Inventory;
            if (target == null || inventory == null || inventory.Loading)
            {
                SendLeaseResult(request, null, granted: false, SettingsText.ContainerNotLoaded);
                return;
            }

            var lease = new HostLease(
                request.ContainerId,
                target,
                inventory,
                remoteOwned: true,
                GetVersion(request.ContainerId));
            _hostLeases.Add(request.ContainerId, lease);
            SendLeaseResult(request, lease, granted: true, string.Empty);
        }

        private void SendLeaseResult(
            ContainerLeaseRequestMessage request,
            HostLease? lease,
            bool granted,
            string reason)
        {
            _runtime.Send(new ContainerLeaseResultMessage
            {
                RequestId = request.RequestId,
                ContainerId = request.ContainerId,
                Granted = granted,
                Reason = reason,
                Version = lease?.Version ?? GetVersion(request.ContainerId),
                InventoryJson = lease == null ? string.Empty : SerializeInventory(lease.Inventory),
            });
        }

        private async UniTask HandleClientLeaseResultAsync(ContainerLeaseResultMessage result)
        {
            if (!string.IsNullOrEmpty(result.RequestId))
            {
                if (!_pendingClientLeases.TryGetValue(result.RequestId, out var pending))
                {
                    return;
                }

                _pendingClientLeases.Remove(result.RequestId);
                if (!result.Granted)
                {
                    pending.Inventory.Loading = false;
                    DenyCurrentView(pending.Lootbox, result.Reason);
                    return;
                }

                _clientLease = new ClientLease(
                    pending.ContainerId,
                    pending.Lootbox,
                    pending.Inventory,
                    result.Version);
                pending.Inventory.onContentChanged += OnClientInventoryChanged;
            }
            else if (_clientLease == null || _clientLease.ContainerId != result.ContainerId)
            {
                return;
            }

            var lease = _clientLease;
            if (lease == null)
            {
                return;
            }

            if (!string.Equals(
                    SerializeInventory(lease.Inventory),
                    result.InventoryJson,
                    StringComparison.Ordinal))
            {
                await ApplyInventoryAsync(lease.Inventory, result.InventoryJson);
            }

            lease.Version = result.Version;
            lease.Inventory.Loading = false;
            _clientCommitInFlight = false;

            if (_clientReleaseRequested)
            {
                SendClientRelease();
            }
            else if (_clientDirty)
            {
                _clientCommitAt = Time.unscaledTime + 0.05f;
            }
        }

        private void OnClientInventoryChanged(Inventory inventory, int index)
        {
            if (_applyingInventoryDepth > 0 || _clientLease == null || inventory != _clientLease.Inventory)
            {
                return;
            }

            _clientDirty = true;
            _clientCommitAt = Time.unscaledTime + 0.08f;
        }

        private void SendClientCommit()
        {
            var lease = _clientLease;
            if (lease == null)
            {
                return;
            }

            _clientDirty = false;
            _clientCommitInFlight = true;
            var health = CharacterMainControl.Main?.Health;
            _runtime.Send(new ContainerCommitMessage
            {
                ContainerId = lease.ContainerId,
                BaseVersion = lease.Version,
                InventoryJson = SerializeInventory(lease.Inventory),
                CharacterJson = _runtime.CaptureMainCharacterSnapshot(),
                CharacterHealth = health?.CurrentHealth ?? 0f,
            });
        }

        private void SendClientRelease()
        {
            var lease = _clientLease;
            if (lease == null)
            {
                return;
            }

            _clientDirty = false;
            _clientCommitInFlight = true;
            var health = CharacterMainControl.Main?.Health;
            _runtime.Send(new ContainerReleaseMessage
            {
                ContainerId = lease.ContainerId,
                BaseVersion = lease.Version,
                InventoryJson = SerializeInventory(lease.Inventory),
                CharacterJson = _runtime.CaptureMainCharacterSnapshot(),
                CharacterHealth = health?.CurrentHealth ?? 0f,
            });
        }

        private async UniTask HandleClientReleaseResultAsync(ContainerReleaseResultMessage result)
        {
            var lease = _clientLease;
            if (lease == null || !_clientReleaseRequested ||
                !string.Equals(lease.ContainerId, result.ContainerId, StringComparison.Ordinal))
            {
                return;
            }

            if (result.Accepted)
            {
                lease.Version = result.Version;
                _clientCommitInFlight = false;
                ClearClientLease();
                return;
            }

            if (!string.Equals(
                    SerializeInventory(lease.Inventory),
                    result.InventoryJson,
                    StringComparison.Ordinal))
            {
                await ApplyInventoryAsync(lease.Inventory, result.InventoryJson);
            }

            lease.Version = result.Version;
            _clientCommitInFlight = false;
            if (_clientLease == lease && _clientReleaseRequested)
            {
                SendClientRelease();
            }
        }

        private async UniTask HandleHostCommitAsync(ContainerCommitMessage commit)
        {
            if (!_hostLeases.TryGetValue(commit.ContainerId, out var lease) || !lease.RemoteOwned)
            {
                return;
            }

            if (!await TryApplyHostCommitAsync(commit, lease))
            {
                SendHostCorrection(lease);
                return;
            }

            SendHostCorrection(lease);
        }

        private async UniTask HandleHostReleaseAsync(ContainerReleaseMessage release)
        {
            if (!_hostLeases.TryGetValue(release.ContainerId, out var lease) || !lease.RemoteOwned)
            {
                SendHostReleaseResult(
                    release.ContainerId,
                    accepted: true,
                    reason: SettingsText.ContainerLeaseEnded,
                    GetVersion(release.ContainerId),
                    string.Empty);
                return;
            }

            var commit = new ContainerCommitMessage
            {
                ContainerId = release.ContainerId,
                BaseVersion = release.BaseVersion,
                InventoryJson = release.InventoryJson,
                CharacterJson = release.CharacterJson,
                CharacterHealth = release.CharacterHealth,
            };
            if (!await TryApplyHostCommitAsync(commit, lease))
            {
                SendHostReleaseResult(
                    lease.ContainerId,
                    accepted: false,
                    reason: SettingsText.ContainerVersionUpdated,
                    lease.Version,
                    SerializeInventory(lease.Inventory));
                return;
            }

            _hostLeases.Remove(release.ContainerId);
            SendHostReleaseResult(
                lease.ContainerId,
                accepted: true,
                reason: string.Empty,
                lease.Version,
                SerializeInventory(lease.Inventory));

            SaveHostData();
        }

        private async UniTask<bool> TryApplyHostCommitAsync(
            ContainerCommitMessage commit,
            HostLease lease)
        {
            if (lease.Applying || commit.BaseVersion != lease.Version)
            {
                return false;
            }

            lease.Applying = true;
            var remotePlayerId = _runtime.RemotePlayerId;
            try
            {
                await ApplyInventoryAsync(lease.Inventory, commit.InventoryJson);
                lease.Version++;
                _versions[lease.ContainerId] = lease.Version;
                _runtime.StoreClientCharacter(
                    remotePlayerId,
                    commit.CharacterJson,
                    commit.CharacterHealth);
                if (!_runtime.Connected)
                {
                    SaveHostData();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockovParty] 提交共享容器失败: {ex.Message}");
                return false;
            }
            finally
            {
                lease.Applying = false;
            }
        }

        private void SendHostReleaseResult(
            string containerId,
            bool accepted,
            string reason,
            long version,
            string inventoryJson)
        {
            _runtime.Send(new ContainerReleaseResultMessage
            {
                ContainerId = containerId,
                Accepted = accepted,
                Reason = reason,
                Version = version,
                InventoryJson = inventoryJson,
            });
        }

        private static void SaveHostData()
        {
            try
            {
                SavesSystem.CollectSaveData();
                SavesSystem.SaveFile();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockovParty] 保存共享容器失败: {ex.Message}");
            }
        }

        private void SendHostCorrection(HostLease lease)
        {
            _runtime.Send(new ContainerLeaseResultMessage
            {
                RequestId = string.Empty,
                ContainerId = lease.ContainerId,
                Granted = true,
                Version = lease.Version,
                InventoryJson = SerializeInventory(lease.Inventory),
            });
        }

        private void OnHostInventoryChanged(Inventory inventory, int index)
        {
            if (_applyingInventoryDepth > 0)
            {
                return;
            }

            var lease = _hostLeases.Values.FirstOrDefault(value => value.Inventory == inventory);
            if (lease == null || lease.RemoteOwned)
            {
                return;
            }

            lease.Version++;
            _versions[lease.ContainerId] = lease.Version;
        }

        private void OnPlayerStorageChanged(PlayerStorage storage, Inventory inventory, int index)
        {
            if (!_runtime.Connected || _applyingInventoryDepth > 0)
            {
                return;
            }

            if (_runtime.IsClient && _clientLease?.Inventory == inventory)
            {
                OnClientInventoryChanged(inventory, index);
                return;
            }

            if (_runtime.IsHost && !_hostLeases.ContainsKey(StorageContainerId))
            {
                _versions[StorageContainerId] = GetVersion(StorageContainerId) + 1;
                var lootbox = PlayerStorage.Instance?.InteractableLootBox;
                if (lootbox != null)
                {
                    BroadcastContainer(lootbox, inventory, _versions[StorageContainerId]);
                }
            }
        }

        private void DiscoverHostContainers()
        {
            foreach (var lootbox in UnityEngine.Object.FindObjectsOfType<InteractableLootbox>(true))
            {
                if (lootbox == null || !_knownHostContainers.Add(lootbox.GetInstanceID()))
                {
                    continue;
                }

                var inventory = lootbox.Inventory;
                if (inventory == null || inventory.Loading)
                {
                    _knownHostContainers.Remove(lootbox.GetInstanceID());
                    continue;
                }

                BroadcastContainer(lootbox, inventory, GetVersion(GetContainerId(lootbox)));
            }
        }

        private void BroadcastContainer(InteractableLootbox lootbox, Inventory inventory, long version)
        {
            if (!_runtime.IsHost || !_runtime.Connected || lootbox == null || inventory == null || inventory.Loading)
            {
                return;
            }

            _runtime.Send(new ContainerSpawnMessage
            {
                ContainerId = GetContainerId(lootbox),
                Transform = lootbox.transform.ToNetwork(lootbox.transform.rotation),
                Version = version,
                InventoryJson = SerializeInventory(inventory),
            });
        }

        private async UniTask ApplyContainerSpawnAsync(ContainerSpawnMessage spawn)
        {
            if (_clientLease != null && _clientLease.ContainerId == spawn.ContainerId)
            {
                return;
            }

            var lootbox = FindContainer(spawn.ContainerId);
            if (lootbox == null)
            {
                var prefab = InteractableLootbox.Prefab;
                if (prefab == null)
                {
                    return;
                }

                lootbox = UnityEngine.Object.Instantiate(
                    prefab,
                    spawn.Transform.Position.ToUnity(),
                    spawn.Transform.Rotation.ToUnity());
                lootbox.gameObject.name = $"DockovParty_Container_{spawn.ContainerId}";
                var tag = lootbox.gameObject.AddComponent<NetworkContainer>();
                tag.Initialize(spawn.ContainerId);
            }

            var inventory = lootbox.Inventory;
            if (inventory == null)
            {
                return;
            }

            await ApplyInventoryAsync(inventory, spawn.InventoryJson);
            _versions[spawn.ContainerId] = spawn.Version;
        }

        private async UniTask ApplyInventoryAsync(Inventory inventory, string json)
        {
            var data = GameDataSerializer.DeserializeInventory(json);
            if (data == null || inventory == null)
            {
                return;
            }

            await _inventoryApplyGate.WaitAsync();
            _applyingInventoryDepth++;
            inventory.Loading = true;
            try
            {
                inventory.DestroyAllContent();
                inventory.SetCapacity(data.capacity);
                await InventoryData.LoadIntoInventory(data, inventory);
                inventory.lockedIndexes.Clear();
                if (data.lockedIndexes != null)
                {
                    inventory.lockedIndexes.AddRange(data.lockedIndexes);
                }
            }
            finally
            {
                inventory.Loading = false;
                _applyingInventoryDepth--;
                _inventoryApplyGate.Release();
            }
        }

        private void DenyCurrentView(InteractableLootbox lootbox, string reason)
        {
            _closingDeniedView = true;
            try
            {
                lootbox.StopInteract();
                LootView.Instance?.Close();
            }
            finally
            {
                _closingDeniedView = false;
            }

            PartyRuntime.NotifyUser(string.IsNullOrWhiteSpace(reason) ? SettingsText.ContainerOpenFailed : reason);
        }

        private InteractableLootbox? FindContainer(string containerId)
        {
            if (containerId == StorageContainerId)
            {
                return PlayerStorage.Instance?.InteractableLootBox;
            }

            return UnityEngine.Object.FindObjectsOfType<InteractableLootbox>(true)
                .FirstOrDefault(value => value != null &&
                    (value.GetComponent<NetworkContainer>()?.ContainerId == containerId ||
                     GetContainerId(value) == containerId));
        }

        private string GetContainerId(InteractableLootbox lootbox)
        {
            if (PlayerStorage.Instance != null &&
                (lootbox == PlayerStorage.Instance.InteractableLootBox || lootbox.Inventory == PlayerStorage.Inventory))
            {
                return StorageContainerId;
            }

            var position = lootbox.transform.position * 10f;
            return $"{_runtime.CurrentSceneId}:{Mathf.RoundToInt(position.x)}:{Mathf.RoundToInt(position.y)}:{Mathf.RoundToInt(position.z)}";
        }

        private long GetVersion(string containerId)
        {
            return _versions.TryGetValue(containerId, out var value) ? value : 0L;
        }

        private static string SerializeInventory(Inventory inventory)
        {
            return GameDataSerializer.SerializeInventory(InventoryData.FromInventory(inventory));
        }

        private void OnAfterLevelInitialized()
        {
            Clear();
        }

        private void OnDisconnected()
        {
            if (_runtime.IsHost && _hostLeases.Values.Any(lease => lease.RemoteOwned))
            {
                SaveHostData();
            }

            Clear();
        }

        private void Clear()
        {
            foreach (var lease in _hostLeases.Values)
            {
                if (!lease.RemoteOwned && lease.Inventory != null)
                {
                    lease.Inventory.onContentChanged -= OnHostInventoryChanged;
                }
            }

            _hostLeases.Clear();
            _knownHostContainers.Clear();
            foreach (var pending in _pendingClientLeases.Values)
            {
                if (pending.Inventory != null)
                {
                    pending.Inventory.Loading = false;
                }
            }

            _pendingClientLeases.Clear();
            _versions.Clear();
            _nextHostScanTime = 0f;
            ClearClientLease();
        }

        private void ClearClientLease()
        {
            if (_clientLease != null && _clientLease.Inventory != null)
            {
                _clientLease.Inventory.onContentChanged -= OnClientInventoryChanged;
                _clientLease.Inventory.Loading = false;
            }

            _clientLease = null;
            _clientDirty = false;
            _clientCommitInFlight = false;
            _clientReleaseRequested = false;
        }

        private sealed class HostLease
        {
            public HostLease(
                string containerId,
                InteractableLootbox lootbox,
                Inventory inventory,
                bool remoteOwned,
                long version)
            {
                ContainerId = containerId;
                Lootbox = lootbox;
                Inventory = inventory;
                RemoteOwned = remoteOwned;
                Version = version;
            }

            public string ContainerId { get; }
            public InteractableLootbox Lootbox { get; }
            public Inventory Inventory { get; }
            public bool RemoteOwned { get; }
            public long Version { get; set; }
            public bool Applying { get; set; }
        }

        private sealed class PendingLease
        {
            public PendingLease(string containerId, InteractableLootbox lootbox, Inventory inventory)
            {
                ContainerId = containerId;
                Lootbox = lootbox;
                Inventory = inventory;
            }

            public string ContainerId { get; }
            public InteractableLootbox Lootbox { get; }
            public Inventory Inventory { get; }
        }

        private sealed class ClientLease
        {
            public ClientLease(
                string containerId,
                InteractableLootbox lootbox,
                Inventory inventory,
                long version)
            {
                ContainerId = containerId;
                Lootbox = lootbox;
                Inventory = inventory;
                Version = version;
            }

            public string ContainerId { get; }
            public InteractableLootbox Lootbox { get; }
            public Inventory Inventory { get; }
            public long Version { get; set; }
        }
    }
}
