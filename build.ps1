# Builds a single self-contained soundflip.exe into .\dist
# Run from Windows PowerShell in this folder:  .\build.ps1
$ErrorActionPreference = "Stop"
dotnet publish .\soundflip.csproj -c Release -o .\dist
if ($LASTEXITCODE -ne 0) { Write-Error "build failed (see errors above)"; exit 1 }
Write-Host ""
Write-Host "Done. Files are in .\dist :"
Write-Host "  soundflip.exe        tray-first app and CLI (windowless; no console flash)"
Write-Host ""
Write-Host "Settings are stored per-user at %LocalAppData%\SoundFlip\soundflip.json"
Write-Host "Run '.\dist\soundflip.exe export-assets .\Store\Assets' to generate default Store logos"
Write-Host "Run '.\package-msix.ps1' to build an unsigned MSIX for Microsoft Store submission"
