---
phase: 3
slug: commander-aware-floor-defaults
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-28
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `03-RESEARCH.md` §Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`); `xunit.runner.visualstudio` 3.1.4 for discovery |
| **Config file** | none — standard `Microsoft.NET.Sdk` test projects, no custom `xunit.runner.json` |
| **Quick run command** | `dotnet build DeckFlow.sln -c Release` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release` (do **not** set `MTG_DATA_DIR`) |
| **Estimated runtime** | build ~60s; full suite several minutes |

**Why build-clean is the quick signal:** `CLAUDE.md` records VSTest as unreliable under WSL. The fast local gate is a clean `dotnet build` plus the changed-lines format gate; the authoritative gate is the Windows `dotnet.exe test` run or push-and-watch CI.

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln -c Release` (plus the versioned pre-commit changed-lines format hook when `core.hooksPath` is configured).
- **After every plan wave:** full suite via the Windows `dotnet.exe`, or push-and-watch CI.
- **Before `/gsd:verify-work`:** full suite must be green.
- **Max feedback latency:** ~60 seconds for the per-task signal.

---

## Per-Task Verification Map

*Populated 2026-07-28 by `/gsd-plan-phase`. The Owning plan column names where each anchor is discharged.*

| Anchor | Owning plan | Automated command |
|--------|-------------|-------------------|
| RFLR-05 (`max(bracket, commander)`) | `03-04` task 3 | `--filter FullyQualifiedName~CutLabFloorDefaultsTests` |
| RFLR-06 (byte-identical fallback) | `03-03` task 3 (fail-open) + `03-04` task 3 (no-match parity) | `--filter FullyQualifiedName~RoleFloorBaselineProviderTests` and `~CutLabFloorDefaultsTests` |
| RFLR-07 (three-path coverage) | `03-04` task 3 | `--filter FullyQualifiedName~CutLabFloorDefaultsTests` |
| RFLR-08 (side-by-side columns) | `03-05` tasks 1-3 plus its blocking human checkpoint | `--filter FullyQualifiedName~CutLabViewModelTests` |
| D-06 / D-06a (overlap-corrected advisory) | `03-06` task 3 | `--filter FullyQualifiedName~CutLabFloorFeasibilityTests` |
| D-09 (fail-closed generation) | `03-01` task 3 (rules) + `03-02` task 3 (live refusal) | `--filter FullyQualifiedName~RoleFloorBaseline` |
| D-13 (headroom ranking) | `03-07` tasks 2-3 | `--filter FullyQualifiedName~CutLabCutRoundEngineTests` |


| Requirement | Behavior under test | Test type | Automated command | Home file |
|-------------|---------------------|-----------|-------------------|-----------|
| RFLR-05 | `ResolveDefaults` applies `max(bracket, commander)` per D-04 across the six GO roles | unit | `dotnet test DeckFlow.sln --filter FullyQualifiedName~CutLabFloorDefaultsTests` | ✅ `DeckFlow.Web.Tests/CutLabFloorDefaultsTests.cs` |
| RFLR-06 | Below-bar commander / out-of-scope role / failed snapshot load all produce byte-identical floors to today | unit | same filter | ✅ extend existing file |
| RFLR-07 | Commander-hit, bracket-fallback, and role-not-in-scope paths each covered | unit | same filter | ✅ extend existing file |
| RFLR-08 | `BuildFloorRows` emits D-12's two distinct empty states (`n/a` for structurally-out-of-scope, empty marker for GO-role-no-match) plus populated cells | unit | `dotnet test DeckFlow.sln --filter FullyQualifiedName~CutLabViewModel` | ❌ **Wave 0** — no `CutLabViewModelTests.cs` exists |
| D-06 / D-06a | Overlap-corrected aggregate floor sum fires the infeasibility advisory only when genuinely infeasible; `max(engines, draw)` collapse and `wincons` free-riding are exercised | unit | new class or extend `CutLabStructuralFindingsTests.cs` | ❌ **Wave 0** — feature does not exist yet |
| D-13 | Overshoot advisory ranks by headroom `(in-pool − effective floor)` descending, with `LockedOvershootRoleOrder` as deterministic tiebreak | unit | `dotnet test DeckFlow.sln --filter FullyQualifiedName~CutLabCutRoundEngineTests` | ✅ `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` — **2 existing tests must be rewritten, not merely extended** (see `03-RESEARCH.md` §O-3) |
| D-09 | Snapshot generator refuses to write when the new snapshot diverges from the committed one beyond threshold (fail-closed) | unit | `DeckFlow.Core.Tests` filter for the new drift check | ❌ **Wave 0** — mirrors `CedhBaselineDriftCheckTests` from `main` |

---

## Wave 0 Requirements

All four gaps are assigned to a plan. None is deferred.

- [ ] `DeckFlow.Web.Tests/CutLabViewModelTests.cs` — created by **`03-05` task 3**, covering `BuildFloorRows`'s D-12 two-empty-state logic, then extended by `03-06` task 3 for the advisory copy.
- [ ] Test home for the D-06a overlap-corrected aggregate-infeasibility check — **resolved: a new `DeckFlow.Web.Tests/CutLabFloorFeasibilityTests.cs`**, created by `03-06` task 3. The discretionary call is settled in `03-06`: the advisory is a **panel-level notice**, not a new `CutLabFindingKind`, because it attaches to no card and would otherwise have to be added to `ExcludedFindingKindsFromTally`.
- [ ] `FakeRoleFloorBaselineProvider` test double — created by **`03-04` task 3** inside `CutLabFloorDefaultsTests.cs`, mirroring `FakeCedhBaselineProvider`, and additionally recording queried roles so out-of-scope roles can be proven never to be looked up.
- [ ] Drift-check tests for D-09 — created by **`03-01` task 3** as `DeckFlow.Core.Tests/RoleFloorBaselineDriftCheckTests.cs`, mirroring `CedhBaselineDriftCheckTests` from `main`, plus three live failure-path runs in `03-02` task 3.
- Framework install: **none** — xUnit is already wired in both test projects. Adding a test framework or mocking library is forbidden by `CLAUDE.md` without explicit approval.

---

## Manual-Only Verifications

| Behavior | Requirement | Why manual | Test instructions |
|----------|-------------|------------|-------------------|
| Six-column role-floors table renders correctly across the 24 guild themes and the stacked mobile layout | RFLR-08 / D-11 | Visual/theme regression is not unit-testable; `data-label` correctness only shows under the mobile stacked layout | Start via `scripts/run-web-test.sh` (sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`), drive with `npx --no-install playwright test` headless. Check desktop + mobile viewports, and at least one dark and one light guild theme. **Ask before opening a browser.** |
| Reset-to-default restores the `max()` value, not the pre-`max()` bracket value | RFLR-05 / D-04 | Round-trips through `data-cut-lab-floor-default` and `cut-lab.ts`; the DOM contract is the thing under test | Adjust a floor, press Reset, confirm the restored number equals the Floor column, not the Bracket column |

---

## Validation Sign-Off

- [x] All tasks have an `<automated>` verify — every one of the 21 tasks across the 7 plans carries one, except `03-05`'s blocking human checkpoint, which is a manual-only verification already listed in the table above.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 covers all four MISSING references above, each assigned to a named plan and task.
- [x] No watch-mode flags — every command is a one-shot `dotnet build` or `dotnet test --filter`.
- [x] Feedback latency < 60s for the per-task signal (`dotnet build DeckFlow.sln -c Release`).
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** planned 2026-07-28; pending execution.
