# AGENTS.md

## Repo Shape
- Windows app plus one test project. There is no solution or monorepo in this repo.
- The app is still small, but behavior is now split across focused files: `Program.cs` for CLI/bootstrap, `TrayContext.cs` for tray UX, `SettingsStore.cs` for persistence, `AutoStart.cs` for packaged startup handling, `Hotkeys.cs` for global hotkeys, `AboutDialog.cs` for release metadata UI, `StoreAssets.cs` for generating default Store logos, and `UI.cs` for rendering/forms.
- `audsw.Tests/` covers deterministic logic only; it is not an end-to-end UI/audio test suite.
- Build/publish settings live in `audsw.csproj`; release packaging is driven by `build.ps1`.

## Build And Verification
- This project targets `net8.0-windows` with `UseWindowsForms=true`; meaningful build/run verification needs Windows with the .NET 8 SDK.
- Canonical release build: `.\build.ps1` in PowerShell. It runs `dotnet publish .\audsw.csproj -c Release -o .\dist`.
- Unit tests: `dotnet test .\audsw.Tests\audsw.Tests.csproj -c Release`.
- Practical verification path is tests plus publish on Windows, then exercise `audsw`, `audsw list`, `audsw cycle`, `audsw export-assets .\Store\Assets`, and the tray UI from the built exe.
- `start-daemon.vbs` is copied by `build.ps1`; live settings are no longer copied beside the exe.

## GitHub Automation
- GitHub Actions live in `.github/workflows/`.
- `ci.yml` runs on PRs to `main` and pushes to `main` using GitHub-hosted Windows runners.
- `release.yml` runs on tags matching `v*`, rebuilds/tests/publishes on Windows, and uploads a zipped `dist/` bundle to the GitHub Release.

## Runtime Gotchas
- No-arg launch is tray-first now; use `audsw help` if you want the usage text explicitly.
- Live settings are stored per-user as JSON at `%LocalAppData%\audsw\audsw.json`, not beside the executable. An older `audsw.cfg` is migrated once on first launch.
- `audsw` is still built as a console `Exe`, not `WinExe`. Tray launch hides its own console only when it owns that console; `start-daemon.vbs` remains the fully windowless unpackaged launcher.
- The tray's **Start with Windows** flow is Store/MSIX-oriented and uses `Windows.ApplicationModel.StartupTask`. In unpackaged builds the menu item stays visible but reports that startup is unavailable.
- Device resolution is case-insensitive substring matching against currently active playback/recording devices.
- Switching sets both the default role and the default communications role, for outputs and inputs alike.
- `cycle [outputs|inputs]` advances the matching ring to the next entry after the current default; a default outside the ring restarts at the first entry.
- There is no settings window: the output/input cycle rings are ticked directly in the tray **Output**/**Input** checklists (saved immediately), and the two cycle hotkeys are set from tray menu items. Pairs and per-device jump hotkeys were removed in 1.1.2; old JSON files containing them still load (the retired properties are ignored).

## Commands And Config
- Supported commands: `audsw`, `audsw list [outputs|inputs]`, `audsw set [output|input] <name>`, `audsw cycle [outputs|inputs]`, `audsw daemon`, `audsw export-assets <dir>`, `audsw help`.
- Settings JSON holds `outputs`/`inputs` rings (`match` per entry) plus `cycleOutputs`/`cycleInputs` hotkeys. The legacy `audsw.cfg` (`device1`/`device2`/`hotkey`, `#` comments) is only read for one-time migration.
- Supported hotkeys are modifier(s) plus `A-Z`, `0-9`, or `F1`-`F12`. Default is `ctrl+alt+o` for cycle-outputs.
- `Store\Package.appxmanifest.template` is a packaging template, not a finished manifest. It still needs real publisher metadata before submission.

## Dependency Quirk
- `AudioSwitcher.AudioApi` and `AudioSwitcher.AudioApi.CoreAudio` are pinned to `4.0.0-alpha5`.
- WinRT APIs for packaged startup-task integration come from the version-specific `net8.0-windows10.0.22621.0` target framework, not from `Microsoft.Windows.SDK.Contracts`.
- `NU1701` is intentionally suppressed because those packages target .NET Framework but are used here on `net8.0-windows`.
