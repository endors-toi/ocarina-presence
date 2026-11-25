# LEDGER — Ocarina Presence

## Current State

- Version 1.3.0. Windows tray app, self-contained single-file exe, packed by `pack.ps1`.
- Supported: Switch emulators (Fritchii, Lichii, Eden and forks) and the PC ports Hookshot, Longshot, HeyListen, Triforce, Project ReLink, Ocarina Reborn, NX2navi, NX2Saria and Rekarina.
- Game detection by process name, window title and, for ports, a marker file beside the exe.
- Save reading from `caption.sav`, `progress.sav` and `option.sav`; boss defeats announced; newest slot since launch shown.
- Port saves located from the game folder, the game's command line (`--user`, `--profile`) and its environment (Longshot's profile, the NX2 family's profile override).
- A port's own Discord status is hidden live (thread pause plus pipe swap) for every port that sends one; Project ReLink sends none and gets no toggle.
- Settings: per-detail toggles, game picker, Hide port presence, a save folder field that is auto-detected, editable and browsable, and a Check for updates button with the running version and the check's result.
- Self-update from the repository's latest GitHub release, gated by the player's click, from the tray or Settings.
- Sprint 1 (ports and save folders) validated.

## Next Steps

- Publish v1.3.0: tag `v1.3.0`, attach the zip from `pack.ps1`, use the text in `release/notes`.
- Confirm the ports added in 1.3.0 against real installs: they were mapped from each port's source, never run.
