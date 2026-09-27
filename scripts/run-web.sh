#!/usr/bin/env bash
# Rebuild and run the DeckFlow web app on http://localhost:5173.
#
# No Windows browser pops on startup: the custom DevelopmentBrowserLauncher
# (gated in Program.cs by DECKFLOW_DISABLE_AUTO_BROWSER, NOT by the launch
# profile's launchBrowser flag) is suppressed by exporting the gate var below.
# This keeps a pre-started dev server from popping a browser that a later
# Playwright run would just reuse. Export DECKFLOW_DISABLE_AUTO_BROWSER=false
# before running if you actually want the browser to open.
set -euo pipefail

cd "$(dirname "$0")/.."

export DECKFLOW_DISABLE_AUTO_BROWSER="${DECKFLOW_DISABLE_AUTO_BROWSER:-true}"

DOTNET="$(command -v dotnet 2>/dev/null || command -v dotnet.exe 2>/dev/null || true)"
if [ -z "$DOTNET" ]; then
  echo "error: neither 'dotnet' nor 'dotnet.exe' found on PATH" >&2
  exit 1
fi

# Why: resolve dotnet symlinks so WSLENV detects the Windows executable.
DOTNET_RESOLVED="$(readlink -f "$DOTNET" 2>/dev/null || printf '%s' "$DOTNET")"
# WSL-exported vars do not cross into Windows .exe processes unless named in WSLENV.
if [[ "$DOTNET_RESOLVED" == *.exe || "$DOTNET_RESOLVED" == *"/mnt/c/"* ]]; then
  export WSLENV="${WSLENV:+${WSLENV}:}DECKFLOW_DISABLE_AUTO_BROWSER:ASPNETCORE_ENVIRONMENT:FEEDBACK_ADMIN_USER:FEEDBACK_ADMIN_PASSWORD"
fi

"$DOTNET" build DeckFlow.Web
"$DOTNET" run --project DeckFlow.Web --launch-profile http --no-build
