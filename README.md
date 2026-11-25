# Ocarina Presence

Discord Rich Presence for Ocarina of Time on Nintendo Switch 2, running on a PC port or a Switch emulator. Wherever your adventure takes you, your friends will know.

A Windows tray app. It reads the game's own save files, never its memory, and never writes to them.

## Features

- Follows you from area to area
- Announces your boss defeats
- Shows Link's age, hearts, magic, play time, Skulltulas and heart pieces
- Every detail can be turned on or off
- Shows the time elapsed since your session started
- Detects the game when it launches, and clears your status when it closes
- Hides a port's own Discord status while yours is showing
- Lets you pick which game to show when more than one is open
- Rests in the tray and can start with Windows
- Checks GitHub for new releases, from the tray or Settings, and updates itself when you say so

## Supported games

- Switch emulators: Fritchii, Lichii, Eden and their forks
- PC ports: Hookshot, Longshot, HeyListen, Triforce, Project ReLink, Ocarina Reborn, NX2navi, NX2Saria, Rekarina

## Requirements

- Windows 10 or 11 (64-bit)
- The Discord desktop app

## Installation

1. Download the latest zip from the [releases page](https://github.com/endors-toi/ocarina-presence/releases).
2. Unzip it anywhere you can write to (not Program Files).
3. Open Discord.
4. Run `OcarinaPresence.exe` once, then right-click the tray icon for settings.

## How it works

One loop wakes every few seconds, finds the game, reads its newest save, and updates Discord only when something changed. Emulators are found by process name and a window title that names the game. Ports are found by process name, a marker file beside the executable and the port's name in the window title.

Because location and stats come from the save files, they appear after the game autosaves, which it does when you move between areas. Switching save slots is noticed on the next autosave too.

A port that sends its own Discord status is silenced while yours shows: its Discord thread is paused and its pipe handle is swapped for a dead pipe. Its own status returns when Ocarina Presence stops showing yours.

## Troubleshooting

- **"Windows protected your PC"**: click More info, then Run anyway.
- **Nothing shows on Discord**: in Discord, turn on User Settings > Activity Privacy > Share your detected activities with others.
- **The game isn't detected**: open Settings from the tray icon. It lists what it can see, and "Open log" shows what the app has been doing.

## Building

```
dotnet build -c Release
```

`.\pack.ps1` publishes the self-contained single-file exe and zips it into `release\`. Releases are tagged with the project's `Version` (for example `v1.2.0`), and the zip is attached as a release asset, which is what the in-app updater looks for.

## AI disclosure

This tool was developed with AI coding assistance from [Claude Code](https://claude.com/claude-code) by Anthropic. I designed the tool and directed its development, drawing on my experience building previous Discord presences. Claude wrote the code to my direction. I reviewed the critical parts myself, in particular making sure the tool only reads save files and never writes to them. No AI-generated art is included.

## Credits

- Claude (Anthropic): AI coding assistant, wrote the code
- Thanks to the Eden devs, sortos, frizti, and the open source community

Not affiliated with Nintendo.

## License

[GPL-3.0](LICENSE)
