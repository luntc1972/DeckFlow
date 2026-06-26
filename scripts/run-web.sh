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

dotnet build DeckFlow.Web
dotnet run --project DeckFlow.Web --launch-profile http --no-build
