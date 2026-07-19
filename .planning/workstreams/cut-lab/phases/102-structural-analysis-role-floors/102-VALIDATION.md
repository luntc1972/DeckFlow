---
phase: 102
slug: structural-analysis-role-floors
status: ready
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-19
---

# Phase 102 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Web.Tests, DeckFlow.Core.Tests) + Vitest (wwwroot/ts tests) + Playwright e2e |
| **Config file** | DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj, DeckFlow.Web/package.json, playwright.config |
| **Quick run command** | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` (Windows dotnet.exe from WSL per repo conventions) |
| **Full suite command** | `dotnet test` (full solution) + `npx --no-install vitest run` + `npx --no-install playwright test` (headless, DECKFLOW_DISABLE_AUTO_BROWSER=true) |
| **Estimated runtime** | quick ~30s · full ~5min · e2e ~1min |

---

## Sampling Rate

- **After every task commit:** Run the CutLab-filtered quick command
- **After every plan wave:** Run full Web suite; e2e at UI waves
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 300 seconds

---

## Per-Task Verification Map

*One row per task, populated from the five plans' `<automated>` fields. All test files exist or are created inside the task itself on Phase 101 infrastructure — no framework installs, no Wave 0 scaffolds.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 102-01-T1 | 102-01 | 1 | FLOOR-02 | T-102-01-01 | ClampFloors drops unknown keys, clamps tampered floors | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabFloorRulesTests" --nologo` | ✅ | ⬜ pending |
| 102-01-T2 | 102-01 | 1 | FLOOR-02 | T-102-01-01 | Clamp chained at the serializer choke point (EnforceCommanderLock pattern) | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabStateSerializerTests" --nologo` | ✅ | ⬜ pending |
| 102-01-T3 | 102-01 | 1 | FLOOR-01 | — | — | unit + build | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabFloorDefaultsTests" --nologo && dotnet build DeckFlow.Core/DeckFlow.Core.csproj -c Debug --nologo -clp:ErrorsOnly` | ✅ | ⬜ pending |
| 102-02-T1 | 102-02 | 1 | SLOT-01 | T-102-02-03 | Role input is server-computed only; no client channel by construction | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabRoleAssignerTests" --nologo` | ✅ | ⬜ pending |
| 102-02-T2 | 102-02 | 1 | SLOT-02 | T-102-02-01 | Degradation flags prevent fabricated findings from absent upstream data | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabStructuralFindingsTests" --nologo` | ✅ | ⬜ pending |
| 102-03-T1 | 102-03 | 2 | SLOT-01, SLOT-02 | T-102-03-02, T-102-03-03 | Fail-open batched I/O; exactly one category call site; cancellation propagates | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabPageServiceTests" --nologo` | ✅ | ⬜ pending |
| 102-03-T2 | 102-03 | 2 | SLOT-01, SLOT-02, FLOOR-01, FLOOR-02 | T-102-03-01 | Roles/findings recomputed server-side; only user-set floors persist | unit + build | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabPageServiceTests" --nologo && dotnet build DeckFlow.Web/DeckFlow.Web.csproj -c Debug --nologo -clp:ErrorsOnly` | ✅ | ⬜ pending |
| 102-04-T1 | 102-04 | 3 | SLOT-01, SLOT-02, FLOOR-01 | T-102-04-02 | Razor default encoding on all new sections; no Html.Raw | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj -c Debug --nologo -clp:ErrorsOnly` | ✅ | ⬜ pending |
| 102-04-T2 | 102-04 | 3 | FLOOR-02 | T-102-04-01 | Client clamp is UX-only; server re-clamps authoritatively | type-check | `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit` | ✅ | ⬜ pending |
| 102-04-T3 | 102-04 | 3 | SLOT-01, FLOOR-02 | T-102-04-03 | Recalculate reuses the single antiforgery-protected POST form | unit (TS) + e2e | `cd DeckFlow.Web && npx --no-install vitest run ts-tests/cut-lab-lock-interactions.test.ts && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/cut-lab-smoke.spec.ts` | ✅ | ⬜ pending |
| 102-04-T4 | 102-04 | 3 | — (pattern-compliance cleanup) | — | Removes unused server-side bulk-lock path; single lock surface preserved | unit | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab" --nologo` | ✅ | ⬜ pending |
| 102-05-T1 | 102-05 | 4 | SLOT-01, SLOT-02, FLOOR-01, FLOOR-02 | T-102-05-01 | Flag restored OFF + admin lock released in afterEach try/finally | e2e | `env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/cut-lab-structure.spec.ts e2e/cut-lab-smoke.spec.ts` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — Phase 101 established CutLab test files (CutLabStateSerializerTests, CutLabControllerTests, CutLabLockRules tests, cut-lab Vitest suite, cut-lab-smoke.spec.ts e2e). New test files for role-floor rules and structural findings follow the same patterns and are created inside their own tasks; no framework install needed, no Wave 0 scaffold plans.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Theme/viewport visual review of slot groups + findings + floors UI | SLOT-01, SLOT-02, FLOOR-01 | Visual quality judgment | Screenshot 3 themes × 2 viewports per house rule (produced by 102-05-T1's matrix test; reviewed by human) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (none — Phase 101 infra covers every framework; new test files ship inside their tasks)
- [x] No watch-mode flags
- [x] Feedback latency < 300s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-19
