# Builds an UNSIGNED MSIX package for Microsoft Store submission into .\dist-msix.
# The Store signs uploaded packages itself, so this package cannot be sideloaded
# as-is — it exists to be uploaded in Partner Center (or via `msstore publish`).
#
# The identity defaults below are dev placeholders. For a submittable package,
# pass the real values from Partner Center (Product management > Product identity):
#   .\package-msix.ps1 -PackageName "12345Publisher.SoundFlip" `
#                      -Publisher "CN=xxxxxxxx-xxxx-..." `
#                      -PublisherDisplayName "Publisher Name"
param(
    [string]$PackageName = "KirillFedorov.SoundFlip",
    [string]$Publisher = "CN=00000000-0000-0000-0000-000000000000",
    [string]$PublisherDisplayName = "Kirill Fedorov"
)

$ErrorActionPreference = "Stop"

# 4-part MSIX version from the csproj <Version> (the Store requires it to
# strictly increase between submissions).
[xml]$proj = Get-Content .\soundflip.csproj
$version = ($proj.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { throw "Could not read <Version> from soundflip.csproj" }
$msixVersion = "$version.0"

# Store packages must NOT be single-file: MSIX updates are differential per
# 64 KB file block, so keeping the .NET runtime as separate (unchanging) files
# means an app update downloads only the few files that actually changed.
$layout = ".\obj\msix-layout"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
dotnet publish .\soundflip.csproj -c Release -o $layout `
    -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=none
if ($LASTEXITCODE -ne 0) { Write-Error "publish failed (see errors above)"; exit 1 }

# Store logos (WinExe: a bare call would not wait or surface the exit code).
$p = Start-Process -FilePath "$layout\soundflip.exe" -ArgumentList "export-assets `"$layout\Assets`"" `
    -NoNewWindow -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "soundflip export-assets exited with code $($p.ExitCode)" }

# Manifest from the template with identity + version filled in.
$manifest = Get-Content .\Store\Package.appxmanifest.template -Raw
$manifest = $manifest -replace '__PACKAGE_NAME__', $PackageName `
                      -replace '__PUBLISHER__', $Publisher `
                      -replace '__PUBLISHER_DISPLAY_NAME__', $PublisherDisplayName `
                      -replace '__VERSION__', $msixVersion
Set-Content "$layout\AppxManifest.xml" $manifest -Encoding utf8

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe not found — install the Windows 10/11 SDK" }

New-Item -ItemType Directory -Force -Path .\dist-msix | Out-Null
$out = ".\dist-msix\soundflip-$version-x64.msix"
& $makeappx.FullName pack /d $layout /p $out /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with code $LASTEXITCODE" }

Write-Host ""
Write-Host "Wrote $out ($msixVersion, identity $PackageName)"
Write-Host "Unsigned: upload it to Partner Center / msstore publish — the Store signs it."
