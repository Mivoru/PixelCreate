# Starts a local server and the game client. Pass -ServerHost to join another server instead.
# Controls: WASD move, mouse look, Space jump, Shift dash, left button attack, Esc quit.
# With -Capture <folder> a bot plays, saves frames, prints a STATS line and quits.
# -Test starts the local server in test mode: a kit of gear in the bag and a frail examiner.
param(
    [string]$ServerHost = "127.0.0.1",
    [string]$Capture = "",
    [int]$CaptureFrames = 8,
    [switch]$Test
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$folder = "GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64"
# The console build matters: PowerShell does not wait for a GUI program, so the script would
# reach "finally" at once and stop the server while the game is starting.
$godot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\$folder\Godot_v4.7.2-stable_mono_win64_console.exe"
if ($env:GODOT) { $godot = $env:GODOT }

dotnet build (Join-Path $root "client") -v q
if ($LASTEXITCODE -ne 0) { throw "The client did not build." }

$server = $null
if ($ServerHost -eq "127.0.0.1") {
    dotnet build (Join-Path $root "server") -v q
    if ($LASTEXITCODE -ne 0) { throw "The server did not build." }
    if ($Test) { $env:PIXEL_TEST = "1" }
    $server = Start-Process (Join-Path $root "server\bin\Debug\net8.0\Server.exe") -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root "export\previews\server.log")
}

try {
    $arguments = @("--path", (Join-Path $root "client"), "--", $ServerHost, "7777")
    if ($Capture) { $arguments += @($Capture, $CaptureFrames) }
    & $godot @arguments
}
finally {
    if ($server) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
}
