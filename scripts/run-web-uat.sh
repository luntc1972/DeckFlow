#!/usr/bin/env bash
# LOCAL UAT launcher for DeckFlow web (http://localhost:5173).
# Sets throwaway admin BasicAuth creds so /Admin/* is reachable for local UAT.
#
# SECURITY: the creds below are local-machine-only PLACEHOLDERS, not secrets.
# NEVER put a real/prod password in this file — it is tracked in a PUBLIC repo.
# Prod admin creds live in the Render dashboard (sync: false). Override the
# local defaults any time by exporting FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD
# before running.
#
# Note: tool.knowledge-base.enabled is a DB-backed feature flag, NOT an env var.
# Toggle it via /Admin/Flags (or the Content KB admin panel) after login.
set -euo pipefail

cd "$(dirname "$0")/.."

export FEEDBACK_ADMIN_USER="${FEEDBACK_ADMIN_USER:-admin}"
export FEEDBACK_ADMIN_PASSWORD="${FEEDBACK_ADMIN_PASSWORD:-changeme-local}"

echo "Admin login: ${FEEDBACK_ADMIN_USER} / ${FEEDBACK_ADMIN_PASSWORD}"
echo "After login, enable tool.knowledge-base.enabled via /Admin/Flags for UAT."

# Free port 5173 before building/running so a stale server does not block the
# bind. Best-effort: fuser (Linux) if available.
if command -v fuser >/dev/null 2>&1; then
    fuser -k 5173/tcp 2>/dev/null || true
    sleep 0.5
fi

dotnet build DeckFlow.Web
dotnet run --project DeckFlow.Web --launch-profile http --no-build
