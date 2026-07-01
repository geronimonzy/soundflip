# Manual testing checklist (Windows)

The unit tests (`dotnet test`) only cover deterministic logic — settings JSON +
legacy migration, CLI parsing, ring math, hotkey grammar. The actual device
switching and "do call apps follow the change?" behavior **must be checked by hand
on Windows**, because it depends on the undocumented `IPolicyConfig` COM path and on
how each app resolves its audio device.

## Build & launch

```powershell
.\build.ps1
.\dist\audsw.exe          # tray app
```

## A. Core switching

- [ ] `audsw list` shows playback devices, `*` on the current default.
- [ ] `audsw list inputs` shows capture devices, `*` on the current default.
- [ ] `audsw set output <name>` changes the default playback device.
- [ ] `audsw set input <name>` changes the default capture device.
- [ ] In the tray menu, **Output ▸** / **Input ▸** list active devices with ● on the
      current default.

## B. Rings (configure in tray ▸ Output/Input checklists)

- [ ] Tick 3 outputs in **Output ▸** (menu stays open across clicks); **Cycle output**
      (and its hotkey) advances through all three and wraps around.
- [ ] Tick 2 inputs in **Input ▸**; **Cycle input** cycles them.
- [ ] Unticking a device drops it from the ring on the next cycle; ticks survive a
      restart (saved immediately).
- [ ] **Hotkeys…** edits both cycle hotkeys in one window; saved combos work right away,
      Cancel discards. Buttons follow the theme (no clipped controls, accent Save).
- [ ] Picking **Hotkeys…** while the window is already open focuses it — no duplicate
      window appears.
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

- [ ] Settings survive an app restart (`%LocalAppData%\audsw\audsw.json`).
- [ ] An existing `%LocalAppData%\audsw\audsw.cfg` from an older build is imported
      on first launch (device1/device2 → output ring, hotkey → cycle-output hotkey)
      and an `audsw.json` is written.
- [ ] **Start with Windows** toggles in packaged Store/MSIX builds (StartupTask) AND
      in the unpackaged exe (HKCU Run key appears/disappears; app starts on sign-in).
- [ ] Double-clicking `audsw.exe` opens no console window (not even a flash);
      `audsw list` from a terminal still prints (note: prompt may return first).
