---
phase: 90-directpush-correctness-seed-sync
plan: 06
subsystem: content-kb
tags: [blazor-server, ui-state-machine, resume-durability]

# Dependency graph
requires:
  - phase: 90-directpush-correctness-seed-sync
    plan: 05
    provides: "DirectPushCoordinator.WriteContentAsync / VerifyAndPublishAsync / ConfirmAndPublishAsync split + IDeployedBodyConfirmer hash-match poll"
  - phase: 90-directpush-correctness-seed-sync
    plan: 03
    provides: "awaiting_confirm_utc durable marker + SetAwaitingConfirmAsync/ClearAwaitingConfirmAsync"
provides:
  - "DirectPush.razor Stage 5 (Verify Deploy & Publish) wired to VerifyAndPublishAsync — the UI now enforces expand(1-4)->verify(5)->contract at the stage-gating level, never just reordered coordinator calls"
  - "DirectPushCoordinator.GetAwaitingConfirmRowsAsync — diff-independent, in-memory-filtered read of marker-set approved rows"
  - "DirectPush.razor 'Awaiting Confirm — Resume Interrupted Push' card — durable resume UI for interrupted pushes"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Coordinator-owned diff-independent resume query (GetAwaitingConfirmRowsAsync) rather than deriving 'pending' state from a re-diff, avoiding the Pitfall-4 Unchanged-reclassification trap and the Pitfall-3 timestamp-WHERE trap in one method"
    - "Bucket-visibility-survives-emptying render guard: a card gated on a mutable collection must also stay rendered while that same action's result is pending display, or a successful terminal state clears itself before the operator can see it"

key-files:
  created: []
  modified:
    - DeckFlow.Studio/Pages/DirectPush.razor
    - DeckFlow.Studio/Pages/DirectPush.razor.cs
    - DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
    - DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs
    - README.md

key-decisions:
  - "Split plan execution into two atomic commits matching the plan's two tasks even though both edited the same DirectPush.razor(.cs) files — extracted Task 2's resume-bucket pieces out of the merged edit, committed Task 1 (Stage 5 + Stage 4 copy corrections) standalone-buildable, then re-added Task 2 (resume bucket + coordinator method + tests) on top."
  - "Stage 5 gates on _gitSuccess (any of the three CommitAndPushBodiesAsync outcomes — Committed/PushedExistingCommits/AlreadyInSync), not on a stricter 'a push just happened' signal — the confirm poll itself is the actual safety net regardless of which git outcome preceded it; gating any tighter would block the legitimate re-run-Stage-5-after-waiting-for-deploy operator flow."
  - "Added DirectPushCoordinator.GetAwaitingConfirmRowsAsync even though it was not in the plan's files_modified list — the page has no direct IContentSiteIndexStore access under the H1 split architecture (all store I/O goes through the coordinator), so a coordinator method was structurally required to implement Task 2's explicit resume-bucket requirement (Rule 2)."
  - "Corrected every 'content is already live' claim in Stage 4's UI copy (razor markup + all 4 razor.cs exception handlers) — those strings predate the 90-05 write/confirm split and are now false: a git/deploy failure after 90-05+90-06 leaves rows hidden and awaiting-confirm, never live (Rule 1 bug fix, not in the plan's literal task text but required by the plan's own acceptance criteria that git/deploy failure must surface as hidden+awaiting-confirm, not as a false 'already live' claim)."

patterns-established:
  - "RefreshAwaitingConfirmBucketAsync as a single shared refresh point called from every stage handler that can mutate the marker set (Stage 3 sets it, Stage 5/Resume clear it) plus page load and Stage 1 — avoids re-deriving 'pending' state ad hoc at each call site."

requirements-completed: [SYNC-09, SYNC-10]

# Metrics
duration: ~30min
completed: 2026-07-08
---

# Phase 90 Plan 06: DirectPush Stage Re-sequencing + Resume Summary

**Wired a new Stage 5 (Verify Deploy & Publish) into the DirectPush Blazor page so the UI itself enforces expand→verify→contract — never contract-before-expand — and added a durable "Awaiting Confirm — Resume Interrupted Push" card so a push left mid-flight across a Studio reload is resumable instead of silently reclassified Unchanged and stranded.**

## Performance

- **Duration:** ~30 min
- **Tasks:** 2 completed
- **Files modified:** 6 (0 created, 6 modified)

## Accomplishments
- `DirectPush.razor.cs` gains `RunVerifyAndPublishAsync` (Stage 5), gated on `_gitSuccess` (hard-guarded, not just a disabled button) — it calls `DirectPushCoordinator.VerifyAndPublishAsync(_publishRows, ct)` and shows confirmed rows as PUBLISHED & VISIBLE vs. not-confirmed rows as an operator-visible "did NOT confirm" state with a re-run affordance. A git/deploy failure or a failed bounded poll now correctly leaves rows hidden and awaiting-confirm — the page never claims content is "already live" once content-only writes stopped auto-publishing (90-05).
- Fixed every stale "content is already live in production" string across Stage 4's markup and all 4 exception handlers (`DirectPushPushBlockedException`, `DirectPushUnreviewedCommitsException`, `DirectPushPushException`, generic `catch`, plus the cancellation path) — these predated the 90-05 write/confirm split and were actively misleading post-split (a git failure now genuinely leaves rows hidden, not live).
- `DirectPushCoordinator.GetAwaitingConfirmRowsAsync` reads locally-approved rows with a non-null `AwaitingConfirmUtc` marker, filtered **in memory** (never a `WHERE` on the timestamp column — Pitfall 3) — the diff-independent way to find rows a `ClassifyDiff` re-run would otherwise misclassify Unchanged and drop from `PublishRows` (Pitfall 4).
- `DirectPush.razor` renders an "Awaiting Confirm — Resume Interrupted Push" card whenever the bucket is non-empty (or a resume just produced a result worth showing), refreshed at page load, after Stage 1 (diff), after Stage 3 (sets new markers), and after Stage 5/Resume (clears confirmed markers) — a `ResumeVerifyAsync` handler re-runs `VerifyAndPublishAsync` for exactly the marker-set rows.
- README documents the new 5-stage operator flow and the resume path for interrupted pushes.

## Task Commits

Each task was committed atomically:

1. **Task 1: Re-sequence DirectPush stages to expand -> verify -> contract** - `703e8602` (feat)
2. **Task 2: Resume-from-marker + page tests + manual-verification note** - `b9626956` (feat)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP update)

## Files Created/Modified
- `DeckFlow.Studio/Pages/DirectPush.razor` - Stage 5 card, resume-bucket card, corrected Stage 4/banner/intro copy (hidden-until-confirmed model, no more "already live" claims).
- `DeckFlow.Studio/Pages/DirectPush.razor.cs` - `RunVerifyAndPublishAsync` + Stage 5 state, `ResumeVerifyAsync` + resume-bucket state, `RefreshAwaitingConfirmBucketAsync`, `ToRowResult`/`ResumeKeyLabel` helpers, `InvokeVerifyAndPublishForTest` hard-guard seam, `OnInitializedAsync`/`ComputeDiffAsync`/`WriteRowsAsync` all now refresh the bucket at the right points, corrected exception-handler copy.
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` - New `GetAwaitingConfirmRowsAsync` (diff-independent, in-memory marker filter).
- `DeckFlow.Studio.Tests/DirectPushPageTests.cs` - `DriveThroughStage4` helper, `confirmerOverride` param on `RenderDirectPush`, 3 Stage 5 tests (hard-guard, confirmed publish, not-confirmed stays hidden) + 4 resume-bucket tests (a)-(d) per the plan's acceptance criteria.
- `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` - 2 new tests for `GetAwaitingConfirmRowsAsync` (approved+marker filter, empty-bucket case).
- `README.md` - Documents the 5-stage expand→verify→contract flow and the resume-from-marker operator path.

## Decisions Made
- Executed Task 1 and Task 2 as genuinely separate, independently-buildable commits despite both touching `DirectPush.razor(.cs)` — Task 2's resume-bucket pieces were temporarily extracted, Task 1 committed and verified standalone (build clean, 68/68 DirectPush-filtered tests green), then Task 2's pieces re-applied and committed on top (74/74 green).
- Stage 5 gates on `_gitSuccess` broadly (any of the 3 `CommitAndPushBodiesAsync` outcomes), not a narrower "a push just happened this run" signal — the confirm poll itself is the real safety net; the git outcome variant is orthogonal to whether a deploy might now be live.
- `GetAwaitingConfirmRowsAsync` added to the coordinator even though absent from the plan's `files_modified` — required by the H1 architecture (page has no direct store access) to satisfy Task 2's explicit resume-bucket requirement.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Stage 4 UI copy claimed "content is already live" after 90-05 made that false**
- **Found during:** Task 1, while re-reading Stage 4's existing markup/error-handler copy to add Stage 5.
- **Issue:** `DirectPush.razor` and all 4 `CommitAndPushAsync` exception handlers in `DirectPush.razor.cs` still said "Content is already live in production" / "the content was already live" — true before Plan 90-05 (Stage 3 stamped+published immediately) but false afterward (Stage 3 is content-only; nothing is visible until the new Stage 5 confirms). An operator reading a git-failure message under the new model would have been told content was safely live when it was actually still hidden.
- **Fix:** Reworded every occurrence to state the accurate post-90-05/90-06 invariant: pushed rows stay HIDDEN and awaiting-confirm through Stage 4; only a confirmed Stage 5 makes them visible.
- **Files modified:** `DeckFlow.Studio/Pages/DirectPush.razor`, `DeckFlow.Studio/Pages/DirectPush.razor.cs`.
- **Verification:** Full `DeckFlow.Studio.Tests` DirectPush filter green after the change (67→68, no regressions); the 4 Stage-4-outcome tests (`Stage4_AfterDbWrite_...`, `Stage4_AlreadyInSync_...`, `Stage4_PushedExistingCommits_...`) still assert their original invariants against the reworded copy.
- **Committed in:** `703e8602` (Task 1).

**2. [Rule 2 - Missing critical functionality] Page has no store access to read the awaiting-confirm marker**
- **Found during:** Task 2, implementing the resume bucket per the plan's explicit interfaces note ("On page (re)load/diff, detect rows whose AwaitingConfirmUtc marker is non-null...").
- **Issue:** The plan's `files_modified` list only names `DirectPush.razor(.cs)` and the test file, but under the existing H1 split architecture `DirectPush.razor.cs` has no `IContentSiteIndexStore` dependency at all — every store read/write goes through `DirectPushCoordinator`. Without a coordinator method, Task 2's explicit resume-bucket requirement was structurally unimplementable.
- **Fix:** Added `DirectPushCoordinator.GetAwaitingConfirmRowsAsync` — reads local approved rows and filters in memory for a non-null `AwaitingConfirmUtc` (never a SQL `WHERE` on the marker column, avoiding the Pitfall-3 F-51-PG-01 class).
- **Files modified:** `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs`, `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` (2 new tests).
- **Verification:** `dotnet build DeckFlow.Studio DeckFlow.Studio.Tests` clean; new coordinator tests pass; full page-level tests (a)-(d) exercise the method end-to-end through the page.
- **Committed in:** `b9626956` (Task 2).

**3. [Rule 1 - Bug] Resume-bucket card vanished the instant a resume fully confirmed, hiding its own success result**
- **Found during:** Task 2, writing test (c) (`DirectPush_Resume_ConfirmerConfirmed_PublishesRow_ClearsFromBucket`) — the test failed because `Assert.Contains("Confirmed", cut.Markup)` never matched.
- **Issue:** The resume card's outer `@if` was gated solely on `_awaitingConfirmRows.Count > 0`. A fully-successful resume clears the marker (via `ConfirmAndPublishAsync` inside `VerifyAndPublishAsync`), so `RefreshAwaitingConfirmBucketAsync` immediately emptied the bucket on the very next render — the whole card, including the "Confirmed & Published" result row the same action had just produced, disappeared before the operator could see it. This is exactly the "silent deadlock / no operator-visible state" failure mode the plan's threat model (T-90-14/T-90-15) explicitly warns against, just on the success path instead of the failure path.
- **Fix:** Changed the card's visibility condition to also stay rendered while `_resumeConfirmedResults`/`_resumeNotConfirmedResults` are non-empty, and split the body into a "still pending" section (rows table + resume button, shown only while the bucket has entries) and a persistent results section (shown whenever a resume just ran, independent of whether the bucket emptied).
- **Files modified:** `DeckFlow.Studio/Pages/DirectPush.razor`.
- **Verification:** Test (c) passes after the fix; tests (a), (b), (d) unaffected (re-verified full DirectPush filter: 74/74 green).
- **Committed in:** `b9626956` (Task 2) — found and fixed within the same task, before committing.

---

**Total deviations:** 3 auto-fixed (2 Rule 1 — bugs; 1 Rule 2 — missing critical functionality). None expanded scope beyond what SYNC-09/SYNC-10/D-06/D-10 require; all three were necessary for the plan's own acceptance criteria and threat-model mitigations to actually hold.

## Issues Encountered

None beyond the bucket-visibility bug documented above (found and fixed during test-writing, not after ship).

## User Setup Required

None new. The Stage 5 confirm-poll config (`Studio:PublicSiteBaseUrl`/`AdminUser`/`AdminPassword`) was already documented as user-setup in Plan 90-05's summary; this plan only wires the already-configured confirmer into the UI.

## Next Phase Readiness

- The DirectPush page now fully enforces expand(1-4)→verify(5)→contract at the UI level — the last piece of SYNC-09/D-06 that Plan 90-05 deferred ("VerifyAndPublishAsync exists... but is NOT yet wired to a DirectPush.razor UI stage") is now closed.
- `DeckFlow.sln` builds with 0 warnings/0 errors; full suite green (Core 1149, Studio 347 + 3 Postgres-skip, Web 1249 + 12 Postgres-skip).
- The live end-to-end round-trip (DirectPush expand → real Render autodeploy → Stage 5 confirms → visible) remains explicitly deferred to operator/Phase-93 verification per this plan's `<verification>` block — not unit-coverable, not faked here.
- Per STATE.md, Phase 90 (7/7 plans) is now complete; Phases 88-90 remain unpushed to origin (`git push origin main` after the operator fast-forwards `plan/cycle-16-kb-sync` — carried forward from prior plan summaries, not new to this plan).
- No blockers.

## Self-Check: PASSED

Both task commit hashes (`703e8602`, `b9626956`) verified present in `git log --oneline --all`. All 6 modified files verified present on disk via direct file-existence checks (`DirectPush.razor`, `DirectPush.razor.cs`, `DirectPushCoordinator.cs`, `DirectPushPageTests.cs`, `DirectPushCoordinatorTests.cs`, `README.md`).

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-08*
