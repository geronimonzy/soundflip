# Helpers for driving the SoundFlip test VM from the host (PowerShell Direct,
# no guest networking). Import with:  Import-Module .\tools\testvm\TestVM.psm1
# Requires "Hyper-V Administrators" membership (or elevation) on the host.

$script:VmName = 'SoundFlipTest'
$script:GuestDir = 'C:\sf'

function Get-TestVMCredential {
    # Throwaway local account created by autounattend.xml; the VM is offline.
    $secure = ConvertTo-SecureString 'tester' -AsPlainText -Force
    [pscredential]::new('tester', $secure)
}

# PowerShell Direct is flaky for a while after a boot, restore or checkpoint
# ("credential is invalid" / "session state broken"), so every session open
# retries.
function New-TestVMSession {
    param([int] $Attempts = 8)
    for ($i = 1; ; $i++) {
        try { return New-PSSession -VMName $script:VmName -Credential (Get-TestVMCredential) -ErrorAction Stop }
        catch { if ($i -ge $Attempts) { throw }; Start-Sleep -Seconds 10 }
    }
}

function Invoke-TestVM {
    param([Parameter(Mandatory)] [scriptblock] $ScriptBlock, [object[]] $ArgumentList = @())
    $session = New-TestVMSession
    try { Invoke-Command -Session $session -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList }
    finally { Remove-PSSession $session }
}

function Wait-TestVMReady {
    # Waits until the unattended install has finished its first logon.
    param([int] $TimeoutMinutes = 60)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        try {
            $s = New-PSSession -VMName $script:VmName -Credential (Get-TestVMCredential) -ErrorAction Stop
            try { if (Invoke-Command -Session $s { Test-Path 'C:\sf\setup-done.txt' }) { return $true } }
            finally { Remove-PSSession $s }
        } catch { }
        Start-Sleep -Seconds 20
    }
    throw "Test VM not ready after $TimeoutMinutes min."
}

function Copy-ToTestVM {
    param([Parameter(Mandatory)] [string] $Path, [string] $Destination = $script:GuestDir)
    $session = New-TestVMSession
    try {
        Invoke-Command -Session $session { param($d) New-Item -ItemType Directory -Force -Path $d | Out-Null } -ArgumentList $Destination
        Copy-Item -ToSession $session -Path $Path -Destination $Destination -Recurse -Force
    } finally { Remove-PSSession $session }
}

function Copy-FromTestVM {
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [string] $Destination)
    $session = New-TestVMSession
    try { Copy-Item -FromSession $session -Path $Path -Destination $Destination -Recurse -Force }
    finally { Remove-PSSession $session }
}

function Start-TestVMUserProcess {
    # PowerShell Direct runs outside the desktop. Anything with UI (the tray app,
    # dialogs) must start in tester's interactive session: do it via a one-shot
    # scheduled task with an interactive logon type.
    param([Parameter(Mandatory)] [string] $FilePath, [string] $Arguments = '')
    Invoke-TestVM {
        param($file, $arguments)
        $name = 'sf-run-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
        $action = if ($arguments) { New-ScheduledTaskAction -Execute $file -Argument $arguments }
                  else { New-ScheduledTaskAction -Execute $file }
        $principal = New-ScheduledTaskPrincipal -UserId 'tester' -LogonType Interactive -RunLevel Limited
        Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Force | Out-Null
        Start-ScheduledTask -TaskName $name
        Start-Sleep -Seconds 1
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
    } -ArgumentList $FilePath, $Arguments
}

function Save-TestVMScreenshot {
    # Grabs the VM's console framebuffer from the host (no guest cooperation
    # needed) and writes a PNG. Hyper-V hands back raw RGB565.
    param([Parameter(Mandatory)] [string] $Path)
    $video = Get-VMVideo -VMName $script:VmName
    [uint16] $w = $video.HorizontalResolution
    [uint16] $h = $video.VerticalResolution

    $vm = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$script:VmName'"
    $vssd = Get-CimAssociatedInstance -InputObject $vm -ResultClassName Msvm_VirtualSystemSettingData |
        Where-Object VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized'
    $svc = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_VirtualSystemManagementService
    $result = Invoke-CimMethod -InputObject $svc -MethodName GetVirtualSystemThumbnailImage -Arguments @{
        TargetSystem = $vssd; WidthPixels = $w; HeightPixels = $h
    }
    if ($result.ReturnValue -ne 0 -or -not $result.ImageData) { throw "Thumbnail failed: $($result.ReturnValue)" }

    Add-Type -AssemblyName System.Drawing
    $bmp = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format16bppRgb565)
    try {
        $rect = [System.Drawing.Rectangle]::new(0, 0, $w, $h)
        $data = $bmp.LockBits($rect, 'WriteOnly', $bmp.PixelFormat)
        try {
            # Rows are tightly packed (w*2 bytes); copy row by row in case the
            # bitmap stride is padded.
            for ($y = 0; $y -lt $h; $y++) {
                [Runtime.InteropServices.Marshal]::Copy([byte[]]$result.ImageData, $y * $w * 2,
                    [IntPtr]::Add($data.Scan0, $y * $data.Stride), $w * 2)
            }
        } finally { $bmp.UnlockBits($data) }
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }
    Get-Item $Path
}

function Get-TestVMDevice([string] $Class) {
    $vm = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$script:VmName'"
    Get-CimInstance -Namespace root\virtualization\v2 -ClassName $Class | Where-Object SystemName -eq $vm.Name
}

function Send-TestVMClick {
    # Drives the VM's synthetic mouse from the host. X/Y are physical guest
    # pixels (same space as Save-TestVMScreenshot). Button: Left or Right.
    param([Parameter(Mandatory)] [int] $X, [Parameter(Mandatory)] [int] $Y,
          [ValidateSet('Left', 'Right')] [string] $Button = 'Left', [switch] $Double, [switch] $MoveOnly)
    $mouse = Get-TestVMDevice Msvm_SyntheticMouse
    # Approach from a few pixels away: a click delivered as the very first event at
    # a position (e.g. right after a restore) is sometimes ignored by the shell.
    foreach ($p in @(@(($X - 20), ($Y - 20)), @($X, $Y))) {
        Invoke-CimMethod -InputObject $mouse -MethodName SetAbsolutePosition -Arguments @{
            horizontalPosition = [uint32][Math]::Max(0, $p[0]); verticalPosition = [uint32][Math]::Max(0, $p[1]) } | Out-Null
        Start-Sleep -Milliseconds 120
    }
    if ($MoveOnly) { return }
    Start-Sleep -Milliseconds 150
    $index = [uint32]$(if ($Button -eq 'Left') { 1 } else { 2 })
    # Explicit down/up with a short hold: the shell's tray handling misses the
    # instantaneous ClickButton often enough to be flaky.
    foreach ($n in 1..$(if ($Double) { 2 } else { 1 })) {
        Invoke-CimMethod -InputObject $mouse -MethodName SetButtonState -Arguments @{ ButtonIndex = $index; IsDown = $true } | Out-Null
        Start-Sleep -Milliseconds 80
        Invoke-CimMethod -InputObject $mouse -MethodName SetButtonState -Arguments @{ ButtonIndex = $index; IsDown = $false } | Out-Null
        if ($Double) { Start-Sleep -Milliseconds 60 }
    }
}

function Send-TestVMKey {
    # Virtual-key code (e.g. 0x1B Esc, 0x0D Enter) or literal text.
    param([int] $KeyCode, [string] $Text)
    $kbd = Get-TestVMDevice Msvm_Keyboard
    if ($Text) { Invoke-CimMethod -InputObject $kbd -MethodName TypeText -Arguments @{ asciiText = $Text } | Out-Null }
    else { Invoke-CimMethod -InputObject $kbd -MethodName TypeKey -Arguments @{ keyCode = [uint32]$KeyCode } | Out-Null }
}

function Send-TestVMHotkey {
    # Chord of virtual-key codes, e.g. 0x11,0x12,0x4F for Ctrl+Alt+O.
    param([Parameter(Mandatory)] [int[]] $Keys)
    $kbd = Get-TestVMDevice Msvm_Keyboard
    foreach ($k in $Keys) { Invoke-CimMethod -InputObject $kbd -MethodName PressKey -Arguments @{ keyCode = [uint32]$k } | Out-Null }
    [array]::Reverse($Keys)
    foreach ($k in $Keys) { Invoke-CimMethod -InputObject $kbd -MethodName ReleaseKey -Arguments @{ keyCode = [uint32]$k } | Out-Null }
}

function Start-TestVMCapture {
    # Starts an in-guest burst capture (guest\capture.ps1, copied to C:\sf) and
    # returns once it is running; trigger the UI right after.
    param([double] $Seconds = 4, [int] $IntervalMs = 200)
    Invoke-TestVM { Remove-Item C:\sf\cap -Recurse -Force -ErrorAction SilentlyContinue }
    Start-TestVMUserProcess -FilePath 'powershell.exe' -Arguments (
        "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\sf\capture.ps1 -Seconds $Seconds -IntervalMs $IntervalMs")
    # PowerShell + Add-Type startup takes seconds; wait for the first frame.
    $deadline = (Get-Date).AddSeconds(90)
    while (-not (Invoke-TestVM { Test-Path C:\sf\cap\started.txt })) {
        if ((Get-Date) -gt $deadline) { throw 'Capture did not start.' }
        Start-Sleep -Milliseconds 300
    }
}

function Receive-TestVMCapture {
    # Waits for the capture to finish and copies the frames to the host.
    param([Parameter(Mandatory)] [string] $Destination, [int] $TimeoutSeconds = 120)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not (Invoke-TestVM { Test-Path C:\sf\cap\done.txt })) {
        if ((Get-Date) -gt $deadline) { throw 'Capture did not finish.' }
        Start-Sleep -Seconds 3
    }
    Remove-Item $Destination -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-FromTestVM -Path 'C:\sf\cap\*.png' -Destination $Destination
    Get-ChildItem $Destination -Filter *.png
}

function Set-TestVMScale {
    # Display scaling for tester, applied via the classic "custom scaling" value
    # (LogPixels = 96 * percent / 100). Takes effect after the restart this does.
    param([Parameter(Mandatory)] [ValidateSet(100, 125, 150, 175, 200, 225, 250, 300)] [int] $Percent)
    $dpi = [int](96 * $Percent / 100)
    Invoke-TestVM {
        param($dpi)
        Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name LogPixels -Value $dpi -Type DWord
        Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Win8DpiScaling -Value 1 -Type DWord
    } -ArgumentList $dpi
    # Graceful in-guest restart: Restart-VM -Force is a hard reset and loses the
    # not-yet-flushed registry write.
    Invoke-TestVM { Restart-Computer -Force }
    Start-Sleep -Seconds 30
    Wait-TestVMDesktop
}

function Wait-TestVMDesktop {
    # After a boot/restore: wait until tester is auto-logged on and Explorer is up
    # (PS Direct refuses credentials until then), plus a settle delay.
    param([int] $TimeoutMinutes = 15)
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        try {
            $s = New-PSSession -VMName $script:VmName -Credential (Get-TestVMCredential) -ErrorAction Stop
            try { $up = Invoke-Command -Session $s { [bool](Get-Process explorer -ErrorAction SilentlyContinue) } }
            finally { Remove-PSSession $s }
            if ($up) { Start-Sleep -Seconds 15; return }
        } catch { }
        Start-Sleep -Seconds 10
    }
    throw "Test VM desktop not up after $TimeoutMinutes min."
}

function Checkpoint-TestVM { param([Parameter(Mandatory)] [string] $Name) Checkpoint-VM -Name $script:VmName -SnapshotName $Name }

function Restore-TestVM {
    param([Parameter(Mandatory)] [string] $Name)
    Restore-VMSnapshot -VMName $script:VmName -Name $Name -Confirm:$false
    if ((Get-VM $script:VmName).State -ne 'Running') { Start-VM $script:VmName }
}

Export-ModuleMember -Function *TestVM*
