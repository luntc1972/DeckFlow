---
phase: 104
slug: goals-what-if-scenarios
status: mapped
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-20
---

# Phase 104 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source of truth: `104-RESEARCH.md` §"Validation Architecture" + §"Security Domain".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Web.Tests`), Vitest (jsdom, `DeckFlow.Web/ts-tests/**/*.test.ts`), Playwright (`DeckFlow.Web/e2e/*.spec.ts`) |
| **Config file** | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`; `DeckFlow.Web/vitest.config.ts`; Playwright config alongside `e2e/` |
| **Quick run command** | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` + `npx vitest run ts-tests/cut-lab*` |
| **Full suite command** | `dotnet build` (clean) + `dotnet test` both projects + `npx vitest run` + `npx --no-install playwright test e2e/cut-lab*.spec.ts` (via `scripts/run-web-test.sh`) |
| **Estimated runtime** | ~90 seconds (quick ~15s; full incl. e2e ~90s) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` + `npx vitest run ts-tests/cut-lab*`
- **After every plan wave:** Run the full suite (`dotnet build` clean + both xUnit projects + Vitest + Playwright `cut-lab*` specs)
- **Before `/gsd:verify-work`:** Full suite must be green (including `cut-lab-scenarios.spec.ts` and `cut-lab-whatif.spec.ts` if added)
- **Max feedback latency:** ~15 seconds (quick), ~90 seconds (full)

---

## Per-Task Verification Map

> Task IDs assigned by the planner; requirement → test-type mapping is fixed here.

| Requirement | Behavior | Test Type | Automated Command | File Exists |
|-------------|----------|-----------|-------------------|-------------|
| GOAL-01 | Editable turn goal changes the reported per-turn probability for the corresponding metric | unit | `dotnet test --filter FullyQualifiedName~CutLabSimulationServiceTests` | ✅ file / ❌ new cases (W0) |
| GOAL-01 | `PercentByTurn` returns `CastPercent` (not clamped early value) when goal turn ≥ `OnCurveTurn` (Pitfall 1) | unit | same file (regression) | ❌ W0 |
| GOAL-01 | Goal turn persists across a page POST round-trip (`CutLabState.Goals`) | unit | `dotnet test --filter FullyQualifiedName~CutLabStateSerializerTests` | ✅ file / ❌ new cases (W0) |
| GOAL-01 | Goal turn inputs clamped server-side to sane range (e.g. 1–15) before `PercentByTurn` | unit | extend `CutLabSimulationServiceTests` / controller test | ❌ W0 |
| GOAL-02 | Save/load/delete named scenario round-trips full `CutLabState` via localStorage | unit (Vitest, jsdom) | `npx vitest run ts-tests/cut-lab-scenarios.test.ts` | ❌ W0 (new file) |
| GOAL-02 | Scenario slot cap (20) blocks 21st save; quota-exceeded handled gracefully | unit (Vitest) | same file | ❌ W0 |
| GOAL-02 | Save → reload page → load restores pool/locks/intent end-to-end | e2e | `playwright test e2e/cut-lab-scenarios.spec.ts` | ❌ W0 (new spec) |
| GOAL-03 | Swap preview computes correct before/after deltas WITHOUT mutating server state (Discard leaves state unchanged) | unit | `dotnet test --filter FullyQualifiedName~CutLabWhatifTests` | ❌ W0 |
| GOAL-03 | Swap-B candidates are exactly `Pool − Derive(pool, decisions)` (accepted/cut-pile set) | unit | extend `CutLabWorkingListTests` | ✅ file / ❌ new cases |
| GOAL-03 | Keep commits both decisions atomically (A cut, B in working list, cuts-made shows swap) | integration | extend `CutLabApiControllerTests` | ✅ file / ❌ new cases |
| GOAL-03 | Locked card A excluded from swap-A picker / rejected server-side (Pitfall 4) | unit + e2e | extend `CutLabApiControllerTests` + `cut-lab-structure.spec.ts` | ✅/❌ mixed |
| SIM-01 | No new `Random`/trial code paths; swap deltas deterministic for fixed pool/turn/trials | unit | extend `CutLabMetricsContractTests` / `CutLabEngineDeterminismTests` | ✅ files / ❌ new cases |

*Status legend: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Web/ts-tests/cut-lab-scenarios.test.ts` — GOAL-02 (new; jsdom `localStorage` mock via Vitest jsdom env)
- [ ] `DeckFlow.Web.Tests/CutLabWhatifTests.cs` (or extend `CutLabApiControllerTests.cs`) — GOAL-03 preview + commit
- [ ] `DeckFlow.Web/e2e/cut-lab-scenarios.spec.ts` — end-to-end save/reload/load round trip
- [ ] `DeckFlow.Web/e2e/cut-lab-whatif.spec.ts` (or extend `cut-lab-structure.spec.ts`) — swap pick/preview/keep/discard + no-JS fallback
- [ ] Regression case for Pitfall 1 (`PercentByTurn` late-turn clamp) in `CutLabSimulationServiceTests.cs`
- [ ] Framework install: **none** — all frameworks already present.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Goals editor + scenarios panel + swap preview render correctly across themes (Classic/Nyx) and mobile | GOAL-01/02/03 | Visual/theme correctness not asserted by unit/e2e | Playwright screenshots at 2 viewports × 2 themes; cross-AI UI review per project rule |
| localStorage scenario persistence survives a real browser refresh (not just jsdom mock) | GOAL-02 | jsdom mock ≠ real browser storage semantics | Covered by e2e spec; manual confirm on one real browser during UAT |

---

## Validation Sign-Off

- [x] All requirements have an automated verify or a Wave 0 dependency
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 90s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** mapped 2026-07-20 (plan-checker confirmed every Wave-0 gap has a covering plan task; `wave_0_complete` flips true once those tasks land during execution)
