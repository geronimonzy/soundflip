<#
.SYNOPSIS
  Creates the SoundFlip test VM (Hyper-V Gen2, Windows 11 Pro, unattended install).

.DESCRIPTION
  Needs membership in "Hyper-V Administrators" (or an elevated shell).
  The VM has NO network adapter on purpose: no Windows Update noise, no
  Microsoft-account prompts, reproducible state. Files go in/out through
  PowerShell Direct (see TestVM.psm1).

  Installation is fully unattended (autounattend.xml on a second DVD). The only
  interactive moment is Hyper-V's "Press any key to boot from CD", which this
  script answers by typing Enter into the VM's keyboard for a few seconds.

.EXAMPLE
  .\New-TestVM.ps1 -WindowsIso E:\VMs\iso\Win11_25H2_English_x64.iso
#>
param(
    [Parameter(Mandatory)] [string] $WindowsIso,
    [string] $Name = 'SoundFlipTest',
    [string] $Root = 'E:\VMs',
    [string] $UnattendIso = (Join-Path $PSScriptRoot 'unattend.iso'),
    [int] $CpuCount = 4,
    [long] $MemoryBytes = 6GB,
    [long] $DiskBytes = 80GB,
    # Guest resolution in the basic (vmconnect) session. 3840x2160 lets the guest
    # run at 250% scaling with a realistic 1536x864 logical desktop.
    [int] $Width = 3840,
    [int] $Height = 2160
)

$ErrorActionPreference = 'Stop'

if (Get-VM -Name $Name -ErrorAction SilentlyContinue) { throw "VM '$Name' already exists." }
foreach ($iso in $WindowsIso, $UnattendIso) { if (-not (Test-Path $iso)) { throw "Missing ISO: $iso" } }

$vmDir = Join-Path $Root $Name
$vhd = Join-Path $vmDir "$Name.vhdx"
New-Item -ItemType Directory -Force -Path $vmDir | Out-Null

New-VM -Name $Name -Generation 2 -Path $Root -MemoryStartupBytes $MemoryBytes `
    -NewVHDPath $vhd -NewVHDSizeBytes $DiskBytes | Out-Null

Set-VM -Name $Name -ProcessorCount $CpuCount -StaticMemory `
    -AutomaticCheckpointsEnabled $false -CheckpointType Standard `
    -AutomaticStartAction Nothing -AutomaticStopAction ShutDown

# Offline by design.
Get-VMNetworkAdapter -VMName $Name | Remove-VMNetworkAdapter

# Windows 11 wants Secure Boot + TPM 2.0.
Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
Set-VMKeyProtector -VMName $Name -NewLocalKeyProtector
Enable-VMTPM -VMName $Name

# Copy-VMFile needs Guest Services.
Enable-VMIntegrationService -VMName $Name -Name 'Guest Service Interface'

Set-VMVideo -VMName $Name -ResolutionType Single -HorizontalResolution $Width -VerticalResolution $Height

$winDvd = Add-VMDvdDrive -VMName $Name -Path $WindowsIso -Passthru
Add-VMDvdDrive -VMName $Name -Path $UnattendIso
Set-VMFirmware -VMName $Name -FirstBootDevice $winDvd

Start-VM -Name $Name

# Answer "Press any key to boot from CD or DVD..." (it waits ~5 s).
$kbd = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_Keyboard |
    Where-Object { $_.SystemName -eq (Get-VM -Name $Name).Id.ToString().ToUpper() }
for ($i = 0; $i -lt 15; $i++) {
    if ($kbd) { Invoke-CimMethod -InputObject $kbd -MethodName TypeKey -Arguments @{ keyCode = [uint32]0x0D } | Out-Null }
    Start-Sleep -Milliseconds 700
}

Write-Host "VM '$Name' is installing Windows (~15-25 min, reboots a few times)."
Write-Host "Watch it with:  vmconnect localhost $Name"
Write-Host "When C:\sf\setup-done.txt exists in the guest, run:  Initialize-TestVM (TestVM.psm1)"
