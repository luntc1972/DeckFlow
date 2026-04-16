#!/usr/bin/env bash
# Rebuild and run the DeckFlow web app on http://localhost:5173.
# No browser launch (the http launch profile has launchBrowser: false).
set -euo pipefail

cd "$(dirname "$0")/.."

dotnet build DeckFlow.Web
dotnet run --project DeckFlow.Web --launch-profile http --no-build
