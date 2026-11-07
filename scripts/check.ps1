# The full check after a change: tests, the Guild story played live, then all studio pictures.
# Read export/previews/live/sheets and export/previews/studio/sheets afterwards.
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

dotnet test net-tests -v q --nologo | Select-String "error|Úspěšné:|Neúspěšné:|Passed!|Failed!"

$live = Join-Path $root "export\previews\live"
New-Item -ItemType Directory -Force $live | Out-Null
Remove-Item (Join-Path $live "*.png") -ErrorAction SilentlyContinue
$env:PIXEL_TEST = "1"
$env:PIXEL_SPAWN = "420,4"
$env:PIXEL_BOT = "guild"
powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\play.ps1") -Capture ($live -replace "\\", "/") -CaptureFrames 24 2>&1 |
    Select-String "^BOT|^STATS"
Remove-Item Env:PIXEL_TEST, Env:PIXEL_SPAWN, Env:PIXEL_BOT
python (Join-Path $root "tools\sheet.py") $live

powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\studio.ps1") 2>&1 | Select-String "sheets in"
