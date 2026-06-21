# Rebuild and run the DeckFlow web app on http://localhost:5173.
# No browser launch (the http launch profile has launchBrowser: false).
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

# Free port 5173 before building/running. A stale Kestrel (from a prior run or
# another worktree) both blocks the bind AND locks DeckFlow.Web.exe so the build
# copy fails. Kill any listener first; ignore if there is none.
$Port = 5173
$listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
foreach ($procId in ($listeners.OwningProcess | Select-Object -Unique)) {
    if ($procId -and $procId -ne 0) {
        Write-Host "Port $Port in use by PID $procId - stopping it."
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
}
Start-Sleep -Milliseconds 500

dotnet build DeckFlow.Web
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project DeckFlow.Web --launch-profile http --no-build
exit $LASTEXITCODE
