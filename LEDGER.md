# LEDGER — Ocarina Presence

## Current State

- Version 1.2.0. Windows tray app, self-contained single-file exe, packed by `pack.ps1`.
- Supported: Switch emulators (Fritchii, Lichii, Eden and forks) and the PC ports Hookshot, Triforce and NX2navi.
- Game detection by process name, window title and, for ports, a marker file beside the exe.
- Save reading from `caption.sav`, `progress.sav` and `option.sav`; boss defeats announced; newest slot since launch shown.
- Port saves located from the game folder and the game's `--user` argument.
- A port's own Discord status is hidden live (thread pause plus pipe swap) for Hookshot and Triforce.
- Settings: per-detail toggles, game picker, Hide port presence, and a save folder field that is auto-detected, editable and browsable.
- Self-update from the repository's latest GitHub release, gated by the player's click.
- Sprint 1 (ports and save folders) validated.

## Next Steps

- Publish v1.2.0: tag `v1.2.0`, attach the zip from `pack.ps1`, use the text in `release/notes`.
