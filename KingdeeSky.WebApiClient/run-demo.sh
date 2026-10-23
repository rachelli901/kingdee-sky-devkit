#!/usr/bin/env bash
# ============================================================
#  KingdeeSky.WebApiClient Demo Runner (Git Bash / POSIX)
#  Starts the local mock server (port 18080) if not running,
#  then launches the .NET Framework WebApi client demo.
# ============================================================
set -e
cd "$(dirname "$0")"

if netstat -ano 2>/dev/null | grep ":18080" | grep -q "LISTENING"; then
    echo "[1/2] Mock server already running on port 18080, reuse it."
else
    echo "[1/2] Starting mock server on port 18080 ..."
    nohup python mock_server.py 18080 > mock_server.log 2>&1 &
    sleep 2
fi

echo "[2/2] Starting WebApi client demo ..."
cd app
./KingdeeSky.WebApiClient.exe

echo
echo "Demo finished."