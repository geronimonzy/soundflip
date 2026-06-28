# audsw — minimal Windows audio device switcher

A small Windows utility that lives in the tray, toggles between two playback
devices on a global hotkey, and still exposes focused CLI commands when needed.

## Commands

```
audsw                launch the tray app
audsw list           list active playback devices (* = current default)
audsw set <name>     set default playback device (case-insensitive substring)
audsw cycle          toggle between the two configured devices
audsw daemon         alias for launching the tray app
audsw export-assets <dir>
                     generate default Microsoft Store logo assets
audsw help           show the usage text explicitly
```

Both the *default* and *default communications* roles are set together.

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
parsing/serialization, hotkey grammar, and Store asset export. It does not try
to automate tray UI, audio device switching, or packaged startup behavior.

## Tray app (recommended)

Run `audsw` (or double-click `start-daemon.vbs` for no window) to get a speaker
icon in the notification area. Right-click it to:

- **Switch now** — toggle between the two devices (same as the hotkey).
- **Device 1 / Device 2** — pick each device from a live list of your outputs.
- **Set hotkey...** — press a new modifier+key combo to rebind on the spot.
- **Start with Windows** — available in packaged Store/MSIX builds via the
  Windows startup task model.
- **About audsw** — version, settings path, and release metadata status.
- **Exit**.

The menu and the toast follow the Windows light/dark setting automatically, and
the menu uses a Windows 11 style (rounded, flat, themed).

Double-clicking the icon also switches. Every switch pops a small toast showing
the new device, and the icon tooltip shows the current one.

The default hotkey is `Ctrl+Alt+O`. Live settings are stored in
`%LocalAppData%\audsw\audsw.cfg`.

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
