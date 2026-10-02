# Runs inside the test VM, in tester's interactive session (via
# Start-TestVMUserProcess). Burst-captures the screen so short-lived UI such as
# SoundFlip's 1.8 s toast can be inspected. Host-side thumbnails take ~11 s/frame.
param([double] $Seconds = 4, [int] $IntervalMs = 200, [string] $OutDir = 'C:\sf\cap')

Add-Type -AssemblyName System.Drawing
Add-Type -Namespace Native -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[Native.Dpi]::SetProcessDPIAware() | Out-Null   # physical pixels at any scale

Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Add-Type -AssemblyName System.Windows.Forms
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

$frames = New-Object System.Collections.Generic.List[System.Drawing.Bitmap]
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $Seconds) {
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # Plain SourceCopy: with DWM the screen DC already includes layered windows
    # (the toast); adding CaptureBlt produced all-black frames in the VM.
    $g.CopyFromScreen(0, 0, 0, 0, $bounds.Size)
    $g.Dispose()
    $frames.Add($bmp)
    if ($frames.Count -eq 1) { Set-Content (Join-Path $OutDir 'started.txt') 1 }
    Start-Sleep -Milliseconds $IntervalMs
}
for ($i = 0; $i -lt $frames.Count; $i++) {
    $frames[$i].Save((Join-Path $OutDir ('f{0:D3}.png' -f $i)), [System.Drawing.Imaging.ImageFormat]::Png)
    $frames[$i].Dispose()
}
Set-Content (Join-Path $OutDir 'done.txt') $frames.Count
