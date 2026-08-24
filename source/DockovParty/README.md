# DockovParty

DockovParty is an experimental, host-authoritative two-player co-op mod for *Escape from Duckov*.

## Player Flow

1. The host selects a save and clicks Continue Game. The mod calls `Listen` immediately and the original save-loading flow continues.
2. The client sets the host address in Duckov Mod Settings and clicks Join Game. No extra connection dialog is shown.
3. The host validates the protocol and game versions, then assigns the client character stored inside the host save.
4. Both players remain in the same scene. A normal transition is committed only when both living players select the same destination.
5. A dead player spectates the survivor until the survivor returns to base. A party wipe is committed to base by the host.

## Configuration

Configuration is exposed as ordinary serialized MonoBehaviour fields. DuckovModSettings discovers it automatically, so DockovParty has no settings-API dependency.

| Setting | Default | Purpose |
| --- | --- | --- |
| Player name | `玩家` | Name shown to the other player. |
| Listen address | `0.0.0.0` | Address used by the host listener. |
| Join address | `127.0.0.1` | Host name or address used by Join Game. |
| Port | `37622` | TCP listener and connector port. |
| State rate | `15` Hz | Player and NPC state update rate. |
| Interpolation delay | `0.10` s | Remote transform smoothing. |
| Diagnostic logging | Off | Enables additional networking logs. |

## Transport And Protocol

Gameplay code depends on `IStreamListener`, `IStreamConnector`, `StreamConnection`, and `StreamPeer`. A transport only needs to provide a connected, readable, writable `Stream`, so TCP can be replaced by a KCP-backed stream or another framed/reliable stream implementation without changing gameplay messages.

The default TCP implementation uses a persistent connection, `NoDelay`, socket keepalive, a serialized write path, a two-second application heartbeat, and a twelve-second receive timeout. The listener continues accepting after a peer disconnects, but rejects an additional peer while the two-player session is full.

Protocol frames contain:

```text
uint32 magic | uint16 protocol | uint16 message kind | int32 payload length | payload
```

Payloads are capped at 16 MiB. The protocol carries handshake, scene, player, NPC, container, ground-item, health, death, notice, and heartbeat messages.

## Authority Model

- The host simulates NPC AI and applies NPC damage. Client hits are requests against host NPC identifiers.
- The host owns the authoritative remote-player health representation and returns health corrections to the client.
- Player and NPC replicas use sequenced transform snapshots and interpolation.
- Both players use `Teams.player`; player-to-player `DamageReceiver.Hurt` calls are rejected.
- Container access uses an exclusive host lease and monotonic version. A client release remains pending until the host accepts the final inventory and character snapshot.
- Ground items receive host identifiers. Client pickup runs only after a host claim succeeds, and async spawn tombstones prevent a late spawn from recreating an already claimed item.
- The client character item tree and health are stored under a per-player key in the host save. Client `SaveFile` calls remain suppressed until the original local save cache is reloaded on the main menu.

## Current Boundaries

This code is an alpha foundation, not a claim of complete game-wide synchronization.

- Exactly one host and one client are supported.
- There is no host migration, reconnect resume transaction, rollback, encryption, authentication, relay, or NAT traversal.
- Character inventories, storage, loot containers, ground items, NPCs, damage, death, and scene transitions are covered.
- Quests, merchants, dialogue choices, base construction, crafting queues, world time, weather, and every mod-defined state are not yet fully authoritative.
- The implementation is compiled and protocol-tested against the game assemblies configured in `../Global.props`. Two live game processes are still required for end-to-end gameplay validation.

Do not expose the listener directly to an untrusted network. Use a trusted LAN or trusted overlay/VPN while the protocol has no authentication or encryption.

## Build And Test

```powershell
dotnet build .\source\DockovParty\DockovParty.csproj
dotnet test .\source\DockovParty.Tests\DockovParty.Tests.csproj
```

The tests round-trip every protocol message, force one-byte Stream reads, reject oversized and truncated frames, and exchange multiple frames over one TCP loopback connection.
