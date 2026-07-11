---
phase: 90-directpush-correctness-seed-sync
plan: 05
subsystem: content-kb
tags: [blazor-server, http-polling, feature-flags, basic-auth, dapper, tdd]

# Dependency graph
requires:
  - phase: 90-directpush-correctness-seed-sync
    plan: 03
    provides: "awaiting_confirm_utc durable marker + SetAwaitingConfirmAsync/ClearAwaitingConfirmAsync composite-key writers"
  - phase: 90-directpush-correctness-seed-sync
    plan: 04
    provides: "DirectPushCoordinator seed re-export + IProdContentReader.ReadFlagAsync + [skip render] drop under sync.directpush-gitbody"
  - phase: 90-directpush-correctness-seed-sync
    plan: 07
    provides: "GET Admin/api/contentkb/deployed-body-hash — authenticated, git-/app-only, is_visible-independent endpoint returning { bodySha256 }"
provides:
  - "DirectPushCoordinator.WriteContentAsync / ConfirmAndPublishAsync split — content upsert no longer stamps pushed_to_prod_utc or flips is_visible; those move to the post-confirm method"
  - "DirectPushCoordinator.VerifyAndPublishAsync — per-row hash-match gate before ConfirmAndPublishAsync runs"
  - "IDeployedBodyConfirmer/DeployedBodyConfirmer — bounded (5-attempt, 3s backoff) hash-match poll of the Plan 90-07 endpoint"
  - "StudioConfig.IsConfirmerConfigured + three new Studio:PublicSiteBaseUrl/AdminUser/AdminPassword config keys + DirectPush badge/gate"
affects: [90-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bounded application-level poll loop over the Studio shared HttpClient (distinct from ResilientHttpHandler's transport-level retry — a 404/hash-mismatch is a valid business outcome, not a transport error)"
    - "internal test-seam constructor (maxAttempts + retryDelay) mirroring ResilientHttpHandler's pattern, so unit tests exercise the real bounded-retry code path in milliseconds"
    - "Presence-only StudioConfig flag with a default parameter value (IsConfirmerConfigured = false) so existing 2-arg test call sites keep compiling"

key-files:
  created:
    - DeckFlow.Studio/Services/IDeployedBodyConfirmer.cs
    - DeckFlow.Studio/Services/DeployedBodyConfirmer.cs
    - DeckFlow.Studio.Tests/Services/DeployedBodyConfirmerTests.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeDeployedBodyConfirmer.cs
  modified:
    - DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs
    - DeckFlow.Studio/StudioConfig.cs
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio/Pages/DirectPush.razor
    - DeckFlow.Studio/Pages/DirectPush.razor.cs
    - DeckFlow.Studio/STUDIO-SETUP.md
    - DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj
    - DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs
    - DeckFlow.Studio.Tests/DirectPushPageTests.cs
    - DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs

key-decisions:
  - "WriteContentAsync/ConfirmAndPublishAsync split keeps the exact prod-first-then-local stamp/visibility order from the pre-split WritePublishAsync (PUB-01/HIGH-3), and both derive keys via the same ContentIndexExportRow.From helper (Pitfall 5 — ordering invariant must survive the split)."
  - "VerifyAndPublishAsync treats a row with a null/empty BodySha256 as not-confirmed WITHOUT calling the confirmer — there is nothing to match against, and a real HTTP call would be wasted."
  - "DeployedBodyConfirmer reads its config (base URL + creds) per-call from IConfiguration, not cached at construction — mirrors DirectPushCoordinator.CreateProdStore's pattern and lets a misconfigured badge/gate stay accurate without a restart."
  - "StudioConfig.IsConfirmerConfigured defaults to false (not true) so any test/call site that forgets to update stays fail-closed, matching the Studio-side D-04 fail-closed convention — the one exception is DirectPushPageTests.RenderDirectPush's own default (true), scoped explicitly to that helper."
  - "IDeployedBodyConfirmer registered as a Program.cs singleton (not scoped) since its only dependencies are the already-singleton shared HttpClient and IConfiguration — no captive-dependency risk."

patterns-established:
  - "Shared cross-test-file fake (FakeDeployedBodyConfirmer in TestDoubles/) for a coordinator-DI-required interface, mirroring FakeDirectPushFlagReader's placement — used by both DirectPushCoordinatorTests (behavior) and DirectPushPageTests (DI satisfaction only, VerifyAndPublishAsync not yet wired to a page stage)."

requirements-completed: [SYNC-09, SYNC-10]

# Metrics
duration: ~50min
completed: 2026-07-07
---

# Phase 90 Plan 05: Hash-Gated DirectPush Ordering Re-plumb Summary

**Split `DirectPushCoordinator.WritePublishAsync` into a content-only write (sets the D-10 awaiting-confirm marker) and a post-confirm stamp/visibility flip gated by a new bounded `DeployedBodyConfirmer` HTTP poll of the Plan 90-07 deployed-body-hash endpoint — a DirectPush'd row can no longer go visible before its body is durably in git and deployed (SYNC-09/D-06/D-07), and Studio refuses to even start the flow when the new deploy-confirm config is missing (D-09 REVISED/D-10).**

## Performance

- **Duration:** ~50 min
- **Tasks:** 3 completed
- **Files modified:** 14 (4 created, 10 modified)

## Accomplishments
- `DirectPushCoordinator.WritePublishAsync` is gone, replaced by `WriteContentAsync` (content-columns-only batch upsert + `_localStore.SetAwaitingConfirmAsync`, no stamp, no visibility) and `ConfirmAndPublishAsync` (the exact prod-first-then-local Stamp→SetVisibility→Stamp→SetVisibility sequence from before, plus `ClearAwaitingConfirmAsync`) — Stage 3 of DirectPush.razor can never again make a row "Published" before git even runs.
- `DirectPushCoordinator.VerifyAndPublishAsync` derives each pushed row's natural key + `BodySha256`, calls `IDeployedBodyConfirmer`, and runs `ConfirmAndPublishAsync` ONLY for confirmed rows; not-confirmed rows stay content-upserted, hidden, and durably awaiting-confirm — resumable by Plan 90-06, never a false-positive publish.
- `IDeployedBodyConfirmer`/`DeployedBodyConfirmer` (own files, per Codex MED #5) poll `GET Admin/api/contentkb/deployed-body-hash` by natural key with admin BasicAuth: 5 bounded attempts, 3s backoff, `200 && bodySha256 == expected` (ordinal) is the ONLY confirm condition; a 404, hash mismatch, or transient failure is "not yet confirmed" and retried, never a hang and never a false positive.
- `StudioConfig` gains `IsConfirmerConfigured` (three new presence-only keys: `Studio:PublicSiteBaseUrl`, `Studio:AdminUser`, `Studio:AdminPassword`, values never logged); DirectPush.razor shows a third red/green "Deploy-confirm" badge and refuses to start the whole flow (Stage 1 button disabled, code-behind hard guard) when unconfigured — a missing-creds push can never silently hang on a future 401.
- `RichardSzalay.MockHttp` 7.0.0 added to `DeckFlow.Studio.Tests` (pinned to the same version already used by `DeckFlow.Web.Tests`) so `DeployedBodyConfirmerTests` stubs all HTTP — no live network call anywhere in the test suite.

## Task Commits

Each task was committed atomically:

1. **Task 1: Split WritePublishAsync into content-only write (+marker) and post-confirm stamp/visibility** - `acdcb581` (feat)
2. **Task 2: Add Studio deploy-confirm config (keys + StudioConfig flag + badge/gate + docs)** - `5f108760` (feat)
3. **Task 3: DeployedBodyConfirmer (endpoint hash-match poll by natural key) gating ConfirmAndPublishAsync** - `e80000ac` (feat)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP update)

## Files Created/Modified
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` - `WriteContentAsync`/`ConfirmAndPublishAsync` split; `VerifyAndPublishAsync` + `DirectPushVerifyResult`; `IDeployedBodyConfirmer` ctor dependency; shared `DeriveKeys` helper.
- `DeckFlow.Studio/Services/IDeployedBodyConfirmer.cs` - New. Confirm-poll contract.
- `DeckFlow.Studio/Services/DeployedBodyConfirmer.cs` - New. Bounded HTTP poll implementation + internal test-seam ctor.
- `DeckFlow.Studio/StudioConfig.cs` - `IsConfirmerConfigured` (default `false`).
- `DeckFlow.Studio/Program.cs` - Presence checks for the three new keys; `IDeployedBodyConfirmer` DI registration; startup log line.
- `DeckFlow.Studio/Pages/DirectPush.razor` - Third badge, warning banner, Stage-1 gate extension; corrected the Stage-3 success copy (no longer claims "published visible").
- `DeckFlow.Studio/Pages/DirectPush.razor.cs` - `WriteRowsAsync` calls `WriteContentAsync`; `ComputeDiffAsync` guard extended with `IsConfirmerConfigured`.
- `DeckFlow.Studio/STUDIO-SETUP.md` - Documents the three new keys, their `Studio__*` env mappings, and the "must match web `FEEDBACK_ADMIN_*`" note.
- `DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` - Added `RichardSzalay.MockHttp` 7.0.0.
- `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` - Replaced `WritePublishAsync` tests with `WriteContentAsync`/`ConfirmAndPublishAsync` tests; added `VerifyAndPublishAsync` tests (confirmed/not-confirmed/no-hash/mixed-batch).
- `DeckFlow.Studio.Tests/DirectPushPageTests.cs` - `RenderDirectPush` gains `isConfirmerConfigured`; registers `FakeDeployedBodyConfirmer`; rewrote the three Stage-3 tests that previously asserted immediate stamp/visibility; added confirmer badge/gate tests.
- `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs` - Real (tracked) `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync` implementations (the interface's default throws).
- `DeckFlow.Studio.Tests/TestDoubles/FakeDeployedBodyConfirmer.cs` - New. Shared deterministic confirmer double.
- `DeckFlow.Studio.Tests/Services/DeployedBodyConfirmerTests.cs` - New. MockHttp-stubbed match/mismatch/404/retry-then-match/missing-config coverage.

## Decisions Made
- Kept the exact prod-first-then-local stamp/visibility ordering and `ContentIndexExportRow.From` key derivation across the split (Pitfall 5) — verified by dedicated tests, not just code inspection.
- `VerifyAndPublishAsync` short-circuits a null/empty `BodySha256` row to not-confirmed without ever calling the confirmer (nothing to match against).
- `DeployedBodyConfirmer` reads config per-call, not at construction, so a badge/gate change doesn't require a Studio restart to take effect in the confirm path.
- `IDeployedBodyConfirmer` registered singleton (not scoped) — its dependencies (`HttpClient`, `IConfiguration`) are already singletons, so no captive-dependency risk exists.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Three existing DirectPushPageTests asserted the pre-split Stage-3 behavior**
- **Found during:** Task 1, running the full `DeckFlow.Studio.Tests` suite after the split (not just the plan's filtered `DirectPushCoordinator` subset).
- **Issue:** `DirectPush_Success_StampsLocalAndProd_WithSameInstant`, `DirectPush_Success_PublishesRowsVisible_LocalAndProd`, and `H4_Success_BatchMethodCalled_AllRowsWritten_StampAndVisibilityRan` asserted that clicking the Stage-3 button immediately stamps `pushed_to_prod_utc` and flips `is_visible` — the exact contract-before-expand behavior this plan fixes. They failed for the correct reason (bUnit `WaitForAssertion` timeout, empty `StampCalls`/`VisibilityKeyCalls`).
- **Fix:** Rewrote all three to assert the NEW D-06/D-07 behavior: content-only batch upsert + local awaiting-confirm marker set, zero stamp/visibility calls on either store, rows stay hidden, and the success banner reads "awaiting deploy confirmation."
- **Files modified:** `DeckFlow.Studio.Tests/DirectPushPageTests.cs`.
- **Verification:** `dotnet test DeckFlow.Studio.Tests --filter DirectPush` — 65/65 passed.
- **Committed in:** `acdcb581` (Task 1 commit).

**2. [Rule 3 - Blocking] `FakeContentSiteIndexStore` had no real `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync`**
- **Found during:** Task 1, writing the marker-set/clear assertions for `WriteContentAsync`/`ConfirmAndPublishAsync`.
- **Issue:** `IContentSiteIndexStore`'s default interface methods for these two members throw `NotSupportedException` (90-03's intentional throwing-escape-hatch idiom for unrelated doubles) — the local store fake used in these tests would throw the moment `WriteContentAsync` called `SetAwaitingConfirmAsync`.
- **Fix:** Added tracked, real implementations (`SetAwaitingConfirmCalls`/`ClearAwaitingConfirmCalls` + row mutation) mirroring the existing `StampPushedToProdAsync`/`SetVisibilityAsync` pattern in the same fake.
- **Files modified:** `DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs`.
- **Verification:** `dotnet build DeckFlow.Studio.Tests` clean; marker-set/clear tests pass.
- **Committed in:** `acdcb581` (Task 1 commit).

**3. [Rule 3 - Blocking] `DirectPushCoordinator`'s new required ctor param broke `DirectPushPageTests`' DI setup twice**
- **Found during:** Task 2 (`StudioConfig`'s new 3rd positional arg) and Task 3 (`IDeployedBodyConfirmer` ctor param).
- **Issue:** `DirectPushPageTests.RenderDirectPush` builds its own bUnit `Services` collection; each new required constructor input needed a corresponding registration or the page would fail to resolve `DirectPushCoordinator` at render time.
- **Fix:** Task 2 added `isConfirmerConfigured` to `RenderDirectPush` (default `true`) and passed it into the 3-arg `StudioConfig`. Task 3 registered a shared `FakeDeployedBodyConfirmer` (extracted to `TestDoubles/` since both `DirectPushCoordinatorTests` and `DirectPushPageTests` needed it, mirroring `FakeDirectPushFlagReader`'s placement).
- **Files modified:** `DeckFlow.Studio.Tests/DirectPushPageTests.cs`, `DeckFlow.Studio.Tests/TestDoubles/FakeDeployedBodyConfirmer.cs` (new).
- **Verification:** `dotnet test DeckFlow.Studio.Tests` full suite green after each task.
- **Committed in:** `5f108760` (Task 2), `e80000ac` (Task 3).

---

**Total deviations:** 3 auto-fixed, all Rule 3 (blocking — required to keep the build/test suite compiling and honest after the split). None expanded scope beyond what SYNC-09/SYNC-10/D-06/D-07/D-09-REVISED/D-10 require.

## Issues Encountered
One pre-existing flaky bUnit test unrelated to this plan (`ReviewPageTests.ApproveEntry_OnPendingPodcastRow_CallsSetApprovalStatusWithPodcastType`, a `WaitForAssertion` timing flake) failed once in a full-suite run and passed on immediate re-run in isolation and in a second full-suite run. Not touched by this plan's changes (Review page, not DirectPush); not fixed here as out of scope (SCOPE BOUNDARY).

## User Setup Required

None required to close this plan — the DirectPush page already shows the correct red "not configured" state and refuses to start with helpful copy. To exercise the new deploy-confirm poll end-to-end (deferred to the operator, likely alongside Plan 90-06/93), set in Studio user-secrets:
- `Studio:PublicSiteBaseUrl` = the live site base URL (e.g. `https://www.deckflow.gg`)
- `Studio:AdminUser` / `Studio:AdminPassword` = the SAME values as the web app's `FEEDBACK_ADMIN_USER`/`FEEDBACK_ADMIN_PASSWORD`

See `DeckFlow.Studio/STUDIO-SETUP.md` for exact commands.

## Next Phase Readiness
- `VerifyAndPublishAsync` exists and is fully unit-tested but is NOT yet wired to a DirectPush.razor UI stage — per this plan's scope (files_modified lists `DirectPush.razor`/`.razor.cs` only for the Task 2 badge/gate, not a new Stage 5), that wiring — plus the durable-marker resume UI — is Plan 90-06's job. A `WriteContentAsync` push today leaves rows durably awaiting-confirm with no operator-visible path to confirm them until 90-06 lands; this is the intended, documented interim state (D-10 exists precisely so this is safe and recoverable).
- The confirmer's real HTTP behavior against a live Render deploy is unit-tested (MockHttp) but not exercised end-to-end against production — that manual verification is explicitly deferred per the plan's `<verification>` block (async, live Render, not unit-coverable) to Phase 93 / operator flag-flip time.
- `DeckFlow.sln` builds with 0 warnings/0 errors; full suite green (Core 1149, Studio 338 + 3 Postgres-skip, Web 1249 + 12 Postgres-skip).
- No blockers.

## Self-Check: PASSED

All 4 created files verified present on disk; all 3 task commit hashes (`acdcb581`, `5f108760`, `e80000ac`) verified present in `git log --oneline --all`.

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-07*
