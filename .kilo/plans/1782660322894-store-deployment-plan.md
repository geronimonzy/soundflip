# GitHub CI/CD Plan for audsw

## Goal
Add GitHub Actions that validate the Windows app on GitHub-hosted runners, run real unit tests for logic that can be isolated from Windows shell/audio state, build the published app, and automatically create GitHub Releases from version tags.

## Resolved Scope
- CI runs on:
  - pull requests targeting `main`
  - pushes to `main`
- Runners: GitHub-hosted Windows runners
- Tests: add a real unit test project for pure logic
- Release trigger: pushed version tags such as `v1.2.3`
- Release version source of truth: git tag only
- Release payload: ZIP built from published `dist/`
- Out of scope for this first pipeline:
  - Microsoft Store submission automation
  - MSIX signing/publishing
  - tray UI end-to-end automation
  - hardware-dependent audio switching tests

## Current Repo Constraints
- The app is Windows-only: `net8.0-windows` + WinForms.
- Canonical release build today is `./build.ps1` in PowerShell, which runs `dotnet publish .\audsw.csproj -c Release -o .\dist`.
- There is no test project yet.
- Real runtime behavior depends on Windows audio devices, global hotkeys, and tray/shell state, so CI must avoid treating those as reliable automated tests.
- There is no `.github/workflows/` directory yet.

## Implementation Plan

### 1. Create a small testable seam before adding CI
- Keep the test project focused on logic that does not require audio devices, WinForms handles, or package identity.
- Test candidates:
  - command parsing in `Program.cs`
  - settings parsing/serialization in `SettingsStore.cs`
  - hotkey parsing/formatting in `Hotkeys.cs`
  - Store asset generation file/output expectations in `StoreAssets.cs` where feasible
- Avoid direct unit tests for:
  - `TrayContext`
  - `AutoStart` packaged startup task integration
  - live audio device resolution/switching
  - message-loop or shell-bound behavior
- If needed, extract tiny pure helpers so tests can target deterministic logic without instantiating WinForms or touching real hardware state.

### 2. Add a dedicated test project
- Create a test project under the repo, for example `audsw.Tests`.
- Target a framework compatible with the testable logic split.
- Reference the app project or extracted logic assembly as needed.
- Add focused tests for:
  - valid and invalid command parsing
  - settings load/save round-trip with temp files
  - comment/blank-line handling in config parsing
  - hotkey grammar acceptance/rejection
  - canonical hotkey formatting
  - asset export creates the expected filenames
- Keep tests deterministic and non-interactive.

### 3. Add a CI workflow for validation
- Add `.github/workflows/ci.yml`.
- Trigger on:
  - `pull_request` to `main`
  - `push` to `main`
- Use a Windows runner.
- Steps:
  - checkout
  - setup .NET 8 SDK
  - restore
  - build the solution/project in Release
  - run unit tests
  - publish the app using the repo’s Windows publish flow
  - run non-interactive smoke checks against published output where safe
- Recommended smoke checks after publish:
  - `audsw.exe help`
  - `audsw.exe export-assets <tempdir>`
- Do not make CI depend on `audsw list` / `set` / `cycle` succeeding on a GitHub runner, because audio device state is not reliable there.

### 4. Add a release workflow
- Add `.github/workflows/release.yml`.
- Trigger on pushed tags matching `v*`.
- Use a Windows runner.
- Steps:
  - checkout the tagged commit
  - setup .NET 8 SDK
  - restore
  - build
  - run unit tests
  - publish via the repo’s Windows publish flow
  - package the contents of `dist/` into a ZIP named from the tag, for example `audsw-v1.2.3-win-x64.zip`
  - create or update the GitHub Release for that tag
  - upload the ZIP as a release asset
- Keep the git tag as the release version source of truth in this first version.
- Do not fail the release based on csproj version matching yet.

### 5. Keep workflow responsibilities clear
- CI workflow responsibility:
  - validate code on `main` and PRs
  - fail fast on compile/test/smoke-check issues
- Release workflow responsibility:
  - validate the tagged revision again
  - produce the downloadable artifact
  - publish the GitHub Release asset
- Do not combine PR CI and tagged release logic into one large workflow unless there is a strong reason.

### 6. Artifact conventions
- Release asset should be a ZIP of the built `dist/` directory.
- Include at minimum:
  - `audsw.exe`
  - `start-daemon.vbs`
- Recommended artifact naming:
  - `audsw-<tag>-win-x64.zip`
- Keep naming stable so future release consumers can script against it.

### 7. GitHub configuration after workflows land
- In GitHub repo settings, require the CI workflow as a status check for `main`.
- Restrict release creation to version tags that point to validated commits.
- No secrets are required for the first GitHub Release pipeline if it uses the default `GITHUB_TOKEN` only.
- Store signing/secrets can be added later in a separate pipeline when publisher metadata and MSIX signing are ready.

## Risks To Manage
- The repo currently has no tests, so the first implementation step must avoid trying to test WinForms/audio behavior directly.
- `System.Drawing` and asset generation should only be validated on Windows runners.
- `AudioSwitcher.AudioApi*` uses older alpha packages and undocumented Windows audio behavior; CI should not pretend to verify live device switching on shared runners.
- The app is split into multiple files but still not a multi-project solution; workflow commands should target the actual project shape rather than assuming a solution file exists.

## Validation Plan
- CI is green for PRs and pushes to `main`.
- Unit tests pass on GitHub-hosted Windows.
- Publish step produces `dist/` successfully on GitHub-hosted Windows.
- Smoke checks succeed for non-interactive commands.
- Pushing a tag like `v1.2.3` creates a GitHub Release with a downloadable ZIP artifact.
- The ZIP can be downloaded and unpacked on Windows, and contains the expected direct-download bundle.

## Open Questions For Implementation Agent
- Whether a tiny logic extraction is needed before tests can be added cleanly.
- Whether smoke checks should call `build.ps1` directly or use raw `dotnet publish` in workflows.
- Whether to upload CI artifacts on non-tag runs for easier debugging, or only on releases.
