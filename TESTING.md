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
- [ ] In the tray menu, **Output ▸** / **Input ▸** pick a device and switch immediately.

## B. Rings & pairs (configure in tray ▸ Settings…)

- [ ] Add 3 outputs to the output ring; **Cycle output** (and its hotkey) advances
      through all three and wraps around.
- [ ] Add 2 inputs to the input ring; **Cycle input** cycles them.
- [ ] Create 2 pairs (e.g. "Desk" = speakers+webcam mic, "Headset" = headphones+headset mic);
      **Cycle pair** alternates and each pair's own hotkey jumps straight to it.
- [ ] Per-device hotkeys jump straight to that device.
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
- [ ] **Start with Windows** toggles in packaged Store/MSIX builds.
