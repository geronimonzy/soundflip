# Builds a single self-contained audsw.exe into .\dist
# Run from Windows PowerShell in this folder:  .\build.ps1
$ErrorActionPreference = "Stop"
dotnet publish .\audsw.csproj -c Release -o .\dist
if ($LASTEXITCODE -ne 0) { Write-Error "build failed (see errors above)"; exit 1 }
Write-Host ""
Write-Host "Done. Files are in .\dist :"
Write-Host "  audsw.exe        tray-first app and CLI"
Write-Host "  start-daemon.vbs windowless unpackaged launcher"
Write-Host ""
Write-Host "Settings are stored per-user at %LocalAppData%\audsw\audsw.cfg"
Write-Host "Run '.\dist\audsw.exe export-assets .\Store\Assets' to generate default Store logos"
Copy-Item .\start-daemon.vbs .\dist\ -Force
