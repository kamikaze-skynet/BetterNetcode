# BetterNetcode

A [BepInEx](https://github.com/BepInEx/BepInEx) IL2CPP mod for **The Spell Brigade** that reduces lag for non-host players in busy fights, and shows what the connection is doing.

See [thunderstore/README.md](thunderstore/README.md) for the player-facing description and config.

## What's going on in the game

The game uses Netcode for GameObjects with the community Facepunch (Steam) transport, and an EOS transport. With Steam it keeps GameNetworkingSockets' defaults:

| Steam setting | Default | Effect |
|---|---|---|
| `SendRateMin` / `SendRateMax` | 256 KB/s / 256 KB/s | Equal, so the rate is fixed (`SNP_ClampSendRate`; bandwidth estimation isn't implemented). |
| `SendBufferSize` | 512 KB | Past this, `SendMessage` returns `LimitExceeded`. `FacepunchTransport.Send` ignores the result, so the message is lost. |
| `NagleTime` | 5000 µs | The transport sends with `SendType.Reliable` / `Unreliable`, without `NoNagle`. |

Other notes from the game code:
- Enemy movement is batched by `EnemyMovementSnapshotBatcher` (up to 100 snapshots, every 0.1 s at most) and sent unreliably to clients, which interpolate with a 2-snapshot delay.
- `FacepunchTransport.GetCurrentRtt` and `EOSTransport.GetCurrentRtt` both return 0.

## What the mod does

- `SteamTuning`: a prefix on `FacepunchTransport.StartServer` / `StartClient` sets the global `SendRateMin` = `SendRateMax`, `SendBufferSize` and `NagleTime` through `SteamNetworkingUtils`. New connections copy the global values.
- `NetOverlay`: twice a second, reads `Connection.QuickStatus()` for each connection (the host's `connectedClients`, or the client's `connectionManager.Connection`). Shows ping, quality, KB/s in/out against the send limit, pending bytes and queue time. It also logs a warning when the queue time passes `WarnQueueMs`.

## Building

Requires the .NET 6 SDK and a game install with BepInEx IL2CPP 6.0.0-be.788 that has been run once (so `BepInEx/interop` exists).

```powershell
.\package.ps1 -GameDir "D:\SteamLibrary\steamapps\common\The Spell Brigade"
```

## License

MIT, see [LICENSE](LICENSE).
