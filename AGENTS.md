# AGENTS.md

## Repo Shape
- Windows app plus one test project. There is no solution or monorepo in this repo.
- The app is still small, but behavior is now split across focused files: `Program.cs` for CLI/bootstrap, `TrayContext.cs` for tray UX, `SettingsStore.cs` for persistence, `AutoStart.cs` for packaged startup handling, `Hotkeys.cs` for global hotkeys, `AboutDialog.cs` for release metadata UI, `StoreAssets.cs` for generating default Store logos, and `UI.cs` for rendering/forms.
- `soundflip.Tests/` covers deterministic logic only; it is not an end-to-end UI/audio test suite.
- Build/publish settings live in `soundflip.csproj`; release packaging is driven by `build.ps1`.

## Build And Verification
- This project targets `net8.0-windows` with `UseWindowsForms=true`; meaningful build/run verification needs Windows with the .NET 8 SDK.
- Canonical release build: `.\build.ps1` in PowerShell. It runs `dotnet publish .\soundflip.csproj -c Release -o .\dist`.
- Unit tests: `dotnet test .\soundflip.Tests\soundflip.Tests.csproj -c Release`.
- Practical verification path is tests plus publish on Windows, then exercise `soundflip`, `soundflip list`, `soundflip cycle`, `soundflip export-assets .\Store\Assets`, and the tray UI from the built exe.
- Live settings are not copied beside the exe.

## GitHub Automation
- GitHub Actions live in `.github/workflows/`.
- `ci.yml` runs on PRs to `main` and pushes to `main` using GitHub-hosted Windows runners.
- `release.yml` runs on tags matching `v*`, rebuilds/tests/publishes on Windows, and uploads a zipped `dist/` bundle to the GitHub Release.

## Runtime Gotchas
- No-arg launch is tray-first now; use `soundflip help` if you want the usage text explicitly.
- Live settings are stored per-user as JSON at `%LocalAppData%\SoundFlip\soundflip.json`, not beside the executable. Pre-rename `audsw` settings (`audsw.json`, then `audsw.cfg`) are migrated once on first launch; a stale `audsw` HKCU Run value is migrated to `SoundFlip`.
- `soundflip` is built as a `WinExe`: no console window is ever created. CLI verbs attach to the parent terminal's console unless stdout is already redirected; a bare PowerShell invocation does not wait for the exe, so CI smoke checks use `Start-Process -Wait` with redirected output.
- The tray's **Start with Windows** works in every build: `Windows.ApplicationModel.StartupTask` when packaged (Store/MSIX), a per-user `HKCU\...\CurrentVersion\Run` registry value when unpackaged.
- Device resolution is case-insensitive substring matching against currently active playback/recording devices.
- Switching sets both the default role and the default communications role, for outputs and inputs alike.
- `cycle [outputs|inputs]` advances the matching ring to the next entry after the current default; a default outside the ring restarts at the first entry.
- There is no settings window: the output/input cycle rings are ticked directly in the tray **Output**/**Input** checklists (saved immediately), and both cycle hotkeys are edited together via the tray **Hotkeys…** window. Pairs and per-device jump hotkeys were removed in 1.1.2; old JSON files containing them still load (the retired properties are ignored).

## Commands And Config
- Supported commands: `soundflip`, `soundflip list [outputs|inputs]`, `soundflip set [output|input] <name>`, `soundflip cycle [outputs|inputs]`, `soundflip daemon`, `soundflip export-assets <dir>`, `soundflip help`.
- Settings JSON holds `outputs`/`inputs` rings (`match` per entry) plus `cycleOutputs`/`cycleInputs` hotkeys. The legacy `audsw.cfg` (`device1`/`device2`/`hotkey`, `#` comments) is only read for one-time migration.
- Supported hotkeys are modifier(s) plus `A-Z`, `0-9`, or `F1`-`F12`. Default is `ctrl+alt+o` for cycle-outputs.
- `Store\Package.appxmanifest.template` is a packaging template, not a finished manifest. It still needs real publisher metadata before submission.

## Dependency Quirk
- `AudioSwitcher.AudioApi` and `AudioSwitcher.AudioApi.CoreAudio` are pinned to `4.0.0-alpha5`.
- WinRT APIs for packaged startup-task integration come from the version-specific `net8.0-windows10.0.22621.0` target framework, not from `Microsoft.Windows.SDK.Contracts`.
- `NU1701` is intentionally suppressed because those packages target .NET Framework but are used here on `net8.0-windows`.
