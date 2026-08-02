---
phase: 8
slug: plan-profile-checkbox-selection
status: ready
nyquist_compliant: true
wave_0_complete: true
created: 2026-08-02
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (DeckFlow.Core.Tests, DeckFlow.Web.Tests) + vitest (ts-tests) + Playwright e2e |
| **Config file** | existing test projects — no Wave 0 install |
| **Quick run command** | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` + targeted `dotnet test --filter` |
| **Full suite command** | `dotnet test` (or push-and-watch CI per repo constraint — VSTest unreliable in WSL) |
| **Estimated runtime** | ~120 seconds build; targeted filters <60s; CI for full suite |

---

## Sampling Rate

- **After every task commit:** Run the task's `<automated>` verify (build + targeted filter)
- **After every plan wave:** Run full test suite (CI if WSL VSTest misbehaves)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~180 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | PLPR-01 | — | N/A | unit | `dotnet build` + `dotnet test --filter CutLabPlanProfileTests` | ✅ (new file in task) | ⬜ pending |
| 08-01-02 | 01 | 1 | PLPR-01/02 | — | N/A | unit | `dotnet test --filter DeckPlanStrategyCatalogTests` | ✅ (new file in task) | ⬜ pending |
| 08-02-01 | 02 | 2 | PLPR-03/05 | — | N/A | build | `dotnet build DeckFlow.Web` | ✅ | ⬜ pending |
| 08-02-02 | 02 | 2 | PLPR-02/03/05 | — | N/A | unit | `dotnet test --filter CutLabPlanAffinityResolverTests` | ✅ (new file in task) | ⬜ pending |
| 08-03-01 | 03 | 2 | PLPR-04 | T-EDH (untrusted JSON) | size-limited, malformed-payload-safe parse | build | `dotnet build DeckFlow.Web` | ✅ | ⬜ pending |
| 08-03-02 | 03 | 2 | PLPR-04 | T-CACHE (slug path) | slug validated + containment-checked cache path | build | `dotnet build DeckFlow.Web` | ✅ | ⬜ pending |
| 08-03-03 | 03 | 2 | PLPR-04 | T-EDH | 403-XML = absent; fail-open | unit | `dotnet test --filter EdhrecCommanderThemeServiceTests` | ✅ (new file in task) | ⬜ pending |
| 08-04-01 | 04 | 2 | PLPR-02/05 | — | N/A | unit | `dotnet test --filter CutLabFloorDefaultsTests\|CutLabCommanderFloorsFlagTests\|CutLabAjaxFloorByRoleRegressionTests` | ✅ | ⬜ pending |
| 08-04-02 | 04 | 2 | PLPR-05 | — | N/A | unit | `dotnet test --filter CutLabPlanFloorDeltaTests` | ✅ (new file in task) | ⬜ pending |
| 08-05-01 | 05 | 3 | PLPR-03 | — | N/A | unit | `dotnet test --filter CutLabCutRoundEngineTests\|CutLabEngineDeterminismTests` | ✅ | ⬜ pending |
| 08-05-02 | 05 | 3 | PLPR-06 | — | finding text Razor-encoded downstream | unit | `dotnet test --filter CutLabStructuralFindings\|CutLabEngineDeterminismTests` | ✅ | ⬜ pending |
| 08-05-03 | 05 | 3 | PLPR-02/03/05/06 | — | N/A | unit | `dotnet test --filter CutLabPlanReorderTests\|CutLabStrandedOffPlanPackageTests` | ✅ (new files in task) | ⬜ pending |
| 08-06-01 | 06 | 4 | PLPR-02/04 | — | N/A | build | `dotnet build DeckFlow.Web` | ✅ | ⬜ pending |
| 08-06-02 | 06 | 4 | PLPR-02/04 | — | N/A | unit | `dotnet test --filter CutLabPageServiceTests\|CutLabApiControllerTests\|CutLabAjaxFloorByRoleRegressionTests` | ✅ | ⬜ pending |
| 08-06-03 | 06 | 4 | PLPR-02/04 | — | N/A | unit | `dotnet test --filter CutLabPlanAffinityFactoryTests` | ✅ (new file in task) | ⬜ pending |
| 08-07-01 | 07 | 5 | PLPR-01/04 | T-RAZOR (theme names) | theme display names HTML-encoded in Razor | unit | `dotnet test --filter CutLabPageServiceTests\|CutLabControllerTests` | ✅ | ⬜ pending |
| 08-07-02 | 07 | 5 | PLPR-01/02 | T-RAZOR | encoded rendering | unit+ts | `dotnet build` + `npx --no-install vitest run --dir DeckFlow.Web/ts-tests` | ✅ | ⬜ pending |
| 08-07-03 | 07 | 5 | PLPR-01 | — | N/A | source | `grep -rc "cut-lab-primary-plan" DeckFlow.Web/e2e/` returns no non-zero counts | ✅ | ⬜ pending |
| 08-08-01 | 08 | 6 | PLPR-01..06 | — | N/A | e2e | headless server + `env -u DISPLAY npx --no-install playwright test e2e/cut-lab-plan-panel.spec.ts` | ✅ (new file in task) | ⬜ pending |
| 08-08-02 | 08 | 6 | PLPR-01..06 | — | N/A | manual | human UI checkpoint (below) | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. No new framework, no new test
project — all new test files are authored inline within their owning tasks (no
`MISSING` automated references). Catalog tests land in `DeckFlow.Core.Tests`; service,
engine and UI tests in `DeckFlow.Web.Tests`; e2e in `DeckFlow.Web/e2e`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Plan panel visual check, 2 viewports (08-08 task 2, `autonomous: false`) | PLPR-01/02 | Guild-theme rendering judgment | Headless server via `scripts/run-web-test.sh`; Playwright screenshots at ~1440px and 390x844; ask user before any browser |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (none exist)
- [x] No watch-mode flags
- [x] Feedback latency < 180s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-08-02 (plan-checker pass: 0 BLOCK, 0 HIGH)
