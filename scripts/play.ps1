# Starts the game. Without options it opens the main menu, which starts the game's own server for
# the save chosen there (Continue, New Game, Load Game). Pass -ServerHost to join another server instead.
# Controls: WASD move, mouse look, Space jump, Shift dash, left button attack, Esc menu (settings, quit).
# With -Capture <folder> a bot plays, saves frames, prints a STATS line and quits (no menu: the script
# starts the server itself, with PIXEL_SLOT and the other PIXEL_ variables as before).
# -Test starts the local server in test mode: a kit of gear in the bag and a frail examiner (no menu).
# -MenuShots <folder> photographs every page of the main menu into the folder and quits.
param(
    [string]$ServerHost = "127.0.0.1",
    [string]$Capture = "",
    [int]$CaptureFrames = 8,
    [switch]$Test,
    [string]$MenuShots = ""
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

$local = $ServerHost -eq "127.0.0.1"
$menu = $local -and -not $Capture -and -not $Test
$serverExe = Join-Path $root "server\bin\Debug\net8.0\Server.exe"
$server = $null
if ($local) {
    # The menu's server is a Release build: the zone takes about 19 s to build there, 34 s in Debug.
    if ($menu) {
        dotnet build (Join-Path $root "server") -c Release -v q
        $serverExe = Join-Path $root "server\bin\Release\net8.0\Server.exe"
    } else {
        dotnet build (Join-Path $root "server") -v q
    }
    if ($LASTEXITCODE -ne 0) { throw "The server did not build." }
}

if ($local -and -not $menu) {
    if ($Test) { $env:PIXEL_TEST = "1" }
    $server = Start-Process $serverExe -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root "export\previews\server.log") -RedirectStandardError (Join-Path $root "export\previews\server-errors.log")
}

try {
    if ($menu) {
        # The menu starts the server itself; tell it where the fresh build is.
        $env:PIXEL_SERVER_EXE = $serverExe
        $arguments = @("--path", (Join-Path $root "client"))
        if ($MenuShots) { $arguments += @("--", "menu", $MenuShots) }
    } else {
        $arguments = @("--path", (Join-Path $root "client"), "--", $ServerHost, "7777")
        if ($Capture) { $arguments += @($Capture, $CaptureFrames) }
    }

    # The client's output also goes to a file, so a crash leaves its last lines behind. Its
    # warnings come on stderr, which must not stop the script.
    $ErrorActionPreference = "Continue"
    & $godot @arguments 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath (Join-Path $root "export\previews\client.log")
}
finally {
    if ($server) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
    Remove-Item Env:PIXEL_SERVER_EXE -ErrorAction SilentlyContinue
}
