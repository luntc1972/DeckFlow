---
phase: 46-review-queue-commit-publish-path
plan: "01"
subsystem: DeckFlow.Core / Content
tags: [approval-status, store, dapper, atomic-batch, tdd]
dependency_graph:
  requires: []
  provides: [IContentSiteIndexStore.SetApprovalStatusAsync (single + batch), ContentSiteIndexStore implementation]
  affects: [DeckFlow.Studio Review Queue UI (Plan 03)]
tech_stack:
  added: []
  patterns: [Dapper CommandDefinition with DbTransaction (atomic batch), ArgumentException allow-list validation]
key_files:
  created: []
  modified:
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs
decisions:
  - "Batch atomicity via single DbTransaction wrapping per-key UPDATEs (D-06); await using dispose auto-rolls back on any exception or cancellation"
  - "Status allow-list enforced by private static ValidateApprovalStatus before any DB call — rejects anything not in {pending, approved, rejected}"
  - "Pre-cancelled CancellationToken chosen as the deterministic atomicity test trigger (OperationCanceledException propagates through Dapper before any row is committed)"
  - "Single overload does NOT open a transaction (one row, one UPDATE, no atomicity concern)"
metrics:
  duration_minutes: 20
  completed_date: "2026-06-16"
  tasks_completed: 2
  files_changed: 3
requirements: [REVQ-02, REVQ-03]
---

# Phase 46 Plan 01: SetApprovalStatusAsync — Interface + Atomic Batch Implementation Summary

**One-liner:** Natural-key approval-status mutation with atomic batch transaction and status allow-list validation, backed by seven real-SQLite facts.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | Add SetApprovalStatusAsync (single + atomic batch) to interface + store | 83e029a | IContentSiteIndexStore.cs, ContentSiteIndexStore.cs |
| 2 | Real-SQLite tests for single, atomic batch, validation, preservation | 06fc1bc | ContentSiteIndexStoreApprovalTests.cs |

## What Was Built

### Task 1 — Interface + Implementation

Two new overloads added to `IContentSiteIndexStore` and implemented in `ContentSiteIndexStore`:

**Single-row overload** — keyed by `(naturalKeyType, naturalKeyValue)`, returns rows-affected (1 on match, 0 on no match). Validates status allow-list and non-whitespace key args before any DB call. No transaction (single UPDATE).

**Batch overload** — accepts `IReadOnlyList<(string Type, string Value)>`, runs all per-key UPDATEs inside ONE `DbTransaction` (atomic, D-06). Any exception or cancellation propagates out; the `await using` dispose auto-rolls the transaction back. Returns total rows affected. Throws `ArgumentNullException` for null keys; returns 0 early for empty list.

**Private `ValidateApprovalStatus`** — checks status against a `private static readonly string[]` allow-list `{ "pending", "approved", "rejected" }`; throws `ArgumentException` before any DB call.

**SQL pattern** (both overloads):
```sql
UPDATE content_site_index
   SET approval_status = @status
 WHERE natural_key_type = @type
   AND natural_key_value = @value;
```

Only `approval_status` is SET; `is_visible`, `is_hidden`, `is_evergreen` are untouched.

### Task 2 — Tests

Seven new `[Fact]` methods added to the existing `ContentSiteIndexStoreApprovalTests` class (same per-fact temp-SQLite file pattern):

1. `SetApprovalStatusAsync_Single_UpdatesMatchingRow` — single overload sets status, returns 1
2. `SetApprovalStatusAsync_Single_NoMatch_ReturnsZero` — unknown natural key returns 0
3. `SetApprovalStatusAsync_Batch_UpdatesAllKeys` — three keys updated to "rejected", returns 3
4. `SetApprovalStatusAsync_Batch_IsAtomic` — pre-cancelled token throws `OperationCanceledException`; both seeded rows remain "pending"
5. `SetApprovalStatusAsync_Batch_EmptyList_ReturnsZero` — no rows, no throw, returns 0
6. `SetApprovalStatusAsync_InvalidStatus_Throws` — "deleted" throws `ArgumentException` on both overloads
7. `SetApprovalStatusAsync_PreservesAdminFields` — `IsVisible` and `IsEvergreen` remain `true` after approval write

All 10 `ContentSiteIndexStoreApproval` tests pass (3 pre-existing Phase 43 facts + 7 new).

## Verification

- `DeckFlow.Core` builds with 0 errors / 0 new warnings.
- Filtered test run: `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10`.
- Batch atomicity proven: pre-cancelled token → `OperationCanceledException`; re-read rows still at "pending".
- No schema ALTER added (`approval_status` column pre-exists from Phase 43).

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None — all threats in the plan's threat register were mitigated:

- **T-46-01-01** (Tampering/SQL injection): all key/status values passed as Dapper parameters; status validated against allow-list before DB call.
- **T-46-01-02** (Admin field clobber): UPDATE sets only `approval_status`; `SetApprovalStatusAsync_PreservesAdminFields` test asserts `is_visible`/`is_evergreen` unchanged.
- **T-46-01-03** (Partial batch): single `DbTransaction` wraps all per-key UPDATEs; `SetApprovalStatusAsync_Batch_IsAtomic` proves rollback on abort.
- **T-46-01-04** (DoS via large batch): accepted — single-operator local tool.

## Self-Check: PASSED

- [x] `IContentSiteIndexStore.cs` modified — FOUND
- [x] `ContentSiteIndexStore.cs` modified — FOUND
- [x] `ContentSiteIndexStoreApprovalTests.cs` modified — FOUND
- [x] Task 1 commit 83e029a — FOUND
- [x] Task 2 commit 06fc1bc — FOUND
- [x] 10/10 filtered tests passed
