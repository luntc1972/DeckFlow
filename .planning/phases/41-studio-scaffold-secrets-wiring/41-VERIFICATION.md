---
phase: 41-studio-scaffold-secrets-wiring
verified: 2026-06-16T20:22:00-06:00
status: human_needed
score: 5/6 must-haves verified (1 deferred to operator runtime)
overrides_applied: 0
human_verification:
  - test: "Run `dotnet run --project DeckFlow.Studio`, open http://localhost:5271, confirm 'Studio is running.' and 'Prod connection: configured' render."
    expected: "Blazor Server page at localhost:5271 renders correctly with prod connection presence status."
    why_human: "Requires running the server process and a live browser check. Cannot verify Blazor Server startup and page rendering with grep."
  - test: "Check startup stdout shows 'Studio prod connection: configured' with no connection string value present anywhere in the output."
    expected: "Log line reads exactly 'Studio prod connection: configured'; raw connection string value is absent from all stdout."
    why_human: "Requires observing live process stdout during `dotnet run`."
---

# Phase 41: Studio Scaffold + Secrets Wiring Verification Report

**Phase Goal:** Standalone DeckFlow.Studio Blazor Server project added to the solution; prod connection string lives in user-secrets only (never in repo); presence-only StudioConfig (no connection-string member); .gitignore hardened.
**Requirements:** STU-01, STU-02, STU-03
**Verified:** 2026-06-16T20:22:00-06:00
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DeckFlow.Studio Blazor Server project exists in DeckFlow.sln and references DeckFlow.Core | ✓ VERIFIED | `DeckFlow.sln` line 16 contains the Studio project entry; `DeckFlow.Studio.csproj` line 10: `<ProjectReference Include="..\DeckFlow.Core\DeckFlow.Core.csproj" />` |
| 2 | The prod Postgres connection string has exactly one safe home: OS user-secrets (never a tracked file) | ✓ VERIFIED | `git log --all -- "**/secrets.json"` returns empty; `git grep -niE "postgres\|password\|Host=" -- DeckFlow.Studio/` returns only documentation placeholders in `PROD-CONNECTION.md` (not credential values); appsettings files contain only logging config |
| 3 | StudioConfig carries only presence bool(s) — no connection string member | ✓ VERIFIED | `StudioConfig.cs:7`: `public sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured)` — two presence bools only; no `ProdConnectionString` or any string connection member. (IsScpConfigured was added in Phase 47; presence-only contract is preserved.) |
| 4 | The connection string value never enters logs or UI output — presence status only | ✓ VERIFIED | `Program.cs:38-39`: `prodConnStr` is a local variable used only to compute `isProdConfigured` bool; `Program.cs:118`: `Log.Information("Studio prod connection: {Status}", isProdConfigured ? "configured" : "not configured")` — value never passed to logger. `DirectPush.razor:514,683`: reads conn string as local variable, passes to `ProdStoreFactory.Create()` — no log call contains the value (grep for log calls + conn-string patterns returns empty). |
| 5 | .gitignore blocks Studio secret-capable files | ✓ VERIFIED | `.gitignore` lines 29-31: `DeckFlow.Studio/appsettings*.local.json`, `DeckFlow.Studio/appsettings.Local.json`, `DeckFlow.Studio/secrets.json` — all three required patterns present |
| 6 | Operator runs `dotnet run --project DeckFlow.Studio` and reaches a rendered Blazor Server page at http://localhost:5271 | ? UNCERTAIN | Runtime-only check — cannot verify without running the server. Deferred to human verification (operator SC1). |

**Score:** 5/6 truths verified (1 requires human runtime check)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Studio/DeckFlow.Studio.csproj` | net10.0, UserSecretsId GUID, ProjectReference→Core, Serilog packages | ✓ VERIFIED | net10.0 (line 3), UserSecretsId (line 6), Core ProjectReference (line 10), Serilog.AspNetCore 9.0.0 + Serilog.Sinks.Console 6.0.0 (lines 20-21). SSH.NET added by Phase 47 — not a Phase 41 artifact. |
| `DeckFlow.Studio/Program.cs` | UseSerilog bootstrap, AddUserSecrets, presence-only StudioConfig singleton, Log.Fatal+throw+CloseAndFlush | ✓ VERIFIED | UseSerilog (line 26), AddUserSecrets (line 36), StudioConfig singleton with presence bool only (line 53), Log.Fatal+throw+CloseAndFlush (lines 137-142) |
| `DeckFlow.Studio/StudioConfig.cs` | Presence-only sealed record, no connection string member | ✓ VERIFIED | `sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured)` — no string connection field. Doc comment explicitly states "never carries the underlying connection string or SSH values." |
| `DeckFlow.Studio/Pages/Home.razor` | `@page "/"`, renders "Studio is running." + presence flag only | ✓ VERIFIED | Lines 1-19: `@page "/"`, "Studio is running." text, injects `StudioConfig`, renders `Config.IsProdConfigured` as "configured"/"not configured" |
| `DeckFlow.Studio/Properties/launchSettings.json` | Single http profile, localhost:5271, launchBrowser false | ✓ VERIFIED | Single profile, `http://localhost:5271`, `launchBrowser: false`, no IIS/https profiles |
| `.gitignore` | Studio secret-capable file patterns ignored | ✓ VERIFIED | Three required patterns at lines 29-31 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `DeckFlow.Studio.csproj` | `DeckFlow.Core.csproj` | ProjectReference | ✓ WIRED | `csproj:10`: `<ProjectReference Include="..\DeckFlow.Core\DeckFlow.Core.csproj" />` |
| `Program.cs` | user-secrets store | `AddUserSecrets<Program>()` then local var only | ✓ WIRED | `Program.cs:36`: `builder.Configuration.AddUserSecrets<Program>().AddEnvironmentVariables()`; `Program.cs:38`: reads to local var `prodConnStr`; local var used only for bool computation, never stored in DI |
| `Pages/Home.razor` | `StudioConfig` | Injected singleton, reads `IsProdConfigured` | ✓ WIRED | `Home.razor:11`: `[Inject] private StudioConfig Config { get; set; }`; `Home.razor:17`: `_isProdConfigured = Config.IsProdConfigured;`; rendered at line 7 |
| `DeckFlow.Studio` | `DeckFlow.sln` | `dotnet sln add` | ✓ WIRED | `DeckFlow.sln:16` contains Studio project GUID and path |
| Dockerfile restore | DeckFlow.Web only | Project-scoped restore command | ✓ WIRED | `Dockerfile:29-31`: SC4 comment + `RUN dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` — solution-level restore not present |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `Home.razor` | `_isProdConfigured` | `StudioConfig.IsProdConfigured` singleton registered in `Program.cs:53` from local bool computed at startup | Bool computed from `!string.IsNullOrEmpty(prodConnStr)` where `prodConnStr` comes from user-secrets config | ✓ FLOWING — presence flag flows from user-secrets → local var → bool → StudioConfig singleton → Razor render |

### Behavioral Spot-Checks

Step 7b SKIPPED for SC1 (runtime page render) — requires live server process. Static analysis checks are complete; runtime behavior routed to human verification.

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| secrets.json never committed | `git log --all -- "**/secrets.json"` | Empty output | ✓ PASS |
| No conn-string value in tracked Studio files | `git grep -niE "postgres\|password\|Host=" -- DeckFlow.Studio/` | Only documentation placeholders in PROD-CONNECTION.md (no actual credentials) | ✓ PASS |
| appsettings files are logging-only | grep for postgres/password/Host= in both appsettings files | No matches (exit 1) | ✓ PASS |
| Dockerfile restore stays project-scoped | `grep -n "dotnet restore" Dockerfile` | Line 31: `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` only; SC4 comment on line 29 | ✓ PASS |
| Connection string value absent from all log templates | grep log calls + conn-string patterns in Program.cs | No matches | ✓ PASS |
| Runtime render at localhost:5271 | `dotnet run --project DeckFlow.Studio` + browser | Cannot test without live server | ? SKIP → human |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| STU-01 | 41-01 | Standalone local Blazor Server project in DeckFlow.sln, references Core, launches via `dotnet run` | ✓ SATISFIED (automated) / ? runtime requires human | Project in sln (line 16), Core ProjectReference, localhost:5271 profile. Runtime render deferred to operator. |
| STU-02 | 41-01 | Operator secrets stored via `dotnet user-secrets`, never in git-tracked file; .gitignore excludes local artifacts | ✓ SATISFIED | UserSecretsId in csproj, AddUserSecrets in Program.cs, three .gitignore patterns, git log + git grep confirm no secrets in history or tracked files |
| STU-03 | 41-01 | Secrets and connection strings never appear in logs or UI output | ✓ SATISFIED (static) / ? runtime requires human | prodConnStr local-only in Program.cs; Log.Information passes only "configured"/"not configured"; StudioConfig has no string member; Home.razor renders presence flag only |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `DeckFlow.Studio.csproj` | 22 | SSH.NET 2025.1.0 PackageReference present | ℹ️ Info | Added in Phase 47 (commit `a1d14ed`, bumped `a5c291c`) — not a Phase 41 defect. Phase 41 scaffold commit `7ca2248` contained only the two Serilog packages. Current state correctly reflects later-phase additions. |
| `DeckFlow.Studio/StudioConfig.cs` | 7 | `IsScpConfigured` second bool added | ℹ️ Info | Added in Phase 47 for SCP wiring. Presence-only contract is preserved — no string members. Not a Phase 41 defect. |

No TBD/FIXME/XXX debt markers found in Phase 41 artifacts. No stub return values. No empty implementations.

### Human Verification Required

#### 1. SC1 — Runtime Render at localhost:5271

**Test:** Set secrets with `dotnet user-secrets set "Studio:ProdConnectionString" "<your-prod-conn-string>" --project DeckFlow.Studio/DeckFlow.Studio.csproj`, then run `dotnet run --project DeckFlow.Studio`, open http://localhost:5271 in a browser.

**Expected:** Page renders with heading "DeckFlow Studio", paragraph "Studio is running.", and "Prod connection: configured".

**Why human:** Requires running the Blazor Server process and observing a browser-rendered page. Cannot verify Blazor Server startup, SignalR hub wiring, and component rendering with static analysis.

#### 2. STU-03 Runtime — No Secret in Startup Log

**Test:** In the `dotnet run` stdout from the check above, inspect all log lines.

**Expected:** Startup log contains `Studio prod connection: configured` (or `not configured`) and the raw connection string value is absent from all stdout lines.

**Why human:** Requires observing live process stdout. Static analysis confirms the code path routes only the presence string to the logger, but runtime confirmation is the contract.

### Gaps Summary

No automated gaps. All verifiable truths are VERIFIED by static codebase inspection. The two human verification items (SC1 runtime render + STU-03 runtime log confirmation) are the only open items. Both were explicitly flagged as operator-run in the 41-01-SUMMARY.md and are non-blocking for downstream phases (42–47 all completed successfully with Studio building and running as a foundation).

The csproj currently carries SSH.NET (Phase 47) and StudioConfig carries two presence bools (Phase 47) — these are legitimate later-phase evolutions, not Phase 41 regressions. Phase 41's own deliverables are all substantive, wired, and data-flowing.

---

_Verified: 2026-06-16T20:22:00-06:00_
_Verifier: Claude (gsd-verifier)_
