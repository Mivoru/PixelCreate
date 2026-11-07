# Renders check pictures without a server and joins them into labelled contact sheets.
# Jobs: people, motion, models, world, hitboxes, map. Pictures land in export/previews/studio.
param(
    [string]$Jobs = "people,animals,motion,models,world,hitboxes,map"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$folder = "GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64"
$godot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\$folder\Godot_v4.7.2-stable_mono_win64_console.exe"
if ($env:GODOT) { $godot = $env:GODOT }
$out = Join-Path $root "export\previews\studio"

dotnet build (Join-Path $root "client") -v q
if ($LASTEXITCODE -ne 0) { throw "The client did not build." }
& $godot --path (Join-Path $root "client") res://scenes/studio.tscn -- ($out -replace "\\", "/") $Jobs
python (Join-Path $root "tools\sheet.py") $out
