Import-Module E:\VMs\tools\TestVM.psm1 -Force
Restore-TestVM -Name audio-100
Wait-TestVMDesktop
foreach ($f in 'E:\VMs\build\dist\soundflip.exe','E:\VMs\tools\guest\audiostate.ps1','E:\VMs\tools\guest\capture.ps1','E:\VMs\tools\guest\store-look.ps1','E:\VMs\tools\guest\rename-endpoints.ps1','E:\VMs\tools\guest\rename-map.json') { Copy-ToTestVM -Path $f -Destination C:\sf }
Invoke-TestVM {
    New-Item -Force "HKCU:\Console\%%Startup" | Out-Null
    Set-ItemProperty "HKCU:\Console\%%Startup" -Name DelegationConsole -Value "{B23D10C0-E52E-411E-9D5B-C09FDF709C7D}"
    Set-ItemProperty "HKCU:\Console\%%Startup" -Name DelegationTerminal -Value "{B23D10C0-E52E-411E-9D5B-C09FDF709C7D}"
    & C:\sf\rename-endpoints.ps1 2>&1 | % { "$_" }
    Start-Sleep 5
    "--- soundflip list:"; & C:\sf\soundflip.exe list | Out-String; & C:\sf\soundflip.exe list inputs | Out-String
}
Start-TestVMUserProcess -FilePath powershell.exe -Arguments '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\sf\store-look.ps1'
$deadline = (Get-Date).AddMinutes(4)
while (-not (Invoke-TestVM { Test-Path C:\sf\look.done }) -and (Get-Date) -lt $deadline) { Start-Sleep 10 }
Start-Sleep 10
Invoke-TestVM { "look applied: " + (Test-Path C:\sf\look.done) }
Save-TestVMScreenshot -Path E:\VMs\s.png | Out-Null
