# Rebuild and run the MtgDeckStudio web app on http://localhost:5173.
# No browser launch (the http launch profile has launchBrowser: false).
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

dotnet build MtgDeckStudio.Web
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project MtgDeckStudio.Web --launch-profile http --no-build
exit $LASTEXITCODE
