---
phase: 260627-qyc
verified: 2026-06-27T00:00:00Z
status: passed
score: 8/8 must-haves verified
overrides_applied: 0
---

# Quick Task 260627-qyc: DirectPush Prod-Write Integrity Verification

**Task Goal:** H4 (atomic all-or-nothing batch upsert; rollback on any row failure; D-08 content-columns-only preserved) + M2 (content-aware diff classifying New/Updated/Unchanged; Unchanged excluded from publish set, keyed on full natural-key tuple).
**Verified:** 2026-06-27
**Status:** PASSED
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | A mid-batch prod content upsert failure leaves PROD with zero rows from that batch written (all-or-nothing). | ✓ VERIFIED | `ContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync` (lines 628-722): one connection, one `BeginTransactionAsync`, per-row loop with exception catch that calls `transaction.RollbackAsync(CancellationToken.None)` then throws `ContentSiteIndexBatchUpsertException`. Core test `UpsertContentColumnsOnlyBatchAsync_BadRowMidBatch_RollsBackAll` inserts `validRow` first (mid-batch), then `badRow` (bad path), asserts `GetAllRowsAsync()` returns 0. bUnit test `H4_BatchRollback_ZeroRowsCommitted_TitleSurfaced_NoSecretLeak_NoStamp` asserts `Assert.Empty(prodStore.Rows)`. |
| 2  | On full success, every approved publish-set row is content-upserted to PROD in one transaction, then stamped + published visible. | ✓ VERIFIED | `WriteRowsAsync` (DirectPush.razor line 734): `await prodStore.UpsertContentColumnsOnlyBatchAsync(_publishRows, Cts.Token)` — single call. On success: `StampPushedToProdAsync` then `SetVisibilityAsync` on prod store, then same on local store. bUnit test `H4_Success_BatchMethodCalled_AllRowsWritten_StampAndVisibilityRan` asserts single batch call + both stores stamped + visible. |
| 3  | DirectPush calls the single transactional batch method, never the per-row upsert loop. | ✓ VERIFIED | `WriteRowsAsync` has exactly one call: `prodStore.UpsertContentColumnsOnlyBatchAsync(_publishRows, Cts.Token)` — no foreach loop calling `UpsertContentColumnsOnlyAsync`. FakeContentSiteIndexStore tracks `UpsertMethodCalls`. Test `DirectPush_UsesContentColumnsOnlyUpsert` asserts `Count(c => c == "UpsertContentColumnsOnlyBatchAsync") == 1` and `DoesNotContain("UpsertContentColumnsOnlyAsync")`. |
| 4  | ComputeDiffAsync classifies an already-present row as Unchanged when its content columns match prod, and excludes it from the publish set (no SCP, no DB write). | ✓ VERIFIED | `ComputeDiffAsync` (line 582): `!ContentSiteIndexContentSignature.AreContentEqual(row, prodRow)` — when equal, only `unchangedCount++` is executed; `publishRows.Add(row)` is NOT called. `UploadArtifactsAsync` uses `_publishRows` (line 649). `WriteRowsAsync` uses `_publishRows` (line 734). Test `M2_AllUnchanged_Stage2And3CardsDoNotRender` asserts no Stage 2/3 cards render and `Assert.Empty(prodStore.BatchUpsertCalls)`. Test `M2_BatchWrite_ExcludesUnchangedRows` asserts batch receives only New + Updated rows, not Unchanged. |
| 5  | ComputeDiffAsync classifies a changed row as Updated and a missing row as New, and includes both in the publish set. | ✓ VERIFIED | Missing key: `newCount++; publishRows.Add(row); diffRows.Add(...IsNew: true)`. Key present but `!AreContentEqual`: `updatedCount++; publishRows.Add(row); diffRows.Add(...IsNew: false)`. Tests `M2_ComputeDiff_ClassifiesNewUpdatedUnchanged_Correctly` (New=1, Updated=1, Unchanged=1) and `DirectPush_DiffPreview_ShowsNewUpdatedCounts` cover both paths. |
| 6  | The UI shows accurate New / Updated / Unchanged counts. | ✓ VERIFIED | DirectPush.razor lines 149-151 render `<span class="badge bg-success">New: @_newCount</span>`, `<span class="badge bg-primary">Updated: @_updatedCount</span>`, `<span class="badge bg-secondary">Unchanged: @_unchangedCount</span>` inside the `else` branch (shown when `_newCount > 0 || _updatedCount > 0`). "already up to date" gated on `_newCount == 0 && _updatedCount == 0`. Multiple tests assert all three badge counts appear in markup. |
| 7  | On rollback, the UI states nothing was written and names the row that aborted, without leaking any secret from the underlying DB exception. | ✓ VERIFIED | `catch (ContentSiteIndexBatchUpsertException ex)` (line 773): `_dbError = $"Row '{ex.FailedRowTitle}' failed — the entire batch was rolled back. NOTHING was written to production. See logs."` — only `ex.FailedRowTitle` (non-secret) used; `ex.Message` and `ex.InnerException.Message` are never accessed. Tests `H4_BatchRollback_ZeroRowsCommitted_TitleSurfaced_NoSecretLeak_NoStamp` and `DirectPush_DbWriteFailure_SecretsNeverSurface` set a sentinel string `Host=prod-db.example.com;Username=admin;Password=hunter2` as `UpsertFailureMessage` and assert none of `["Host=", "Password", "hunter2", "prod-db.example.com"]` appear in markup. |
| 8  | DeckFlow.Core, DeckFlow.Studio, DeckFlow.Core.Tests, and DeckFlow.Studio.Tests all compile. | ✓ VERIFIED | Orchestrator confirms: full solution build clean (0 warnings, 0 errors). Core batch tests 13/13 pass. DirectPush bUnit tests 24/24 pass (including new M2 classification + H4 rollback + collision regression). |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | Transactional `UpsertContentColumnsOnlyBatchAsync` (one connection, one DbTransaction, content-columns-only SQL, commit-at-end, rollback-on-any-failure) | ✓ VERIFIED | Lines 628-722: `ArgumentNullException.ThrowIfNull(rows)`; guard `rows.Count == 0`; `EnsureSchemaAsync`; single `OpenConnectionAsync`; `BeginTransactionAsync`; per-row validation + `connection.ExecuteAsync(UpsertContentColumnsOnlySql, ..., transaction: transaction)`; `OperationCanceledException` rethrows after rollback; generic catch wraps in `ContentSiteIndexBatchUpsertException` with `CancellationToken.None` rollback; `CommitAsync` on success. |
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` | Batch upsert contract with default-throwing implementation | ✓ VERIFIED | Lines 199-215: full XML doc, method signature `Task UpsertContentColumnsOnlyBatchAsync(IReadOnlyList<ContentSiteIndexRow>, CancellationToken)`, default implementation `=> throw new NotSupportedException("This content site-index store does not support batch content upsert.")` — mirrors `DeleteAllRowsAsync` precedent at line 111. |
| `DeckFlow.Core/Content/ContentSiteIndexBatchUpsertException.cs` | Typed batch-abort exception with title + key + non-secret invariant | ✓ VERIFIED | `public sealed class ContentSiteIndexBatchUpsertException : Exception` with `FailedRowTitle`, `FailedKeyType`, `FailedKeyValue` read-only properties; ctor takes all three + `message` + `innerException`; XML doc explicitly states "MUST NOT be surfaced to the UI" for `innerException`. |
| `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` | Stable content signature over exact UpsertContentColumnsOnly column set; cross-dialect date normalization; tags by value | ✓ VERIFIED | `static class ContentSiteIndexContentSignature`. `BuildSignature` signs: source, title, video_url, artifact_path, published_utc (null sentinel), indexed_utc, archetype_tags, bracket_tags, card_category_tags. Excludes is_visible/is_hidden/is_evergreen/approval_status/pushed_to_prod_utc. `TruncateToSeconds` strips sub-second ticks; `ContentArtifactSpec.SerializeTags` used for tags; null-byte `FieldDelimiter` prevents value collisions. `AreContentEqual` delegates to `string.Equals(..., Ordinal)`. |
| `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreBatchUpsertTests.cs` | SQLite integration tests: commit, rollback, content-columns-only preservation, empty list, signature unit tests | ✓ VERIFIED | 13 `[Fact]` methods covering: AllValid_CommitsAllRows, BadRowMidBatch_RollsBackAll, BadRowMidBatch_ExceptionHasInnerException, ExistingRow_PreservesIsVisibleAndIsEvergreen, EmptyList_IsNoOp; signature: SameContent_Equal, DifferentTitle_Different, SubSecond_Equal (IndexedUtc + PublishedUtc), SameTagsDifferentListReferences_Equal, AreContentEqual_Identical_True, DifferentTitle_False, NullPublishedUtc_NotMatchNonNull. |
| `DeckFlow.Studio/Pages/DirectPush.razor` | Content-aware diff (New/Updated/Unchanged) + single atomic batch write call | ✓ VERIFIED | `ComputeDiffAsync`: `Dictionary<string, ContentSiteIndexRow>` keyed on composite `$"{prodKeyType} {prodKeyValue}"`. `AreContentEqual` branch for Unchanged. `_publishRows` = New + Updated only. `UploadArtifactsAsync` sources `_publishRows`. `WriteRowsAsync` calls `UpsertContentColumnsOnlyBatchAsync(_publishRows, ...)`. Typed catch for `ContentSiteIndexBatchUpsertException` with rollback UI. |
| `DeckFlow.Studio.Tests/DirectPushPageTests.cs` | bUnit tests for M2 classification + H4 batch commit/rollback + (type,value) collision regression | ✓ VERIFIED | New tests: `M2_ComputeDiff_ClassifiesNewUpdatedUnchanged_Correctly`, `M2_ComputeDiff_DifferentKeyTypeSameValue_NotMisclassifiedUnchanged`, `M2_BatchWrite_ExcludesUnchangedRows`, `M2_AllUnchanged_Stage2And3CardsDoNotRender`, `H4_Success_BatchMethodCalled_AllRowsWritten_StampAndVisibilityRan`, `H4_BatchRollback_ZeroRowsCommitted_TitleSurfaced_NoSecretLeak_NoStamp`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `DirectPush.razor` | `ContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync` | `prodStore` batch call in `WriteRowsAsync` | ✓ WIRED | Line 734: `await prodStore.UpsertContentColumnsOnlyBatchAsync(_publishRows, Cts.Token)` — single call, no per-row loop. |
| `DirectPush.razor` | `ContentSiteIndexContentSignature.BuildSignature` | `ComputeDiffAsync` content comparison | ✓ WIRED | Line 582: `!ContentSiteIndexContentSignature.AreContentEqual(row, prodRow)` — called for every prod-key-matched local row. |
| `ContentSiteIndexStore.cs` | `UpsertContentColumnsOnlySql` | Batch loop reuses existing content-columns-only SQL inside one transaction | ✓ WIRED | Line 664: `UpsertContentColumnsOnlySql` passed as the SQL to `connection.ExecuteAsync` inside the batch loop; same const used by per-row `UpsertContentColumnsOnlyAsync` at line 220. ON CONFLICT clause excludes is_visible/is_hidden/is_evergreen/approval_status (line 1015-1025 + comment at 1025). |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|--------------------|--------|
| `DirectPush.razor` | `_publishRows` | `ComputeDiffAsync`: `IndexStore.GetApprovedRowsAsync` (local) + `prodStore.GetAllRowsAsync` (prod) → signature compare | Yes — built from live store queries, classified rows, excludes Unchanged | ✓ FLOWING |
| `DirectPush.razor` | batch upsert | `WriteRowsAsync` calls `UpsertContentColumnsOnlyBatchAsync(_publishRows)` → real `UpsertContentColumnsOnlySql` executed inside DbTransaction | Yes — SQL DML in one transaction, committed or rolled back | ✓ FLOWING |

### Behavioral Spot-Checks

Step 7b: SKIPPED — the runnable path requires a live Postgres + SSH endpoint unavailable in WSL; bUnit and SQLite integration tests exercise all logic paths covered by the must-haves. Build + test pass per orchestrator confirms the code is exercisable.

### Probe Execution

No probes declared in PLAN.md and no conventional `scripts/*/tests/probe-*.sh` referenced for this task. SKIPPED.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| H4 | 260627-qyc-01 | Atomic all-or-nothing batch upsert; rollback on any row failure | ✓ SATISFIED | `UpsertContentColumnsOnlyBatchAsync` in store (single connection + transaction); typed exception; rollback tested; DirectPush single-call wiring verified. |
| M2 | 260627-qyc-01 | Content-aware diff classifying New/Updated/Unchanged; Unchanged excluded from publish set; keyed on full (type,value) composite | ✓ SATISFIED | `ComputeDiffAsync` composite key map + `AreContentEqual` classification; `_publishRows` excludes Unchanged; SCP + DB both source `_publishRows`; collision regression test added. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ContentSiteIndexStore.cs` | 686 | `transaction.RollbackAsync(cancellationToken)` inside `catch (OperationCanceledException)` — passes the already-cancelled token to RollbackAsync | ℹ️ Info | Non-blocking: the generic `catch (Exception ex)` path correctly uses `CancellationToken.None` at line 691. The OCE path uses the cancelled token; in practice SQLite and Postgres rollback operations complete regardless, and `await using var transaction` provides a disposal safety net. Comment at line 685 acknowledges this: "the transaction will be cleaned up by Dispose." No observable correctness impact. |

No `TBD`, `FIXME`, or `XXX` debt markers found in any file modified by this task. No stub patterns. No empty implementations on production data paths. No hardcoded empty data flowing to renders. The `{ get; }` properties on `ContentSiteIndexBatchUpsertException` are correct for an exception class (set via constructor, not deserializer) — not a carve-out violation.

### Human Verification Required

None. All must-haves are verifiable from source code and the orchestrator-confirmed test results. No visual, real-time, or external-service checks are required to establish goal achievement.

### Gaps Summary

No gaps. All 8 must-have truths verified against actual source code. The one INFO-level observation (cancelled token passed to `RollbackAsync` in the OCE handler) has no correctness impact and is self-documented in the code.

---

_Verified: 2026-06-27_
_Verifier: Claude (gsd-verifier)_
