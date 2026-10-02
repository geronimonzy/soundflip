param([Parameter(Mandatory)] [string] $Lang, [int] $DropIndex = -1)
# Captures raw full-screen shots for one language into E:\VMs\shots\<lang>\.
# Coordinates are for the store-100 VM state (3840x2160 at 100%, icon pinned).
Import-Module E:\VMs\tools\TestVM.psm1 -Force
Add-Type -AssemblyName System.Drawing
$dir = "E:\VMs\shots\$Lang"; New-Item -ItemType Directory -Force $dir | Out-Null
function Shot($name) { Save-TestVMScreenshot -Path "$dir\$name.png" | Out-Null }
function PixelIsDesktop($x, $y) {
    Save-TestVMScreenshot -Path E:\VMs\shots\probe.png | Out-Null
    $b = [System.Drawing.Bitmap]::FromFile('E:\VMs\shots\probe.png'); $p = $b.GetPixel($x, $y); $b.Dispose()
    ($p.R + $p.G + $p.B) -lt 15
}
$menuX = 3600
function OpenMenu { Send-TestVMClick -X 3687 -Y 2136 -Button Right; Start-Sleep 2 }

# --- Welcome (first run) ----------------------------------------------------
Invoke-TestVM {
    Get-Process soundflip -EA SilentlyContinue | Stop-Process -Force
    Remove-Item "$env:LOCALAPPDATA\SoundFlip" -Recurse -Force -EA SilentlyContinue
}
Start-Sleep 2
Shot 'base'
Start-TestVMUserProcess -FilePath C:\sf\soundflip.exe
$t0 = Get-Date
while ((PixelIsDesktop 1680 1240) -and ((Get-Date) - $t0).TotalSeconds -lt 240) { Start-Sleep 3 }
Start-Sleep 2
if ($DropIndex -ge 0) {
    Send-TestVMClick -X 1863 -Y 1124; Start-Sleep 2
    Send-TestVMClick -X 1821 -Y (1154 + 22 * $DropIndex); Start-Sleep 3
}
Send-TestVMClick -X 1760 -Y 896; Start-Sleep 2        # activate via title bar
Shot 'welcome'
Send-TestVMKey -KeyCode 0x1B; Start-Sleep 1

# --- Tray shots with the store settings ------------------------------------
Invoke-TestVM {
    param($lang)
    Get-Process soundflip -EA SilentlyContinue | Stop-Process -Force
    $d = "$env:LOCALAPPDATA\SoundFlip"; New-Item -ItemType Directory -Force $d | Out-Null
    @{ outputs = @(@{ match = 'Speakers (Realtek(R) Audio)' }, @{ match = 'Headset (Realtek(R) Audio)' }); inputs = @(@{ match = 'Microphone (Realtek(R) Audio)' });
       cycleOutputs = 'ctrl+alt+o'; cycleInputs = 'ctrl+alt+i'; language = $lang } | ConvertTo-Json -Depth 4 | Set-Content "$d\soundflip.json"
    & C:\sf\soundflip.exe set output Speakers | Out-Null
} -ArgumentList $Lang
Start-TestVMUserProcess -FilePath C:\sf\soundflip.exe
Start-Sleep 10
Send-TestVMClick -X 1920 -Y 700 -MoveOnly; Start-Sleep 1
Shot 'base-tray'

OpenMenu; Shot 'menu'
Send-TestVMClick -X $menuX -Y (1860 + 113) -MoveOnly; Start-Sleep 2; Shot 'checklist'
Send-TestVMClick -X $menuX -Y (1860 + 185) -MoveOnly; Start-Sleep 2; Shot 'language'
Send-TestVMKey -KeyCode 0x1B; Send-TestVMKey -KeyCode 0x1B; Start-Sleep 1

OpenMenu; Send-TestVMClick -X $menuX -Y (1860 + 163); Start-Sleep 4; Shot 'hotkeys'
Send-TestVMKey -KeyCode 0x1B; Start-Sleep 1
OpenMenu; Send-TestVMClick -X $menuX -Y (1860 + 241); Start-Sleep 4; Shot 'about'
Send-TestVMKey -KeyCode 0x1B; Start-Sleep 1

# --- Toast (in-guest burst, it only lives ~2 s) ----------------------------
Send-TestVMClick -X 1920 -Y 700 -MoveOnly
Start-TestVMCapture -Seconds 4 -IntervalMs 200
Send-TestVMHotkey -Keys 0x11,0x12,0x4F
Receive-TestVMCapture -Destination "$dir\toast-frames" | Out-Null
Invoke-TestVM { & C:\sf\soundflip.exe set output Speakers | Out-Null }
"captured $Lang"
