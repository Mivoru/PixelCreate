# Plays the prologue with the bot, from the intro to the Guild badge, against a fresh save that is
# thrown away (export/previews/prologue-save), so the real save is never touched. Prints the bot's
# stages; the run passed when "BOT done" is among them. Frames go to export/previews/prologue.
# -Flags and -Spawn start partway: story flags to set and where to stand, as PIXEL_FLAGS and PIXEL_SPAWN.
# -Steps also prints every scene step, to see where a stuck run stopped.
param(
    [int]$Frames = 60,
    [int]$Every = 300,
    [string]$Flags = "",
    [string]$Spawn = "",
    [switch]$Steps
)

$root = Split-Path $PSScriptRoot -Parent
Get-Process Server, Godot* -ErrorAction SilentlyContinue | Stop-Process -Force
$out = Join-Path $root "export\previews\prologue"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*.png" -ErrorAction SilentlyContinue
$save = Join-Path $root "export\previews\prologue-save"
Remove-Item $save -Recurse -Force -ErrorAction SilentlyContinue

$env:PIXEL_SAVE = $save
$env:PIXEL_SLOT = "prologue"
$env:PIXEL_BOT = "prologue"
$env:PIXEL_FRAIL = "1"
$env:PIXEL_TEST = $null
$env:PIXEL_FLAGS = $Flags
$env:PIXEL_SPAWN = $Spawn
# A run that starts partway skips the creator, which the intro would have opened.
if ($Flags) { $env:PIXEL_LOOK = "default" } else { $env:PIXEL_LOOK = $null }
$env:PIXEL_CAPTURE_EVERY = "$Every"

$pattern = if ($Steps) { "BOT|STATS|ERROR|Exception" } else { "BOT (?!at |scene )|STATS|ERROR|Exception" }
powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "play.ps1") -Capture $out -CaptureFrames $Frames |
    Select-String -Pattern $pattern

foreach ($name in "PIXEL_SAVE", "PIXEL_SLOT", "PIXEL_BOT", "PIXEL_FRAIL", "PIXEL_FLAGS", "PIXEL_SPAWN", "PIXEL_LOOK", "PIXEL_CAPTURE_EVERY") {
    Remove-Item "Env:$name" -ErrorAction SilentlyContinue
}
