---
phase: 41
slug: studio-scaffold-secrets-wiring
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-13
---

# Phase 41 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Scaffold phase: build + manual spot-checks are the gates; no new unit tests required.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (existing — DeckFlow.Core.Tests, DeckFlow.Web.Tests; no new test project for scaffold) |
| **Config file** | none — no new test infrastructure for Phase 41 |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj && "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Estimated runtime** | build ~30s; full suites ~minutes (VSTest unreliable in WSL — push-and-watch CI is fallback) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build DeckFlow.sln` (0 errors, 0 new warnings)
- **After every plan wave:** `dotnet build DeckFlow.sln` + existing Core/Web suites green (regression gate — Studio addition must not break existing projects)
- **Before `/gsd:verify-work`:** Build clean + SC1–SC4 manual checks pass
- **Max feedback latency:** ~30 seconds (build)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 41-01-* | 01 | 1 | STU-01 | — | Studio project builds + launches Blazor Server on localhost | build + smoke | `dotnet build DeckFlow.sln`; manual: `dotnet run --project DeckFlow.Studio` → browser | ❌ W0 (new project) | ⬜ pending |
| 41-0x-* | — | — | STU-02 | T-41-01 (secret in git) | No secret in any tracked file; user-secrets only | manual | `git log --all -- "**/secrets.json"` empty; `grep -rn "postgres\|password\|Host=" DeckFlow.Studio/` empty in tracked files | ❌ W0 | ⬜ pending |
| 41-0x-* | — | — | STU-03 | T-41-02 (string in logs) | Log "configured"/"not configured" only | manual | Inspect `dotnet run` startup output — no connection string / no `{ConnectionString}` template | ❌ W0 | ⬜ pending |
| (regression) | — | — | SC4 | — | Docker restore path unchanged | build | `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` does not reference Studio | ✅ existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- None — Phase 41 has no domain logic, so no unit-test stubs to author. The phase IS the Wave 0 for the Studio track. Existing Core.Tests / Web.Tests suites serve as the regression gate.

*Existing infrastructure covers the regression check; new behavior (SC1–SC4) is build + manual-verified.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Blazor Server first page renders in browser | STU-01 / SC1 | Requires running server + browser; no headless assertion in scaffold | `dotnet run --project DeckFlow.Studio` → open `http://localhost:5271` → confirm a page renders |
| Prod connection string only in user-secrets | STU-02 / SC2 | Inspects OS-level secret store outside repo | `dotnet user-secrets list --project DeckFlow.Studio` shows the key; no appsettings file in project tree contains it |
| No secret leaks into git | STU-02 / SC3 | Cross-tree grep + git history scan | `git log --all -- "**/secrets.json"` empty; `grep -rn "postgres\|password\|Host=" DeckFlow.Studio/` empty in tracked files |
| Connection string never logged | STU-03 | Requires reading runtime stdout | Review `dotnet run` startup log — only "configured"/"not configured" appears |
| Docker build excludes Studio | SC4 | Confirms container restore isolation | `dotnet restore DeckFlow.Web/DeckFlow.Web.csproj` unchanged; Studio not pulled in |

---

## Validation Sign-Off

- [ ] All tasks have automated build verify or manual instructions
- [ ] Sampling continuity: build runs after every task commit
- [ ] Wave 0 covers all MISSING references (none for scaffold)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s (build)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
