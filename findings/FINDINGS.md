# SoundFlip 1.4.0: user-feedback investigation

Investigated 2026-09-27/28 in the Hyper-V test VM (`tools/testvm/`, Windows 11 Pro
25H2 build 26200, 3840×2160, VB-Audio Virtual Cable = 2 outputs + 1 input).
Build: `dotnet publish` of `main` @ `eaec64f` (v1.4.0, unpackaged single-file exe).

**Status legend:** ✅ reproduced in VM · 📄 from code reading, not yet exercised · ❓ hypothesis / untested

## User reports → findings

| User said | Finding(s) |
|---|---|
| "notification on install: tell where it is in the tray, possibly add to pinned" | F1, F2 |
| "if one device, strange hint pops up for a couple of seconds" | F3, F4 |
| "left click should open a window, or tips or something" | F5, F6 |
| "scaling is weird at least in About (Windows scale 250%)" | F7 |

## Suggested order

1. **F7 DPI** – objective bug; affects every dialog at high scale, easy to verify in VM.
2. **F3 + F4 toast content/cases** – small, fixes the "strange hint" directly.
3. **F5 + F6 + F1 left-click flyout + first-run** – the bigger UX change; F1/F2 build on it.
4. **F8 tooltip**, then the minor items (F9–F12).
5. **F2 Store pin persistence** – needs a signed test MSIX in the VM first.

---

## F1 · After install/launch the app is invisible ✅ · **FIXED** on `fix/toast-messages`

> One-time welcome window on first run (`WelcomeDialog.cs`): where the icon is, how
> to keep it visible (+ *Taskbar settings* button), click/hotkey basics, optional
> *Start with Windows*, and a *Language* drop-down (applied and saved immediately,
> the window re-renders). Verified in VM at 250% incl. the autostart checkbox and
> switching to de/es/ru.
> Second launch: signals the running instance (named event `Local\SoundFlip.Show`),
> which shows "SoundFlip is already running" + opens its menu. Verified in VM.

- **Observed:** on a fresh Windows 11 profile the tray icon goes straight into the `^`
  overflow; the taskbar shows nothing new, no window, no notification
  (`img/01-fresh-install-tray.png`, `img/01b-overflow-opened.png`).
- **Also:** launching SoundFlip a second time (e.g. from Start, "did it start?") does
  nothing visible: the single-instance mutex makes the second process exit silently
  (`Program.cs:118`). Verified: still exactly one process, no screen change at all.
- **Store path makes it worse (📄):** the Store doesn't start the app after install,
  "Start with Windows" is off by default, so after reboot it isn't running either.
- **Fix direction:**
  - First-run (no settings file yet) welcome: small window or tray-anchored flyout:
    "SoundFlip lives in the tray. Click ^ and drag the speaker onto the taskbar to keep
    it visible", button *Open taskbar settings* (`ms-settings:taskbar`), optional
    "Start with Windows" checkbox, 3-line how-to (click = switch, hotkey = cycle).
  - Second launch: signal the running instance (named event / `WM_COPYDATA` to a hidden
    window) to open its flyout/menu instead of exiting silently.
- **Verify in VM:** `Restore-TestVM clean-100` → launch → screenshot; launch again →
  screenshot.

## F2 · Pinning to the visible tray ❓

- Apps can't programmatically pin (only the undocumented
  `HKCU\Control Panel\NotifyIconSettings\<id>\IsPromoted`; don't write it, Store-policy
  risk). Guidance in F1 is the supported route.
- **Suspected bug:** WinForms `NotifyIcon` registers without a GUID, so Windows keys the
  icon by exe path. MSIX installs live under a versioned `WindowsApps\…_1.4.0.0_…`
  path, so every Store update may look like a new icon and **drop the user's pin**.
- **Test plan:** build two signed test MSIX versions (self-signed cert trusted only in
  the VM), install v1 → pin → install v2 → check. Fix if confirmed: register the icon
  with `NIF_GUID` via our own `Shell_NotifyIcon` P/Invoke (GUID icons are also bound to
  a path, so behavior under MSIX needs the same test).

## F3 · The "strange hint" is a context-free warning toast ✅ · **FIXED** on `fix/toast-messages`

> Toasts now show a semibold title ("No outputs configured", "Audio output", ...) over
> the body; warnings stay 4 s, switch confirmations 2.2 s. Verified in VM at 250%.
> Empty ring now means "all active devices" (decided 2026-09-28), so the warning no
> longer appears on a fresh install; the Output/Input submenus say "None ticked: all
> devices are cycled". Verified in VM.

- **Observed:** with nothing ticked yet (fresh install), pressing the default hotkey
  **Ctrl+Alt+O** or double-clicking the tray icon shows, bottom-center, for ~1.8 s, an
  amber pill: **"Tick devices to cycle in the tray menu."**, no app name, no icon, no
  title (`img/02-hint-toast.png`).
- **Why:** `TrayContext.Notify` shows only the body when there is one:
  `string message = string.IsNullOrEmpty(text) ? title : text;` (`TrayContext.cs:296`),
  so the title "No outputs configured" (`TrayContext.cs:266`) is never shown. The
  hotkey is registered by default (`SettingsStore.cs:16`, bound at
  `TrayContext.cs:49`) before the user has configured anything, so the first contact
  with the app can easily be this message.
- **Fix direction:**
  - Best: **empty ring = all active devices**: cycling works out of the box, and the
    checklist becomes an optional filter. Then this toast mostly disappears.
  - Otherwise: show title + body + app glyph, longer duration for warnings (~4 s),
    and make clicking the toast open the menu/flyout at the right place.

## F4 · One device: cycling "switches" to the device you're already on ✅ · **FIXED** on `fix/toast-messages`

> Now shows "Only one output to cycle / <device>" (5 languages). Default is still
> re-applied so the communications role is aligned. Verified in VM.

- **Observed:** ring with one ticked device (or two where one is unplugged/disabled):
  the hotkey flashes the current device's name as if a switch happened; nothing
  changed (`img/03-one-device-toast.png`). Confirmed audio state is unchanged.
- **Also:** switch toasts show only the device name, no "Output"/"Input" label; same
  cause as F3 (title dropped). With inputs in play it's ambiguous which kind changed.
- **Why:** `Audio.CycleRing` always calls `MakeDefault(target)` and returns it, even
  when the target is already the default.
- **Fix direction:** if the resolved ring has one device (or target == current), show
  "Only one output available: X" (or "Already on X"), and don't re-set the default.
  Always show the kind ("Output → X").

## F5 · Single left-click does nothing; double-click cycles 📄 · **FIXED** on `fix/toast-messages`

> Decision: left-click opens the existing menu (not a flyout); double-click still
> cycles. The left-click menu waits the system double-click time (~0.5 s) so the two
> can coexist; the cycle is posted, not run inside the double-click handler (running
> it inline let NotifyIcon swallow the next single click). Verified in VM.

- `TrayContext.cs:26` wires only `DoubleClick` → `CycleOutputs()`. A single left-click
  has no handler. Double-click-to-cycle is undiscoverable and, before setup, produces
  F3's warning.
- **Fix direction:** left-click opens a compact **device flyout** anchored to the tray
  (like the Windows volume flyout): outputs (and inputs) as radio items, click = switch
  immediately, the current default marked; footer links: *Settings / Hotkeys / About*.
  First-run tips (F1) can live in the same flyout. Keep the right-click menu for power
  features. Decide what double-click should do then (probably nothing or cycle).

## F6 · Clicking a device in the menu doesn't switch to it ✅ · **FIXED** (renamed) on `fix/toast-messages`

> Decision: keep click = tick; submenus renamed "Outputs in cycle" / "Inputs in cycle"
> (5 languages) so they no longer read as device pickers.

- **Observed:** Output/Input submenus are checklists of *ring membership* (✓ = in ring,
  ● = current default, `img/09-output-submenu-250.png`). Clicking a device toggles the
  ✓ (`TrayContext.cs:173`) and does not switch audio. Almost every user will expect
  click = switch.
- **Fix direction:** the flyout from F5 does switching. In the menu, either rename the
  submenus ("Include in cycle ▸") or split into "Switch to ▸" + "Cycle through ▸".

## F7 · Dialogs break at high display scaling ✅ (main bug) · **FIXED** on `fix/dpi-scaling`

> Fix: `Dpi` helper in `UI.cs`; every 96-DPI pixel constant in About, Hotkeys,
> Set-hotkey, `Win11Button`, `DialogFooter`, toast and menu renderer goes through it;
> tray icon rendered at `SystemInformation.SmallIconSize`. Verified in VM at 100%
> (pixel-identical dialogs), 150% and 250% (About, Hotkeys, Set-hotkey, toast, icon).
> Still open: per-monitor DPI (PerMonitorV2) for mixed-scale multi-monitor setups.

- **Observed at 250%:**
  - About: window stays 100%-sized while text renders at 250%; description, publisher,
    copyright, settings path cut off; buttons "Ho / Su / Clo"
    (`img/04b-about-250.png` vs `img/04a-about-100.png`).
  - Hotkeys window: unusable: labels "Cycl", buttons "Cl", "Ca"
    (`img/05-hotkeys-250.png`).
  - Set-hotkey prompt: hint text cut mid-line, status line invisible, "Ca…"
    (`img/06-hotkeydialog-250.png`).
  - Toast: works but wraps a normal device name onto two lines; padding tight
    (`img/07-toast-250.png`).
  - Tray menu and submenus: **fine**.
  - German at 100% (longest strings): About and Hotkeys fit, so it's DPI only.
- **Why:** `Application.SetHighDpiMode(HighDpiMode.SystemAware)` (`Program.cs:126`) +
  code-built forms with default `AutoScaleMode.None` + hard-coded pixel sizes: fonts
  (in points) scale, pixel sizes don't.
  - `AboutDialog.cs:30` `ClientSize(470, 312)`, `:97/:112/:144` `MaximumSize(340, 0)`,
    56 px icon, 76 px column
  - `Hotkeys.cs:218` `ClientSize(430, 226)`, `:259` `MaximumSize(380, 0)`,
    `:350` `ClientSize(460, 184)`
  - `UI.cs:115` `Win11Button` 88×32, `UI.cs:170` footer height 60,
    `UI.cs:306` toast `MaxTextWidth = 480` and padding constants
- **Fix direction:** switch to PerMonitorV2 (`SetHighDpiMode(PerMonitorV2)` +
  `ApplicationHighDpiMode` in csproj) and make layouts DPI-independent: either
  `AutoScaleMode.Dpi` with `AutoScaleDimensions = new SizeF(96, 96)` set before controls
  are added, or scale every constant by `DeviceDpi / 96f` (one helper in `UI.cs`), and
  prefer `AutoSize` forms over fixed `ClientSize`. Handle `DpiChanged` for monitor moves.
- **Related ❓:** tray icon is rendered once from a 32 px bitmap (`UI.cs:492`); at 250%
  the tray wants 40 px, so likely slightly soft. Render at
  `SystemInformation.SmallIconSize` and re-render on DPI change. Not yet zoom-checked.
- **Verify in VM:** `Restore-TestVM audio-250` → open About / Hotkeys / Set hotkey,
  cycle toast; also at 100%, 150%, 200%.

## F8 · Tooltip goes stale when the default changes elsewhere ✅ · **FIXED** on `fix/toast-messages`

> Subscribes to `CoreAudioController.AudioDeviceChanged` (default/state/add/remove);
> tooltip now shows app / output / input. Verified in VM after a CLI switch.

- **Observed:** changed the default output outside SoundFlip (via `soundflip set`; same
  as Windows Settings/other apps); tray tooltip still said the old device
  (`img/08-stale-tooltip.png`).
- **Why:** `UpdateTooltip()` runs only at startup, after SoundFlip's own cycle and on
  language change (`TrayContext.cs:29/143/277`); no default-device change subscription.
- **Fix direction:** subscribe to default-device changes
  (`CoreAudioController.AudioDeviceChanged` / `IMMNotificationClient`) and refresh the
  tooltip (+ flyout if open). Consider showing output **and** input in the tooltip.

## F9 · Tray glyph ≈ Windows' own volume icon ✅ (observation)

- SoundFlip's filled Fluent "Speaker 2" is almost indistinguishable from the system
  volume icon. Once pinned next to it (what F1 asks users to do) it's confusing.
- **Fix direction:** a distinct glyph (speaker + swap arrows / "flip"), still Fluent style.
  Would also touch `app.ico` and Store assets (see CLAUDE.md icon note).

## F10 · "Cycle input" is always clickable ✅ (minor) · **RESOLVED** by empty ring = all devices

- Shown and enabled even with no inputs ticked and no input hotkey; clicking gives
  the same context-free warning as F3. Grey it out or, with F3's "empty = all"
  default, just make it work.

## F11 · Redundant startup hint line ✅ (minor) · **FIXED** on `fix/toast-messages`

> The status line is shown only when the toggle can't be used.

- Under "Start with Windows" the menu shows a disabled line "Enable startup from the
  tray menu.", which is the item right above it (`AutoStart.cs:100`,
  `TrayContext.cs:107`). Drop the hint when the toggle itself is available.

## F12 · Default Ctrl+Alt+O is claimed before the user opts in 📄 (minor) · **RESOLVED**

> With an empty ring cycling all devices, the default hotkey is useful from the first
> launch, and the welcome window names it. Kept as is.

- Registered on first launch although nothing is configured (F3). Conflicts are
  reported, but a silent grab of a common chord on first run is surprising. With
  F3's "empty = all" it at least becomes useful.

## F13 · Slow first start right after a VM snapshot restore ✅ (test-rig artifact, low priority)

- In the offline VM, right after restoring a snapshot, the first launch of a new
  build took 23-34 s for a CLI verb and ~3 min for the tray. Trace: ~68 s before
  `Main` (likely Defender scanning the new 188 MB single-file exe without cloud
  access), then ~190 s inside `new NotifyIcon { Visible = true }` (Shell_NotifyIcon
  blocking on a busy Explorer). In a settled session the whole tray startup is 275 ms.
- Probably not user-facing, but worth one check on a real machine: enable *Start
  with Windows*, sign out/in, and time until the icon appears.
- Test rig: warm the exe up (`soundflip list`) and give the VM a few minutes after a
  restore before launching the tray.

---

## Checked and fine ✅

- Cycling two outputs sets **both** Default and Communications roles each time
  (the app's key promise); verified with an in-guest Core Audio probe.
- A ring device that gets disabled is skipped; when it comes back it's picked up
  again without restarting the tray.
- Device names in the menu match Windows Settings > Sound.
- Single-instance: second launch never creates a second tray icon.
- Tray menu and submenus scale correctly at 250%.
- German strings fit the dialogs at 100%.

## Not tested yet ❓

- Store/MSIX install flow and pin persistence across updates (F2).
- Dark mode (menus, dialogs, toast colors).
- Mixed-DPI multi-monitor (SystemAware → blurry on the secondary monitor).
- Real call apps (Teams/Discord/Zoom) following the switch; TESTING.md matrix.
- Tray icon sharpness at 250% (F7 related).
- Input cycling with 2+ inputs (VM currently has one input; Hi-Fi Cable install
  was not done).

## Test-rig notes

- Evidence frames: `E:\VMs\ev\`, full screenshots `E:\VMs\evidence\`.
- Snapshots: `clean-100`, `clean-250` (no audio), `audio-100`, `audio-250`
  (VB-Cable installed, SoundFlip never run).
- Toasts live 1.8 s but a host-side screenshot takes ~11 s, so short-lived UI is
  recorded with the in-guest burst capture (`Start-TestVMCapture` /
  `Receive-TestVMCapture`).
- To simulate unplugging, use `guest\endpoint.ps1 -Name 16ch -Enabled $false`
  (IPolicyConfig). `Disable-PnpDevice` leaves the endpoint *Active* to Core Audio.
