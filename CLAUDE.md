# CLAUDE.md — Ocarina Presence

A Windows tray app that shows what the player is doing in Ocarina of Time (Nintendo Switch 2) as their Discord
status. It works with the game's PC ports and with the Switch emulators that run it. It reads
the game's own save files, never the game's memory, and while it shows its status it hides the status the port sends
by itself.

This file is stateless: rules, architecture and reference only.

## Working rules

**DO NOT:**
- Use `git` in any form. The dev manages all git operations. Reading history or diffs is fine when a task explicitly
  calls for it; never stage, commit, branch, push, or reset.
- Write, move or lock the game's save files. Ocarina Presence only reads them, briefly, and holds no handle between
  reads. A port that commits its save by renaming folders fails to commit while another program holds one of its
  files open.
- Ship anything debug. Research loggers, save recorders, hotkeys, embedded research data, and config switches for
  any of them must not be in the code or the build at all. Turning them off by default is not enough. Research
  happens outside the app, in throwaway scripts kept in the scratchpad or in `research\`, which is not compiled.
- Copy source code from the ports. Hookshot and Triforce are GPL projects: Ocarina Presence copies how they behave
  (where they keep files, how they name their window), never their code.

**DO:**
- Ask the dev before taking actions as usual.
- Keep every release's AI disclosure and the Claude credit, as the modding site that hosts the app requires.
- **Work sprints only when the dev asks for one.** Sprints are opt-in, not the default mode of work. When the dev
  names a sprint or asks for sprint-related work, read its doc under `docs/` before touching the code it covers,
  execute one story at a time, and keep the doc's Quick Reference and progress annotations up to date. Otherwise
  ignore the sprint docs and work the request directly.

## Comments

**ZERO comments. None. Ever.** No explanatory comments, no "why" comments, no section headers, no TODOs, no XML doc
blocks. Not in new code, not in edited code.

## Architecture

**One poll loop, one direction of data.** A worker thread wakes every few seconds, finds the game, reads its newest
save, and sends a Discord activity only when that activity changed, with a minimum gap between sends. The tray icon
and the Settings window show what the loop reports and never compute anything themselves.

**Finding the game.** Emulators are found by process name plus a window title that names the game. Ports are found
by process name, a marker file beside the executable (the port's renderer DLL) and the port's name in the window
title. The window is what tells the game apart from a launcher or helper process with the same process name. When several games are open, the player picks which one to show.

**Reading the save.** The game's save files are key/value tables whose keys are murmur3 hashes of field names. The
names come from the game's own scripts and data in its romfs. Location and stats come from the slot's `caption.sav`
and `progress.sav`, boss defeats from `option.sav`. The newest slot written since the game started is the one shown.
Each port and emulator keeps these files in its own place.

**Hiding the port's own status.** A port that sends its own Discord status is silenced live: its Discord thread is
paused only while it sleeps, and its Discord pipe handle is swapped for a dead pipe in the same handle slot. Leaving
the slot empty would let the game reuse it for a save file, which the resumed thread would then write into.
Resuming the thread makes the port reconnect and send its status again. The port's own settings cannot do this:
they are read only at launch, and an invalid application id breaks the port's connection. A port's Discord thread is
found by its thread name when it has one, else by scanning the executable for the code that references Discord's
pipe name and matching that function against each thread's start address.

**Self-update.** The app asks GitHub for the repository's latest release and compares its tag to the running assembly
version, so the tag must match the csproj `Version`. The release zip is what `pack.ps1` produces, and only the exe
inside it is taken. Nothing is installed without the player's click. A running exe can be renamed but not
overwritten, so the old exe is renamed aside, the new one takes its path, and the new process waits for the old one
to exit before it takes the single-instance mutex. The download is accepted only from the repository's own release
URLs and is checked against GitHub's asset digest when one is given.

## Commands

| Task | Command |
|---|---|
| Build | `dotnet build -c Release` |
| Publish the release zip | `.\pack.ps1` (deletes the whole `release\stage` folder first, including any config and log in it) |

To try a test build in place, copy only the new exe over the staged one. `pack.ps1` would wipe the stage.

## Sprints

Structural work is organized as **sprints**: each is a self-contained doc under `docs/`, written for a cold agent,
with the goal, verified code anchors, stories with build gates, the definition of done, and explicit non-goals. A
session picking up a sprint should be able to execute it from the doc alone. Sprint docs are stateful (they track
in-flight progress); everything else in `docs/` is timeless intent.

Every sprint doc opens with a **Quick Reference** section: what the sprint accomplishes and its current status. That
section is the recall surface. An agent that needs to know what a sprint does or where it stands reads the Quick
Reference only; the rest of the doc is execution detail for the agent working the sprint.

**Sprints carry no state in this file.** This section is an index and nothing more: one line per sprint, with name,
doc path and extended title. Status, progress, and in-flight findings live in the doc's Quick Reference, never here.
Do not open a sprint doc unless the dev asks for that sprint's work.

- **Sprint 1 — Ports and Save Folders** (`docs/sprint-1-ports-and-save-folders.md`) — Hookshot's latest releases
  working again, Triforce detected with its own status hidden, and the save folder shown in Settings: detected
  automatically, editable, and explained by a tooltip
