# Phase 41: Studio Scaffold + Secrets Wiring - Research

**Researched:** 2026-06-13
**Domain:** .NET 10 Blazor Server project scaffolding, user-secrets wiring, .gitignore hardening, solution/Dockerfile isolation
**Confidence:** HIGH — all claims verified against codebase files, live `dotnet new --dry-run`, and milestone research documents (STACK.md, ARCHITECTURE.md, PITFALLS.md) produced 2026-06-13

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| STU-01 | Standalone Blazor Server project in DeckFlow.sln, references DeckFlow.Core, launches via `dotnet run` decoupled from the deployed site | Template command confirmed, solution add command confirmed, ProjectReference pattern confirmed from DeckFlow.Web.csproj |
| STU-02 | Operator secrets stored via `dotnet user-secrets`; never written to any git-tracked file; `.gitignore` excludes Studio data/config artifacts | user-secrets commands verified, secrets.json path confirmed outside repo, gitignore gaps identified from dry-run output |
| STU-03 | Secrets and connection strings never appear in logs or UI output; referenced by name only | Logging pattern documented from existing codebase; `PostgresConnectionStringNormalizer` location confirmed |
</phase_requirements>

---

## Summary

Phase 41 creates the `DeckFlow.Studio` project — a standalone local Blazor Server app that will serve as the UI host for the v1.7 Local Harvest & Publish Studio. This is a scaffold-and-wire phase: no feature logic, no UI pages beyond a home screen confirming the app runs, and no Core modifications. The three deliverables are (1) the project on disk in the solution, (2) user-secrets initialized with the prod connection string having a safe home, and (3) `.gitignore` hardened so no Studio secrets or local-config artifacts can enter git history.

The milestone research (STACK.md, ARCHITECTURE.md, PITFALLS.md) already resolved all architectural questions. This document distills only what the planner needs to turn those decisions into concrete tasks: exact commands, file names that need `.gitignore` entries, csproj properties to set manually after scaffolding, and the specific SC4 Dockerfile constraint.

**Primary recommendation:** Scaffold with `dotnet new blazorserver`, manually retarget the csproj to `net10.0` (the template defaults to net6.0 on this SDK install), add `<UserSecretsId>` via `dotnet user-secrets init`, add `.gitignore` entries for all template-generated secret-capable files before a single `git add` is run.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Blazor Server host | DeckFlow.Studio | — | New standalone project; localhost only; never deployed |
| user-secrets initialization | Developer OS (AppData) | DeckFlow.Studio.csproj (GUID only) | secrets.json lives outside repo tree by design |
| .gitignore coverage | Repo root `.gitignore` | — | Single .gitignore at repo root covers all subdirs |
| DI wiring of Core stores | DeckFlow.Studio/Program.cs | DeckFlow.Core | Studio is the composition root for its DI container |
| launchSettings port | DeckFlow.Studio/Properties/launchSettings.json | — | Separate from DeckFlow.Web (port 5173/7173); Studio gets its own port |
| Dockerfile restore scope | Dockerfile (unchanged) | — | Stays `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj`; SC4 is a hard constraint |

---

## Standard Stack

### Core (what this phase actually uses)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core + Blazor Server | net10.0 (in-solution) | UI host scaffolded via `dotnet new blazorserver` | Milestone decision locked in STACK.md; lowest friction for localhost tool with real-time UI |
| Microsoft.Extensions.Configuration.UserSecrets | 10.x (transitively available in any SDK.Web project) | Local secret storage for prod connection string | Keeps secrets outside repo; no new package — included in `Microsoft.NET.Sdk.Web` SDK |
| DeckFlow.Core | (project reference) | All domain stores, models, and utilities | Single source of truth for KB logic |

### Supporting (available at no cost, used in later phases)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Serilog.AspNetCore | 9.0.0 (already in DeckFlow.Web) | Structured logging | Wire at Studio startup to match DeckFlow.CLI log format; pulls in via Web reference pattern |
| Microsoft.Data.Sqlite | 10.0.0 (already in DeckFlow.Core) | Local SQLite store access | Comes in transitively from Core reference |
| Npgsql | 10.0.0 (already in DeckFlow.Core) | Prod Postgres access | Comes in transitively from Core reference |

**No new NuGet packages are required for this phase.** All dependencies arrive transitively through the `<ProjectReference>` to `DeckFlow.Core`. [VERIFIED: codebase — DeckFlow.Core.csproj and DeckFlow.Web.csproj read 2026-06-13]

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `blazorserver` template | `blazor` (Blazor Web App) | `blazor` defaults to auto/server render mode hybrid in .NET 8+; `blazorserver` is the explicit server-only mode matching our requirement; simpler startup for localhost tool |
| `dotnet user-secrets` | `appsettings.local.json` | local json files risk accidental commit; user-secrets is stored in OS AppData outside repo tree |

---

## Package Legitimacy Audit

No new NuGet packages are installed in this phase. All packages are already present in the solution. [VERIFIED: codebase] Skipping audit table.

**Packages removed due to slopcheck:** none (no new packages)
**Packages flagged as suspicious:** none

---

## Architecture Patterns

### System Architecture Diagram

```
[Developer: dotnet run --project DeckFlow.Studio]
    ↓
DeckFlow.Studio (Blazor Server, localhost only)
  Program.cs — DI wiring, user-secrets, ConfigurationBuilder
  Components/ — Blazor .razor pages (stub home page this phase)
    ↓ <ProjectReference>
DeckFlow.Core
  Content stores, models, orchestrator (unchanged this phase)

DeckFlow.Web (unchanged — separate process, Render-deployed)
DeckFlow.CLI (unchanged — separate executable)

Dockerfile
  restore → DeckFlow.Web/DeckFlow.Web.csproj  ← UNCHANGED (SC4 hard constraint)
  publish → DeckFlow.Web only
  Studio project NOT referenced in Dockerfile
```

### Recommended Project Structure

```
DeckFlow.Studio/
  DeckFlow.Studio.csproj           ← net10.0, Sdk="Microsoft.NET.Sdk.Web", <UserSecretsId>
  Program.cs                       ← ConfigurationBuilder + AddUserSecrets<Program>() + DI stub
  _Imports.razor
  App.razor
  Components/
    Layout/
      MainLayout.razor
      NavMenu.razor
    Pages/
      Home.razor                   ← stub: "Studio running." (replaces template Index.razor)
  appsettings.json                 ← Kestrel/logging config only; NO secrets
  appsettings.Development.json     ← logging override only; NO secrets
  Properties/
    launchSettings.json            ← http profile: localhost:<port>, launchBrowser: false
  wwwroot/
    css/site.css                   ← template default; trim to minimal
```

Files generated by `dotnet new blazorserver` that MUST be stripped or replaced:

- `Data/WeatherForecast.cs`, `Data/WeatherForecastService.cs` — delete
- `Pages/Counter.razor`, `Pages/FetchData.razor` — delete
- `Shared/SurveyPrompt.razor` — delete
- `Pages/Index.razor` — replace with stub Home page

### Pattern 1: User-Secrets Initialization

**What:** `dotnet user-secrets init` writes a `<UserSecretsId>` GUID into the `.csproj`. Secrets are stored in `%APPDATA%\Microsoft\UserSecrets\<GUID>\secrets.json` on Windows — outside the repo tree, never committed.

**When to use:** Once during scaffold. The GUID in the csproj is public and safe to commit.

```bash
# Run from repo root — creates <UserSecretsId> in DeckFlow.Studio.csproj
dotnet user-secrets init --project DeckFlow.Studio/DeckFlow.Studio.csproj

# Set the prod connection string (operator does this once; never committed)
dotnet user-secrets set "Studio:ProdConnectionString" "postgres://..." \
    --project DeckFlow.Studio/DeckFlow.Studio.csproj

# Set the Render SSH address (needed for Phase 47 direct-push)
dotnet user-secrets set "Studio:RenderSshAddress" "srv-xxx@ssh.oregon.render.com" \
    --project DeckFlow.Studio/DeckFlow.Studio.csproj

# Verify (planner: this is SC2's verification command)
dotnet user-secrets list --project DeckFlow.Studio/DeckFlow.Studio.csproj
```

The `<UserSecretsId>` GUID appears in the csproj. No secrets appear in any tracked file. [VERIFIED: Microsoft user-secrets docs via STACK.md citation; pattern consistent with `AddUserSecrets<Program>()` in ASP.NET Core]

### Pattern 2: ConfigurationBuilder in Program.cs

**What:** Wire `AddUserSecrets<Program>()` before `AddEnvironmentVariables()`. This means env vars can override user-secrets in CI (where user-secrets are unavailable).

```csharp
// Source: STACK.md (verified against Microsoft docs)
// DeckFlow.Studio/Program.cs
var builder = WebApplication.CreateBuilder(args);

// User-secrets for local dev; env vars override in CI
builder.Configuration
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Read secrets — NEVER log the value
var prodConnStr = builder.Configuration["Studio:ProdConnectionString"];
var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);
builder.Services.AddSingleton(new StudioConfig(
    ProdConnectionString: prodConnStr,
    IsProdConfigured: isProdConfigured));

// STU-03: log presence only
Log.Information("Studio prod connection: {Status}",
    isProdConfigured ? "configured" : "not configured");
```

### Pattern 3: launchSettings.json for Studio

**What:** The Studio needs its own localhost port, distinct from DeckFlow.Web (5173/7173). Auto-browser launch must be disabled (MEMORY: "never auto-launch DeckFlow web; ask user to start" — applies equally to Studio).

```json
// Source: DeckFlow.Web/Properties/launchSettings.json pattern (verified 2026-06-13)
// DeckFlow.Studio/Properties/launchSettings.json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5271",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Port 5271 is chosen to avoid collision with DeckFlow.Web (5173). [ASSUMED — port 5271 not in use by any project verified in codebase; planner should verify no conflict]

**launchBrowser: false is mandatory** — Studio should log its URL and let the operator open it. No `DevelopmentBrowserLauncher` equivalent needed.

### Pattern 4: csproj Properties to Set Manually After Scaffolding

The `blazorserver` template defaults to net6.0 on this machine's SDK install. The csproj must be updated immediately after scaffold:

```xml
<!-- DeckFlow.Studio/DeckFlow.Studio.csproj — properties to set/verify post-scaffold -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>   <!-- MUST change from net6.0 default -->
    <Nullable>enable</Nullable>                   <!-- MUST add: project convention -->
    <ImplicitUsings>enable</ImplicitUsings>       <!-- MUST add: project convention -->
    <UserSecretsId><!-- GUID from dotnet user-secrets init --></UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DeckFlow.Core\DeckFlow.Core.csproj" />
  </ItemGroup>
</Project>
```

[VERIFIED: template dry-run confirmed net6.0 default; DeckFlow.Web.csproj pattern confirmed as the reference for net10.0/Nullable/ImplicitUsings]

### Pattern 5: Solution Add Command

```bash
# Run from repo root
dotnet sln DeckFlow.sln add DeckFlow.Studio/DeckFlow.Studio.csproj
```

After this, `DeckFlow.sln` will have 6 projects. `dotnet build DeckFlow.sln` must succeed. `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` (the Dockerfile command) must be unchanged and must NOT pull in Studio. [VERIFIED: Dockerfile line 29 confirmed as project-scoped; ARCHITECTURE.md Q6 confirmed no change needed]

### Anti-Patterns to Avoid

- **Changing Dockerfile restore to solution-level:** `dotnet restore DeckFlow.sln` would pull Studio/Blazor workload packages into the container build. The Dockerfile MUST stay `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj`. This is SC4 and an explicit "Out of Scope" entry in REQUIREMENTS.md.
- **Creating appsettings.local.json with connection strings:** Use user-secrets. The template generates `appsettings.Development.json` — this file stays but must contain ONLY logging config, never secrets.
- **launchBrowser: true:** Studio is a local tool; auto-launch adds complexity and was explicitly prohibited for DeckFlow.Web.
- **Logging the connection string:** Log `"configured"` / `"not configured"` only. `PostgresConnectionStringNormalizer.Normalize()` exists in Core but its output must also never be passed to a logger.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Local secret storage | Custom encrypted JSON, `.env` file | `dotnet user-secrets` (`AddUserSecrets<Program>()`) | OS-managed path outside repo; zero new deps; consistent with .NET 10 dev pattern |
| Config layering | Manual config file merging | `WebApplication.CreateBuilder` + `AddUserSecrets` + `AddEnvironmentVariables` | Standard .NET 10 pattern; env vars override user-secrets automatically (CI compat) |

**Key insight:** There is nothing complex to hand-roll in this phase. The value is in doing the standard things in the right order before any other Studio work begins.

---

## Common Pitfalls

### Pitfall 1: Template Targets net6.0 — Build Will Fail Against net10.0 Core

**What goes wrong:** `dotnet new blazorserver` on this machine generates `<TargetFramework>net6.0</TargetFramework>`. `dotnet build DeckFlow.sln` will attempt to build Studio against net6.0 and fail to resolve `DeckFlow.Core` (net10.0). The planner must include an explicit step to set `net10.0` in the csproj before the first build.

**Why it happens:** The `blazorserver` template in the installed SDK feature pack predates .NET 10. The `-f` option only offers net6.0/net5.0/netcoreapp3.1. [VERIFIED: `dotnet new blazorserver --help` dry-run 2026-06-13]

**How to avoid:** Immediately after `dotnet new blazorserver`, before `dotnet sln add`, edit the csproj to set `net10.0`, add `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. Add `<ProjectReference>` to Core. Then run `dotnet build DeckFlow.sln` to confirm.

**Warning signs:** `error NU1202: Package DeckFlow.Core ... is not compatible with net6.0`

### Pitfall 2: appsettings.Development.json Generated by Template — gitignore Gap

**What goes wrong:** The `blazorserver` template generates `DeckFlow.Studio/appsettings.Development.json`. The current `.gitignore` does NOT cover `appsettings.Development.json` for any project path — the existing entry `appsettings.Development.json` is not present in the repo's `.gitignore` at all (verified: only `scripts/run-web-uat.*` and path-specific ignores exist). If a developer adds the prod connection string to this file "just to test," `git add .` will stage it.

**Why it happens:** The `.gitignore` was written before Studio existed. The template generates the file unconditionally.

**How to avoid:** Add `.gitignore` entries BEFORE running `git add` on any Studio files. Required entries (see Code Examples section). The entry must be broad enough to cover both `appsettings.Development.json` and any future `appsettings.local.json` or `secrets.json` that might appear in the project tree.

**Warning signs:** `git status` shows `DeckFlow.Studio/appsettings.Development.json` as untracked (expected) — it should never appear as staged.

### Pitfall 3: Dockerfile COPY of DeckFlow.sln Copies Studio Project Name

**What goes wrong:** The Dockerfile already does `COPY DeckFlow.sln ./` at line 21, then copies only Core/Web/CLI csproj files. After adding Studio to the .sln, the copied `.sln` file will reference `DeckFlow.Studio`, but since no Studio `.csproj` is copied, `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` will still work (it only restores Web and its dependencies — Core, via transitive reference). However, if anyone later changes the restore command to `dotnet restore DeckFlow.sln`, it will fail because Studio's csproj was not COPY'd. Add a comment to the Dockerfile to document this constraint.

**Why it happens:** The restore is scoped by project path, not by solution membership. Studio is in the .sln but not referenced by Web, so the project-scoped restore is unaffected. [VERIFIED: Dockerfile lines 21-29; ARCHITECTURE.md Q6]

**How to avoid:** Do not change the Dockerfile. Add an inline comment on line 29: `# Restore only Web (and its Core/CLI deps) — Studio is local-only, not deployed`. This documents the constraint permanently.

**Warning signs:** SC4 verification: `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` in the Dockerfile context still succeeds.

---

## Code Examples

### .gitignore Entries Required (add to repo root .gitignore)

```gitignore
# DeckFlow.Studio — local-tool secrets and generated artifacts
# appsettings.Development.json is committed (logging config only) but
# any local override files must never enter git history
DeckFlow.Studio/appsettings*.local.json
DeckFlow.Studio/secrets.json
DeckFlow.Studio/appsettings.Local.json
```

Note: `DeckFlow.Studio/appsettings.Development.json` IS committed (contains only logging config). The entries above cover files that COULD carry secrets. The `<UserSecretsId>` GUID in the csproj is safe to commit.

### SC3 Verification Commands

```bash
# No secrets.json ever committed to any path in repo history
git log --all -- "**/secrets.json"

# No connection strings in tracked Studio files
grep -r "postgres\|password\|Host=" DeckFlow.Studio/

# user-secrets listing (the ONLY place the prod connection string may live)
dotnet user-secrets list --project DeckFlow.Studio/DeckFlow.Studio.csproj
```

### SC4 Verification

```bash
# Dockerfile restore path must be unchanged and must not pull Studio
dotnet restore DeckFlow.Web/DeckFlow.Web.csproj
# Must succeed with 0 errors; Studio packages must not appear in restore output
```

### SC1 Verification

```bash
dotnet run --project DeckFlow.Studio
# Browse to http://localhost:5271 (or whichever port launchSettings.json sets)
# Must render a Blazor page (even a stub "Studio running." message)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate `.sln` per tool | All projects in one `DeckFlow.sln` | Project standard (existing) | One `dotnet build` verifies all |
| `appsettings.*.json` for local secrets | `dotnet user-secrets` / env vars | .NET Core 2.0+ | Secrets outside repo tree |
| `launchBrowser: true` (template default) | `launchBrowser: false` | DeckFlow.Web pattern (v1.0) | Operator controls browser; no race conditions |

**Deprecated/outdated:**

- Template `net6.0` default: immediately superseded by manually setting `net10.0`.
- Template demo files (`WeatherForecast`, `Counter`, `FetchData`): delete on scaffold; they add noise and no value.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Port 5271 chosen for Studio launchSettings.json does not conflict with any existing project | Pattern 3 | Port conflict at `dotnet run`; trivially fixable by choosing another port |
| A2 | `blazorserver` template's `appsettings.Development.json` contains only logging config (no secrets) by default | Pitfall 2 | If template changed, file might contain something unexpected; always review before first commit |

---

## Open Questions (RESOLVED)

> All three resolved with concrete recommendations, implemented by 41-01-PLAN.md (port 5271, console-only logging, single Home.razor status stub).

1. **RESOLVED — Port selection for Studio**
   - What we know: DeckFlow.Web uses 5173 (http) and 7173 (https); `launchSettings.json` verified.
   - What's unclear: Whether any other local service on the developer's machine uses 5271.
   - Recommendation: Default to 5271; planner should note it is configurable and not a hard requirement.

2. **Serilog wiring in Phase 41 vs later**
   - What we know: Serilog.AspNetCore is in DeckFlow.Web (not in Core); DeckFlow.CLI uses Serilog.Sinks.File directly.
   - What's unclear: Whether Phase 41 should wire full Serilog (with file sink) or just use the default console logger for the scaffold.
   - Recommendation: Wire only `Console` logging in Phase 41 (`builder.Logging.AddConsole()`). Full Serilog file-sink wiring can wait for Phase 45 when Studio starts doing real work. This keeps Phase 41 tight.

3. **Blazor home page content**
   - What we know: SC1 requires "a first page rendered."
   - What's unclear: Whether a minimal "Studio running. Secrets: configured/not configured." stub is sufficient or if the planner should include a minimal nav shell.
   - Recommendation: A single `Home.razor` page displaying the secrets status is sufficient for SC1 and provides a useful diagnostic for secrets wiring.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All tasks | Yes | 10.x (confirmed: `dotnet new blazorserver` ran successfully) | — |
| `dotnet user-secrets` CLI | STU-02 | Yes | Built into .NET SDK | — |
| `git` CLI | SC3 verification | Yes | (confirmed: git repo active) | — |

**Missing dependencies with no fallback:** None.

---

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json` — section required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (existing in DeckFlow.Core.Tests, DeckFlow.Web.Tests) |
| Config file | No new test project for Phase 41 (scaffold only; no test infrastructure gap) |
| Quick run command | `dotnet build DeckFlow.sln` (build = primary gate for scaffold phase) |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj && "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| STU-01 | `dotnet run --project DeckFlow.Studio` starts Blazor Server reachable at localhost | smoke (manual) | `dotnet build DeckFlow.sln` (automated gate); manual browser verification for SC1 | N/A — new project |
| STU-02 | `dotnet user-secrets list` is the only place prod connection string exists | manual-only | `git log --all -- "**/secrets.json"` returns nothing; `grep -r "postgres" DeckFlow.Studio/` returns nothing | N/A — new project |
| STU-03 | Connection string never appears in log output | manual-only | Review Studio startup log output after `dotnet run`; assert "configured"/"not configured" only | N/A — new project |

**Note on test project:** Phase 41 is a scaffold phase with no domain logic. No unit tests are required. SC1–SC4 are all verified by build commands and manual spot-checks. The existing Core.Tests and Web.Tests suites must pass unchanged — this is the regression gate confirming Studio's addition to the solution did not break anything.

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln` (0 errors, 0 new warnings)
- **Per wave merge:** `dotnet build DeckFlow.sln` + existing test suites green
- **Phase gate:** SC1 (browser), SC2 (`user-secrets list`), SC3 (grep/git-log), SC4 (`dotnet restore DeckFlow.Web/DeckFlow.Web.csproj`)

### Wave 0 Gaps

None — Phase 41 has no unit tests to write. The phase IS the Wave 0 for the Studio track.

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Studio is single-operator localhost; no auth |
| V3 Session Management | No | Blazor Server uses SignalR circuit; no custom session |
| V4 Access Control | No | Localhost only; `applicationUrl: http://localhost:5271` |
| V5 Input Validation | No | No user input in Phase 41 (stub page only) |
| V6 Cryptography | Yes | `dotnet user-secrets` stores secrets via OS credential manager; no hand-rolled crypto |

### Known Threat Patterns for this Phase

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secret in git history | Information Disclosure | `.gitignore` entries before first `git add`; `dotnet user-secrets` for all secret values |
| Connection string in logs | Information Disclosure | Log `"configured"/"not configured"` only; never pass connection string to any `ILogger` method |
| Studio exposed on LAN | Elevation of Privilege | `applicationUrl: http://localhost:5271` — localhost bind only; no `0.0.0.0` bind |

---

## Sources

### Primary (HIGH confidence)

- `DeckFlow.sln` (read 2026-06-13) — confirmed 5 existing projects; no Studio entry yet
- `Dockerfile` (read 2026-06-13) — confirmed `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` on line 29; confirmed project-scoped restore
- `DeckFlow.Web/DeckFlow.Web.csproj` (read 2026-06-13) — confirmed `net10.0`, `Nullable enable`, `ImplicitUsings enable`, `<ProjectReference>` to Core pattern
- `DeckFlow.Web/Properties/launchSettings.json` (read 2026-06-13) — confirmed `launchBrowser: false` pattern; port 5173 (http)/7173 (https) in use by Web
- `.gitignore` (read 2026-06-13) — confirmed current entries; confirmed `appsettings.Development.json` is NOT currently ignored
- `.planning/config.json` (read 2026-06-13) — confirmed `nyquist_validation: true`, `commit_docs: true`
- `dotnet new blazorserver --dry-run` (run 2026-06-13) — confirmed exact file list generated; confirmed `appsettings.Development.json` is generated; confirmed `-f` option defaults to net6.0
- `.planning/research/STACK.md` (read 2026-06-13) — HIGH confidence; user-secrets pattern, `AddUserSecrets<Program>()`, port/launchBrowser guidance
- `.planning/research/ARCHITECTURE.md` (read 2026-06-13) — HIGH confidence; Q6 (Dockerfile implication), recommended project structure, csproj properties
- `.planning/research/PITFALLS.md` (read 2026-06-13) — HIGH confidence; Pitfall 3 (secret leakage), security mistakes table

### Secondary (MEDIUM confidence)

- `.planning/ROADMAP.md` Phase 41 section — SC1–SC4 verbatim (locked success criteria)
- `.planning/REQUIREMENTS.md` STU-01, STU-02, STU-03 — requirement text
- `.planning/STATE.md` — Decisions section confirming Studio binds to localhost only, no auto-browser

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all patterns confirmed in existing csproj files
- Architecture: HIGH — Dockerfile constraint verified line-by-line; template behavior confirmed via dry-run
- Pitfalls: HIGH — gitignore gap and net6.0 default confirmed by running actual tool; not training-data assumptions

**Research date:** 2026-06-13
**Valid until:** 2026-07-13 (stable domain; only risk is .NET SDK template updates which are infrequent)
