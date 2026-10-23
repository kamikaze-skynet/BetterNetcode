# BetterNetcode

Less lag for players who aren't the host when a lot is going on.

## Why there's lag in busy fights

The game sends everything over Steam's networking and keeps Steam's default settings:

- **256 KB/s per player.** This is a fixed send limit. In a big fight (lots of enemies, spells and effects) the host can need more than that for each player, so data starts waiting in line. That's the lag that builds up when the screen gets busy and goes away when it calms down.
- **512 KB of waiting room.** Once that fills up, Steam refuses new messages and the game doesn't notice, so they're lost. That can leave players out of sync (for example, someone's spell missing on other screens).
- **Up to 5 ms of bundling delay** on every small message.

## What this mod changes

| Setting | Game | This mod |
|---|---|---|
| Send limit per player | 256 KB/s | 1024 KB/s |
| Waiting room before messages are lost | 512 KB | 4096 KB |
| Bundling delay | up to 5 ms | none |

The send limit is a cap, not a constant rate: only what the game needs is sent.

## Who needs it

**The host matters most**, since nearly all the game's data comes from the host. Players who also have it get the same settings for what they send, plus the overlay.

Works with MoreWizards, StarterSpells, ReviveRegen and TripleInfusion. It only changes Steam connections. If the game uses Epic (EOS) networking, the overlay says so and nothing is changed.

## Network overlay (F9)

Press **F9** in game to see:

- your FPS (a slow host also causes lag);
- each player's ping and connection quality;
- data sent to each player, against the limit;
- how much data is waiting, and for how long.

Green is fine, yellow is getting busy, red is a problem. If "waiting" goes red in big fights, the send limit is too low; if the host's FPS goes red, the host's PC is the bottleneck. When data waits longer than 250 ms, a warning is written to the BepInEx log even with the overlay hidden.

## Config

In your mod manager's config editor (or `BepInEx/config`):

| Section | Setting | Default | What it does |
|---|---|---|---|
| Steam Connection | Enabled | true | Use the settings below. Off = the game's normal settings. |
| Steam Connection | SendRateKB | 1024 | Most data per second sent to each player (game: 256). Lower it if the host's upload speed is slow. |
| Steam Connection | SendBufferKB | 4096 | How much can wait before messages are lost (game: 512). |
| Steam Connection | DisableNagle | true | Send right away instead of waiting up to 5 ms to bundle. |
| Overlay | ToggleKey | F9 | Show or hide the overlay. |
| Overlay | ShowOnStart | false | Show the overlay when the game starts. |
| Overlay | ScreenPosition | TopRight | Where the overlay sits. |
| Overlay | WarnQueueMs | 250 | Log a warning when data waits this long (0 = never). |

Settings apply when you host or join, so change them in the main menu.
