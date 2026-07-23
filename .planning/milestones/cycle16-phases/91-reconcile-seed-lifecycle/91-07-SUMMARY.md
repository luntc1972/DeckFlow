---
phase: 91-reconcile-seed-lifecycle
plan: 07
subsystem: content-kb
tags: [content-kb, reconcile, studio, blazor, operator-ui, seed-availability]

# Dependency graph
requires:
  - phase: 91-reconcile-seed-lifecycle (91-06)
    provides: "IContentKbReconcileOrchestrator.RunDryRunAsync + ReconcileDryRunResult(SeedAvailable, Discrepancies)"
provides:
  - "ReconcileCoordinator — operator-action coordinator delegating to the orchestrator's dry-run, surfacing SeedAvailable intact"
  - "Reconcile.razor(.cs) — operator page: Run dry-run action + four-class results panel + seed-unavailable banner"
  - "/reconcile route + Pipeline nav entry"
affects: [91-08-removal-scoped-apply]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ReconcileCoordinator mirrors DirectPushCoordinator's optional-logger constructor convention (ArgumentNullException.ThrowIfNull + NullLogger fallback) but carries no IConfiguration — the orchestrator (91-06) already owns the ephemeral prod connection-string read, so the coordinator has nothing to read config for"
    - "Reconcile.razor.cs uses a manual RenderTreeBuilder RenderFragment (with SetKey per discrepancy Id) for the four near-identical discrepancy-list groups, avoiding the ASP0006 ever-incrementing-sequence-number pitfall by reusing literal sequence numbers per loop iteration"

key-files:
  created:
    - DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs
    - DeckFlow.Studio/Pages/Reconcile.razor
    - DeckFlow.Studio/Pages/Reconcile.razor.cs
    - DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentKbReconcileOrchestrator.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentKbReconcileStore.cs
  modified:
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio/Shared/NavMenu.razor
    - DeckFlow.Studio.Tests/NavMenuTests.cs
    - README.md

key-decisions:
  - "ReconcileCoordinator does NOT take IConfiguration, unlike DirectPushCoordinator/PullFromProdCoordinator. Those coordinators build their own on-demand prod store or read the prod flag directly, so they need the connection string. ReconcileCoordinator's RunDryRunAsync delegates entirely to IContentKbReconcileOrchestrator.RunDryRunAsync(scopeTag, ct) — a signature that already carries no connection-string parameter, because the orchestrator (91-06) reads Studio:ProdConnectionString itself. Adding an unused IConfiguration field would have produced a CS0414 (assigned-but-never-read) warning, violating the plan's own 0-new-warnings gate."
  - "Discrepancy-list rendering uses a hand-written RenderTreeBuilder RenderFragment (not a plain foreach in markup) so the four groups (published-orphan / file-orphan / seed-drift-or-banner / body-hash-mismatch) share one empty-state + list-item renderer instead of four copies of the same markup. Fixed an initial ASP0006 analyzer warning (ever-incrementing seq++ sequence numbers) by reusing literal sequence numbers per loop iteration plus builder.SetKey(item.Id) for stable list identity — the analyzer-correct manual-builder loop pattern."
  - "NavMenuTests.NavMenu_Renders_AllNineDestinations (hardcoded count=9) was in the direct blast radius of adding the Reconcile nav entry (10th Pipeline destination) — renamed to AllTenDestinations and updated the assertion, plus added a dedicated NavMenu_Renders_ReconcileLink test, rather than leaving a stale/failing pre-existing test (Rule 1 — bug relative to the new page's own scope)."

requirements-completed: [SYNC-11]

# Metrics
duration: ~45min
completed: 2026-07-09
---

# Phase 91 Plan 07: Reconcile Operator Dry-Run Page Summary

**A new Studio `/reconcile` page lets the operator run the SYNC-11 reconcile dry-run with one click and review all four prod<->git<->seed discrepancy classes — read-only, flag-independent, with a "seed unavailable" banner instead of a misleading empty seed-drift group when the seed cannot be read.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-07-09T22:28:00Z
- **Completed:** 2026-07-09T23:13:00Z
- **Tasks:** 2
- **Files modified:** 10 (6 created, 4 modified)

## Accomplishments
- `ReconcileCoordinator` (mirrors `DirectPushCoordinator`'s optional-logger constructor convention) delegates `RunDryRunAsync(scopeTag, ct)` straight to `IContentKbReconcileOrchestrator.RunDryRunAsync` and returns the `ReconcileDryRunResult` unchanged — `SeedAvailable` is surfaced intact, never dropped or re-derived, so the page can always distinguish "seed unreadable" from "no drift found" (T-91-28, the Codex BLOCK closure carried forward from 91-06).
- `GetOpenDiscrepanciesAsync` passes straight through to `IContentKbReconcileStore.GetOpenAsync` for future read-only display of previously-persisted discrepancies; the coordinator itself never calls the store's write surface (`PersistRunAsync`/`EnsureSchemaAsync`) — all persistence is the orchestrator's job (91-06), proven by a dedicated test asserting zero store-write calls after a dry-run (T-91-17).
- Registered as a singleton in `DeckFlow.Studio/Program.cs` alongside the other operator coordinators.
- `Reconcile.razor`/`.razor.cs`: a "Run dry-run" button (gated on `Config.IsProdConfigured`, since the orchestrator reads prod) triggers the coordinator and renders four grouped result cards with per-class counts and item lists. When `SeedAvailable` is `false`, the Seed Drift card is replaced with a prominent warning banner ("SEED UNAVAILABLE — seed-drift/removal skipped … not the same as 'no drift found'") instead of an empty "0 items" group — mirroring the D-06 report's own seed-unavailable advisory from 91-06. The page is explicitly labeled read-only/detection-only; there is no Apply/removal action here (91-08).
- Routed at `/reconcile` with a new Pipeline nav entry (`Home → Harvest → Creators → Review → Publish → Direct Push → Pull from Prod → Reconcile`), consistent with the existing Direct Push / Pull from Prod entries.
- 7 new `ReconcileCoordinatorTests` (2 new fakes: `FakeContentKbReconcileOrchestrator`, `FakeContentKbReconcileStore`) covering: the coordinator returns the orchestrator's result unchanged; an unavailable-seed result is surfaced intact; the default scope tag is `"full"` and an explicit one passes through unchanged; the coordinator never writes to the local store directly; `GetOpenDiscrepanciesAsync` passes through to the store with the right scope tag; and null-argument constructor guards.
- README documents the new Reconcile page (four discrepancy classes, the seed-unavailable notice, and the "no destructive write this plan" framing) alongside the existing Direct Push / Pull from Prod entries.

## Task Commits

Each task was committed atomically:

1. **Task 1: ReconcileCoordinator (dry-run)** - `8b01ebfe` (feat)
2. **Task 2: Reconcile.razor operator page (dry-run) + README** - `a1f6001a` (feat)

**Plan metadata:** commit follows this SUMMARY.

## Files Created/Modified
- `DeckFlow.Studio/ViewModels/ReconcileCoordinator.cs` - `ReconcileCoordinator` (dry-run delegate + open-discrepancy pass-through)
- `DeckFlow.Studio/Pages/Reconcile.razor` - Operator page markup: config gate, read-only banner, Run dry-run button, four-class results panel with seed-unavailable banner
- `DeckFlow.Studio/Pages/Reconcile.razor.cs` - Page code-behind: `RunDryRunAsync`, `Items` filter helper, `RenderDiscrepancyList` RenderFragment, sanitized error copy, cancellation, test seam
- `DeckFlow.Studio/Program.cs` - Registers `ReconcileCoordinator` as a singleton
- `DeckFlow.Studio/Shared/NavMenu.razor` - Adds the `/reconcile` Pipeline nav entry
- `DeckFlow.Studio.Tests/ViewModels/ReconcileCoordinatorTests.cs` - 7 tests (result pass-through, seed-unavailable surfaced, scope-tag default/explicit, no direct store writes, store pass-through, null-arg guards)
- `DeckFlow.Studio.Tests/TestDoubles/FakeContentKbReconcileOrchestrator.cs` - In-memory fake orchestrator (seeded result, call count, last scope tag)
- `DeckFlow.Studio.Tests/TestDoubles/FakeContentKbReconcileStore.cs` - In-memory fake store (seeded open discrepancies, per-method call counts)
- `DeckFlow.Studio.Tests/NavMenuTests.cs` - Adds a Reconcile-link assertion; renames/updates the destination-count test (9 -> 10)
- `README.md` - Documents the Reconcile dry-run page and the seed-unavailable notice

## Decisions Made
See `key-decisions` in frontmatter. In summary: (1) `ReconcileCoordinator` deliberately omits `IConfiguration` — the plan's `<action>` text names it as a dependency, but the orchestrator's own `RunDryRunAsync(scopeTag, ct)` contract (91-06) already owns the ephemeral prod connection-string read, so an unused config field would have produced a `CS0414` warning and violated the 0-new-warnings gate; (2) the four discrepancy-list groups render through one shared hand-written `RenderFragment` (fixed for the ASP0006 analyzer by reusing literal sequence numbers + `SetKey` instead of an incrementing counter) rather than four copies of near-identical markup; (3) the pre-existing `NavMenu_Renders_AllNineDestinations` test was updated (not left failing) since adding the Reconcile nav entry was squarely in this task's blast radius.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Pre-existing `NavMenuTests.NavMenu_Renders_AllNineDestinations` broke on the new nav entry**
- **Found during:** Task 2 (full-suite test run after adding the Reconcile nav link)
- **Issue:** `NavMenuTests.cs` hardcoded an expected count of 9 Pipeline+Support nav destinations. Adding the Reconcile link (Task 2's own scope) made the actual count 10, failing the pre-existing test.
- **Fix:** Renamed the test to `NavMenu_Renders_AllTenDestinations`, updated the expected count to 10 and its comment, and added a dedicated `NavMenu_Renders_ReconcileLink` assertion alongside the existing per-link tests.
- **Files modified:** `DeckFlow.Studio.Tests/NavMenuTests.cs`
- **Verification:** `dotnet test DeckFlow.Studio.Tests` — 373 passed (4 Postgres-gated skips), 0 failed.
- **Committed in:** `a1f6001a` (Task 2 commit)

**2. [Rule 1 - Bug] Manual `RenderTreeBuilder` loop triggered ASP0006**
- **Found during:** Task 2 initial build
- **Issue:** The first draft of `RenderDiscrepancyList` used an ever-incrementing `seq++` local as the manual `RenderTreeBuilder` sequence number inside a `foreach`, which the ASP0006 analyzer flags (a genuine anti-pattern — it defeats Blazor's render-tree diffing across re-renders).
- **Fix:** Reused literal sequence numbers (`2`, `3`) on every loop iteration — the analyzer-correct manual-builder loop pattern — paired with `builder.SetKey(item.Id)` for stable list-item identity across re-renders.
- **Files modified:** `DeckFlow.Studio/Pages/Reconcile.razor.cs`
- **Verification:** `dotnet build DeckFlow.sln` — 0 warnings, 0 errors.
- **Committed in:** `a1f6001a` (Task 2 commit; caught and fixed before the commit was made)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — bugs surfaced directly by this plan's own changes, fixed within Task 2's scope before commit).
**Impact on plan:** No scope creep — both fixes are directly caused by the new page/nav-link this task adds, not pre-existing unrelated issues.

## Issues Encountered
None beyond the two auto-fixed items above. No build lock (`TypeScript.Tasks.dll`) — no Studio dev server was running during this plan, per the project constraint.

## User Setup Required
None — no new external service configuration. The Reconcile page reads the same `Studio:ProdConnectionString` user-secret the orchestrator (91-06) and every other prod-reading Studio page already use.

## Next Phase Readiness
- The operator can now run `/reconcile` → "Run dry-run" and see all four discrepancy classes, with the seed-unavailable notice correctly distinguishing "seed unreadable" from "no drift" — the visibility SYNC-11 promised, shipped ahead of any flag flip (D-09 "dry-run ships first").
- `ReconcileCoordinator.GetOpenDiscrepanciesAsync` is ready for 91-08 (removal-scoped Apply) to reuse for reading the currently-open seed-drift set before offering a soft-hide apply — no further plumbing required on the read side.
- No blockers. Full solution builds clean: `DeckFlow.Core.Tests` 1201/1201, `DeckFlow.Studio.Tests` 373/373 (4 Postgres-gated skips), `DeckFlow.Web.Tests` 1250/1262 (12 Postgres-gated skips) — all 0 errors / 0 new warnings.

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 6 created source/test files verified present on disk; both task commit hashes (`8b01ebfe`, `a1f6001a`) verified present in git log.
