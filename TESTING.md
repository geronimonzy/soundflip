# Manual testing checklist (Windows)

The unit tests (`dotnet test`) only cover deterministic logic — settings JSON +
legacy migration, CLI parsing, ring math, hotkey grammar. The actual device
switching and "do call apps follow the change?" behavior **must be checked by hand
on Windows**, because it depends on the undocumented `IPolicyConfig` COM path and on
how each app resolves its audio device.

## Build & launch

```powershell
.\build.ps1
.\dist\soundflip.exe          # tray app
```

## A. Core switching

- [ ] `soundflip list` shows playback devices, `*` on the current default.
- [ ] `soundflip list inputs` shows capture devices, `*` on the current default.
- [ ] `soundflip set output <name>` changes the default playback device.
- [ ] `soundflip set input <name>` changes the default capture device.
- [ ] In the tray menu, **Output ▸** / **Input ▸** list active devices with ● on the
      current default.

## B. Rings (configure in tray ▸ Output/Input checklists)

- [ ] Tick 3 outputs in **Output ▸** (menu stays open across clicks); **Cycle output**
      (and its hotkey) advances through all three and wraps around.
- [ ] Tick 2 inputs in **Input ▸**; **Cycle input** cycles them.
- [ ] Unticking a device drops it from the ring on the next cycle; ticks survive a
      restart (saved immediately).
- [ ] **Hotkeys…** edits both cycle hotkeys in one window; saved combos work right away,
      Cancel discards. Win11 dialog look: rounded themed buttons, footer band with
      accent Save, no clipped controls.
- [ ] Hotkeys/About/Set-hotkey title bars follow the Windows dark/light theme, in both
      themes.
- [ ] The Hotkeys window is NOT always-on-top: another app window can cover it.
- [ ] Picking **Hotkeys…** while the window is already open focuses it — no duplicate
      window appears.
- [ ] **About** uses the same dialog style (content area + footer band, accent Close).
- [ ] Assigning a hotkey already taken by another app shows the "in use" warning and
      the other hotkeys still work.

## C. The differentiator — do call/voice apps follow? (both speaker AND mic)

For each app: start a call / join a voice channel, then trigger a switch (hotkey or
tray) and confirm audio output **and** microphone move to the new device — ideally
**mid-call**, not just before joining. We set both the *default* and *default
communications* roles, which is what should make these follow.

- [ ] **Microsoft Teams** — output + mic, including during an active call.
- [ ] **Discord** — output + mic (Discord may pin "Default" vs a named device; note which).
- [ ] **Zoom** — output + mic during a meeting.
- [ ] **Steam voice chat** — output + mic in a voice session.
- [ ] **WhatsApp Desktop call** — output + mic during a call.

Record per app: does it follow on output? on mic? mid-call or only on next call?
Known industry caveat: some apps cache the device for the lifetime of an active call
and only pick up the change on the next call. Capture the real behavior here so we
can set expectations in the Store listing.

## D. Persistence & migration

- [ ] Settings survive an app restart (`%LocalAppData%\SoundFlip\soundflip.json`).
- [ ] An existing `%LocalAppData%\audsw\audsw.json` (pre-rename install) is imported
      on first launch and `soundflip.json` is written.
- [ ] An existing `%LocalAppData%\audsw\audsw.cfg` (oldest format) is imported
      on first launch (device1/device2 → output ring, hotkey → cycle-output hotkey).
- [ ] **Start with Windows** toggles in packaged Store/MSIX builds (StartupTask) AND
      in the unpackaged exe (HKCU Run key appears/disappears; app starts on sign-in).
- [ ] A pre-rename `audsw` HKCU Run value is replaced by a `SoundFlip` value pointing
      at the current exe on next status check.
- [ ] Double-clicking `soundflip.exe` opens no console window (not even a flash);
      `soundflip list` from a terminal still prints (note: prompt may return first).
- [ ] **About** shows publisher, copyright, homepage/support links (no "metadata
      still needs to be configured" warning); Homepage/Support open the GitHub pages.
- [ ] Hotkeys window spacing is symmetric (equal air above the first row and below
      the second), nothing cramped.
