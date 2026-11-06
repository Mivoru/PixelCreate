#!/usr/bin/env bash
# Starts a local server and a game client. Pass a host to join another server instead.
# Controls: WASD move, mouse look, Space jump, Shift dash, left button attack, Esc quit.
set -euo pipefail
cd "$(dirname "$0")/.."
godot="${GODOT:-$LOCALAPPDATA/Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe}"
host="${1:-127.0.0.1}"

dotnet build client -v q
if [ "$host" = "127.0.0.1" ]; then
  dotnet build server -v q
  server/bin/Debug/net8.0/Server.exe &
  server=$!
  trap 'kill "$server" 2>/dev/null || true' EXIT
fi
"$godot" --path client -- "$host" 7777
