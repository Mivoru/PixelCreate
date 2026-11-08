# The check after a change: tests, the Guild story played live, then studio pictures of what changed.
# -Jobs picks the studio jobs (for example -Jobs monsters,world); without it no studio runs, and
# -Jobs all runs every job. Read export/previews/live/sheets and export/previews/studio/sheets afterwards.
param(
    [string]$Jobs = ""
)

$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

dotnet test net-tests -v q --nologo | Select-String "error|Úspěšné:|Neúspěšné:|Passed!|Failed!"

$live = Join-Path $root "export\previews\live"
New-Item -ItemType Directory -Force $live | Out-Null
Remove-Item (Join-Path $live "*.png") -ErrorAction SilentlyContinue
$env:PIXEL_TEST = "1"
$env:PIXEL_FRAIL = "1"
$env:PIXEL_SPAWN = "420,4"
$env:PIXEL_BOT = "guild"
$run = powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\play.ps1") -Capture ($live -replace "\\", "/") -CaptureFrames 24 2>&1 | ForEach-Object { "$_" }
$run | Select-String "^BOT|^STATS"
$errors = $run | Select-String "^ERROR: System\.|Exception:" | Group-Object | Select-Object -First 5
if ($errors) { Write-Host "SCRIPT ERRORS IN THE LIVE RUN:"; $errors | ForEach-Object { Write-Host "  $($_.Count)x $($_.Name)" } }
Remove-Item Env:PIXEL_TEST, Env:PIXEL_FRAIL, Env:PIXEL_SPAWN, Env:PIXEL_BOT
python (Join-Path $root "tools\sheet.py") $live

if ($Jobs -eq "all") {
    powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\studio.ps1") 2>&1 | Select-String "sheets in"
}
elseif ($Jobs) {
    powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\studio.ps1") -Jobs $Jobs 2>&1 | Select-String "sheets in"
}
