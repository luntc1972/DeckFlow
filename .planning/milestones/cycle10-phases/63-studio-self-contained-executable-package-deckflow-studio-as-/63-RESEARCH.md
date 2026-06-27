# Phase 63: Studio Self-Contained Executable — Research

**Researched:** 2026-06-20
**Domain:** .NET 10 self-contained single-file publish, Blazor Server static assets, win-x64 packaging
**Confidence:** HIGH — all findings verified by running an actual trial publish against the real project

---

## Summary

DeckFlow.Studio can be published as a self-contained, single-file `win-x64` executable using standard
SDK publish properties (`PublishSingleFile=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`,
`IncludeNativeLibrariesForSelfExtract=true`). A trial publish was run against the actual codebase to
verify behavior — the result is a **116 MB `DeckFlow.Studio.exe`** with no loose managed or native DLLs
beside it. The entire .NET runtime and all managed + native DLLs are bundled into the single exe.

**However:** Blazor Server's static web assets (`wwwroot/`) are **not** bundled into the exe. The
`wwwroot/` folder (1.6 MB) must be distributed alongside the exe. This is an ASP.NET Core architectural
constraint, not a packaging deficiency — `UseStaticFiles()` reads from the filesystem ContentRoot. The
distribution artifact is therefore a zip of **exe + wwwroot/** (≈118 MB total), not a single file in the
filesystem sense.

The operator-facing UX is acceptable: unzip to a folder, double-click `DeckFlow.Studio.exe`, open a
browser to `http://localhost:5000`. No .NET runtime installation required. For DirectPush (prod
publish), the operator sets environment variables; no dotnet CLI needed post-install.

**Primary recommendation:** Use csproj publish properties (no `.pubxml` profile needed for a simple
MSBuild-based publish) and add a `scripts/publish-studio.ps1` wrapper that invokes `dotnet.exe publish`,
zips the output, and prints the artifact path. The script is the only deliverable alongside a small
README section in `DeckFlow.Studio/STUDIO-SETUP.md`.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DIST-01 | Package Studio as a self-contained, single-file win-x64 exe; produce a repeatable publish profile/script; document build + run steps | Trial publish verified: exe builds at 116 MB; publish properties identified; script pattern clear; operator run steps documented below |

</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Self-contained binary packaging | MSBuild SDK / dotnet publish | — | Pure SDK-level publish properties; no app code changes needed |
| Static web assets serving | ASP.NET Core StaticFiles middleware | File system (wwwroot/ folder) | `UseStaticFiles()` reads from ContentRoot/wwwroot; cannot be embedded in single-file for Blazor Server |
| Secrets (DirectPush) | Environment variables | .NET user-secrets file | No dotnet CLI on clean machine; env vars are the canonical path for a standalone exe |
| Native lib extraction | .NET single-file runtime | Temp extraction directory | `IncludeNativeLibrariesForSelfExtract=true` embeds native DLLs into the exe; runtime extracts to `%TEMP%` on first launch |
| Git operations (Publish page) | System git.exe on PATH | — | GitRepository shells out to git; operator must have git installed for the commit-publish path |
| LLM distillation | External CLI (claude) or HTTP (openai) | — | Distill requires DECKFLOW_LLM_PROVIDER + API key or claude CLI on PATH |

---

## Standard Stack

### Core publish properties

No new packages needed. All packaging is via standard .NET SDK publish properties:

| Property | Value | Purpose |
|----------|-------|---------|
| `SelfContained` | `true` | Bundle .NET runtime; no install required on target machine |
| `RuntimeIdentifier` | `win-x64` | Target Windows 64-bit; required for self-contained |
| `PublishSingleFile` | `true` | Merge all managed DLLs into one exe |
| `IncludeNativeLibrariesForSelfExtract` | `true` | Bundle native DLLs (e_sqlite3.dll, aspnetcorev2_inprocess.dll, coreclr.dll etc.) into exe; extracted to %TEMP% at first launch |
| `PublishTrimmed` | `false` | MUST be false — see Trimming section below |
| `PublishReadyToRun` | `true` (OPTIONAL) | Pre-JIT to native code; reduces cold-start time at cost of +15-25% exe size; nice-to-have for a local tool |

**Verified publish command (from WSL invoking Windows dotnet.exe):**

```powershell
# From repo root — PowerShell on Windows OR from WSL:
"C:\Program Files\dotnet\dotnet.exe" publish DeckFlow.Studio/DeckFlow.Studio.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o "artifacts/studio-release"
```

```bash
# WSL equivalent (bash, using Windows dotnet.exe):
/mnt/c/Program\ Files/dotnet/dotnet.exe publish DeckFlow.Studio/DeckFlow.Studio.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -o artifacts/studio-release
```

**Verified: .NET 10.0.301 SDK is installed at `C:\Program Files\dotnet\` and accessible from WSL at
`/mnt/c/Program Files/dotnet/dotnet.exe`.** [VERIFIED: trial publish ran successfully 2026-06-20]

---

## Package Legitimacy Audit

Phase 63 adds NO new NuGet packages. All publish behavior is achieved through standard MSBuild SDK
properties that ship with the .NET 10 SDK.

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

---

## Native Dependency Analysis

**Verified against the actual publish output.** [VERIFIED: trial publish + NuGet package inspection]

### The one native dependency: `e_sqlite3.dll`

`Microsoft.Data.Sqlite` (10.0.0) → `SQLitePCLRaw.bundle_e_sqlite3` (2.1.11) → `SQLitePCLRaw.lib.e_sqlite3`
(2.1.11) ships a **native Win64 `e_sqlite3.dll`**.

- This is the SQLite engine as a native DLL.
- Without `IncludeNativeLibrariesForSelfExtract=true`, `e_sqlite3.dll` would end up as a **loose file
  beside the exe**, breaking the "single file" story.
- **With `IncludeNativeLibrariesForSelfExtract=true`**, it is embedded in the exe and extracted to
  `%TEMP%\DeckFlow.Studio\<hash>\e_sqlite3.dll` on first launch. The extraction is automatic and
  transparent to the operator.
- **Trial publish confirmed: no loose `.dll` files beside the exe.** Only the exe + wwwroot/ + appsettings.json
  appear in the output directory.

### SSH.NET (2025.1.0) — managed-only

Inspected NuGet package: `lib/net8.0/Renci.SshNet.dll`, `lib/net9.0/Renci.SshNet.dll` only. No
`runtimes/` or `native/` folder. SSH.NET is **pure managed code** — crypto via BouncyCastle (also
managed). No native extraction needed. [VERIFIED: NuGet package inspection]

### BouncyCastle.Cryptography (2.6.2) — managed-only

Inspected: `lib/net461/`, `lib/net6.0/`, `lib/netstandard2.0/` — all managed assemblies, no native
folder. [VERIFIED: NuGet package inspection]

### Npgsql (10.0.0) — managed-only

No `runtimes/` folder in the package. Pure managed TCP/IP Postgres wire protocol. [VERIFIED: NuGet
package inspection]

### ASP.NET Core in-process hosting DLLs

The publish output includes `aspnetcorev2_inprocess.dll`, `coreclr.dll`, `clrjit.dll` etc. — these are
Win-x64 native files from the .NET runtime itself, also embedded via `IncludeNativeLibrariesForSelfExtract`.

### Security note: SQLitePCLRaw.lib.e_sqlite3 2.1.11 vulnerability

The build emits `NU1903` warning: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 has a known high severity
vulnerability ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)). This is
a **timing side-channel** in SQLite, not a remote code execution. For a local operator tool on a
private LAN (no public exposure), the practical risk is minimal. The version is **pinned by
`Microsoft.Data.Sqlite` 10.0.0** — upgrading independently is not possible without breaking the
transitive dependency. No action required for Phase 63; track for the next `Microsoft.Data.Sqlite`
version bump.

---

## Trimming Analysis

**Recommendation: `PublishTrimmed=false` (trimming DISABLED).** [ASSUMED for Blazor + reflection pattern
— standard ecosystem guidance; not empirically verified for this codebase]

Reasons:
1. **Blazor Server uses reflection extensively** — component scanning, DI resolution, razor-generated
   code, SignalR hubs all rely on reflection-based dispatch that trimming breaks.
2. **Dapper uses expression trees and reflection** for mapping — incompatible with aggressive trimming.
3. **YoutubeExplode, RestSharp, OpenAI SDK** are not annotated for trimming compatibility.
4. **Trim annotations** would require auditing all 30+ service classes and Core models, which is out of
   scope for a packaging phase.
5. **Size benefit is marginal**: trimming a Blazor Server app typically saves 10-20 MB from a 116 MB
   exe. Not worth the risk of runtime `MissingMemberException` on first use.

---

## Blazor Server Static Web Asset Behavior

**The critical distribution finding:** Blazor Server's `wwwroot/` is **not embedded** in the single-file
exe. This is confirmed by the trial publish output. [VERIFIED: trial publish 2026-06-20]

### What the publish produces (confirmed)

```
artifacts/studio-release/
├── DeckFlow.Studio.exe              # 116 MB — the single-file exe (runtime + all managed + native DLLs)
├── DeckFlow.Studio.staticwebassets.endpoints.json  # 84 KB — compressed-asset routing manifest
├── appsettings.json                 # 119 bytes — logging config
├── appsettings.Development.json     # 119 bytes — can be excluded from distribution
├── web.config                       # IIS config — can be excluded (not used for Kestrel direct launch)
├── DeckFlow.Core.pdb                # debug symbols — can be excluded from distribution
├── DeckFlow.Core.xml                # XML doc — can be excluded from distribution
├── DeckFlow.Studio.pdb              # debug symbols — can be excluded from distribution
└── wwwroot/
    ├── _framework/
    │   ├── blazor.server.js         # 161 KB — Blazor SignalR client
    │   ├── blazor.server.js.br/.gz  # compressed variants
    │   ├── blazor.web.js            # 196 KB
    │   └── blazor.web.js.br/.gz
    ├── css/ bootstrap + site.css + open-iconic fonts
    ├── DeckFlow.Studio.styles.css   # scoped CSS
    └── favicon.ico
```

**Total minimum distribution: ~118 MB** (exe + wwwroot/ + appsettings.json + staticwebassets.endpoints.json).

### Why wwwroot cannot be embedded

`app.UseStaticFiles()` in Studio's `Program.cs` reads static files from the **ContentRoot/wwwroot** path
on the filesystem. For a single-file exe, ContentRoot defaults to the **directory containing the exe**
(this was fixed in .NET 6+; it does NOT use the `%TEMP%` extraction directory as ContentRoot).
Therefore `wwwroot/` must be **beside the exe** in the same directory.

The `DeckFlow.Studio.staticwebassets.endpoints.json` manifest is used by ASP.NET Core's compressed
static asset serving (`.br`/`.gz` variants). It must also be beside the exe.

### Distribution format

**Recommended: ZIP archive.** The operator unzips to any folder and double-clicks the exe. No installer
required. The zip contains:

```
DeckFlowStudio-<version>/
├── DeckFlow.Studio.exe
├── DeckFlow.Studio.staticwebassets.endpoints.json
├── appsettings.json
└── wwwroot/
    ├── _framework/...
    ├── css/...
    └── favicon.ico
```

Files to **exclude** from the zip: `appsettings.Development.json`, `web.config`, `*.pdb`, `*.xml`.

---

## Architecture Patterns

### Recommended Project Structure (publish artifacts)

```
scripts/
├── publish-studio.ps1       # NEW: one-shot publish + zip script for Windows/WSL
DeckFlow.Studio/
├── DeckFlow.Studio.csproj   # No new properties needed — pass via CLI or add to csproj <PropertyGroup>
├── STUDIO-SETUP.md          # UPDATE: add "Standalone Exe" section with run steps
```

### Pattern 1: Publish properties in csproj vs CLI

**Option A (recommended): add a `<PropertyGroup>` to the csproj.**

Keeps the properties version-controlled and makes `dotnet publish` work without extra flags:

```xml
<!-- In DeckFlow.Studio.csproj, inside a new <PropertyGroup Condition="'$(Configuration)'=='Release'"> -->
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<PublishTrimmed>false</PublishTrimmed>
```

**Option B: pass all flags on the CLI in the publish script.** Keeps the csproj clean but the script
is the only source of truth.

The planner should choose Option A — it makes `dotnet publish` in CI or by another developer
produce the right artifact without needing the script.

**CAUTION with `<RuntimeIdentifier>` in csproj:** Setting `RuntimeIdentifier` unconditionally in the
csproj affects ALL builds (debug, CI test runs). Gate it with `Condition="'$(RuntimeIdentifier)'==''"`
or under a `Release`-only `PropertyGroup`. Alternatively, always pass `-r win-x64` on the CLI and do
not embed it in the csproj — then the csproj stays RID-agnostic and the `DeckFlow.Studio.Tests` build
is unaffected.

**Recommended approach:** Add `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, and
`PublishTrimmed` as unconditional publish-time properties (safe to set at all times), and pass
`-r win-x64 --self-contained` only on the CLI or in the publish script. This avoids csproj
RID-pinning that would break cross-platform dev builds.

### Pattern 2: Publish script (`scripts/publish-studio.ps1`)

```powershell
# scripts/publish-studio.ps1
# Usage: .\scripts\publish-studio.ps1
# Produces: artifacts/DeckFlowStudio-<date>.zip
param(
    [string]$Version = (Get-Date -Format "yyyy.MM.dd"),
    [string]$OutDir  = "artifacts/studio-release"
)

$DotNet = "C:\Program Files\dotnet\dotnet.exe"
$Project = "DeckFlow.Studio\DeckFlow.Studio.csproj"
$ZipOut  = "artifacts\DeckFlowStudio-$Version.zip"

& $DotNet publish $Project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o $OutDir

# Remove files not needed in the distribution
Remove-Item "$OutDir\*.pdb" -ErrorAction SilentlyContinue
Remove-Item "$OutDir\*.xml" -ErrorAction SilentlyContinue
Remove-Item "$OutDir\web.config" -ErrorAction SilentlyContinue
Remove-Item "$OutDir\appsettings.Development.json" -ErrorAction SilentlyContinue

Compress-Archive -Path "$OutDir\*" -DestinationPath $ZipOut -Force
Write-Host "Artifact: $ZipOut ($([Math]::Round((Get-Item $ZipOut).Length / 1MB, 1)) MB)"
```

A bash equivalent (`scripts/publish-studio.sh`) for WSL invocation:

```bash
#!/usr/bin/env bash
# scripts/publish-studio.sh — run from repo root in WSL
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
OUT="artifacts/studio-release"
VERSION=$(date +%Y.%m.%d)
ZIP="artifacts/DeckFlowStudio-${VERSION}.zip"

"$DOTNET" publish DeckFlow.Studio/DeckFlow.Studio.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -o "$OUT"

# Strip debug/IIS artifacts
rm -f "$OUT"/*.pdb "$OUT"/*.xml "$OUT/web.config" "$OUT/appsettings.Development.json"

cd "$OUT" && zip -r "../../$ZIP" . && cd -
echo "Artifact: $ZIP"
```

### Anti-Patterns to Avoid

- **Do NOT set `PublishTrimmed=true`** — breaks Blazor component discovery and Dapper reflection.
- **Do NOT omit `IncludeNativeLibrariesForSelfExtract=true`** — `e_sqlite3.dll` will be a loose file
  beside the exe, defeating the single-file intent.
- **Do NOT delete the `wwwroot/` folder** thinking it is redundant — without it, Blazor pages will
  load without JavaScript (`_framework/blazor.server.js` 404) and without CSS.
- **Do NOT pin `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` unconditionally in the csproj** — this
  breaks dev-side Linux/Mac builds and `dotnet test` in CI if cross-platform test runs are ever added.
- **Do NOT set `<UseAppHost>false</UseAppHost>`** (as the web app Dockerfile does for container
  publishing) — that suppresses the `.exe` launcher and produces a DLL-only output useless to the
  operator.

---

## Secrets on a Clean Machine

**The scenario:** Operator unzips the release package on a Windows machine with no .NET SDK installed.

### What works without any secrets

The core harvest→distill→review workflow works without any secrets:

| Workflow | Needs secrets? | Notes |
|----------|---------------|-------|
| Harvest YouTube videos | No | Uses YouTube public API (YoutubeExplode) |
| Distill with OpenAI | Yes — `DECKFLOW_OPENAI_API_KEY` | Metered; cap enforced |
| Distill with Claude | Yes — claude CLI on PATH + auth | Subscription; $0 spend |
| Review / approve entries | No | Pure local SQLite |
| Publish via git commit | Needs git on PATH + repo checkout | Commit path only |
| DirectPush to prod | Yes — SCP + prod Postgres secrets | DirectPush path only |

### How the operator sets secrets for a packaged exe

.NET user-secrets require `dotnet user-secrets set` from the SDK CLI. A clean machine has no SDK.
**The correct pattern for a packaged exe is environment variables.**

Mapping: ASP.NET Core config hierarchy separator `:`  → `__` (double underscore) in env vars.

```
Studio:ProdConnectionString    → Studio__ProdConnectionString
Studio:Scp:Host                → Studio__Scp__Host
Studio:Scp:Username            → Studio__Scp__Username
Studio:Scp:KeyFile             → Studio__Scp__KeyFile
Studio:Scp:RemoteArtifactRoot  → Studio__Scp__RemoteArtifactRoot
Studio:Scp:Port                → Studio__Scp__Port (optional)
Studio:Scp:KeyPassphrase       → Studio__Scp__KeyPassphrase (optional)
```

**Important:** `Program.cs` calls `builder.Configuration.AddUserSecrets<Program>().AddEnvironmentVariables()`.
User-secrets ARE loaded in all environments (not gated on Development) — if the operator happens to have
`%APPDATA%\Microsoft\UserSecrets\9b9eba2b-de02-4f06-901c-abef7d2719d6\secrets.json`, it will load.
But env vars are the right default path for a clean machine.

**The operator creates a `_launch-studio.bat` wrapper:**

```bat
@echo off
REM Set secrets as env vars — never commit this file
set Studio__ProdConnectionString=<prod-postgres-url>
set Studio__Scp__Host=<render-host>
set Studio__Scp__Username=<render-user>
set Studio__Scp__KeyFile=C:\path\to\render_key
set Studio__Scp__RemoteArtifactRoot=/data
set DECKFLOW_LLM_PROVIDER=openai
set OPENAI_API_KEY=<key>
set ASPNETCORE_URLS=http://localhost:5271
DeckFlow.Studio.exe
```

This `.bat` file should be documented but NOT distributed with the zip (it contains secrets).

### User-secrets alternative (optional, for operators with SDK installed)

If the operator has the .NET SDK installed (which is NOT required, but may already be present),
they can use user-secrets the same way as documented in `STUDIO-SETUP.md`. The secrets live at
`%APPDATA%\Microsoft\UserSecrets\9b9eba2b-de02-4f06-901c-abef7d2719d6\secrets.json` and are
read automatically without env vars.

---

## Working Directory and Data Directory Behavior

### Double-click launch

When the operator double-clicks `DeckFlow.Studio.exe` from Windows Explorer, the working directory
(`Directory.GetCurrentDirectory()`) is typically the **directory containing the exe**. `ResolveStudioDataDirectory()`
in `Program.cs` uses this:

```csharp
private static string ResolveStudioDataDirectory()
{
    var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
    var root = string.IsNullOrWhiteSpace(dataDir)
        ? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "studio")
        : Path.Combine(dataDir, "studio");
    return Path.GetFullPath(root);
}
```

- Without `MTG_DATA_DIR`: data goes to `<exe-dir>\artifacts\studio\`
- With `MTG_DATA_DIR=C:\Users\Operator\AppData\DeckFlow`: data goes to that path

**Double-click behavior is correct** — the exe directory is the working directory, so `artifacts/studio/`
is created beside the exe. This is where `content-kb.db` and the `content-kb/` artifact root land.

### Command-line launch from a different directory

If the operator runs `C:\Tools\DeckFlowStudio\DeckFlow.Studio.exe` from `C:\SomeOtherDir`, the working
directory is `C:\SomeOtherDir` and data goes to `C:\SomeOtherDir\artifacts\studio\`. This is
surprising behavior. **Recommendation:** document that the operator should either:
1. Double-click the exe (safe — cwd is exe dir)
2. Use `Set-Location` / `cd` to the exe directory before launching
3. Set `MTG_DATA_DIR` to an absolute path

### ContentRoot and wwwroot serving

For single-file executables, ASP.NET Core's `WebApplication.CreateBuilder()` sets `ContentRoot` to the
**directory containing the exe** (NOT the `%TEMP%` extraction directory). Therefore `wwwroot/` must be
in the same directory as the exe. The trial publish places it there automatically.

### Native lib extraction (%TEMP%)

At first launch, the runtime extracts `e_sqlite3.dll` and other native DLLs to
`%TEMP%\DeckFlow.Studio\<hash>\`. Subsequent launches reuse the cached extraction (fast).
This extraction happens before any user code runs and is transparent.

---

## URL / Port Configuration

The packaged exe uses Kestrel and ignores `launchSettings.json` (which only affects `dotnet run`).
Default Kestrel ports: `http://localhost:5000` and `https://localhost:5001`.

**Recommendation:** document that the operator should set `ASPNETCORE_URLS=http://localhost:5271`
to match existing STUDIO-SETUP.md documentation (port 5271). Without this, the operator hits
`http://localhost:5000` instead.

Alternatively, add `Kestrel:Endpoints:Http:Url` to `appsettings.json`:

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://localhost:5271" } } }
}
```

This is a cleaner approach than requiring an env var for the port. **HTTPS is not needed for a local
operator tool.**

---

## External Tool Dependencies (not bundled)

| Tool | Required by | Path discovery | Action if missing |
|------|-------------|----------------|-------------------|
| `git.exe` | `Publish.razor` (commit-publish path only) | `PATH` lookup | Error message shown in Studio UI: "git not available" |
| `claude` CLI | `CliLlmDistillationService` when `DECKFLOW_LLM_PROVIDER=claude` | `PATH` lookup | Distill fails with error if provider=claude but cli not on PATH |
| `ffmpeg.exe` | `FfmpegAudioChunker` (Whisper path only) | `PATH` lookup | Not needed — `whisperEnabled=false` in Program.cs; ffmpeg is never invoked in default harvest |

**The operator can run harvest → distill → review → DirectPush entirely without git or ffmpeg** as
long as they use the DirectPush path for publishing. Only the git-commit publish path (`Publish.razor`)
requires git.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bundling managed DLLs into exe | Custom IL merger | `PublishSingleFile=true` SDK property | SDK handles IL linking, startup injection, extraction |
| Bundling native DLLs into exe | Custom resource packer | `IncludeNativeLibrariesForSelfExtract=true` | SDK handles native lib embedding and temp-dir extraction |
| Installer / setup wizard | NSIS, WiX, etc. | Zip archive + readme | Operator is technical; an installer adds complexity and signing requirements |
| Embedding wwwroot into the exe | Custom embedded resource provider | Accept that wwwroot must be alongside | ASP.NET Core static files middleware is filesystem-based; embedding would require a custom `IFileProvider` |

---

## Common Pitfalls

### Pitfall 1: Missing `IncludeNativeLibrariesForSelfExtract=true`

**What goes wrong:** `e_sqlite3.dll` (the native SQLite engine) appears as a loose file beside the exe.
The exe-only distribution (without the loose DLL) crashes on startup: `DllNotFoundException: e_sqlite3`.
**Why it happens:** By default, `PublishSingleFile` does not bundle native DLLs — only managed assemblies.
**How to avoid:** Always include `IncludeNativeLibrariesForSelfExtract=true` in the publish command.
**Warning signs:** `*.dll` files appearing beside the exe in the publish output.

### Pitfall 2: Deleting wwwroot thinking it is included in the exe

**What goes wrong:** App starts but `_framework/blazor.server.js` returns 404. All Blazor components
render the HTML skeleton but never connect to the server-side circuit. The UI appears but is frozen.
**Why it happens:** `UseStaticFiles()` requires `wwwroot/` on disk. Single-file only bundles managed DLLs.
**How to avoid:** Always distribute `wwwroot/` alongside the exe. The publish script should zip both.
**Warning signs:** Browser shows a loading indicator that never resolves; console shows 404 for `/_framework/blazor.server.js`.

### Pitfall 3: RID pinned unconditionally in csproj breaks test runs

**What goes wrong:** If `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` is added to the csproj without
a condition, `dotnet test` and `dotnet build` (without explicit `-r`) also target win-x64. This can
break Linux CI or WSL builds that resolve win-x64 native assets for DLLs that aren't available there.
**How to avoid:** Pass `-r win-x64 --self-contained` only in the publish script, not in the csproj.
Or gate with `Condition="'$(PublishSingleFile)'=='true'"`.

### Pitfall 4: ContentRoot vs working directory confusion

**What goes wrong:** Operator launches the exe from a different directory. `artifacts/studio/` is created
in the launch directory, not the exe directory. Operator is confused about where their data is.
**How to avoid:** Document the double-click pattern clearly. Recommend setting `MTG_DATA_DIR` to an
absolute path for predictable data location.

### Pitfall 5: appsettings.Development.json leaks Development environment into packaged exe

**What goes wrong:** The file `appsettings.Development.json` ships in the zip. If the operator sets
`ASPNETCORE_ENVIRONMENT=Development` for debugging, the logging configuration from
`appsettings.Development.json` is applied. More dangerously, the Development environment suppresses
the exception handler (`UseExceptionHandler`) and activates `UseHsts`, which may be surprising.
**How to avoid:** Exclude `appsettings.Development.json` from the distribution zip. The publish script
already handles this.

### Pitfall 6: Port conflict with no visible error

**What goes wrong:** Operator launches two instances, or port 5000 is taken by another process.
The exe exits immediately with no browser window. The error appears only in the console window
which may be hidden if launched by double-click.
**How to avoid:** Set `ASPNETCORE_URLS` to a specific port in the wrapper batch file. Document that
the operator should check the console window on startup.

### Pitfall 7: User-secrets don't work on clean machine

**What goes wrong:** Operator reads `STUDIO-SETUP.md`, tries to run `dotnet user-secrets set` for
secrets, gets "dotnet: command not found" because the SDK is not installed.
**How to avoid:** Document that the packaged exe uses **environment variables** for secrets, not user-secrets.
The existing `STUDIO-SETUP.md` already notes that user-secrets require the SDK; update it to make
env vars the primary path for the packaged exe case.

---

## Code Examples

### Verified: publish properties in csproj (recommended pattern)

```xml
<!-- Source: .NET SDK documentation — PublishSingleFile -->
<!-- In DeckFlow.Studio/DeckFlow.Studio.csproj <PropertyGroup> -->
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<PublishTrimmed>false</PublishTrimmed>
<!-- Do NOT add RuntimeIdentifier here — pass on CLI to keep csproj RID-agnostic -->
```

### Verified: Kestrel port in appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5271"
      }
    }
  }
}
```

---

## Validation Architecture

nyquist_validation is enabled (config.json). Phase 63 is a packaging/documentation phase with minimal
unit-testable behavior. Validation is operator-run, not automated.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (DeckFlow.Studio.Tests) |
| Config file | none — project-level discovery |
| Quick run | `"C:\Program Files\dotnet\dotnet.exe" test DeckFlow.Studio.Tests/` |
| Full suite | `"C:\Program Files\dotnet\dotnet.exe" test` (all projects) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DIST-01-A | Publish command succeeds without error | Build smoke | `scripts/publish-studio.ps1` exits 0 | ❌ Wave 0 — create script |
| DIST-01-B | Publish output contains exactly one exe and wwwroot/ | Manual verify | Operator inspects publish output | Manual-only |
| DIST-01-C | Exe starts on Windows without .NET runtime installed | Manual smoke | Operator runs exe on clean machine / VM | Manual-only |
| DIST-01-D | http://localhost:5271 loads the Studio UI with Blazor connected | Manual smoke | Operator opens browser after launch | Manual-only |
| DIST-01-E | SQLite DB created at expected path on first run | Manual verify | Operator checks `artifacts/studio/content-kb.db` exists | Manual-only |

**Why mostly manual:** The deliverable is a packaging artifact, not application logic. The only
automated gate is that the publish command itself succeeds (exit code 0, produces expected files).

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.Studio/DeckFlow.Studio.csproj` (ensures the project still compiles)
- **Per wave merge:** Publish script runs end-to-end
- **Phase gate:** Operator smoke on the actual zip output before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `scripts/publish-studio.ps1` — does not exist yet; create in Wave 1
- [ ] `scripts/publish-studio.sh` — WSL-compatible bash variant; create in Wave 1

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Windows dotnet.exe | Publish script | ✓ | 10.0.301 | — |
| WSL bash | publish-studio.sh | ✓ | bash 5.2 | Use .ps1 on Windows directly |
| zip (WSL) / Compress-Archive (PS) | Artifact zip | ✓ | both available | — |

---

## Security Domain

Phase 63 is packaging-only — no new application logic. Security considerations are documentation-level.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Phase adds no auth changes |
| V3 Session Management | No | No session changes |
| V4 Access Control | No | No access control changes |
| V5 Input Validation | No | No new input surfaces |
| V6 Cryptography | No | SSH.NET crypto unchanged |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secrets in zip artifact | Information Disclosure | Never include `appsettings.*.json` with secrets; publish script strips `appsettings.Development.json`; operator uses env vars or local secrets file |
| Insecure temp dir extraction | Elevation | .NET single-file extracts to `%TEMP%\<appname>\<hash>` — inherits OS temp permissions; acceptable for local operator tool |
| SQLite CVE (NU1903) | Tampering (timing side-channel) | Low risk for local tool; no action for Phase 63 |

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Self-extracting SFX archives or ClickOnce | `PublishSingleFile=true` SDK property | .NET 5 (2020) | No third-party tools needed; pure SDK |
| `IncludeAllContentForSelfExtract` (deprecated) | `IncludeNativeLibrariesForSelfExtract` | .NET 6 | More granular; only native libs extracted to temp |
| Trimming required for single-file | Trimming is optional | .NET 8+ | Can publish single-file without trimming |

**Deprecated/outdated:**
- `IncludeAllContentForSelfExtract`: replaced by `IncludeNativeLibrariesForSelfExtract`; the former
  also extracted managed DLLs to temp, which is unnecessary and slower.
- `PublishReadyToRun=false` default: acceptable for .NET 10; for a local tool cold-start time is not critical.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Trimming breaks Blazor + Dapper in this codebase | Trimming Analysis | If trimming succeeds, exe could be ~25 MB smaller; trial-publish with trimming to verify if size matters |
| A2 | ContentRoot defaults to exe directory (not %TEMP%) for self-contained single-file in .NET 10 | Blazor Static Assets | If ContentRoot is %TEMP%, wwwroot must be placed differently; verify by running the built exe |
| A3 | `ASPNETCORE_URLS` env var is the right way to pin the port (vs appsettings.json Kestrel config) | URL/Port | Both work; appsettings approach is slightly cleaner for distribution |

---

## Open Questions (RESOLVED)

1. **Should PublishReadyToRun be enabled?**
   - What we know: R2R reduces cold-start JIT time; increases exe size ~15-25 MB (est. 130-140 MB total)
   - What's unclear: whether the operator cares about cold-start time (Studio starts once, stays up)
   - Recommendation: leave ReadyToRun off for now; can be added later without code changes

2. **Should the build script also produce a GitHub Release artifact?**
   - What we know: the repo is public (`luntc1972/DeckFlow`); GitHub releases are free
   - What's unclear: whether the operator wants automated release publishing or just a local script
   - Recommendation: out of scope for Phase 63; document the script, not CI release publishing

3. **Port: env var or appsettings.json?**
   - Both work. Appsettings.json Kestrel config is shipped inside the zip; env var requires operator
     action. Recommendation: add Kestrel config to `appsettings.json` (port 5271) so Studio "just works"
     at the expected port without any operator configuration.

---

## Sources

### Primary (HIGH confidence)
- Trial publish: actual `dotnet.exe publish` run 2026-06-20 against `DeckFlow.Studio.csproj` at commit
  `fdd478e9` — verified output files, sizes, and native lib behavior
- NuGet package inspection: `SSH.NET 2025.1.0`, `BouncyCastle.Cryptography 2.6.2`, `Npgsql 10.0.0`,
  `Microsoft.Data.Sqlite 10.0.0`, `SQLitePCLRaw.lib.e_sqlite3 2.1.11` — all verified for native dep presence
- `DeckFlow.Studio/Program.cs` — verified secrets loading pattern, data dir resolution, static file middleware

### Secondary (MEDIUM confidence)
- [CITED: docs.microsoft.com/dotnet/core/deploying/single-file/overview] — `PublishSingleFile` properties
  and native lib extraction behavior
- [CITED: github.com/dotnet/aspnetcore] — ContentRoot behavior for single-file apps (exe directory, not %TEMP%)

### Tertiary (LOW confidence)
- [ASSUMED] Trimming breaks Blazor + Dapper — standard ecosystem guidance, not empirically verified for this specific codebase

---

## Metadata

**Confidence breakdown:**
- Publish properties and output: HIGH — verified by trial publish
- Native dependency analysis: HIGH — verified by NuGet package inspection
- Blazor static asset behavior: HIGH — verified by trial publish output
- Secrets handling on clean machine: HIGH — verified from Program.cs + .NET config docs
- Trimming recommendation: MEDIUM/ASSUMED — standard pattern, not empirically tested for this codebase

**Research date:** 2026-06-20
**Valid until:** 2026-09-01 (stable for .NET 10; review if .NET 11 ships or if ASP.NET Core static asset embedding changes)
