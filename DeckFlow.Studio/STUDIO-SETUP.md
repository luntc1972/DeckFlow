# DeckFlow Studio — Local Setup & Secrets

How to run DeckFlow.Studio locally and (only if you need it) configure user-secrets.

## TL;DR — do the Phase 51 (51-02) smokes WITHOUT secrets

The Phase 51 Studio smokes (P41 render, P45 re-distill/cap/cancel, P46 Review/Publish git
commit) **do not need any user-secrets**. Studio boots against a **local SQLite** database and
local artifact folder. User-secrets only gate the *direct prod-publish* page (DirectPush), which
is Phase 52 work.

From the repo root (`DeckFlow.Studio/`), just run the batch file you already have:

```
DeckFlow.Studio\_run-claude.bat
```

Then open **http://localhost:5271** in a browser.

That script sets:
- `DECKFLOW_LLM_PROVIDER=claude` — subscription distiller, **$0** live spend (used by the P45 re-distill smoke)
- `DECKFLOW_DISABLE_AUTO_BROWSER=true` — no auto browser pop
- `ASPNETCORE_ENVIRONMENT=Development`
- `DECKFLOW_LLM_CLI_COMMAND=[...]` — routes the claude CLI through WSL

## Where Studio keeps its local data

`Program.cs` → `ResolveStudioDataDirectory()`:

- If `MTG_DATA_DIR` is set → data lives under `%MTG_DATA_DIR%\studio\`
- If not set → data lives under `.\artifacts\studio\` (relative to the working dir)

Inside that folder:
- `content-kb.db` — SQLite store (sources, videos, site index, blocked videos, harvest runs, spend ledger)
- `content-kb\` — artifact root (distilled prompt files copied here)

For the P45 re-distill smoke you need **at least one enabled YouTube source and one
already-distilled video** in that DB. If empty, browse a channel + distill one video first
(that setup run is a WAIVED-setup-step, not a smoke pass/fail).

## Optional env vars for the smokes

| Var | Purpose | Default |
|-----|---------|---------|
| `MTG_DATA_DIR` | Move the Studio data dir off the repo tree | unset → `.\artifacts\studio` |
| `DECKFLOW_LLM_PROVIDER` | `claude` = subscription/$0; `openai` = metered (cap enforced) | unset (metered) |
| `DECKFLOW_LLM_MONTHLY_CAP_USD` | Monthly spend cap (P45 cap-block smoke) | `15.00` |
| `DECKFLOW_DISABLE_AUTO_BROWSER` | `true` stops the browser auto-pop | unset |

## User-secrets — ONLY for the prod-publish (DirectPush) page (Phase 52)

The DirectPush page (live SCP-to-Render + safe Postgres upsert) reads these from .NET
user-secrets. They are **presence-only** checked at startup and **never logged**. You do NOT
need them for the Phase 51 smokes.

The project's `UserSecretsId` is `9b9eba2b-de02-4f06-901c-abef7d2719d6` (already in the csproj).

Set them from the `DeckFlow.Studio/` directory:

```
cd DeckFlow.Studio

REM Prod Postgres connection string (URL or key-value form) — enables the safe content-columns-only upsert
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:ProdConnectionString" "<prod-postgres-conn-string>"

REM SCP target = the Render /data disk (artifact upload over SSH). All four are required to enable SCP.
REM Get Host (the service SSH address) and Username (the service id) from the Render dashboard → service → SSH.
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:Scp:Host" "<service>@ssh.<region>.render.com host part"
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:Scp:Username" "<render-service-id>"
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:Scp:KeyFile" "C:\path\to\render_ssh_key"
REM RemoteArtifactRoot = the disk root MTG_DATA_DIR points at (/data), NOT /data/content-kb.
REM Each row's RemoteRelativePath already begins with "content-kb/", so the root must be the
REM parent (/data) or the content-kb/ segment is doubled (DirectPush.razor _dataRoot note, D-01/D-03/D-10).
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:Scp:RemoteArtifactRoot" "/data"

REM Optional
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:Scp:Port" "22"
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:Scp:KeyPassphrase" "<passphrase-if-key-encrypted>"
```

To view / clear:

```
"C:\Program Files\dotnet\dotnet.exe" user-secrets list
"C:\Program Files\dotnet\dotnet.exe" user-secrets clear
```

At startup Studio logs only presence, never values:
```
Studio prod connection: configured | not configured
Studio SCP: configured | not configured
```

### Security rules (from CLAUDE.md)

- Secrets live ONLY in user-secrets (or Render dashboard with `sync: false`) — **never** in
  `appsettings*.json`, the repo, commits, or logs. The repo is public.
- Get the actual prod connection string and SSH key from the Render dashboard / your secret
  store — do not paste them into chat, code, or this file.
