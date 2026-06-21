# LOCAL UAT launcher for DeckFlow web (http://localhost:5173).
# Sets throwaway admin BasicAuth creds so /Admin/* is reachable for local UAT.
#
# SECURITY: the creds below are local-machine-only PLACEHOLDERS, not secrets.
# NEVER put a real/prod password in this file — it is tracked in a PUBLIC repo.
# Prod admin creds live in the Render dashboard (sync: false). Override the
# local defaults any time by setting FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD
# in your environment before running.
#
# Note: content.kb.enabled is a DB-backed feature flag, NOT an env var.
# Toggle it via /Admin/Flags (or the Content KB admin panel) after login.
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

if (-not $env:FEEDBACK_ADMIN_USER)     { $env:FEEDBACK_ADMIN_USER = 'admin' }
if (-not $env:FEEDBACK_ADMIN_PASSWORD) { $env:FEEDBACK_ADMIN_PASSWORD = 'changeme-local' }

Write-Host "Admin login: $($env:FEEDBACK_ADMIN_USER) / $($env:FEEDBACK_ADMIN_PASSWORD)"
Write-Host "After login, enable content.kb.enabled via /Admin/Flags for UAT."

# Free port 5173 before building/running. A stale Kestrel (from a prior UAT run
# or another worktree) both blocks the bind AND locks DeckFlow.Web.exe so the
# build copy fails. Kill any listener first; ignore if there is none.
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
