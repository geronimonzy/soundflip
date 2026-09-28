# SoundFlip test VM

Hyper-V VM (`SoundFlipTest`, Windows 11 Pro, offline, local admin `tester`/`tester`,
auto-logon, 3840x2160) for checking tray/UI behavior, DPI scaling and first-run
experience without touching the dev machine.

## One-time setup

1. Download the Windows 11 x64 ISO (English (United States)) to `E:\VMs\iso\`.
2. Build `unattend.iso` from `unattend/` (`xorriso -as mkisofs -V UNATTEND -J -r -o unattend.iso unattend/`)
   and copy this folder to `E:\VMs\tools`.
3. Elevated: `powershell -ExecutionPolicy Bypass -File E:\VMs\tools\Bootstrap-Elevated.ps1 -WindowsIso <iso>`
   (adds you to *Hyper-V Administrators*, creates the VM, installs unattended). Sign out/in afterwards.

## Snapshots

| Name | State |
|---|---|
| `clean-100` | fresh desktop, 100% scale, SoundFlip never run |
| `clean-250` | same at 250% (`LogPixels=240`) |
| `audio-100` | + VB-Audio Virtual Cable (outputs "CABLE Input", "CABLE In 16ch"; input "CABLE Output") |
| `audio-250` | same at 250% |

## Driving it (from the host, no admin)

```powershell
Import-Module E:\VMs\tools\TestVM.psm1
Restore-TestVM clean-250; Wait-TestVMDesktop
Copy-ToTestVM E:\VMs\build\dist\soundflip.exe C:\sf
Start-TestVMUserProcess C:\sf\soundflip.exe          # runs on tester's desktop
Save-TestVMScreenshot E:\VMs\shot.png                # host-side framebuffer grab
Send-TestVMClick -X 3454 -Y 2100                     # physical guest pixels
Send-TestVMClick -X 3407 -Y 1947 -Button Right
Send-TestVMKey -KeyCode 0x1B                         # Esc
Invoke-TestVM { Get-Process soundflip }              # PowerShell Direct
Send-TestVMHotkey -Keys 0x11,0x12,0x4F               # Ctrl+Alt+O
Start-TestVMCapture -Seconds 4 -IntervalMs 150       # in-guest burst capture (for toasts)
Receive-TestVMCapture -Destination E:\VMs\ev\run1
```

Guest scripts (`guest/`, copy to `C:\sf` with `Copy-ToTestVM`):
- `audiostate.ps1`: active endpoints + who holds Default / Communications roles.
- `endpoint.ps1 -Name 16ch -Enabled $false`: disable/enable an endpoint like the Sound
  control panel does (simulates unplugging).
- `capture.ps1`: used by `Start-TestVMCapture`.

Gotchas:
- PowerShell Direct fails intermittently for a while after boot/restore/checkpoint;
  the helpers retry.
- Never hard-reset (`Restart-VM -Force`) after a registry change: it isn't flushed.
  `Set-TestVMScale` restarts gracefully from inside the guest.
- Wait ~3-5 s after opening a window before screenshotting (open animation).
- Host screenshots take ~11 s each at 3840x2160; use the in-guest capture for anything
  short-lived.
- Tray icon positions shift when Windows adds icons (volume, OneDrive): re-screenshot
  the tray before clicking. The overflow flyout toggles, so a stray click closes it.
- The VM's console host is set to classic conhost (`HKCU\Console\%%Startup`); with
  Windows Terminal as default, hidden PowerShell launches stalled on a cold start.
- `Disable-PnpDevice` on an AudioEndpoint does NOT make it inactive to Core Audio; use
  `endpoint.ps1`.
