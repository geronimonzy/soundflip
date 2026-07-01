# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repo shape

`audsw` is a single small Windows tray app plus one test project — no solution file, no monorepo. There is also an `AGENTS.md` in this repo with agent-facing notes; keep both in sync if you change build/behavior conventions.

Behavior is split across focused top-level files (flat namespace, no subfolders for source):
- `Program.cs` — CLI entry point / bootstrap, dispatches to command handlers
- `CommandLine.cs` — argv parsing into a `ParsedCommand` (`Command` verb + `CycleScope`/`AudioKind`)
- `Audio.cs` — thin wrapper over `AudioSwitcher.AudioApi.CoreAudio` (list/current/set/cycle-ring logic)
- `SettingsStore.cs` — JSON settings persistence + one-time legacy `.cfg` migration
- `TrayContext.cs` — tray icon, context menu (including the output/input cycle-ring checklists), toasts
- `UI.cs` — theme, Win11 styling, menu renderer, and toast rendering (WinForms)
- `Hotkeys.cs` — global hotkey registration/parsing (Win32 `RegisterHotKey` + message loop)
- `AutoStart.cs` — "Start with Windows": `Windows.ApplicationModel.StartupTask` when packaged (MSIX), HKCU Run key when unpackaged
- `AboutDialog.cs` — version/release metadata dialog
- `StoreAssets.cs` — generates default Microsoft Store logo assets (`export-assets` command)
- `CommandLine.cs`/`Program.cs` are the only pieces exercised as a CLI; everything else assumes the WinForms tray runtime

`audsw.Tests/` covers deterministic logic only (CLI parsing, settings JSON round-trip + legacy migration, ring-advance math, hotkey grammar, Store asset export) — it is **not** an end-to-end UI/audio test suite.

## Build and test commands

This targets `net8.0-windows10.0.22621.0` with `UseWindowsForms=true`. Meaningful build/run verification requires Windows with the .NET 8 SDK — this cannot be built or run on non-Windows hosts.

```powershell
# Canonical release build (self-contained single-file exe to .\dist)
.\build.ps1

# Unit tests (all)
dotnet test .\audsw.Tests\audsw.Tests.csproj -c Release

# Single test (dotnet test filter, e.g. by fully-qualified name or method name)
dotnet test .\audsw.Tests\audsw.Tests.csproj -c Release --filter "FullyQualifiedName~SettingsStoreTests"
```

`build.ps1` runs `dotnet publish .\audsw.csproj -c Release -o .\dist`. Live user settings are **not** copied beside the exe — they live in `%LocalAppData%`.

Practical verification path: run the unit tests, then publish and manually exercise `audsw`, `audsw list`, `audsw cycle`, `audsw export-assets .\Store\Assets`, and the tray UI from the built exe. See `TESTING.md` for the full manual checklist, including the call/voice-app follow-through matrix (Teams/Discord/Zoom/Steam/WhatsApp) that can only be checked by hand.

## Runtime behavior notes

- No-arg launch (`audsw`) goes straight to the tray; use `audsw help` for usage text.
- `audsw` is built as a `WinExe`: no console window is ever created (no flash on double-click or at login). CLI verbs attach to the parent terminal's console (`AttachConsole`) unless stdout is already redirected — consequence: an interactive shell prompt returns before CLI output appears, and CI must invoke the exe via `Start-Process -Wait` with redirected output rather than a bare call.
- Settings live at `%LocalAppData%\audsw\audsw.json`. An older flat `audsw.cfg` (`device1`/`device2`/`hotkey`) is auto-migrated on first launch into the output ring + cycle-output hotkey, then rewritten as JSON.
- Device resolution is case-insensitive substring matching against currently **active** playback/recording devices only.
- Switching a device sets both the default role and the default *communications* role together, for both outputs and inputs — this is what makes call apps (Teams/Discord/Zoom/etc.) follow the switch, and is the app's main differentiator (see `README.md` and `COMPETITIVE_REVIEW.md`).
- `cycle [outputs|inputs]` advances the matching ring to the next resolvable entry after the current default; a default outside the ring restarts at the first entry.
- There is no settings window: cycle rings are ticked in the tray **Output**/**Input** device checklists (saved to JSON immediately; the dropdown stays open for multi-select, ● marks the current default), and both cycle hotkeys are edited together in the tray **Hotkeys…** window (`HotkeysWindow` in `Hotkeys.cs`). Pairs and per-device jump hotkeys were removed in 1.1.2; retired JSON properties (`pairs`, `cyclePairs`, per-entry `hotkey`) are silently ignored on load.
- Supported hotkey grammar: modifier(s) plus `A-Z`, `0-9`, or `F1`-`F12`. Conflicting/unavailable hotkeys are skipped with a one-off warning; other configured hotkeys keep working.
- "Start with Windows" works in every build: packaged Store/MSIX builds use `Windows.ApplicationModel.StartupTask`, unpackaged builds fall back to a per-user `HKCU\...\CurrentVersion\Run` registry value pointing at the running exe.

## Dependency quirks

- `AudioSwitcher.AudioApi` / `AudioSwitcher.AudioApi.CoreAudio` are pinned to `4.0.0-alpha5` — the only public way to *set* (not just read) the Windows default audio device, via the undocumented `IPolicyConfig` COM interface.
- `NU1701` is intentionally suppressed in `audsw.csproj`: those packages target .NET Framework but work fine on `net8.0-windows`.
- WinRT APIs for packaged startup-task integration require the version-specific `net8.0-windows10.0.22621.0` TFM, not the `Microsoft.Windows.SDK.Contracts` package.
- `Store\Package.appxmanifest.template` is a packaging template, not a finished manifest — it still needs real publisher metadata before Store submission.

## CI/CD

- `.github/workflows/ci.yml` — Windows runner, on PRs/pushes to `main`: restore, build, run unit tests, `.\build.ps1`, smoke-check `help` and `export-assets` output, upload the `dist` artifact.
- `.github/workflows/release.yml` — triggered on `v*` tags: rebuilds/tests/publishes on Windows, zips `dist\`, uploads to the GitHub Release.
