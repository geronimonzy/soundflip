# SoundFlip privacy policy

**SoundFlip does not collect, store, or transmit any personal information.**

- **No network access.** The app contains no networking code at all — no
  telemetry, no analytics, no crash reporting, no update checks. It never
  sends or receives anything over the network.
- **Everything it stores stays on your machine:**
  - `%LocalAppData%\SoundFlip\soundflip.json` — the audio device names you
    ticked for the cycle rings and your two hotkey combinations.
  - If you enable **Start with Windows** in an unpackaged build, one per-user
    registry value (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
    holding the path to the exe. The Microsoft Store version uses the Windows
    startup-task system instead and writes no registry value.
- The **Homepage** and **Support** buttons in the About dialog simply ask
  Windows to open those pages in your browser; the app itself transmits
  nothing.
- The Microsoft Store version behaves identically to the GitHub version in
  all of the above.

Uninstalling the app and deleting the settings file removes everything.

Questions: [github.com/geronimonzy/soundflip/issues](https://github.com/geronimonzy/soundflip/issues)

_Last updated: 2026-07-02_
