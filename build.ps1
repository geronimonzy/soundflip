# Publishes a self-contained, unpackaged WinUI 3 build of audsw into .\dist
# Run from Windows PowerShell in this folder:  .\build.ps1
$ErrorActionPreference = "Stop"

dotnet publish .\app\audsw.App.csproj -c Release -r win-x64 -p:Platform=x64 -o .\dist
if ($LASTEXITCODE -ne 0) { Write-Error "build failed (see errors above)"; exit 1 }

Write-Host ""
Write-Host "Done. Files are in .\dist :"
Write-Host "  audsw.exe   tray-first app and CLI"
Write-Host ""
Write-Host "Settings are stored per-user at %LocalAppData%\audsw\audsw.json"
