# SoundFlip — minimal Windows audio device switcher

A small Windows utility that lives in the tray and switches **playback and recording**
devices on global hotkeys — cycle through a ring of outputs or a ring of inputs,
picked with checkboxes right in the tray menu — with focused CLI commands when
needed. Built for people who swap devices constantly on calls, streams, and games.

> SoundFlip was previously called **audsw**. Settings and autostart entries from
> audsw installs are migrated automatically on first launch.

## Commands

```
soundflip                          launch the tray app
soundflip list [outputs|inputs]    list active devices (* = current default)
soundflip set [output|input] <name>
                                   set the default device (case-insensitive substring)
soundflip cycle [outputs|inputs]   advance the chosen ring to its next device
soundflip daemon                   alias for launching the tray app
soundflip export-assets <dir>      generate default Microsoft Store logo assets
soundflip help                     show the usage text explicitly
```

The scope words are optional and default to outputs, so `soundflip list`,
`soundflip set <name>`, and `soundflip cycle` work on playback devices.

Every switch sets both the *default* and *default communications* roles together —
for outputs **and** inputs — so call/voice apps (Teams, Discord, Zoom, ...) follow
the change. See `TESTING.md` for the manual verification checklist.

## Build

Needs the .NET 8 SDK on Windows. From this folder in PowerShell:

```powershell
.\build.ps1
```

This produces a single self-contained `dist\soundflip.exe` (no .NET install needed
to run it). The exe is a Windows GUI app: launching it never opens a console
window, and CLI commands print into the terminal they were started from.

## Tests

Run the focused unit tests on Windows with:

```powershell
dotnet test .\soundflip.Tests\soundflip.Tests.csproj -c Release
```

The test suite covers deterministic logic only: command parsing, settings
JSON round-trip and legacy migration, ring-advance math, hotkey grammar,
and Store asset export. It does not try to automate tray UI, audio device
switching, or packaged startup behavior — see `TESTING.md` for the manual pass.

## Tray app (recommended)

Run `soundflip` (no window opens) to get a speaker icon in the notification area.
Right-click it to:

- **Cycle output / input** — advance the matching ring (also bound to hotkeys).
- **Output / Input** — a live checklist of active devices: tick the ones you want
  in the cycle ring (the menu stays open for multi-select), ● marks the current
  default. Changes are saved immediately.
- **Hotkeys…** — one window to view/change both cycle hotkeys at once.
- **Start with Windows** — works in every build: packaged Store/MSIX builds use
  the Windows startup task model, unpackaged builds use the per-user registry
  Run key.
- **About SoundFlip** — version, settings path, and release metadata.
- **Exit**.

The menu, dialogs, and toast follow the Windows light/dark setting automatically
(including dialog title bars), and use a Windows 11 style: rounded corners,
flat themed controls, content + footer dialog layout.

Double-clicking the icon cycles the output ring. Every switch pops a small toast
showing the new device, and the icon tooltip shows the current output.

### Hotkeys

Two optional ring hotkeys — **cycle outputs** (default `Ctrl+Alt+O`) and **cycle
inputs** — both set from the tray **Hotkeys…** window. Conflicting/unavailable
combos are skipped with a one-off warning; the rest keep working.

### Settings file

Live settings are stored as JSON at `%LocalAppData%\SoundFlip\soundflip.json`.
On first launch, settings from an older audsw install are imported automatically:
`%LocalAppData%\audsw\audsw.json` if present, otherwise the original flat
`audsw.cfg` (`device1`/`device2` become the output ring, `hotkey` becomes the
cycle-output hotkey). The file is safe to hand-edit.

## Run at login

Use **Start with Windows** in the tray menu — it works in every build. Packaged
Store/MSIX builds register a Windows startup task; unpackaged builds write a
per-user registry Run entry (`HKCU\...\CurrentVersion\Run`). Either way the app
starts silently in the tray with no window. A stale `audsw` Run entry from a
previous install is migrated to the new name automatically.

## Store packaging helpers

Generate default Store logo assets after a Windows build:

```powershell
.\dist\soundflip.exe export-assets .\Store\Assets
```

The repo also includes `Store\Package.appxmanifest.template` with the startup
task and app execution alias wiring needed for a packaged desktop build.

## GitHub Actions

- `.github/workflows/ci.yml` for Windows CI on pull requests to `main` and
  pushes to `main`
- `.github/workflows/release.yml` for tag-triggered GitHub Releases from tags
  such as `v1.3.0`

The release workflow builds on GitHub-hosted Windows runners, runs the unit
tests, publishes `dist\`, zips that bundle, and uploads it to the GitHub
Release.

## How it works

- Switching the default device uses the undocumented Windows `IPolicyConfig`
  COM interface (via the `AudioSwitcher.AudioApi.CoreAudio` library) — the only
  way to *set* (not just read) the default audio device on Windows.
- The hotkeys use Win32 `RegisterHotKey` + a message loop, so they work
  system-wide while the tray app runs in the background.
