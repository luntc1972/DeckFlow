# 41-01 Summary — Studio Scaffold + Secrets Wiring

**Status:** Code complete (Tasks 1–2 done, committed). Task 3 operator runtime checkpoint deferred — see Outstanding.
**Commit:** `7ca2248` feat(studio): scaffold DeckFlow.Studio Blazor Server project (41-01)
**Branch:** v1.7
**Requirements:** STU-01, STU-02, STU-03
**Execution:** Codex (gpt-5.4 medium) implemented; Claude reviewed + committed.

## What shipped

Standalone `DeckFlow.Studio` Blazor Server project (net10.0), referencing `DeckFlow.Core`, in `DeckFlow.sln` (6 projects). Builds clean: **0 errors / 0 warnings**.

- **csproj**: net10.0, Nullable+ImplicitUsings, `<UserSecretsId>` GUID, ProjectReference→Core, EXACTLY two PackageReferences (Serilog.AspNetCore 9.0.0 + Serilog.Sinks.Console 6.0.0 — in-solution reuse, NOT new deps). No Serilog.Sinks.File (deferred to Phase 45), no other Web packages.
- **Program.cs**: composition root mirroring DeckFlow.Web Serilog bootstrap, console-only (`UseSerilog` + `Log.Fatal`/`throw`/`Log.CloseAndFlush`). `AddUserSecrets` + `AddEnvironmentVariables`. Reads `Studio:ProdConnectionString` into a LOCAL var only → computes presence bool. Logs `"Studio prod connection: {Status}"` (configured/not configured) — value never logged.
- **StudioConfig.cs**: `sealed record StudioConfig(bool IsProdConfigured)` — presence flag only, no connection string member (HIGH-2/STU-03).
- **Home.razor** (`Pages/Home.razor`, `@page "/"`): "Studio is running." + presence-only status. Injects StudioConfig.
- **launchSettings.json**: single http profile, `http://localhost:5271`, `launchBrowser: false`.
- **.gitignore**: appended Studio secret-capable patterns (`appsettings*.local.json`, `appsettings.Local.json`, `secrets.json`).
- **Dockerfile**: SC4 comment locking restore to project-scoped `DeckFlow.Web.csproj` (Studio excluded from container build). Comment-only.

## Plan deviations

1. **Routing layout**: `dotnet new blazorserver` emitted the classic `App.razor` + `Pages/_Host.cshtml` scaffold (not `Components/Pages`). Home page placed at `Pages/Home.razor` with `@page "/"` so the build is green and `/` renders. Functionally equivalent.
2. **Review-caught fixes (Claude review → Codex)**: removed `app.UseHttpsRedirection()` (http-only profile → runtime warning) and pruned dead `counter`/`fetchdata` nav links from NavMenu.razor (those template pages were deleted).
3. `dotnet sln add` dropped the " stable" suffix from the sln VisualStudioVersion line — tool artifact, harmless.

## Verification

| Criterion | Result | By |
|-----------|--------|-----|
| Build `DeckFlow.sln` 0E/0W | PASS | Claude/Codex |
| SC2 — no secrets in committed appsettings | PASS (logging-only) | Claude |
| SC3 — no secrets.json commits; no conn-string in tracked Studio files | PASS (`git log --all`, `git grep` both empty) | Claude |
| SC4 — Web-only restore does not pull Studio | PASS | Claude |
| EXACTLY 2 Serilog pkgs, no other / no file sink | PASS | Claude |
| SC1 — runtime render at localhost:5271 | DEFERRED — needs operator `dotnet run` | operator |
| STU-03 runtime — value absent from stdout | DEFERRED — needs operator run | operator |

## Outstanding (operator)

Task 3 human checkpoint was not run before commit (user authorized commit directly). Still to confirm at leisure:
- `dotnet user-secrets set Studio:ProdConnectionString` + `Studio:RenderSshAddress` (one-time)
- `dotnet run --project DeckFlow.Studio` → `http://localhost:5271` renders "Studio is running." + "Prod connection: configured"
- Confirm startup stdout shows `Studio prod connection: configured` with no value leak

Static + build-side criteria (SC2/SC3/SC4, build, package fence) are all verified. Only the runtime-render confirmation (SC1) remains, which is non-blocking for downstream Phase 42 (Orchestrator Extraction depends on the project existing + building, both true).
