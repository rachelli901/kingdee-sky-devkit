#!/usr/bin/env bash
# Author: rachelli901 <lir30481@gmail.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

# ============================================================
#  KingdeeSky.WebApiClient Demo Runner (Git Bash / POSIX)
#  1) build the demo client from KingdeeSky.DevKit/src (app/ is a build output,
#     never a hand-maintained copy)
#  2) start the local mock server on port 18080 if it is not already listening
#  3) run the .NET Framework WebApi client demo
#  Requirements: python, PowerShell, .NET Framework 4.8 (bundled with Win10/11).
# ============================================================
set -e
cd "$(dirname "$0")"

echo "[1/3] Building demo client from DevKit source ..."
powershell -NoProfile -ExecutionPolicy Bypass -File "./build-demo.ps1"

# Login password shared by the mock server and the client. It is never a literal
# in the repo: the environment wins, otherwise mock_server.py stores a generated
# value in the gitignored .demo-credential next to this script.
if [ -z "${KINGDEE_PASSWORD:-}" ] && [ -f .demo-credential ]; then
    KINGDEE_PASSWORD="$(cat .demo-credential)"
fi
if [ -n "${KINGDEE_PASSWORD:-}" ]; then
    export KINGDEE_MOCK_PASSWORD="$KINGDEE_PASSWORD"
fi

# Port check is locale- and case-independent: a listening socket is a TCP line
# whose foreign address is port 0 (0.0.0.0:0). Matching the state column instead
# would break on non-English Windows and would also misread TIME_WAIT leftovers
# as "already listening", which silently skips starting the mock server.
if netstat -ano 2>/dev/null | awk '$1 == "TCP" && $2 ~ /[:.]18080$/ && $3 ~ /[:.]0$/ { f = 1 } END { exit(f ? 0 : 1) }'; then
    echo "[2/3] Mock server already listening on port 18080, reuse it."
else
    echo "[2/3] Starting mock server on port 18080 ..."
    nohup python mock_server.py 18080 > mock_server.log 2>&1 &
    sleep 2
fi

# mock_server.py writes .demo-credential when it had to generate the password.
if [ -z "${KINGDEE_PASSWORD:-}" ] && [ -f .demo-credential ]; then
    KINGDEE_PASSWORD="$(cat .demo-credential)"
fi
if [ -z "${KINGDEE_PASSWORD:-}" ]; then
    echo "ERROR: no demo credential available; set KINGDEE_PASSWORD, or start mock_server.py once." >&2
    exit 1
fi
export KINGDEE_PASSWORD

echo "[3/3] Starting WebApi client demo ..."
cd app
./KingdeeSky.WebApiClient.exe

echo
echo "Demo finished."