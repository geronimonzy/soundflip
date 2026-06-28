# audsw — minimal CLI audio device switcher (Windows)

A tiny, no-UI command-line tool to switch the Windows default playback device,
plus a background daemon that toggles between two devices on a global hotkey.

## Commands

```
audsw list           list active playback devices (* = current default)
audsw set <name>     set default playback device (case-insensitive substring)
audsw cycle          toggle between device1/device2 from audsw.cfg
audsw daemon         run in background; cycle on the configured hotkey
```

Both the *default* and *default communications* roles are set together.

## Build

Needs the .NET 8 SDK on Windows. From this folder in PowerShell:

```powershell
.\build.ps1
```

This produces a single self-contained `dist\audsw.exe` (no .NET install needed
to run it). `audsw.cfg` and `start-daemon.vbs` are copied alongside it.

## Tray app (recommended)

Run `audsw daemon` (or double-click `start-daemon.vbs` for no window) to get a
speaker icon in the notification area. Right-click it to:

- **Switch now** — toggle between the two devices (same as the hotkey).
- **Device 1 / Device 2** — pick each device from a live list of your outputs.
  Your choice is saved to `audsw.cfg`, so no hand-editing.
- **Set hotkey…** — press a new modifier+key combo to rebind on the spot.
- **Start with Windows** — toggles a windowless launcher in your Startup folder.
- **Exit**.

The menu and the toast follow the Windows light/dark setting automatically, and
the menu uses a Windows 11 style (rounded, flat, themed).

Double-clicking the icon also switches. Every switch pops a small balloon
notification showing the new device, and the icon tooltip shows the current one.

The default hotkey is `Ctrl+Alt+O`. To change it, edit `hotkey` in `audsw.cfg`
(modifiers: `ctrl`, `alt`, `shift`, `win`; key: a letter, digit, or `f1`..`f12`)
and restart the daemon.

## Run windowless at login

Use **Start with Windows** in the tray menu — that's the easy path. (It writes a
hidden launcher to your Startup folder pointing at this exe.) The standalone
`start-daemon.vbs` is also there if you prefer to wire it up manually.

## How it works

- Switching the default device uses the undocumented Windows `IPolicyConfig`
  COM interface (via the `AudioSwitcher.AudioApi.CoreAudio` library) — the only
  way to *set* (not just read) the default audio device on Windows.
- The hotkey uses the Win32 `RegisterHotKey` + message loop, so it works
  system-wide while the daemon runs windowless in the background.
