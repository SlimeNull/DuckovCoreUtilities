using SlimeNull.DockovParty.Networking.Protocol;
using System.Net;
using Xunit;

namespace SlimeNull.DockovParty.Tests;

public sealed class PartyWireCodecTests
{
    public static TheoryData<PartyMessage> Messages => new TheoryData<PartyMessage>
    {
        new HelloMessage
        {
            ProtocolVersion = PartyWireCodec.ProtocolVersion,
            ModVersion = "1.2.3",
            GameVersion = "2.0.0",
            PlayerId = "player-a",
            PlayerName = "Alice",
        },
        new WelcomeMessage
        {
            Accepted = true,
            Reason = "ok",
            SessionId = "session",
            HostPlayerId = "host",
            HostPlayerName = "Host",
            SceneId = "Base",
            SceneName = "BaseScene",
            HostAlive = true,
            ClientHealth = 83.5f,
            ClientAlive = true,
            HostCharacterJson = "{\"host\":true}",
            ClientCharacterJson = "{\"client\":true}",
        },
        new ErrorMessage { Code = "error", Description = "description" },
        new PingMessage { Timestamp = 123456789L },
        new PongMessage { Timestamp = 987654321L },
        new PlayerStateMessage
        {
            Sequence = 42,
            PlayerId = "player",
            SceneId = "Warehouse",
            Transform = Transform(1f),
            Velocity = Vector(2f),
            AimPoint = Vector(3f),
            Health = 80f,
            MaxHealth = 120f,
            Alive = true,
            Running = true,
            Aiming = false,
            HeldItemTypeId = 101,
        },
        new CharacterSnapshotMessage
        {
            PlayerId = "player",
            ItemTreeJson = "{\"item\":1}",
            Health = 55f,
            Alive = true,
        },
        new SceneReadyMessage
        {
            RequestId = "ready",
            PlayerId = "player",
            SceneId = "Base",
            SceneName = "BaseScene",
            IsBase = true,
            CharacterJson = "{\"character\":1}",
            Health = 77f,
            Alive = true,
        },
        new SceneCommitMessage
        {
            TransitionId = "transition",
            SceneId = "Base",
            SceneName = "BaseScene",
        },
        new SceneLoadedMessage
        {
            PlayerId = "player",
            TransitionId = "transition",
            SceneId = "Base",
        },
        new ContainerLeaseRequestMessage { RequestId = "lease", ContainerId = "storage" },
        new ContainerLeaseResultMessage
        {
            RequestId = "lease",
            ContainerId = "storage",
            Granted = true,
            Reason = "ok",
            Version = 9,
            InventoryJson = "{\"inventory\":1}",
        },
        new ContainerCommitMessage
        {
            ContainerId = "storage",
            BaseVersion = 9,
            InventoryJson = "{\"inventory\":2}",
            CharacterJson = "{\"character\":2}",
            CharacterHealth = 64f,
        },
        new ContainerReleaseMessage
        {
            ContainerId = "storage",
            BaseVersion = 10,
            InventoryJson = "{\"inventory\":3}",
            CharacterJson = "{\"character\":3}",
            CharacterHealth = 63f,
        },
        new ContainerReleaseResultMessage
        {
            ContainerId = "storage",
            Accepted = true,
            Reason = "ok",
            Version = 11,
            InventoryJson = "{\"inventory\":3}",
        },
        new GroundPickupRequestMessage
        {
            RequestId = "pickup",
            GroundId = "ground-1",
            ItemTypeId = 301,
            Position = Vector(4f),
        },
        new GroundPickupResultMessage
        {
            RequestId = "pickup",
            Accepted = true,
            GroundId = "ground-1",
            Reason = "ok",
        },
        new GroundSpawnMessage
        {
            Item = GroundItem("ground-1", 301, 5f),
        },
        new GroundDespawnMessage
        {
            GroundId = "ground-1",
            ItemTypeId = 301,
            Position = Vector(5f),
        },
        GroundSnapshot(),
        new NpcSpawnMessage
        {
            NpcId = 7,
            PresetAssetName = "preset",
            PresetNameKey = "preset-key",
            CharacterItemJson = "{\"npc\":1}",
            Transform = Transform(6f),
            Team = 3,
            Health = 45f,
            MaxHealth = 90f,
            Alive = true,
        },
        NpcBatch(),
        new NpcDamageRequestMessage
        {
            NpcId = 7,
            Damage = Damage(),
        },
        new PlayerHealthAuthorityMessage { PlayerId = "player", Health = 40f, Alive = true },
        new PeerDeathMessage { PlayerId = "player", SceneId = "Warehouse" },
        new NoticeMessage { Text = "notice" },
        new ContainerSpawnMessage
        {
            ContainerId = "container-1",
            Transform = Transform(7f),
            Version = 12,
            InventoryJson = "{\"inventory\":4}",
        },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public async Task Every_message_round_trips_byte_for_byte(PartyMessage message)
    {
        using var encoded = new MemoryStream();
        await PartyWireCodec.WriteAsync(encoded, message, CancellationToken.None);

        encoded.Position = 0;
        var decoded = await PartyWireCodec.ReadAsync(encoded, CancellationToken.None);

        Assert.NotNull(decoded);
        Assert.Equal(message.GetType(), decoded.GetType());
        Assert.Equal(message.Kind, decoded.Kind);

        using var reencoded = new MemoryStream();
        await PartyWireCodec.WriteAsync(reencoded, decoded, CancellationToken.None);
        Assert.Equal(encoded.ToArray(), reencoded.ToArray());
        Assert.Null(await PartyWireCodec.ReadAsync(encoded, CancellationToken.None));
    }

    [Fact]
    public async Task Decoder_handles_streams_that_return_one_byte_at_a_time()
    {
        var original = new NoticeMessage { Text = new string('x', 4096) };
        using var encoded = new MemoryStream();
        await PartyWireCodec.WriteAsync(encoded, original, CancellationToken.None);
        using var shortReads = new ChunkedReadStream(encoded.ToArray(), 1);

        var decoded = Assert.IsType<NoticeMessage>(
            await PartyWireCodec.ReadAsync(shortReads, CancellationToken.None));

        Assert.Equal(original.Text, decoded.Text);
    }

    [Fact]
    public async Task Decoder_rejects_an_oversized_frame_before_allocating_the_payload()
    {
        using var frame = new MemoryStream();
        using (var writer = new BinaryWriter(frame, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x59545044u);
            writer.Write((ushort)PartyWireCodec.ProtocolVersion);
            writer.Write((ushort)PartyMessageKind.Notice);
            writer.Write(PartyWireCodec.MaximumPayloadLength + 1);
        }

        frame.Position = 0;
        await Assert.ThrowsAsync<PartyProtocolException>(
            () => PartyWireCodec.ReadAsync(frame, CancellationToken.None));
    }

    [Fact]
    public async Task Decoder_rejects_a_truncated_payload()
    {
        using var encoded = new MemoryStream();
        await PartyWireCodec.WriteAsync(
            encoded,
            new NoticeMessage { Text = "truncated" },
            CancellationToken.None);
        var bytes = encoded.ToArray()[..^1];
        using var truncated = new MemoryStream(bytes);

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PartyWireCodec.ReadAsync(truncated, CancellationToken.None));
    }

    private static GroundSnapshotMessage GroundSnapshot()
    {
        var message = new GroundSnapshotMessage();
        message.Items.Add(GroundItem("ground-a", 1, 1f));
        message.Items.Add(GroundItem("ground-b", 2, 2f));
        return message;
    }

    private static NpcStateBatchMessage NpcBatch()
    {
        var message = new NpcStateBatchMessage { Sequence = 8 };
        message.States.Add(new NpcState
        {
            NpcId = 7,
            Transform = Transform(8f),
            AimPoint = Vector(9f),
            Health = 35f,
            Alive = true,
        });
        return message;
    }

    private static GroundItemState GroundItem(string id, int typeId, float seed)
    {
        return new GroundItemState
        {
            GroundId = id,
            ItemTypeId = typeId,
            Transform = Transform(seed),
            ItemTreeJson = $"{{\"type\":{typeId}}}",
        };
    }

    private static DamageState Damage()
    {
        return new DamageState
        {
            DamageType = 2,
            DamageValue = 12.5f,
            DamageFactorToZombie = 1.2f,
            IgnoreArmor = true,
            IgnoreDifficulty = false,
            CritDamageFactor = 1.5f,
            CritRate = 0.25f,
            ArmorPiercing = 3f,
            IsExplosion = true,
            ArmorBreak = 4f,
            WeaponItemTypeId = 99,
            BleedChance = 0.2f,
            PhysicsFactor = 1f,
            FireFactor = 2f,
            PoisonFactor = 3f,
            ElectricityFactor = 4f,
            SpaceFactor = 5f,
            GhostFactor = 6f,
            IceFactor = 7f,
            Point = Vector(10f),
            Normal = Vector(11f),
        };
    }

    private static TransformState Transform(float seed)
    {
        return new TransformState
        {
            Position = Vector(seed),
            Rotation = new QuaternionState(seed, seed + 1f, seed + 2f, seed + 3f),
        };
    }

    private static VectorState Vector(float seed)
    {
        return new VectorState(seed, seed + 1f, seed + 2f);
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _chunkSize;

        public ChunkedReadStream(byte[] bytes, int chunkSize)
        {
            _inner = new MemoryStream(bytes, writable: false);
            _chunkSize = chunkSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, Math.Min(count, _chunkSize));
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
