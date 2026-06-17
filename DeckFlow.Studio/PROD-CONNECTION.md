# Studio — Setting the Production Database Connection String

The Studio reads the prod Postgres connection string from config key **`Studio:ProdConnectionString`**
(see `Program.cs`: `builder.Configuration["Studio:ProdConnectionString"]`). It is loaded from
**.NET user-secrets** and **environment variables** — never from a file in the repo (this is a public
repo; the connection string is a write-capable production credential and must never be committed).

`UserSecretsId` for the Studio project: `9b9eba2b-de02-4f06-901c-abef7d2719d6`.

When set, startup logs `Studio prod connection: configured`; otherwise `not configured`.

---

## Method A — user-secrets (recommended)

Run with the **Windows** dotnet (Studio runs as a Windows process, so the secret must live in the
Windows user-secret store at `%APPDATA%\Microsoft\UserSecrets\9b9eba2b-de02-4f06-901c-abef7d2719d6\secrets.json`).

### PowerShell

PowerShell will NOT run a quoted exe path as a command, and double-quoted strings expand `$`.
Use bare `dotnet` (it's on PATH) and **single-quote** the connection string:

```powershell
cd DeckFlow.Studio
dotnet user-secrets set "Studio:ProdConnectionString" 'PASTE_CONNSTRING_HERE' --project DeckFlow.Studio.csproj
```

If you must use the full dotnet path, prefix with the call operator `&`:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:ProdConnectionString" 'PASTE_CONNSTRING_HERE' --project DeckFlow.Studio.csproj
```

### cmd.exe

```bat
cd DeckFlow.Studio
"C:\Program Files\dotnet\dotnet.exe" user-secrets set "Studio:ProdConnectionString" "PASTE_CONNSTRING_HERE" --project DeckFlow.Studio.csproj
```

### Verify / inspect / remove

```powershell
dotnet user-secrets list --project DeckFlow.Studio.csproj          # shows the key (and value) for this user
dotnet user-secrets remove "Studio:ProdConnectionString" --project DeckFlow.Studio.csproj
dotnet user-secrets clear --project DeckFlow.Studio.csproj
```

---

## Method B — environment variable (alternative)

`Program.cs` also calls `AddEnvironmentVariables()`, so this works too. Nested config keys use a
**double underscore** (`Studio__ProdConnectionString` maps to `Studio:ProdConnectionString`).

Permanent (new shells only):

```powershell
setx Studio__ProdConnectionString 'PASTE_CONNSTRING_HERE'
```

Or in the local launcher `_run-claude.bat`:

```bat
set Studio__ProdConnectionString=PASTE_CONNSTRING_HERE
```

> Caveat: putting the secret in `_run-claude.bat` writes it in plaintext on disk. Prefer user-secrets.

---

## Connection string format

Render gives a URL like `postgresql://USER:PASSWORD@HOST:5432/DBNAME`. Npgsql sometimes needs the
keyword form instead:

```
Host=HOST;Port=5432;Database=DBNAME;Username=USER;Password=PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

If the Studio prod path errors on the `postgresql://` URL, convert to the keyword form above (or ask
whether the Studio path normalizes the URL — the app normalizes `postgres://`/`postgresql://` elsewhere).

---

## Security

- **Never commit** the connection string. This file contains only placeholders — keep it that way.
- It is a **write-capable production credential** (the Studio publish paths write to prod with it).
- If the value is ever exposed (screenshot, shell history, paste), **rotate the DB password in Render**
  and re-set the secret. Clear shell history: PowerShell `Clear-History` plus delete the line from
  `(Get-PSReadlineOption).HistorySavePath` (`ConsoleHost_history.txt`).
- Do not paste the connection string into chat tools or logs.
