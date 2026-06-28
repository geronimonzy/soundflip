# Builds a single self-contained audsw.exe into .\dist
# Run from Windows PowerShell in this folder:  .\build.ps1
$ErrorActionPreference = "Stop"
dotnet publish .\audsw.csproj -c Release -o .\dist
if ($LASTEXITCODE -ne 0) { Write-Error "build failed (see errors above)"; exit 1 }
Write-Host ""
Write-Host "Done. Files are in .\dist :"
Write-Host "  audsw.exe        the tool"
Write-Host "  audsw.cfg        edit device1/device2 to your devices (run 'audsw list')"
Write-Host "  start-daemon.vbs windowless background launcher"
Copy-Item .\start-daemon.vbs .\dist\ -Force
