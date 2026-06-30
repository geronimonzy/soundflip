# audsw — minimal Windows audio device switcher

A small Windows utility that lives in the tray and switches **playback and recording**
devices on global hotkeys — cycle through a ring of outputs, a ring of inputs, or
output+input **pairs** — with focused CLI commands when needed. Built for people who
swap devices constantly on calls, streams, and games.

## Commands

```
audsw                          launch the tray app
audsw list [outputs|inputs]    list active devices (* = current default)
audsw set [output|input] <name>
                               set the default device (case-insensitive substring)
audsw cycle [outputs|inputs|pairs]
                               advance the chosen ring to its next device
audsw daemon                   alias for launching the tray app
audsw export-assets <dir>      generate default Microsoft Store logo assets
audsw help                     show the usage text explicitly
```

The scope words are optional and default to outputs, so `audsw list`, `audsw set
<name>`, and `audsw cycle` behave as before.

Every switch sets both the *default* and *default communications* roles together —
for outputs **and** inputs — so call/voice apps (Teams, Discord, Zoom, ...) follow
the change. See `TESTING.md` for the manual verification checklist.

## Build

Needs the .NET 8 SDK on Windows. From this folder in PowerShell:

```powershell
.\build.ps1
```

This produces a single self-contained `dist\audsw.exe` (no .NET install needed
to run it). `start-daemon.vbs` is copied alongside it for windowless unpackaged
launches.

## Tests

Run the focused unit tests on Windows with:

```powershell
dotnet test .\audsw.Tests\audsw.Tests.csproj -c Release
```

The test suite covers deterministic logic only: command parsing, settings
JSON round-trip and legacy-`.cfg` migration, ring-advance math, hotkey grammar,
and Store asset export. It does not try to automate tray UI, audio device
switching, or packaged startup behavior — see `TESTING.md` for the manual pass.

## Tray app (recommended)

Run `audsw` (or double-click `start-daemon.vbs` for no window) to get a speaker
icon in the notification area. Right-click it to:

- **Cycle output / input / pair** — advance the matching ring (also bound to hotkeys).
- **Output / Input** — pick any active device from a live list to switch immediately.
- **Pairs** — apply a configured output+input combination.
- **Settings…** — manage the output ring, input ring, pairs, and every hotkey in one
  window: add/remove/reorder devices, build pairs, and assign per-device, per-pair,
  and cycle hotkeys.
- **Start with Windows** — available in packaged Store/MSIX builds via the
  Windows startup task model.
- **About audsw** — version, settings path, and release metadata status.
- **Exit**.

The menu, dialogs, and toast follow the Windows light/dark setting automatically, and
use a Windows 11 style (rounded, flat, themed).

Double-clicking the icon cycles the output ring. Every switch pops a small toast
showing the new device(s), and the icon tooltip shows the current output.

### Hotkeys

Three optional ring hotkeys — **cycle outputs** (default `Ctrl+Alt+O`), **cycle
inputs**, and **cycle pairs** — plus an optional **direct-jump** hotkey on each
individual device and each pair. Conflicting/unavailable combos are skipped with a
one-off warning; the rest keep working.

### Settings file

Live settings are stored as JSON at `%LocalAppData%\audsw\audsw.json`. An older
`audsw.cfg` from a previous build is imported automatically on first launch
(`device1`/`device2` become the output ring, `hotkey` becomes the cycle-output
hotkey) and rewritten as JSON. The file is safe to hand-edit.

## Run windowless at login

In packaged Store/MSIX builds, use **Start with Windows** in the tray menu.
For unpackaged builds, `start-daemon.vbs` remains the simple windowless launcher
you can wire into login manually.

## Store packaging helpers

Generate default Store logo assets after a Windows build:

```powershell
.\dist\audsw.exe export-assets .\Store\Assets
```

The repo also includes `Store\Package.appxmanifest.template` with the startup
task and app execution alias wiring needed for a packaged desktop build.

## GitHub Actions

The repo now includes:

- `.github/workflows/ci.yml` for Windows CI on pull requests to `main` and
  pushes to `main`
- `.github/workflows/release.yml` for tag-triggered GitHub Releases from tags
  such as `v1.2.3`

The release workflow builds on GitHub-hosted Windows runners, runs the unit
tests, publishes `dist\`, zips that bundle, and uploads it to the GitHub
Release.

## How it works

- Switching the default device uses the undocumented Windows `IPolicyConfig`
  COM interface (via the `AudioSwitcher.AudioApi.CoreAudio` library) — the only
  way to *set* (not just read) the default audio device on Windows.
- The hotkey uses the Win32 `RegisterHotKey` + message loop, so it works
  system-wide while the tray app runs in the background.
