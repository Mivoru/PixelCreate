# Rebuilds every model with Blender and lets Godot import the new files.
$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$blender = "D:\Program\Blender Foundation\Blender 5.2\blender.exe"
$folder = "GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64"
$godot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\$folder\Godot_v4.7.2-stable_mono_win64_console.exe"
if ($env:GODOT) { $godot = $env:GODOT }

& $blender -b --factory-startup -P (Join-Path $root "asset-pipeline\build_meshes.py") 2>&1 | Select-String "Error|Traceback|error:" | Select-Object -First 10
& $godot --headless --path (Join-Path $root "client") --import 2>&1 | Select-String "ERROR" | Select-Object -First 10
