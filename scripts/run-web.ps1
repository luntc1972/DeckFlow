# Rebuild and run the DeckFlow web app on http://localhost:5173.
# No browser launch (the http launch profile has launchBrowser: false).
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

dotnet build DeckFlow.Web
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project DeckFlow.Web --launch-profile http --no-build
exit $LASTEXITCODE
