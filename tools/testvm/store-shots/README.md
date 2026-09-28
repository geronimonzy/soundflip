# Store screenshots

Regenerates `screenshots/<en|de|es|fr|ru>/*.png` (tight crops, dark theme, 100%
scaling, black desktop) in the test VM.

1. `store-prep.ps1`: restores `audio-100`, renames the VB-Audio endpoints to
   "Speakers / Headset / Microphone (Realtek(R) Audio)" (`guest/rename-endpoints.ps1`,
   `guest/rename-map.json`), applies dark mode + black desktop (`guest/store-look.ps1`).
   Restart Explorer afterwards so the taskbar turns dark, then checkpoint `store-100`.
2. Pin the tray icon once (run SoundFlip, set `IsPromoted=1` on its
   `HKCU\Control Panel\NotifyIconSettings` entry) so it sits next to the clock.
3. `shots-lang.ps1 -Lang en` and `-Lang de -DropIndex 2` / `es 3` / `fr 4` / `ru 5`
   (index in the welcome window's language drop-down). Raw frames land in
   `E:\VMs\shots\<lang>\`.
4. `python3 crop_shots.py en de es fr ru` crops them into `screenshots/<lang>/`.

Coordinates in `shots-lang.ps1` are for that exact VM state (3840x2160 at 100%,
icon pinned between OneDrive and network). If the tray layout changes, re-measure
the icon position and the menu item offsets (menu top 1860, item pitch 22 px).
Right after a snapshot restore the first tray start can take minutes (see the
testvm README); the script waits for the welcome window.
