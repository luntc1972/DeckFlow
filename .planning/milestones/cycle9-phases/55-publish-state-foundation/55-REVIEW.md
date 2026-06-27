---
phase: 55-publish-state-foundation
reviewed: 2026-06-18T18:45:00Z
depth: standard
files_reviewed: 17
files_reviewed_list:
  - DeckFlow.Core/Content/ContentSiteIndexStore.cs
  - DeckFlow.Core/Content/IContentSiteIndexStore.cs
  - DeckFlow.Core/Content/PublishState.cs
  - DeckFlow.Core/Content/PublishStateDeriver.cs
  - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs
  - DeckFlow.Studio/Pages/DirectPush.razor
  - DeckFlow.Studio/Pages/Publish.razor
  - DeckFlow.Core.Tests/Content/ContentSiteIndexStorePushedToProdTests.cs
  - DeckFlow.Core.Tests/Content/PublishStateDeriverTests.cs
  - DeckFlow.Core.Tests/Orchestration/ContentPublishStampTests.cs
  - DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs
  - DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs
  - DeckFlow.Studio.Tests/DirectPushPageTests.cs
  - DeckFlow.Studio.Tests/PublishPageTests.cs
  - DeckFlow.Studio.Tests/TestDoubles/FakeContentSiteIndexStore.cs
  - DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs
  - DeckFlow.Core/Orchestration/ContentIndexExportRow.cs
findings:
  critical: 0
  warning: 3
  info: 4
  total: 7
status: resolved
warnings_resolved: >-
  WR-01/WR-02/WR-03 all fixed in commit b929bde (Publish.razor only): added _stampWarning field
  rendered outside the @if(_diffReady) guard (gated on _commitSuccess); post-commit stamp
  cancellation/failure now folded into a single non-fatal catch (no OCE rethrow past the commit
  boundary, so a landed commit is never mislabeled "cancelled"); the three raw ex.Message UI
  surfaces (init diff, artifact-missing, general export) replaced with sanitized literals matching
  DirectPush's hardening. Build 0 errors, DeckFlow.Studio.Tests 36/36. Info items IN-01..04 left as
  accepted (cosmetic/defensive, mostly pre-existing).
---

# Phase 55: Code Review Report

**Reviewed:** 2026-06-18T18:45:00Z
**Depth:** standard
**Files Reviewed:** 17
**Status:** issues_found

## Summary

Reviewed the Phase 55 (Publish-State Foundation) production code and its test/fake surface
against the locked design contract in 55-01-PLAN.md / 55-02-PLAN.md.

Verified ground-truth invariants hold:

- **HIGH-1 (single writer):** `pushed_to_prod_utc` is absent from all three upserts
  (`UpsertSql`, `UpsertPreservingVisibilitySql`, `UpsertContentColumnsOnlySql`) and their
  anonymous param objects, present in both `CREATE TABLE` constants
  (`ContentSiteIndexStore.cs:900` PG TIMESTAMPTZ, `:923` SQLite TEXT), in the idempotent
  guarded `ADD COLUMN` block (`:94-101`, dialect-branched), in all five SELECT lists
  (`:257, 291, 325, 358, 390`), and written only by `StampPushedToProdAsync` (`:592-625`).
  `CreateTableDdl_AndContentOnlyUpsert_KeepPushedToProdSeparateFromUpsertWriter`
  (test `:130-140`) pins this.
- **DirectPush key-type BLOCKER (ed68afa)** is correctly and completely fixed:
  `DirectPush.razor:722-727` now builds the stamp key set via
  `ContentIndexExportRow.From(row)` → `NaturalKeyType`/`NaturalKeyValue`, which return the
  canonical `ContentSourceType.Youtube` (`"youtube_channel"`) / `.Podcast` (`"podcast_rss"`)
  values that the store actually persists (`ContentIndexExportRow.cs:72-85`,
  `ContentModels.cs:183/186`). The previously-failing bUnit
  `DirectPush_Success_StampsLocalAndProd_WithSameInstant` now asserts both stores receive the
  stamp with equal instants and that rows are actually updated (non-vacuous; the local store is
  seeded with 2 rows and the prod store is populated by the upsert batch). Not re-reported as open.
- **SQLite `DateTimeOffset?` round-trip** is exercised at full tick precision
  (`...PushedToProdTests.cs:83-95` stamps `2026-06-18T23:14:15.1234567+00:00` and asserts exact
  equality after a TEXT round-trip), so no F-51-PG-01-style mismatch on the SQLite path. The
  Postgres `TIMESTAMPTZ` ALTER + stamp remains a documented manual gate (`DECKFLOW_POSTGRES_TESTS`).
- **PublishStateDeriver** precedence (null→NeverPublished, !visible→PushedHidden,
  local>push→LocalNewer, else Published), UTC normalization via `.ToUniversalTime().UtcDateTime`
  on both operands, and equal-instant⇒Published boundary are all correct
  (`PublishStateDeriver.cs:15-36`), with display strings single-sourced in `PublishState.cs:31-39`
  and exhaustively tested including the cross-offset same-instant case.
- No leftover `with { PushedToProdUtc … }` upsert approach remains in either Razor page.

The findings below are quality/robustness defects in the **git Publish** stamp-failure handling
and a few minor consistency items. None are correctness or security blockers.

## Warnings

### WR-01: Post-commit stamp-failure message is set but never rendered (silently swallowed)

**File:** `DeckFlow.Studio/Pages/Publish.razor:527-534` (with markup at `:121` and `:129`)
**Issue:** After a successful commit, when `StampPushedToProdAsync` throws a non-cancellation
exception, the catch sets `_commitError = "Commit succeeded, but pushed-to-prod stamp failed: …"`
(`:529`). Execution then falls through to `:534` which sets `_diffReady = false`. But the only
markup that renders `_commitError` is the Stage 2 card alert at `:129`, which is nested inside
`@if (_diffReady)` (`:121`). Because `_diffReady` is now `false`, that alert is torn down, so the
stamp-failure message is **never shown to the operator**. The success alert at `:180` (gated on
`_commitSuccess`, which is `true`) renders alone, telling the operator everything is fine when the
local `pushed_to_prod_utc` was in fact not stamped. The plan explicitly required a "clear non-fatal
message" on stamp failure (55-01 task 2, HIGH-2) — that contract is not met.
**Fix:** Render the stamp-failure message outside the `@if (_diffReady)` guard, alongside the
post-commit success block (which is already correctly hoisted out of `_diffReady` per the comment
at `:177-179`). For example, add after `:193`:
```razor
@if (_commitSuccess && !string.IsNullOrEmpty(_commitError))
{
    <div class="alert alert-warning py-2">@_commitError</div>
}
```
(Or use a dedicated `_stampWarning` field so it is not confused with the pre-commit `_commitError`.)

### WR-02: Cancellation during the post-commit stamp mislabels a successful commit as "cancelled"

**File:** `DeckFlow.Studio/Pages/Publish.razor:510-540`
**Issue:** The commit succeeds and sets `_commitSha`/`_commitSuccess = true` at `:510-511`. The
stamp block then runs under `_cts.Token`. If the circuit drops (operator closes the tab) between
commit success and stamp completion, `StampPushedToProdAsync` throws `OperationCanceledException`,
which is explicitly rethrown at `:525` and caught by the outer handler at `:538`, setting
`_commitError = "Commit was cancelled."`. This is wrong: the commit already landed on the branch.
The operator would see a "cancelled" message for a commit that actually happened, and the
local stamp would be missing — a confusing and misleading state. (In practice the rethrow also
skips `:534-536`, so `_diffReady` stays `true` and the stale Stage 2 card with the "cancelled"
error renders over a real commit.)
**Fix:** Do not rethrow the stamp's `OperationCanceledException` past the commit boundary. Treat a
cancelled stamp the same as a failed stamp (commit already succeeded), e.g. fold it into the
inner catch:
```csharp
catch (Exception ex)
{
    // Commit already succeeded; a failed/cancelled stamp is non-fatal.
    _stampWarning = $"Commit succeeded, but pushed-to-prod stamp did not complete: {ex.Message}";
}
```
and let the `finally` at `:553` do the single `StateHasChanged`. Combine with WR-01's out-of-guard
rendering so the operator is told to re-export/re-stamp.

### WR-03: Init-error path leaks raw exception text into the UI (inconsistent with the page's own secret-handling rules)

**File:** `DeckFlow.Studio/Pages/Publish.razor:290-294` (and Stage-1 export errors at `:347, 377, 481`)
**Issue:** `OnInitializedAsync` surfaces `ex.Message` directly:
`_initError = $"Could not compute git diff — {ex.Message}. …"`. The sibling DirectPush page
deliberately never surfaces `ex.Message` in any catch (see `DirectPush.razor:474-476, 571-578,
702-708, 761-768`, all citing D-07 / SC5 secret-leak avoidance). `GitCommandException.Message` here
echoes git stderr, which can include absolute filesystem paths (operator home dir / repo location)
and the working directory. While git stderr is lower-risk than an Npgsql/SSH connection string,
surfacing raw subprocess output to the UI is the exact anti-pattern the project hardened against in
Phase 47, and the inconsistency between the two publish pages is a maintenance hazard.
**Fix:** Surface a sanitized literal and log the detail server-side, matching DirectPush's pattern:
```csharp
_initError = "Could not compute git diff — check that Studio is running from the repo root.";
// log ex via ILogger at Warning, not into the rendered message
```
Apply the same treatment to the Stage-1 `_exportError = $"… {ex.Message}"` sites (`:347, 377, 481`).

## Info

### IN-01: Diff/result display rows use literal "youtube"/"podcast" key-type labels

**File:** `DeckFlow.Studio/Pages/DirectPush.razor:530` (and `:689`)
**Issue:** The in-memory `DiffRow.KeyType` (`:530`) and `RowResult.KeyType` (`:689`) are built from
the literal strings `"youtube"`/`"podcast"`, then shown in the Stage-1 diff table (`:173`
`@item.KeyType:@item.KeyValue`) and the Stage-3 reconcile table (`:346`). These are display-only —
verified they do **not** feed any store key (the stamp keys come from `ContentIndexExportRow.From`
at `:722-727`, and the upsert derives its own key internally). So there is no correctness bug. But
the operator-facing label (`youtube:…`) does not match the canonical persisted type
(`youtube_channel`), and the same literal-vs-constant pitfall is what caused the ed68afa BLOCKER
two lines down. `VideoStatusResolver.cs:65` carries an explicit "never the raw string literal"
warning for exactly this.
**Fix:** Derive the display label from `ContentSourceType.Youtube`/`.Podcast` (or a short display
helper) so the table matches stored values and the literal does not invite a future copy-paste into
a key path. Low priority — cosmetic + defensive.

### IN-02: `StampPushedToProdAsync` and the batch `SetApprovalStatusAsync` are near-identical (duplication)

**File:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:553-625`
**Issue:** `StampPushedToProdAsync` (`:592-625`) is a structural clone of the batch
`SetApprovalStatusAsync` (`:553-589`): same null-guard + empty short-circuit + open-connection +
begin-transaction + per-key `ExecuteAsync` loop + commit + return-total. Only the SQL constant and
the bound param differ. This is the second copy of the pattern (the plan even instructs "clone…").
A third writer (a future Phase 56/57 batch setter) would make it three. SOLID/DRY: the per-key
transactional batch-UPDATE loop is a reusable private helper.
**Fix (optional):** Extract a private
`Task<int> BatchUpdateByKeyAsync(string sql, Func<(string Type,string Value), object> paramFactory, IReadOnlyList<…> keys, CancellationToken ct)`
and have both methods delegate to it. Not required for ship; flag for the next touch.

### IN-03: Web `FakeContentSiteIndexStore.StampPushedToProdAsync` is a silent no-op returning 0

**File:** `DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs:187-191`
**Issue:** Unlike the approval-status fakes in the same file (which actually mutate `Rows`), the
stamp fake just returns `Task.FromResult(0)` without recording the call or mutating rows. This is
acceptable today because no Web test inspects the stamp (the plan permits pure no-op for such
fakes). The risk is latent: a future Web test that publishes and then asserts a derived publish
state would pass vacuously because nothing is stamped. The Studio fake (`:204-227`) and the Core
recording fake (`ContentPublishStampTests.cs:137-154`) both apply the stamp by natural key — the
Web fake is the odd one out.
**Fix (optional):** Mirror the Studio fake's by-natural-key apply + `StampCalls` recording so the
Web fake fails loudly rather than silently if a future test depends on stamping. Documentation-only
otherwise.

### IN-04: `OnInitializedAsync` does background work without a try/catch around `InvokeAsync(StateHasChanged)` in finally — minor

**File:** `DeckFlow.Studio/Pages/Publish.razor:296-300`
**Issue:** Every other `StateHasChanged` call in both pages is wrapped in
`try { StateHasChanged(); } catch (ObjectDisposedException/InvalidOperationException) { }` to
survive a disposed circuit. The `finally` block at `:297-300` calls
`await InvokeAsync(StateHasChanged)` unguarded. If the circuit was disposed during init, this can
throw `ObjectDisposedException` from inside `finally`, which would mask the original exception path.
DirectPush's equivalent `finally` (`:478-482`) has the same shape, so this is a consistent (minor)
gap, not a regression introduced by Phase 55.
**Fix (optional):** Wrap the `InvokeAsync(StateHasChanged)` in the `finally` with the same
disposed-circuit guard used elsewhere, or note it as pre-existing and out of Phase 55 scope.

---

_Reviewed: 2026-06-18T18:45:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
