<#
  One-time, run from an ELEVATED PowerShell:
    powershell -ExecutionPolicy Bypass -File E:\VMs\tools\Bootstrap-Elevated.ps1 -WindowsIso <path-to-Win11.iso>

  1. Adds you to "Hyper-V Administrators" so later VM automation needs no UAC
     (takes effect after you sign out and back in).
  2. Creates the VM and starts the unattended Windows install.
#>
param([Parameter(Mandatory)] [string] $WindowsIso)
$ErrorActionPreference = 'Stop'

$me = "$env:USERDOMAIN\$env:USERNAME"
if (-not (Get-LocalGroupMember -Group 'Hyper-V Administrators' | Where-Object Name -eq $me)) {
    Add-LocalGroupMember -Group 'Hyper-V Administrators' -Member $me
    Write-Host "Added $me to Hyper-V Administrators (sign out/in to apply)."
}

& (Join-Path $PSScriptRoot 'New-TestVM.ps1') -WindowsIso $WindowsIso -UnattendIso (Join-Path $PSScriptRoot 'unattend.iso')
