# Plays the prologue with the bot, from the intro to the Guild badge, against a fresh save that is
# thrown away (export/previews/prologue-save), so the real save is never touched. Prints the bot's
# stages; the run passed when "BOT done" is among them. Frames go to export/previews/prologue.
# -Flags and -Spawn start partway: story flags to set and where to stand, as PIXEL_FLAGS and PIXEL_SPAWN.
# -Steps also prints every scene step, to see where a stuck run stopped.
# -Contrary plays it again as a contrary player, once for each way (PIXEL_CONTRARY): runs from the
# finders, goes to the barred Guild first, leaves out Bram, leaves out the road and Pip. Each run must
# reach "BOT done"; the script prints a summary and exits 1 when one did not.
# -Ways picks which contrary runs to play (default all four); the obedient run is skipped then.
param(
    [int]$Frames = 60,
    [int]$Every = 300,
    [string]$Flags = "",
    [string]$Spawn = "",
    [switch]$Steps,
    [switch]$Contrary,
    [string]$Ways = "flee,guild_first,skip_bram,skip_pip"
)

$root = Split-Path $PSScriptRoot -Parent
$pattern = if ($Steps) { "BOT|STATS|ERROR|Exception" } else { "BOT (?!at |scene )|STATS|ERROR|Exception" }

function Invoke-Prologue([string]$way) {
    Get-Process Server, Godot* -ErrorAction SilentlyContinue | Stop-Process -Force
    $name = if ($way) { "prologue-$way" } else { "prologue" }
    $out = Join-Path $root "export\previews\$name"
    New-Item -ItemType Directory -Force $out | Out-Null
    Remove-Item "$out\*.png" -ErrorAction SilentlyContinue
    $save = Join-Path $root "export\previews\$name-save"
    Remove-Item $save -Recurse -Force -ErrorAction SilentlyContinue

    $env:PIXEL_SAVE = $save
    $env:PIXEL_SLOT = "prologue"
    $env:PIXEL_BOT = "prologue"
    $env:PIXEL_CONTRARY = $way
    $env:PIXEL_FRAIL = "1"
    $env:PIXEL_TEST = $null
    $env:PIXEL_FLAGS = $Flags
    $env:PIXEL_SPAWN = $Spawn
    # A run that starts partway skips the creator, which the intro would have opened.
    if ($Flags) { $env:PIXEL_LOOK = "default" } else { $env:PIXEL_LOOK = $null }
    $env:PIXEL_CAPTURE_EVERY = "$Every"

    if ($way) { Write-Host "=== contrary: $way ===" }
    $lines = powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "play.ps1") -Capture $out -CaptureFrames $Frames |
        Select-String -Pattern $pattern
    $lines | ForEach-Object { Write-Host $_.Line }

    foreach ($var in "PIXEL_SAVE", "PIXEL_SLOT", "PIXEL_BOT", "PIXEL_CONTRARY", "PIXEL_FRAIL", "PIXEL_FLAGS", "PIXEL_SPAWN", "PIXEL_LOOK", "PIXEL_CAPTURE_EVERY") {
        Remove-Item "Env:$var" -ErrorAction SilentlyContinue
    }

    $done = [bool]($lines | Where-Object { $_.Line -match "BOT done" })
    $errors = [bool]($lines | Where-Object { $_.Line -cmatch "ERROR|Exception" })
    return [pscustomobject]@{ Way = $(if ($way) { $way } else { "obedient" }); Done = $done; Errors = $errors }
}

$results = @()
if (-not $Contrary) {
    $results += Invoke-Prologue ""
} else {
    foreach ($way in $Ways.Split(",")) {
        $results += Invoke-Prologue $way.Trim()
    }
}

Write-Output "=== summary ==="
$failed = $false
foreach ($r in $results) {
    $verdict = if ($r.Done -and -not $r.Errors) { "passed" } else { $failed = $true; "FAILED" }
    Write-Output ("{0,-12} {1} (done {2}, errors {3})" -f $r.Way, $verdict, $r.Done, $r.Errors)
}

if ($failed) { exit 1 }
