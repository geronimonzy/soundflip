# Settings Window Redesign Plan for audsw

## Goal
Replace the tray-heavy configuration model with a reusable Settings window, add ordered output/input switching with optional per-device hotkeys, and keep the tray menu minimal while preserving fast cycling behavior.

## Resolved Scope
- Tray menu keeps only:
  - `Change output`
  - `Change input`
  - `Settings`
  - `Exit`
- Double-click tray icon continues to run `Change output`.
- Settings become a single-instance modeless window.
- Settings use explicit `Save` / `Cancel`.
- Closing with the window `X` prompts `Save / Discard / Cancel` when there are unsaved changes.
- Output and input each use ordered cycle lists.
- Both output and input switching set both the default and communications roles.
- Cycle actions skip missing devices and continue.
- If the current Windows default is outside the enabled configured list, cycle falls back to the first enabled available item.
- Settings move to structured JSON in the existing per-user settings location.
- No migration from the old flat config is needed.
- Device entries are stored by stable Windows device ID.
- Each device entry has:
  - editable alias
  - visible full Windows device name
  - enable checkbox
  - optional direct hotkey
  - saved order
- `Add new device` uses a picker that shows currently available devices of that type only.
- Already-added devices remain in settings even if later unavailable.
- Per-device hotkeys are optional.
- One hotkey may be shared by exactly one output entry and one input entry to switch both in sequence.
- Any other hotkey duplication is invalid and must block save.
- Shared paired hotkeys may partially succeed: if one side is unavailable, switch the available side and show a non-blocking warning.
- Settings save should validate hotkeys by attempting runtime registration before committing.
- Saving applies immediately to the running tray app with no restart.
- Autostart must support both:
  - packaged/MSIX builds via `StartupTask`
  - unpackaged ZIP builds via Startup-folder launcher/shortcut behavior

## Current Code Constraints
- Current settings model only stores `device1`, `device2`, and one global hotkey in `SettingsStore.cs`.
- Current audio logic only supports playback device switching in `Audio.cs` and `Program.cs`.
- Current tray configuration UI is built directly in `TrayContext.cs`.
- Current autostart logic is packaged-only in `AutoStart.cs`, while shipped GitHub builds are unpackaged ZIP bundles.
- Current hotkey runtime only handles one global switching hotkey.

## Target Behavior
### Tray
- `Change output`: cycle through enabled configured output devices in saved order.
- `Change input`: cycle through enabled configured input devices in saved order.
- `Settings`: open or focus the single settings window.
- `Exit`: quit the tray app.
- Tray double-click: same as `Change output`.

### Settings Window Layout
Top-to-bottom sections:
1. Output cycle hotkey
2. Output device list used for cycling
3. Input cycle hotkey
4. Input device list used for cycling
5. Autostart
6. About
7. Save / Cancel actions

Each output/input device entry includes:
- enabled checkbox
- drag handle / drag reorder support
- alias editor
- full Windows device name shown below alias
- optional direct hotkey picker
- remove action

Each list includes:
- `Add new device`
- reorder support
- unresolved/missing-device warning state when applicable

### Hotkey Rules
- Separate top-level cycle hotkeys for output and input.
- Optional per-device direct hotkeys.
- Duplicates are invalid except one explicit paired case:
  - exactly one output entry + exactly one input entry may share the same hotkey
- Save must fail if any configured hotkey:
  - conflicts internally outside the allowed paired case
  - cannot be registered on the current machine
- Save success must atomically replace old runtime registrations with the new set.

### Switching Rules
- Output cycle uses enabled output entries only.
- Input cycle uses enabled input entries only.
- Missing/unavailable entries are skipped during cycle.
- If current system default is not represented in the enabled available list, the next cycle action selects the first enabled available entry.
- Direct per-device hotkey switches that exact device.
- Shared output+input hotkey switches output first, then input.
- If one side of a shared hotkey is unavailable, still switch the available side and show a non-blocking warning.

## Data Model Changes
Replace the flat settings model with JSON.

Suggested shape:
- `outputCycleHotkey`
- `inputCycleHotkey`
- `autostartEnabled`
- `outputDevices`: array of entries
- `inputDevices`: array of entries

Each device entry should contain at minimum:
- `deviceId`
- `alias`
- `lastKnownSystemName`
- `enabled`
- `hotkey` nullable
- `order`

Implementation should avoid depending on free-form name matching for configured entries after this redesign.

## Implementation Tasks
1. Replace the persisted settings model.
- Introduce a new JSON-backed settings schema and serializer in `SettingsStore.cs`.
- Remove dependence on the old flat `device1/device2/hotkey` config structure.
- Keep the same settings file location under `%LocalAppData%\audsw\`.

2. Extend audio support to input devices.
- Add enumeration, resolution, and switching support for recording/input devices.
- Add output and input cycle helpers that operate on ordered enabled lists.
- Add direct-select helpers for individual configured entries.
- Ensure both default and communications roles are set for both output and input switching.

3. Add a richer device configuration model.
- Introduce output/input device entry types with stable device IDs, aliases, enabled state, order, and optional hotkey.
- Track currently unavailable entries without deleting them.
- Add helpers to build the active cycle sequence from saved config.

4. Replace the current tray configuration UX.
- Simplify `TrayContext` menu to the four final actions only.
- Keep double-click behavior mapped to `Change output`.
- Remove per-device tray submenus and tray-based hotkey editing.
- Make `Settings` open/focus a single modeless settings window instance.

5. Build the Settings window.
- Create a reusable modeless settings form.
- Add output section with cycle hotkey editor and ordered output device list.
- Add input section with cycle hotkey editor and ordered input device list.
- Add drag reorder support for both lists.
- Add alias editing UI while always showing the full Windows device name below.
- Add enable toggles and remove actions per entry.
- Add `Add new device` pickers for output and input using currently available devices only.
- Add Autostart section.
- Add About section or embedded About panel.
- Add Save / Cancel buttons and unsaved-changes close prompt.

6. Rework hotkey management.
- Expand runtime registration from one switch hotkey to:
  - output cycle hotkey
  - input cycle hotkey
  - optional per-device direct hotkeys
  - allowed shared output+input paired hotkeys
- Validate conflicts in the editor before save.
- On save, attempt registration before committing settings.
- If registration fails, keep the editor open and report the exact conflicting hotkey.
- On successful save, unregister old hotkeys and register the new set immediately.

7. Rework autostart behavior.
- Keep packaged builds using `StartupTask`.
- Reintroduce unpackaged autostart using Startup-folder launcher behavior, likely based on `start-daemon.vbs` or a generated shortcut/launcher.
- Present one unified Autostart toggle in Settings.
- Reflect the active mode depending on whether package identity exists.

8. Update runtime save/apply flow.
- Saving should apply immediately to the running tray app without restart.
- Refresh cycle behavior, direct-select behavior, autostart state, and tray labels/tooltips as needed.
- Ensure tray continues running even while the settings window is open.

9. Update tests.
- Extend deterministic tests for the new JSON settings model.
- Add pure-logic tests for ordered cycling behavior, skip-missing behavior, paired-hotkey validation rules, and hotkey conflict rules where feasible.
- Do not attempt hardware/UI end-to-end automation in CI.

10. Update docs after implementation.
- `README.md`
- `AGENTS.md`
- any help text/CLI references affected by the new settings/autostart behavior

## Risks
- Input-device switching may require API behavior that differs from current playback-only assumptions.
- Ordered lists plus paired hotkeys introduce more runtime state; save-time validation must remain deterministic.
- Drag reorder and alias editing increase UI complexity relative to the current tray-only approach.
- Unpackaged autostart must be restored carefully so it does not regress the packaged path.
- Since there is no migration, old local configs become irrelevant once the new format ships.

## Validation Plan
- Settings file saves and reloads correctly as JSON.
- Output cycle works with ordered enabled output entries.
- Input cycle works with ordered enabled input entries.
- Missing configured devices are skipped without breaking cycle behavior.
- Direct per-device hotkeys work.
- Shared output+input hotkeys switch both in sequence and partially succeed when one side is unavailable.
- Save blocks invalid duplicate hotkeys and unregistrable hotkeys.
- Save applies changes immediately without app restart.
- Tray menu is reduced to the four intended items.
- Double-click still cycles output.
- Autostart works for both packaged and unpackaged distribution modes.
- CI remains green after updating tests to the new settings logic.

## Out Of Scope
- Backward migration from the old flat config
- Microsoft Store packaging/submission changes beyond preserving the packaged autostart path
- Full end-to-end UI/audio automation in GitHub Actions
