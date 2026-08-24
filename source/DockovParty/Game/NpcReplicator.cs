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
    internal sealed class NpcReplicator : IDisposable
    {
        private const float ScanInterval = 0.25f;

        private readonly PartyRuntime _runtime;
        private readonly Dictionary<int, CharacterMainControl> _hostNpcs =
            new Dictionary<int, CharacterMainControl>();
        private readonly Dictionary<int, int> _hostIdsByInstance = new Dictionary<int, int>();
        private readonly Dictionary<int, CharacterMainControl> _clientNpcs =
            new Dictionary<int, CharacterMainControl>();
        private readonly Dictionary<int, NpcState> _pendingStates = new Dictionary<int, NpcState>();
        private readonly HashSet<int> _pendingSpawns = new HashSet<int>();
        private int _nextNpcId = 1;
        private uint _sequence;
        private float _nextScanTime;
        private float _nextStateTime;
        private bool _disposed;

        public NpcReplicator(PartyRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.MessageReceived += OnMessage;
            _runtime.Disconnected += OnDisconnected;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            Health.OnDead += OnHealthDead;
        }

        public void Tick()
        {
            if (!_runtime.Connected || !LevelManager.LevelInited)
            {
                return;
            }

            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + ScanInterval;
                if (_runtime.IsHost)
                {
                    DiscoverHostNpcs();
                }
                else
                {
                    DisableUnownedClientNpcs();
                }
            }

            if (_runtime.IsHost && Time.unscaledTime >= _nextStateTime)
            {
                _nextStateTime = Time.unscaledTime + 1f / Mathf.Max(5, _runtime.StateRate);
                SendStateBatch();
            }
        }

        public bool TryForwardClientDamage(
            DamageReceiver receiver,
            DamageInfo damageInfo,
            out bool result)
        {
            result = false;
            if (!_runtime.IsClient || !_runtime.Connected || receiver == null)
            {
                return false;
            }

            if (damageInfo.fromCharacter != CharacterMainControl.Main)
            {
                return false;
            }

            var character = receiver.health?.TryGetCharacter();
            var replica = character?.GetComponent<NetworkNpcReplica>();
            if (replica == null)
            {
                return false;
            }

            _runtime.Send(new NpcDamageRequestMessage
            {
                NpcId = replica.NpcId,
                Damage = ToNetworkDamage(damageInfo),
            });
            result = true;
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
            _runtime.Disconnected -= OnDisconnected;
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            Health.OnDead -= OnHealthDead;
            Clear();
        }

        private void OnAfterLevelInitialized()
        {
            Clear();
            _nextScanTime = 0f;
            _nextStateTime = 0f;
            if (_runtime.IsClient && _runtime.Connected)
            {
                DisableUnownedClientNpcs();
            }
        }

        private void OnMessage(PartyMessage message)
        {
            switch (message)
            {
                case NpcSpawnMessage spawn when _runtime.IsClient:
                    HandleNpcSpawnAsync(spawn).Forget();
                    break;
                case NpcStateBatchMessage batch when _runtime.IsClient:
                    HandleStateBatch(batch);
                    break;
                case NpcDamageRequestMessage request when _runtime.IsHost:
                    HandleDamageRequest(request);
                    break;
            }
        }

        private void DiscoverHostNpcs()
        {
            foreach (var character in UnityEngine.Object.FindObjectsOfType<CharacterMainControl>(true))
            {
                if (!IsNpc(character) || character.GetComponent<NetworkNpcReplica>() != null)
                {
                    continue;
                }

                var instanceId = character.GetInstanceID();
                if (_hostIdsByInstance.ContainsKey(instanceId))
                {
                    continue;
                }

                var npcId = _nextNpcId++;
                _hostIdsByInstance.Add(instanceId, npcId);
                _hostNpcs.Add(npcId, character);
                SendSpawn(npcId, character);
            }
        }

        private void SendSpawn(int npcId, CharacterMainControl character)
        {
            var preset = character.characterPreset;
            var visualRotation = character.modelRoot != null ?
                character.modelRoot.rotation : character.transform.rotation;
            _runtime.Send(new NpcSpawnMessage
            {
                NpcId = npcId,
                PresetAssetName = preset?.name ?? string.Empty,
                PresetNameKey = preset?.nameKey ?? string.Empty,
                CharacterItemJson = character.CharacterItem == null ? string.Empty :
                    GameDataSerializer.SerializeItem(ItemTreeData.FromItem(character.CharacterItem)),
                Transform = character.transform.ToNetwork(visualRotation),
                Team = (int)character.Team,
                Health = character.Health?.CurrentHealth ?? 0f,
                MaxHealth = character.Health?.MaxHealth ?? 0f,
                Alive = character.Health == null || !character.Health.IsDead,
            });
        }

        private void SendStateBatch()
        {
            var batch = new NpcStateBatchMessage { Sequence = ++_sequence };
            foreach (var pair in _hostNpcs.ToArray())
            {
                var character = pair.Value;
                if (character == null)
                {
                    _hostNpcs.Remove(pair.Key);
                    continue;
                }

                batch.States.Add(CreateState(pair.Key, character));
            }

            if (batch.States.Count > 0)
            {
                _runtime.Send(batch);
            }
        }

        private void HandleStateBatch(NpcStateBatchMessage batch)
        {
            foreach (var state in batch.States)
            {
                _pendingStates[state.NpcId] = state;
                if (_clientNpcs.TryGetValue(state.NpcId, out var character) && character != null)
                {
                    character.GetComponent<NetworkNpcReplica>()?.Apply(state);
                }
            }
        }

        private async UniTask HandleNpcSpawnAsync(NpcSpawnMessage spawn)
        {
            if (_clientNpcs.ContainsKey(spawn.NpcId) || !_pendingSpawns.Add(spawn.NpcId))
            {
                return;
            }

            try
            {
                var position = spawn.Transform.Position.ToUnity();
                var character = FindMatchingLocalNpc(spawn, position);
                if (character == null)
                {
                    character = await CreateClientNpcAsync(spawn, position);
                }

                if (character == null || !_runtime.Connected || !_runtime.IsClient ||
                    !LevelManager.LevelInited)
                {
                    if (character != null)
                    {
                        UnityEngine.Object.Destroy(character.gameObject);
                    }

                    return;
                }

                DisableNpcControl(character);
                var replica = character.GetComponent<NetworkNpcReplica>() ??
                    character.gameObject.AddComponent<NetworkNpcReplica>();
                replica.Initialize(spawn.NpcId, character, _runtime.InterpolationDelay);
                _clientNpcs[spawn.NpcId] = character;

                var initialState = _pendingStates.TryGetValue(spawn.NpcId, out var pending) ?
                    pending : new NpcState
                    {
                        NpcId = spawn.NpcId,
                        Transform = spawn.Transform,
                        AimPoint = position.ToNetwork(),
                        Health = spawn.Health,
                        Alive = spawn.Alive,
                    };
                replica.Apply(initialState);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DockovParty] 创建 NPC 副本失败: {ex}");
            }
            finally
            {
                _pendingSpawns.Remove(spawn.NpcId);
            }
        }

        private CharacterMainControl? FindMatchingLocalNpc(NpcSpawnMessage spawn, Vector3 position)
        {
            CharacterMainControl? closest = null;
            var closestDistance = float.MaxValue;
            foreach (var character in UnityEngine.Object.FindObjectsOfType<CharacterMainControl>(true))
            {
                if (!IsNpc(character) || character.GetComponent<NetworkNpcReplica>() != null)
                {
                    continue;
                }

                var preset = character.characterPreset;
                var samePreset = preset != null &&
                    (string.Equals(preset.name, spawn.PresetAssetName, StringComparison.Ordinal) ||
                     string.Equals(preset.nameKey, spawn.PresetNameKey, StringComparison.Ordinal));
                if (!samePreset)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(character.transform.position - position);
                if (distance < closestDistance)
                {
                    closest = character;
                    closestDistance = distance;
                }
            }

            return closestDistance <= 40f * 40f ? closest : null;
        }

        private async UniTask<CharacterMainControl?> CreateClientNpcAsync(
            NpcSpawnMessage spawn,
            Vector3 position)
        {
            var preset = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>()
                .FirstOrDefault(value => value != null &&
                    (string.Equals(value.name, spawn.PresetAssetName, StringComparison.Ordinal) ||
                     string.Equals(value.nameKey, spawn.PresetNameKey, StringComparison.Ordinal)));
            if (preset == null || LevelManager.Instance == null)
            {
                return null;
            }

            Item? item = null;
            var data = GameDataSerializer.DeserializeItem(spawn.CharacterItemJson);
            if (data != null)
            {
                item = await ItemTreeData.InstantiateAsync(data);
            }

            if (item == null)
            {
                return null;
            }

            var character = await LevelManager.Instance.CharacterCreator.CreateCharacter(
                item,
                preset.CharacterModel,
                position,
                spawn.Transform.Rotation.ToUnity());
            if (character == null)
            {
                item.DestroyTree();
                return null;
            }

            character.characterPreset = preset;
            character.dropBoxOnDead = preset.dropBoxOnDead;
            character.deadLootBoxPrefab = preset.lootBoxPrefab;
            character.SetTeam((Teams)spawn.Team);
            character.Health.showHealthBar = preset.showHealthBar;
            character.Health.SetHealth(spawn.Health);
            return character;
        }

        private void DisableUnownedClientNpcs()
        {
            foreach (var character in UnityEngine.Object.FindObjectsOfType<CharacterMainControl>(true))
            {
                if (IsNpc(character) && character.GetComponent<NetworkNpcReplica>() == null)
                {
                    DisableNpcControl(character);
                }
            }
        }

        private void HandleDamageRequest(NpcDamageRequestMessage request)
        {
            if (!_hostNpcs.TryGetValue(request.NpcId, out var target) || target == null ||
                target.Health == null || target.Health.IsDead || _runtime.RemoteCharacter == null ||
                !_runtime.RemotePlayerAlive)
            {
                return;
            }

            var damage = FromNetworkDamage(request.Damage, _runtime.RemoteCharacter);
            var exp = target.CharacterItem?.GetInt("Exp") ?? 0;
            var wasAlive = !target.Health.IsDead;
            target.mainDamageReceiver.Hurt(damage);
            if (wasAlive && target.Health.IsDead && exp > 0)
            {
                Duckov.EXPManager.AddExp(exp);
            }
        }

        private void OnHealthDead(Health health, DamageInfo damageInfo)
        {
            if (!_runtime.IsHost || !_runtime.Connected)
            {
                return;
            }

            var character = health?.TryGetCharacter();
            if (character == null || !_hostIdsByInstance.TryGetValue(character.GetInstanceID(), out var npcId))
            {
                return;
            }

            var batch = new NpcStateBatchMessage { Sequence = ++_sequence };
            batch.States.Add(CreateState(npcId, character));
            _runtime.Send(batch);
        }

        private void OnDisconnected()
        {
            Clear();
        }

        private void Clear()
        {
            _hostNpcs.Clear();
            _hostIdsByInstance.Clear();
            _clientNpcs.Clear();
            _pendingStates.Clear();
            _pendingSpawns.Clear();
            _nextNpcId = 1;
        }

        private static bool IsNpc(CharacterMainControl? character)
        {
            return character != null && character != CharacterMainControl.Main &&
                character.GetComponent<RemotePlayerReplica>() == null &&
                character.Team != Teams.player &&
                (character.characterPreset != null || character.aiCharacterController != null);
        }

        private static void DisableNpcControl(CharacterMainControl character)
        {
            foreach (var behaviour in character.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour is Health || behaviour is Animator ||
                    behaviour is NetworkNpcReplica)
                {
                    continue;
                }

                var name = behaviour.GetType().Name;
                if (name.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("BehaviourTree", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    behaviour.enabled = false;
                }
            }

            character.enabled = false;
        }

        private static NpcState CreateState(int npcId, CharacterMainControl character)
        {
            var visualRotation = character.modelRoot != null ?
                character.modelRoot.rotation : character.transform.rotation;
            return new NpcState
            {
                NpcId = npcId,
                Transform = character.transform.ToNetwork(visualRotation),
                AimPoint = character.GetCurrentAimPoint().ToNetwork(),
                Health = character.Health?.CurrentHealth ?? 0f,
                Alive = character.Health == null || !character.Health.IsDead,
            };
        }

        private static DamageState ToNetworkDamage(DamageInfo value)
        {
            var result = new DamageState
            {
                DamageType = (int)value.damageType,
                DamageValue = value.damageValue,
                DamageFactorToZombie = value.damageFactorToZombie,
                IgnoreArmor = value.ignoreArmor,
                IgnoreDifficulty = value.ignoreDifficulty,
                CritDamageFactor = value.critDamageFactor,
                CritRate = value.critRate,
                ArmorPiercing = value.armorPiercing,
                IsExplosion = value.isExplosion,
                ArmorBreak = value.armorBreak,
                WeaponItemTypeId = value.fromWeaponItemID,
                BleedChance = value.bleedChance,
                Point = value.damagePoint.ToNetwork(),
                Normal = value.damageNormal.ToNetwork(),
            };

            if (value.elementFactors != null)
            {
                foreach (var factor in value.elementFactors)
                {
                    switch (factor.elementType)
                    {
                        case ElementTypes.physics: result.PhysicsFactor += factor.factor; break;
                        case ElementTypes.fire: result.FireFactor += factor.factor; break;
                        case ElementTypes.poison: result.PoisonFactor += factor.factor; break;
                        case ElementTypes.electricity: result.ElectricityFactor += factor.factor; break;
                        case ElementTypes.space: result.SpaceFactor += factor.factor; break;
                        case ElementTypes.ghost: result.GhostFactor += factor.factor; break;
                        case ElementTypes.ice: result.IceFactor += factor.factor; break;
                    }
                }
            }

            return result;
        }

        private static DamageInfo FromNetworkDamage(DamageState value, CharacterMainControl source)
        {
            var result = new DamageInfo(source)
            {
                damageType = (DamageTypes)value.DamageType,
                damageValue = Mathf.Clamp(value.DamageValue, 0f, 100000f),
                damageFactorToZombie = value.DamageFactorToZombie,
                ignoreArmor = value.IgnoreArmor,
                ignoreDifficulty = value.IgnoreDifficulty,
                critDamageFactor = value.CritDamageFactor,
                critRate = value.CritRate,
                armorPiercing = value.ArmorPiercing,
                isExplosion = value.IsExplosion,
                armorBreak = value.ArmorBreak,
                fromWeaponItemID = value.WeaponItemTypeId,
                bleedChance = value.BleedChance,
                damagePoint = value.Point.ToUnity(),
                damageNormal = value.Normal.ToUnity(),
            };
            AddElement(ref result, ElementTypes.physics, value.PhysicsFactor);
            AddElement(ref result, ElementTypes.fire, value.FireFactor);
            AddElement(ref result, ElementTypes.poison, value.PoisonFactor);
            AddElement(ref result, ElementTypes.electricity, value.ElectricityFactor);
            AddElement(ref result, ElementTypes.space, value.SpaceFactor);
            AddElement(ref result, ElementTypes.ghost, value.GhostFactor);
            AddElement(ref result, ElementTypes.ice, value.IceFactor);
            return result;
        }

        private static void AddElement(ref DamageInfo damage, ElementTypes type, float factor)
        {
            if (factor > 0f)
            {
                damage.AddElementFactor(type, factor);
            }
        }
    }
}
