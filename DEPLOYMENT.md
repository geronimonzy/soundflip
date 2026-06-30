# Deployment checklist — Microsoft Store + GitHub

Working checklist to take `audsw` from source to a published Store app and
GitHub release. Grounded in the current repo state. Check items off as you go.

Legend: `[ ]` todo · `[x]` done · **(external)** needs a Microsoft/GitHub action
outside the repo · **(blocker)** gates submission.

---

## A. Accounts & legal (external — do first)

- [ ] **(external, blocker)** Register a Microsoft Partner Center / Store dev
  account (individual ~$19 or company ~$99, one-time) at partner.microsoft.com.
- [ ] **(external, blocker)** Reserve the app name (`audsw` or chosen display
  name). This yields the **Identity Name** and **Publisher** (`CN=…`) that must
  go into the manifest (see B).
- [ ] Add a `LICENSE` file. (None in repo; upstream AudioDeviceSwitcher was
  Apache-2.0.)
- [ ] **(blocker)** Publish a privacy policy URL (required even though the app
  collects no data). GitHub Pages is fine. Reuse it for the listing + About.
- [ ] **(external)** Complete the Store age-rating questionnaire at submission.

## B. Placeholders & metadata (in-repo)

- [ ] `Store/Package.appxmanifest.template` (lines 13/17): replace
  `__PACKAGE_NAME__`, `__PUBLISHER__`, `__PUBLISHER_DISPLAY_NAME__`; set a real
  `Version` (not `1.0.0.0`). Keep only the `.template` in git; render the real
  manifest at package time.
- [ ] `audsw.csproj`: add `Company`, `Copyright`, `Version` /
  `InformationalVersion`, and `<AssemblyMetadata Include="HomepageUrl">` +
  `SupportUrl`. Without these, `AppMetadata` shows "Not configured" and the
  About dialog displays a yellow "Release metadata still needs to be
  configured…" notice. (`AssemblyInfo.cs` only has `InternalsVisibleTo`.)
- [ ] Add an `app.ico` and `<ApplicationIcon>` to the csproj — the .exe /
  Explorer icon is currently blank (the tray glyph is drawn at runtime only).

## C. Window / UI polish

- [ ] About dialog: confirm Homepage/Support buttons enable once URLs are set,
  and the missing-metadata notice disappears.
- [ ] Hotkey dialog: verify Esc/Cancel, Win-combo capture, themed rounded
  corners in light/dark and at high DPI.
- [ ] Toast + tray menu: re-verify at 150% DPI and on Windows 10 (DWM corner
  rounding is a no-op there — should still look fine).
- [ ] Decide sequencing of the **Settings Window Redesign** plan in
  `.kilo/plans/1782660322894-store-deployment-plan.md` (uncommitted) — it
  changes the config model, so pre- vs post-launch matters.

## D. First-run config

- [ ] `SettingsStore.Load()` returns defaults when the file is missing but never
  writes it. Add a first-run write so `%LocalAppData%\audsw\audsw.cfg` is
  created (documented + editable) on first launch.
- [ ] Keep root `audsw.cfg` as a sample only (or move to `docs/`) to avoid
  confusion with the live per-user file.

## E. MSIX packaging (the big missing piece)

- [ ] **(blocker)** There is no MSIX build today — `release.yml` only zips the
  loose `dist\*` exe. Add a real packaging path: a Windows Application Packaging
  project / `dotnet publish` package, or a `package.ps1` driving
  `MakeAppx` + `MakePri` that consumes the rendered manifest + `Store/Assets`.
- [ ] Generate and commit **branded** Store assets. `audsw export-assets`
  produces defaults from the speaker glyph (`Square44x44`, `StoreLogo`,
  `Square150x150`, `Wide310x150`, `Square310x310`). Decide: ship those or design
  real logos. Store also needs scale-200/400 variants + a listing screenshot.
- [ ] Signing: the Store re-signs on submission, but local sideload testing
  needs a self-signed cert. Keep any `.pfx` + password out of git (GitHub
  Secrets only).
- [ ] Add a release job that builds the `.msix` and attaches it to the GitHub
  Release (alongside or instead of the zip).

## F. Git artifacts ("leave only msix/exe")

- [x] `.gitignore` excludes `/bin /obj /dist` — no binaries tracked.
- [ ] Confirm the rendered `Package.appxmanifest` (with real identity) and any
  `.pfx` are also git-ignored; keep only `Package.appxmanifest.template`.
- [ ] Distribute binaries via GitHub Releases, never committed to the repo.

## G. README & docs

- [ ] Rewrite README for end users: what it does, Store install link,
  screenshot/GIF, hotkey usage, light/dark, per-user config path, and a separate
  "Build from source" section. Link LICENSE + privacy policy.

## H. Security / secrets

- [x] Scanned — clean. No API keys, tokens, secrets, certs, or connection
  strings in source. Only `GITHUB_TOKEN` / `secrets.*` in workflows (correct).

## I. Pre-submission QA

- [ ] Install the signed MSIX on a clean Windows 11 VM: tray, hotkey,
  **Start with Windows** (StartupTask works only when packaged), switching,
  About links, clean uninstall.
- [ ] Verify on Windows 10 19041 (the manifest `MinVersion`).
- [ ] Run the Windows App Certification Kit (WACK) on the MSIX before submitting.

## J. Store listing content (external)

- [ ] Description, feature list, search terms, category (Utilities & tools).
- [ ] Screenshots (1366×768+), store logo, optional promotional images.
- [ ] Support contact + privacy policy URL (from A).

---

### Suggested order

1. A (account/name reservation) — unblocks identity values.
2. B + D + G — in-repo, doable now without the account.
3. E (packaging script + assets) — can be drafted now, finalized once B has real
   identity values.
4. F → I → J — finalize, test, submit.
