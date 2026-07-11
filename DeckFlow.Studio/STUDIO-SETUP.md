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
| `DECKFLOW_REPO_ROOT` | Repo working tree the git flows (Publish / Direct Push / Pull from Prod / Reconcile / Git Body Coverage) run from — lets a distributed exe publish without being launched from the repo | unset → process current directory |
| `DECKFLOW_LLM_PROVIDER` | `claude` = subscription/$0; `openai` = metered (cap enforced) | unset (metered) |
| `DECKFLOW_LLM_MONTHLY_CAP_USD` | Monthly spend cap (P45 cap-block smoke) | `15.00` |
| `DECKFLOW_DISABLE_AUTO_BROWSER` | `true` stops the browser auto-pop | unset |

## Sync feature flags (web-DB `feature_flags`, read fail-closed)

Two flags gate Studio sync behavior; Studio reads them from the **production** `feature_flags` table through a read-only accessor that fails closed (a missing row or a connection failure reads as OFF). Both ship **OFF**:

- `sync.directpush-gitbody` — when ON, Direct Push triggers a real Render redeploy and bodies are served from the git `/app` tree only (the `/data` overlay fallback is dropped from serving). Before flipping it ON, run the **Git Body Coverage** page and confirm **0 missing** (every approved+visible prod row's body is present in the local git tree).
- `sync.reconcile` — when ON, the **Reconcile** page's destructive **Apply removals** (seed-drift soft-hide) is enabled. The dry-run detection is always available regardless of this flag.

Flipping either flag is an operator action in the prod web flag store; see the phase 93 pre-flip checklist.

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

REM Deploy-confirm (D-09 REVISED / SYNC-09): base URL of the live site + the SAME admin creds the
REM web app's /Admin BasicAuth gate uses (FEEDBACK_ADMIN_USER/FEEDBACK_ADMIN_PASSWORD). All three
REM are required to enable the DirectPush git-body deploy-confirm poll — without them the
REM DirectPush page shows a red "Deploy-confirm: not configured" badge and refuses to start (no
REM silent 401 hang).
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:PublicSiteBaseUrl" "https://www.deckflow.gg"
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:AdminUser" "<same value as web FEEDBACK_ADMIN_USER>"
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:AdminPassword" "<same value as web FEEDBACK_ADMIN_PASSWORD>"
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
Studio deploy-confirm: configured | not configured
```

### Security rules (from CLAUDE.md)

- Secrets live ONLY in user-secrets (or Render dashboard with `sync: false`) — **never** in
  `appsettings*.json`, the repo, commits, or logs. The repo is public.
- Get the actual prod connection string and SSH key from the Render dashboard / your secret
  store — do not paste them into chat, code, or this file.

---

## Run the standalone Windows executable

Package DeckFlow.Studio as a self-contained `win-x64` executable you can run on a clean
Windows machine with **no .NET runtime or SDK installed**. This is the DIST-01 packaging
path.

### Prerequisites

- **Windows x64 machine** (no .NET install required — the runtime is bundled).
- **Build machine** (where you run the publish step): .NET 10 SDK for Windows at
  `C:\Program Files\dotnet\dotnet.exe` — this is the machine where you checked out the
  repo and where `scripts/publish-studio.ps1` runs.

### How to publish

**From Windows PowerShell (primary):**

```powershell
# From the repo root
.\scripts\publish-studio.ps1
```

**From WSL bash:**

```bash
# From the repo root (or anywhere — the script cds to the repo root)
bash scripts/publish-studio.sh
```

Both scripts:

1. Invoke **`C:\Program Files\dotnet\dotnet.exe publish`** with
   `-p:PublishProfile=win-x64-selfcontained` (selects the
   `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml` profile).
2. Clean the output dir first (`artifacts/studio-release/`) so the script is re-runnable
   with no stale artifacts.
3. Strip non-distribution files (`*.pdb`, `*.xml`, `web.config`,
   `appsettings.Development.json`) from the output.
4. Zip the output folder to **`artifacts/DeckFlowStudio-<date>.zip`**.
5. Print the exe path + size in MB and the zip path + size.

The publish profile carries all the packaging properties (win-x64, self-contained,
single-file, native-lib self-extract, trimming off). Nothing in `DeckFlow.Studio.csproj`
is changed by these scripts, so the default `dotnet build` / `dotnet run` behavior is
unchanged.

### What the artifact contains — "single file" reality

The deliverable is **not** a literal single file. Blazor Server reads `wwwroot/` from disk
relative to the exe at startup (it cannot be embedded). The distribution folder contains:

```
artifacts/studio-release/
  DeckFlow.Studio.exe              (~116 MB, self-contained win-x64 bundle)
  wwwroot/                         (~1.6 MB: _framework/blazor.server.js, CSS, etc.)
  appsettings.json                 (Logging config + Kestrel port pin)
  DeckFlow.Studio.staticwebassets.endpoints.json
```

The `wwwroot/` folder **must stay beside the exe**. If it is deleted or separated, the
Blazor UI freezes (404 on `/_framework/blazor.server.js`, no JS/CSS loaded).

The zip (`artifacts/DeckFlowStudio-<date>.zip`) contains the full folder ready to unzip
and run.

### How to launch

1. Unzip `DeckFlowStudio-<date>.zip` to any folder on the target Windows machine.
2. Double-click **`DeckFlow.Studio.exe`** (or run it from a command prompt / PowerShell).
   - The console window shows startup messages, including any errors (e.g. port already in
     use — kill whatever is on 5271 or override the port via `ASPNETCORE_URLS`).
   - The working directory on launch is the exe directory. Studio's local data folder
     (`artifacts/studio/`) lands beside the exe. Set `MTG_DATA_DIR` to an absolute path
     if you want a predictable location independent of where you place the exe.
3. Studio **opens your default browser automatically** at **http://localhost:5271** once it
   finishes starting. If it doesn't (or you closed the tab), browse there manually.
   (`appsettings.json` pins Kestrel to this port; you can override it at runtime with
   `ASPNETCORE_URLS=http://localhost:XXXX` — the auto-open follows the actual bound URL.)
   To suppress the auto-open, set `DECKFLOW_DISABLE_AUTO_BROWSER=1` before launching.

On first launch Studio creates `content-kb.db` in the data dir. This is expected.

### Troubleshooting — the exe flashes open then closes immediately

A double-clicked exe closes its console window the moment it crashes, so a startup error can
vanish before you read it. Studio now **writes a log file to disk** so you can see what
happened after the fact:

- Look in **`<data dir>/logs/studio-<date>.log`** — by default `artifacts/studio/logs/`
  beside the exe (or under `MTG_DATA_DIR/studio/logs/` if you set that env var). The crash is
  recorded there as a `[FTL]` (fatal) entry with the full stack trace.
- **Most common cause: the port is already in use.** The log will show a Kestrel bind failure
  and a plain-language line: *"Startup bind failure — the configured port (default
  http://localhost:5271) is likely already in use…"*. Fix it by closing whatever already
  holds 5271 (another Studio instance, or the dev server), **or** launch on a different port:
  ```
  set ASPNETCORE_URLS=http://localhost:5280
  DeckFlow.Studio.exe
  ```
  (then open the port you chose). Environment variables override `appsettings.json`.
- To keep the window open and watch errors live, launch from an already-open Command Prompt /
  PowerShell (`DeckFlow.Studio.exe`) rather than double-clicking — the console stays up.

### Basic flow needs no secrets

The **basic harvest → distill → review → approve** workflow works with **no secrets
configured at all**. Studio boots against local SQLite and local artifact storage.

Only two paths need extra config:

| Path | What you need |
|------|---------------|
| DirectPush (SCP + prod Postgres upsert) | `Studio__Scp__*` + `Studio__ProdConnectionString` |
| DirectPush deploy-confirm poll (D-09 REVISED) | `Studio__PublicSiteBaseUrl` + `Studio__AdminUser` + `Studio__AdminPassword` |
| Git commit-publish | `git.exe` on `PATH`; launched from the repo working tree **or** `DECKFLOW_REPO_ROOT` set to it |

LLM distill uses `DECKFLOW_LLM_PROVIDER=claude` (subscription, $0 spend) or
`OPENAI_API_KEY` (metered). Not required to just browse and review existing entries.

### Secrets via environment variables (clean machine — no SDK, no user-secrets)

On a machine where the .NET SDK is not installed, supply secrets as **environment
variables** (configuration key `:` separator → `__` in env vars):

| Config key | Environment variable | Required for |
|------------|----------------------|--------------|
| `Studio:ProdConnectionString` | `Studio__ProdConnectionString` | DirectPush Postgres upsert |
| `Studio:Scp:Host` | `Studio__Scp__Host` | DirectPush SCP upload |
| `Studio:Scp:Username` | `Studio__Scp__Username` | DirectPush SCP upload |
| `Studio:Scp:KeyFile` | `Studio__Scp__KeyFile` | DirectPush SCP upload |
| `Studio:Scp:RemoteArtifactRoot` | `Studio__Scp__RemoteArtifactRoot` | DirectPush SCP upload |
| `Studio:Scp:Port` | `Studio__Scp__Port` | Optional (default 22) |
| `Studio:Scp:KeyPassphrase` | `Studio__Scp__KeyPassphrase` | Optional (if key is encrypted) |
| `Studio:PublicSiteBaseUrl` | `Studio__PublicSiteBaseUrl` | DirectPush deploy-confirm poll (D-09 REVISED) |
| `Studio:AdminUser` | `Studio__AdminUser` | DirectPush deploy-confirm poll — must match web `FEEDBACK_ADMIN_USER` |
| `Studio:AdminPassword` | `Studio__AdminPassword` | DirectPush deploy-confirm poll — must match web `FEEDBACK_ADMIN_PASSWORD` |
| `DECKFLOW_LLM_PROVIDER` | `DECKFLOW_LLM_PROVIDER` | LLM distill (default=openai) |
| `OPENAI_API_KEY` | `OPENAI_API_KEY` | OpenAI distill path |
| (override port) | `ASPNETCORE_URLS` | Only if overriding the pinned 5271 port |

Example `_launch-studio.bat` wrapper that sets secrets then runs the exe:

```bat
@echo off
REM ⚠ WARNING: This file holds secrets. DO NOT commit it. DO NOT share it.
REM            It is listed in .gitignore; keep it in the exe folder only.
set Studio__ProdConnectionString=postgresql://...
set Studio__Scp__Host=...
set Studio__Scp__Username=...
set Studio__Scp__KeyFile=C:\Users\you\.ssh\render_key
set Studio__Scp__RemoteArtifactRoot=/data
set DECKFLOW_LLM_PROVIDER=claude
DeckFlow.Studio.exe
```

**The `.bat` file holds secrets and is NOT committed or distributed** (public repo). Keep
it in the same folder as the exe on the target machine only, and exclude it from any
file sharing that would expose it.

If you happen to have the .NET SDK installed on the target machine, you can use
`dotnet user-secrets` instead — see the **User-secrets** section above for those
commands.

### Optional ReadyToRun (faster cold start)

`PublishReadyToRun=true` is not enabled by default. It pre-compiles native code for
faster cold startup at the cost of ~15 MB extra executable size. For a single-operator
tool that starts once, the tradeoff is not worth it. To enable it for your own build,
add `-p:PublishReadyToRun=true` on the `dotnet publish` command line; do not commit that
flag to the profile so other operators keep the default-off behavior.
