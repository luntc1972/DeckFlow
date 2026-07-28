---
phase: 3
slug: commander-aware-floor-defaults
status: draft
nyquist_compliant: false
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

*Populated by `/gsd-plan-phase` once `03-NN-PLAN.md` files exist. Requirement→behavior anchors below are fixed and plans must map onto them.*

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

- [ ] `DeckFlow.Web.Tests/CutLabViewModelTests.cs` (or equivalent home) — covers `BuildFloorRows`'s D-12 two-empty-state logic. No such file exists today.
- [ ] A test home for the D-06a overlap-corrected aggregate-infeasibility check. Plans must state whether it lands in `CutLabStructuralFindingsTests.cs`, `CutLabCutRoundEngineTests.cs`, or a new file — this follows the still-open discretionary call on whether the advisory is a new `CutLabFindingKind` or a panel-level notice.
- [ ] `FakeRoleFloorBaselineProvider` test double, mirroring `FakeCedhBaselineProvider` at `CutLabFloorDefaultsTests.cs:215-238`. Required by RFLR-06 and RFLR-07.
- [ ] Drift-check tests for D-09, mirroring `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs` (present on `main`; arrives with the O-2 rebase).
- Framework install: **none** — xUnit is already wired in both test projects. Adding a test framework or mocking library is forbidden by `CLAUDE.md` without explicit approval.

---

## Manual-Only Verifications

| Behavior | Requirement | Why manual | Test instructions |
|----------|-------------|------------|-------------------|
| Six-column role-floors table renders correctly across the 24 guild themes and the stacked mobile layout | RFLR-08 / D-11 | Visual/theme regression is not unit-testable; `data-label` correctness only shows under the mobile stacked layout | Start via `scripts/run-web-test.sh` (sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`), drive with `npx --no-install playwright test` headless. Check desktop + mobile viewports, and at least one dark and one light guild theme. **Ask before opening a browser.** |
| Reset-to-default restores the `max()` value, not the pre-`max()` bracket value | RFLR-05 / D-04 | Round-trips through `data-cut-lab-floor-default` and `cut-lab.ts`; the DOM contract is the thing under test | Adjust a floor, press Reset, confirm the restored number equals the Floor column, not the Bracket column |

---

## Validation Sign-Off

- [ ] All tasks have an `<automated>` verify or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all four MISSING references above
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s for the per-task signal
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
