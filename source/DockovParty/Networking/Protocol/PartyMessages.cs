using System;
using System.Collections.Generic;

namespace SlimeNull.DockovParty.Networking.Protocol
{
    public enum PartyMessageKind : ushort
    {
        Hello = 1,
        Welcome = 2,
        Error = 3,
        Ping = 4,
        Pong = 5,
        PlayerState = 6,
        CharacterSnapshot = 7,
        SceneReady = 8,
        SceneCommit = 9,
        SceneLoaded = 10,
        ContainerLeaseRequest = 11,
        ContainerLeaseResult = 12,
        ContainerCommit = 13,
        ContainerRelease = 14,
        GroundPickupRequest = 15,
        GroundPickupResult = 16,
        GroundSpawn = 17,
        GroundDespawn = 18,
        GroundSnapshot = 19,
        NpcSpawn = 20,
        NpcStateBatch = 21,
        NpcDamageRequest = 22,
        PlayerHealthAuthority = 23,
        PeerDeath = 24,
        Notice = 25,
        ContainerSpawn = 26,
        ContainerReleaseResult = 27,
    }

    public abstract class PartyMessage
    {
        public abstract PartyMessageKind Kind { get; }
    }

    public sealed class HelloMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.Hello;
        public int ProtocolVersion;
        public string ModVersion = string.Empty;
        public string GameVersion = string.Empty;
        public string PlayerId = string.Empty;
        public string PlayerName = string.Empty;
    }

    public sealed class WelcomeMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.Welcome;
        public bool Accepted;
        public string Reason = string.Empty;
        public string SessionId = string.Empty;
        public string HostPlayerId = string.Empty;
        public string HostPlayerName = string.Empty;
        public string SceneId = string.Empty;
        public string SceneName = string.Empty;
        public bool HostAlive = true;
        public float ClientHealth;
        public bool ClientAlive = true;
        public string HostCharacterJson = string.Empty;
        public string ClientCharacterJson = string.Empty;
    }

    public sealed class ErrorMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.Error;
        public string Code = string.Empty;
        public string Description = string.Empty;
    }

    public sealed class PingMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.Ping;
        public long Timestamp;
    }

    public sealed class PongMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.Pong;
        public long Timestamp;
    }

    public sealed class PlayerStateMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.PlayerState;
        public uint Sequence;
        public string PlayerId = string.Empty;
        public string SceneId = string.Empty;
        public TransformState Transform;
        public VectorState Velocity;
        public VectorState AimPoint;
        public float Health;
        public float MaxHealth;
        public bool Alive;
        public bool Running;
        public bool Aiming;
        public int HeldItemTypeId;
    }

    public sealed class CharacterSnapshotMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.CharacterSnapshot;
        public string PlayerId = string.Empty;
        public string ItemTreeJson = string.Empty;
        public float Health;
        public bool Alive;
    }

    public sealed class SceneReadyMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.SceneReady;
        public string RequestId = string.Empty;
        public string PlayerId = string.Empty;
        public string SceneId = string.Empty;
        public string SceneName = string.Empty;
        public bool IsBase;
        public string CharacterJson = string.Empty;
        public float Health;
        public bool Alive;
    }

    public sealed class SceneCommitMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.SceneCommit;
        public string TransitionId = string.Empty;
        public string SceneId = string.Empty;
        public string SceneName = string.Empty;
    }

    public sealed class SceneLoadedMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.SceneLoaded;
        public string PlayerId = string.Empty;
        public string TransitionId = string.Empty;
        public string SceneId = string.Empty;
    }

    public sealed class ContainerLeaseRequestMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.ContainerLeaseRequest;
        public string RequestId = string.Empty;
        public string ContainerId = string.Empty;
    }

    public sealed class ContainerLeaseResultMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.ContainerLeaseResult;
        public string RequestId = string.Empty;
        public string ContainerId = string.Empty;
        public bool Granted;
        public string Reason = string.Empty;
        public long Version;
        public string InventoryJson = string.Empty;
    }

    public sealed class ContainerCommitMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.ContainerCommit;
        public string ContainerId = string.Empty;
        public long BaseVersion;
        public string InventoryJson = string.Empty;
        public string CharacterJson = string.Empty;
        public float CharacterHealth;
    }

    public sealed class ContainerReleaseMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.ContainerRelease;
        public string ContainerId = string.Empty;
        public long BaseVersion;
        public string InventoryJson = string.Empty;
        public string CharacterJson = string.Empty;
        public float CharacterHealth;
    }

    public sealed class ContainerReleaseResultMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.ContainerReleaseResult;
        public string ContainerId = string.Empty;
        public bool Accepted;
        public string Reason = string.Empty;
        public long Version;
        public string InventoryJson = string.Empty;
    }

    public sealed class GroundPickupRequestMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.GroundPickupRequest;
        public string RequestId = string.Empty;
        public string GroundId = string.Empty;
        public int ItemTypeId;
        public VectorState Position;
    }

    public sealed class GroundPickupResultMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.GroundPickupResult;
        public string RequestId = string.Empty;
        public bool Accepted;
        public string GroundId = string.Empty;
        public string Reason = string.Empty;
    }

    public sealed class GroundSpawnMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.GroundSpawn;
        public GroundItemState Item = new GroundItemState();
    }

    public sealed class GroundDespawnMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.GroundDespawn;
        public string GroundId = string.Empty;
        public int ItemTypeId;
        public VectorState Position;
    }

    public sealed class GroundSnapshotMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.GroundSnapshot;
        public readonly List<GroundItemState> Items = new List<GroundItemState>();
    }

    public sealed class NpcSpawnMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.NpcSpawn;
        public int NpcId;
        public string PresetAssetName = string.Empty;
        public string PresetNameKey = string.Empty;
        public string CharacterItemJson = string.Empty;
        public TransformState Transform;
        public int Team;
        public float Health;
        public float MaxHealth;
        public bool Alive;
    }

    public sealed class NpcStateBatchMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.NpcStateBatch;
        public uint Sequence;
        public readonly List<NpcState> States = new List<NpcState>();
    }

    public sealed class NpcDamageRequestMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.NpcDamageRequest;
        public int NpcId;
        public DamageState Damage;
    }

    public sealed class PlayerHealthAuthorityMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.PlayerHealthAuthority;
        public string PlayerId = string.Empty;
        public float Health;
        public bool Alive;
    }

    public sealed class PeerDeathMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.PeerDeath;
        public string PlayerId = string.Empty;
        public string SceneId = string.Empty;
    }

    public sealed class NoticeMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.Notice;
        public string Text = string.Empty;
    }

    public sealed class ContainerSpawnMessage : PartyMessage
    {
        public override PartyMessageKind Kind => PartyMessageKind.ContainerSpawn;
        public string ContainerId = string.Empty;
        public TransformState Transform;
        public long Version;
        public string InventoryJson = string.Empty;
    }

    public struct VectorState
    {
        public float X;
        public float Y;
        public float Z;

        public VectorState(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct QuaternionState
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public QuaternionState(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    public struct TransformState
    {
        public VectorState Position;
        public QuaternionState Rotation;
    }

    public sealed class GroundItemState
    {
        public string GroundId = string.Empty;
        public int ItemTypeId;
        public TransformState Transform;
        public string ItemTreeJson = string.Empty;
    }

    public struct NpcState
    {
        public int NpcId;
        public TransformState Transform;
        public VectorState AimPoint;
        public float Health;
        public bool Alive;
    }

    public struct DamageState
    {
        public int DamageType;
        public float DamageValue;
        public float DamageFactorToZombie;
        public bool IgnoreArmor;
        public bool IgnoreDifficulty;
        public float CritDamageFactor;
        public float CritRate;
        public float ArmorPiercing;
        public bool IsExplosion;
        public float ArmorBreak;
        public int WeaponItemTypeId;
        public float BleedChance;
        public float PhysicsFactor;
        public float FireFactor;
        public float PoisonFactor;
        public float ElectricityFactor;
        public float SpaceFactor;
        public float GhostFactor;
        public float IceFactor;
        public VectorState Point;
        public VectorState Normal;
    }
}
