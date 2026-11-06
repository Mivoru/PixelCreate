#!/usr/bin/env bash
# Runs the network simulation: two bots against the server at 80 ms RTT and 2 % loss, and harsher.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test net-tests --logger "console;verbosity=detailed" "$@"
