using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlimeNull.DockovParty.Networking.Protocol
{
    public sealed class PartyProtocolException : IOException
    {
        public PartyProtocolException(string message) : base(message)
        {
        }
    }

    public static class PartyWireCodec
    {
        public const int ProtocolVersion = 2;
        public const int MaximumPayloadLength = 16 * 1024 * 1024;

        private const uint Magic = 0x59545044; // DPTY in little-endian order.
        private const int HeaderLength = 12;
        private const int MaximumCollectionCount = 16384;

        public static async Task WriteAsync(
            Stream stream,
            PartyMessage message,
            CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            byte[] payload;
            using (var payloadStream = new MemoryStream())
            using (var writer = new BinaryWriter(payloadStream, Encoding.UTF8, true))
            {
                WritePayload(writer, message);
                writer.Flush();
                payload = payloadStream.ToArray();
            }

            if (payload.Length > MaximumPayloadLength)
            {
                throw new PartyProtocolException($"Payload is too large: {payload.Length} bytes.");
            }

            byte[] header;
            using (var headerStream = new MemoryStream(HeaderLength))
            using (var writer = new BinaryWriter(headerStream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write((ushort)ProtocolVersion);
                writer.Write((ushort)message.Kind);
                writer.Write(payload.Length);
                writer.Flush();
                header = headerStream.ToArray();
            }

            await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
            if (payload.Length > 0)
            {
                await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public static async Task<PartyMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var header = new byte[HeaderLength];
            if (!await ReadExactlyAsync(stream, header, allowEndOfStream: true, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            PartyMessageKind kind;
            int payloadLength;
            using (var headerStream = new MemoryStream(header, writable: false))
            using (var reader = new BinaryReader(headerStream, Encoding.UTF8, true))
            {
                var magic = reader.ReadUInt32();
                if (magic != Magic)
                {
                    throw new PartyProtocolException("The stream does not contain a DockovParty frame.");
                }

                var version = reader.ReadUInt16();
                if (version != ProtocolVersion)
                {
                    throw new PartyProtocolException(
                        $"Protocol version mismatch. Local={ProtocolVersion}, remote={version}.");
                }

                kind = (PartyMessageKind)reader.ReadUInt16();
                payloadLength = reader.ReadInt32();
            }

            if (payloadLength < 0 || payloadLength > MaximumPayloadLength)
            {
                throw new PartyProtocolException($"Invalid payload length: {payloadLength}.");
            }

            var payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                await ReadExactlyAsync(stream, payload, allowEndOfStream: false, cancellationToken).ConfigureAwait(false);
            }

            using (var payloadStream = new MemoryStream(payload, writable: false))
            using (var reader = new BinaryReader(payloadStream, Encoding.UTF8, true))
            {
                var result = ReadPayload(reader, kind);
                if (payloadStream.Position != payloadStream.Length)
                {
                    throw new PartyProtocolException(
                        $"Message {kind} has {payloadStream.Length - payloadStream.Position} unread payload bytes.");
                }

                return result;
            }
        }

        private static async Task<bool> ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            bool allowEndOfStream,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    if (allowEndOfStream && offset == 0)
                    {
                        return false;
                    }

                    throw new EndOfStreamException("The connection ended in the middle of a DockovParty frame.");
                }

                offset += read;
            }

            return true;
        }

        private static void WritePayload(BinaryWriter writer, PartyMessage message)
        {
            switch (message)
            {
                case HelloMessage value:
                    writer.Write(value.ProtocolVersion);
                    WriteString(writer, value.ModVersion);
                    WriteString(writer, value.GameVersion);
                    WriteString(writer, value.PlayerId);
                    WriteString(writer, value.PlayerName);
                    return;
                case WelcomeMessage value:
                    writer.Write(value.Accepted);
                    WriteString(writer, value.Reason);
                    WriteString(writer, value.SessionId);
                    WriteString(writer, value.HostPlayerId);
                    WriteString(writer, value.HostPlayerName);
                    WriteString(writer, value.SceneId);
                    WriteString(writer, value.SceneName);
                    writer.Write(value.HostAlive);
                    writer.Write(value.ClientHealth);
                    writer.Write(value.ClientAlive);
                    WriteString(writer, value.HostCharacterJson);
                    WriteString(writer, value.ClientCharacterJson);
                    return;
                case ErrorMessage value:
                    WriteString(writer, value.Code);
                    WriteString(writer, value.Description);
                    return;
                case PingMessage value:
                    writer.Write(value.Timestamp);
                    return;
                case PongMessage value:
                    writer.Write(value.Timestamp);
                    return;
                case PlayerStateMessage value:
                    writer.Write(value.Sequence);
                    WriteString(writer, value.PlayerId);
                    WriteString(writer, value.SceneId);
                    WriteTransform(writer, value.Transform);
                    WriteVector(writer, value.Velocity);
                    WriteVector(writer, value.AimPoint);
                    writer.Write(value.Health);
                    writer.Write(value.MaxHealth);
                    writer.Write(value.Alive);
                    writer.Write(value.Running);
                    writer.Write(value.Aiming);
                    writer.Write(value.HeldItemTypeId);
                    return;
                case CharacterSnapshotMessage value:
                    WriteString(writer, value.PlayerId);
                    WriteString(writer, value.ItemTreeJson);
                    writer.Write(value.Health);
                    writer.Write(value.Alive);
                    return;
                case SceneReadyMessage value:
                    WriteString(writer, value.RequestId);
                    WriteString(writer, value.PlayerId);
                    WriteString(writer, value.SceneId);
                    WriteString(writer, value.SceneName);
                    writer.Write(value.IsBase);
                    WriteString(writer, value.CharacterJson);
                    writer.Write(value.Health);
                    writer.Write(value.Alive);
                    return;
                case SceneCommitMessage value:
                    WriteString(writer, value.TransitionId);
                    WriteString(writer, value.SceneId);
                    WriteString(writer, value.SceneName);
                    return;
                case SceneLoadedMessage value:
                    WriteString(writer, value.PlayerId);
                    WriteString(writer, value.TransitionId);
                    WriteString(writer, value.SceneId);
                    return;
                case ContainerLeaseRequestMessage value:
                    WriteString(writer, value.RequestId);
                    WriteString(writer, value.ContainerId);
                    return;
                case ContainerLeaseResultMessage value:
                    WriteString(writer, value.RequestId);
                    WriteString(writer, value.ContainerId);
                    writer.Write(value.Granted);
                    WriteString(writer, value.Reason);
                    writer.Write(value.Version);
                    WriteString(writer, value.InventoryJson);
                    return;
                case ContainerCommitMessage value:
                    WriteString(writer, value.ContainerId);
                    writer.Write(value.BaseVersion);
                    WriteString(writer, value.InventoryJson);
                    WriteString(writer, value.CharacterJson);
                    writer.Write(value.CharacterHealth);
                    return;
                case ContainerReleaseMessage value:
                    WriteString(writer, value.ContainerId);
                    writer.Write(value.BaseVersion);
                    WriteString(writer, value.InventoryJson);
                    WriteString(writer, value.CharacterJson);
                    writer.Write(value.CharacterHealth);
                    return;
                case ContainerReleaseResultMessage value:
                    WriteString(writer, value.ContainerId);
                    writer.Write(value.Accepted);
                    WriteString(writer, value.Reason);
                    writer.Write(value.Version);
                    WriteString(writer, value.InventoryJson);
                    return;
                case GroundPickupRequestMessage value:
                    WriteString(writer, value.RequestId);
                    WriteString(writer, value.GroundId);
                    writer.Write(value.ItemTypeId);
                    WriteVector(writer, value.Position);
                    return;
                case GroundPickupResultMessage value:
                    WriteString(writer, value.RequestId);
                    writer.Write(value.Accepted);
                    WriteString(writer, value.GroundId);
                    WriteString(writer, value.Reason);
                    return;
                case GroundSpawnMessage value:
                    WriteGroundItem(writer, value.Item);
                    return;
                case GroundDespawnMessage value:
                    WriteString(writer, value.GroundId);
                    writer.Write(value.ItemTypeId);
                    WriteVector(writer, value.Position);
                    return;
                case GroundSnapshotMessage value:
                    writer.Write(value.Items.Count);
                    foreach (var item in value.Items)
                    {
                        WriteGroundItem(writer, item);
                    }

                    return;
                case NpcSpawnMessage value:
                    writer.Write(value.NpcId);
                    WriteString(writer, value.PresetAssetName);
                    WriteString(writer, value.PresetNameKey);
                    WriteString(writer, value.CharacterItemJson);
                    WriteTransform(writer, value.Transform);
                    writer.Write(value.Team);
                    writer.Write(value.Health);
                    writer.Write(value.MaxHealth);
                    writer.Write(value.Alive);
                    return;
                case NpcStateBatchMessage value:
                    writer.Write(value.Sequence);
                    writer.Write(value.States.Count);
                    foreach (var state in value.States)
                    {
                        writer.Write(state.NpcId);
                        WriteTransform(writer, state.Transform);
                        WriteVector(writer, state.AimPoint);
                        writer.Write(state.Health);
                        writer.Write(state.Alive);
                    }

                    return;
                case NpcDamageRequestMessage value:
                    writer.Write(value.NpcId);
                    WriteDamage(writer, value.Damage);
                    return;
                case PlayerHealthAuthorityMessage value:
                    WriteString(writer, value.PlayerId);
                    writer.Write(value.Health);
                    writer.Write(value.Alive);
                    return;
                case PeerDeathMessage value:
                    WriteString(writer, value.PlayerId);
                    WriteString(writer, value.SceneId);
                    return;
                case NoticeMessage value:
                    WriteString(writer, value.Text);
                    return;
                case ContainerSpawnMessage value:
                    WriteString(writer, value.ContainerId);
                    WriteTransform(writer, value.Transform);
                    writer.Write(value.Version);
                    WriteString(writer, value.InventoryJson);
                    return;
                default:
                    throw new PartyProtocolException($"Unsupported message type: {message.GetType().FullName}.");
            }
        }

        private static PartyMessage ReadPayload(BinaryReader reader, PartyMessageKind kind)
        {
            switch (kind)
            {
                case PartyMessageKind.Hello:
                    return new HelloMessage
                    {
                        ProtocolVersion = reader.ReadInt32(),
                        ModVersion = ReadString(reader),
                        GameVersion = ReadString(reader),
                        PlayerId = ReadString(reader),
                        PlayerName = ReadString(reader),
                    };
                case PartyMessageKind.Welcome:
                    return new WelcomeMessage
                    {
                        Accepted = reader.ReadBoolean(),
                        Reason = ReadString(reader),
                        SessionId = ReadString(reader),
                        HostPlayerId = ReadString(reader),
                        HostPlayerName = ReadString(reader),
                        SceneId = ReadString(reader),
                        SceneName = ReadString(reader),
                        HostAlive = reader.ReadBoolean(),
                        ClientHealth = reader.ReadSingle(),
                        ClientAlive = reader.ReadBoolean(),
                        HostCharacterJson = ReadString(reader),
                        ClientCharacterJson = ReadString(reader),
                    };
                case PartyMessageKind.Error:
                    return new ErrorMessage
                    {
                        Code = ReadString(reader),
                        Description = ReadString(reader),
                    };
                case PartyMessageKind.Ping:
                    return new PingMessage { Timestamp = reader.ReadInt64() };
                case PartyMessageKind.Pong:
                    return new PongMessage { Timestamp = reader.ReadInt64() };
                case PartyMessageKind.PlayerState:
                    return new PlayerStateMessage
                    {
                        Sequence = reader.ReadUInt32(),
                        PlayerId = ReadString(reader),
                        SceneId = ReadString(reader),
                        Transform = ReadTransform(reader),
                        Velocity = ReadVector(reader),
                        AimPoint = ReadVector(reader),
                        Health = reader.ReadSingle(),
                        MaxHealth = reader.ReadSingle(),
                        Alive = reader.ReadBoolean(),
                        Running = reader.ReadBoolean(),
                        Aiming = reader.ReadBoolean(),
                        HeldItemTypeId = reader.ReadInt32(),
                    };
                case PartyMessageKind.CharacterSnapshot:
                    return new CharacterSnapshotMessage
                    {
                        PlayerId = ReadString(reader),
                        ItemTreeJson = ReadString(reader),
                        Health = reader.ReadSingle(),
                        Alive = reader.ReadBoolean(),
                    };
                case PartyMessageKind.SceneReady:
                    return new SceneReadyMessage
                    {
                        RequestId = ReadString(reader),
                        PlayerId = ReadString(reader),
                        SceneId = ReadString(reader),
                        SceneName = ReadString(reader),
                        IsBase = reader.ReadBoolean(),
                        CharacterJson = ReadString(reader),
                        Health = reader.ReadSingle(),
                        Alive = reader.ReadBoolean(),
                    };
                case PartyMessageKind.SceneCommit:
                    return new SceneCommitMessage
                    {
                        TransitionId = ReadString(reader),
                        SceneId = ReadString(reader),
                        SceneName = ReadString(reader),
                    };
                case PartyMessageKind.SceneLoaded:
                    return new SceneLoadedMessage
                    {
                        PlayerId = ReadString(reader),
                        TransitionId = ReadString(reader),
                        SceneId = ReadString(reader),
                    };
                case PartyMessageKind.ContainerLeaseRequest:
                    return new ContainerLeaseRequestMessage
                    {
                        RequestId = ReadString(reader),
                        ContainerId = ReadString(reader),
                    };
                case PartyMessageKind.ContainerLeaseResult:
                    return new ContainerLeaseResultMessage
                    {
                        RequestId = ReadString(reader),
                        ContainerId = ReadString(reader),
                        Granted = reader.ReadBoolean(),
                        Reason = ReadString(reader),
                        Version = reader.ReadInt64(),
                        InventoryJson = ReadString(reader),
                    };
                case PartyMessageKind.ContainerCommit:
                    return new ContainerCommitMessage
                    {
                        ContainerId = ReadString(reader),
                        BaseVersion = reader.ReadInt64(),
                        InventoryJson = ReadString(reader),
                        CharacterJson = ReadString(reader),
                        CharacterHealth = reader.ReadSingle(),
                    };
                case PartyMessageKind.ContainerRelease:
                    return new ContainerReleaseMessage
                    {
                        ContainerId = ReadString(reader),
                        BaseVersion = reader.ReadInt64(),
                        InventoryJson = ReadString(reader),
                        CharacterJson = ReadString(reader),
                        CharacterHealth = reader.ReadSingle(),
                    };
                case PartyMessageKind.ContainerReleaseResult:
                    return new ContainerReleaseResultMessage
                    {
                        ContainerId = ReadString(reader),
                        Accepted = reader.ReadBoolean(),
                        Reason = ReadString(reader),
                        Version = reader.ReadInt64(),
                        InventoryJson = ReadString(reader),
                    };
                case PartyMessageKind.GroundPickupRequest:
                    return new GroundPickupRequestMessage
                    {
                        RequestId = ReadString(reader),
                        GroundId = ReadString(reader),
                        ItemTypeId = reader.ReadInt32(),
                        Position = ReadVector(reader),
                    };
                case PartyMessageKind.GroundPickupResult:
                    return new GroundPickupResultMessage
                    {
                        RequestId = ReadString(reader),
                        Accepted = reader.ReadBoolean(),
                        GroundId = ReadString(reader),
                        Reason = ReadString(reader),
                    };
                case PartyMessageKind.GroundSpawn:
                    return new GroundSpawnMessage { Item = ReadGroundItem(reader) };
                case PartyMessageKind.GroundDespawn:
                    return new GroundDespawnMessage
                    {
                        GroundId = ReadString(reader),
                        ItemTypeId = reader.ReadInt32(),
                        Position = ReadVector(reader),
                    };
                case PartyMessageKind.GroundSnapshot:
                {
                    var result = new GroundSnapshotMessage();
                    var count = ReadCount(reader);
                    for (var i = 0; i < count; i++)
                    {
                        result.Items.Add(ReadGroundItem(reader));
                    }

                    return result;
                }
                case PartyMessageKind.NpcSpawn:
                    return new NpcSpawnMessage
                    {
                        NpcId = reader.ReadInt32(),
                        PresetAssetName = ReadString(reader),
                        PresetNameKey = ReadString(reader),
                        CharacterItemJson = ReadString(reader),
                        Transform = ReadTransform(reader),
                        Team = reader.ReadInt32(),
                        Health = reader.ReadSingle(),
                        MaxHealth = reader.ReadSingle(),
                        Alive = reader.ReadBoolean(),
                    };
                case PartyMessageKind.NpcStateBatch:
                {
                    var result = new NpcStateBatchMessage { Sequence = reader.ReadUInt32() };
                    var count = ReadCount(reader);
                    for (var i = 0; i < count; i++)
                    {
                        result.States.Add(new NpcState
                        {
                            NpcId = reader.ReadInt32(),
                            Transform = ReadTransform(reader),
                            AimPoint = ReadVector(reader),
                            Health = reader.ReadSingle(),
                            Alive = reader.ReadBoolean(),
                        });
                    }

                    return result;
                }
                case PartyMessageKind.NpcDamageRequest:
                    return new NpcDamageRequestMessage
                    {
                        NpcId = reader.ReadInt32(),
                        Damage = ReadDamage(reader),
                    };
                case PartyMessageKind.PlayerHealthAuthority:
                    return new PlayerHealthAuthorityMessage
                    {
                        PlayerId = ReadString(reader),
                        Health = reader.ReadSingle(),
                        Alive = reader.ReadBoolean(),
                    };
                case PartyMessageKind.PeerDeath:
                    return new PeerDeathMessage
                    {
                        PlayerId = ReadString(reader),
                        SceneId = ReadString(reader),
                    };
                case PartyMessageKind.Notice:
                    return new NoticeMessage { Text = ReadString(reader) };
                case PartyMessageKind.ContainerSpawn:
                    return new ContainerSpawnMessage
                    {
                        ContainerId = ReadString(reader),
                        Transform = ReadTransform(reader),
                        Version = reader.ReadInt64(),
                        InventoryJson = ReadString(reader),
                    };
                default:
                    throw new PartyProtocolException($"Unknown message kind: {(ushort)kind}.");
            }
        }

        private static void WriteString(BinaryWriter writer, string? value)
        {
            writer.Write(value ?? string.Empty);
        }

        private static string ReadString(BinaryReader reader)
        {
            return reader.ReadString();
        }

        private static int ReadCount(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaximumCollectionCount)
            {
                throw new PartyProtocolException($"Invalid collection count: {count}.");
            }

            return count;
        }

        private static void WriteVector(BinaryWriter writer, VectorState value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        private static VectorState ReadVector(BinaryReader reader)
        {
            return new VectorState(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteQuaternion(BinaryWriter writer, QuaternionState value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
            writer.Write(value.W);
        }

        private static QuaternionState ReadQuaternion(BinaryReader reader)
        {
            return new QuaternionState(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        private static void WriteTransform(BinaryWriter writer, TransformState value)
        {
            WriteVector(writer, value.Position);
            WriteQuaternion(writer, value.Rotation);
        }

        private static TransformState ReadTransform(BinaryReader reader)
        {
            return new TransformState
            {
                Position = ReadVector(reader),
                Rotation = ReadQuaternion(reader),
            };
        }

        private static void WriteGroundItem(BinaryWriter writer, GroundItemState value)
        {
            WriteString(writer, value.GroundId);
            writer.Write(value.ItemTypeId);
            WriteTransform(writer, value.Transform);
            WriteString(writer, value.ItemTreeJson);
        }

        private static GroundItemState ReadGroundItem(BinaryReader reader)
        {
            return new GroundItemState
            {
                GroundId = ReadString(reader),
                ItemTypeId = reader.ReadInt32(),
                Transform = ReadTransform(reader),
                ItemTreeJson = ReadString(reader),
            };
        }

        private static void WriteDamage(BinaryWriter writer, DamageState value)
        {
            writer.Write(value.DamageType);
            writer.Write(value.DamageValue);
            writer.Write(value.DamageFactorToZombie);
            writer.Write(value.IgnoreArmor);
            writer.Write(value.IgnoreDifficulty);
            writer.Write(value.CritDamageFactor);
            writer.Write(value.CritRate);
            writer.Write(value.ArmorPiercing);
            writer.Write(value.IsExplosion);
            writer.Write(value.ArmorBreak);
            writer.Write(value.WeaponItemTypeId);
            writer.Write(value.BleedChance);
            writer.Write(value.PhysicsFactor);
            writer.Write(value.FireFactor);
            writer.Write(value.PoisonFactor);
            writer.Write(value.ElectricityFactor);
            writer.Write(value.SpaceFactor);
            writer.Write(value.GhostFactor);
            writer.Write(value.IceFactor);
            WriteVector(writer, value.Point);
            WriteVector(writer, value.Normal);
        }

        private static DamageState ReadDamage(BinaryReader reader)
        {
            return new DamageState
            {
                DamageType = reader.ReadInt32(),
                DamageValue = reader.ReadSingle(),
                DamageFactorToZombie = reader.ReadSingle(),
                IgnoreArmor = reader.ReadBoolean(),
                IgnoreDifficulty = reader.ReadBoolean(),
                CritDamageFactor = reader.ReadSingle(),
                CritRate = reader.ReadSingle(),
                ArmorPiercing = reader.ReadSingle(),
                IsExplosion = reader.ReadBoolean(),
                ArmorBreak = reader.ReadSingle(),
                WeaponItemTypeId = reader.ReadInt32(),
                BleedChance = reader.ReadSingle(),
                PhysicsFactor = reader.ReadSingle(),
                FireFactor = reader.ReadSingle(),
                PoisonFactor = reader.ReadSingle(),
                ElectricityFactor = reader.ReadSingle(),
                SpaceFactor = reader.ReadSingle(),
                GhostFactor = reader.ReadSingle(),
                IceFactor = reader.ReadSingle(),
                Point = ReadVector(reader),
                Normal = ReadVector(reader),
            };
        }
    }
}
