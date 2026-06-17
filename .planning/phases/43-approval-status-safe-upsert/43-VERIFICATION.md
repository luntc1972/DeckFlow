---
phase: 43-approval-status-safe-upsert
verified: 2026-06-13T00:00:00Z
status: passed
score: 4/4 must-haves verified
overrides_applied: 0
---

# Phase 43: Approval Status + Safe Upsert Verification Report

**Phase Goal:** The content_site_index has an approval_status column that drives the review queue; a safe content-only upsert overload exists that never clobbers is_visible or is_evergreen; the export path is filtered to approved rows only.
**Verified:** 2026-06-13
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `approval_status` column exists after `EnsureSchemaAsync` on both fresh SQLite and fresh Postgres | VERIFIED | Both `SqliteCreateTableSql` (line 875) and `PostgresCreateTableSql` (line 853) include `approval_status TEXT NOT NULL DEFAULT 'pending'`. The self-healing ALTER at lines 84-90 adds it to pre-existing schemas that lack the column. `EnsureSchemaAsync_AddsApprovalStatusColumn_ToLegacySchema` integration test exercises the ALTER path against real SQLite. |
| 2 | `UpsertContentColumnsOnlyAsync` exists; setting `is_visible=TRUE` then calling it leaves `is_visible` TRUE | VERIFIED | Method exists on `IContentSiteIndexStore` (line 37) and `ContentSiteIndexStore` (line 186). `UpsertContentColumnsOnlySql` ON CONFLICT SET clause at lines 823-832 contains only content/nav columns — `is_visible`, `is_hidden`, `is_evergreen`, `approval_status` are explicitly absent (comment at line 833 confirms intent). Test `UpsertContentColumnsOnlyAsync_PreservesVisibleEvergreenApprovedFields` sets `is_visible=TRUE`, re-upserts, and asserts `IsVisible` remains `true`. |
| 3 | `GetApprovedRowsAsync` returns only `approval_status='approved'` rows; export calls it (not `GetAllRowsAsync`) | VERIFIED | `GetApprovedRowsAsync` (line 297) filters `WHERE approval_status = 'approved'`. `ContentKbOrchestrator.ExportIndexAsync` (line 610) calls `GetApprovedRowsAsync` — confirmed by direct code read, not just summary claim. `GetApprovedRowsAsync_ReturnsOnlyApprovedRows` tests this with pending/approved/rejected rows and asserts only the approved row is returned. `ContentIndexExportRow` has no `approval_status` property — the Phase 42 byte-shape is unchanged. |
| 4 | Distill pipeline sets new rows to `approval_status='pending'`; pre-migration rows get no data loss (D-01: visible→approved) | VERIFIED | `UpsertContentColumnsOnlySql` INSERT specifies `'pending'` as the literal value for `approval_status` on new rows (line 822). D-01 grandfather backfill in `EnsureSchemaAsync` (lines 94-104) sets `approval_status='approved'` for rows where `is_visible=1 AND approval_status='pending'` — so visible pre-migration rows are promoted rather than silently dropped from the next export. Tests `EnsureSchemaAsync_Grandfather_SetsApprovedForVisibleRows_PendingForOthers` and `EnsureSchemaAsync_Grandfather_DoesNotRestampOperatorChangedStatus` cover both behaviors. |

**Score:** 4/4 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | approval_status column, ALTER migration, grandfather backfill, UpsertContentColumnsOnlyAsync, GetApprovedRowsAsync | VERIFIED | All five elements present and substantive. Column in both DDL constants, ALTER guard, backfill UPDATE runs outside column-presence gate (every EnsureSchemaAsync call, idempotent via `WHERE approval_status='pending'`), both methods fully implemented. |
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` | UpsertContentColumnsOnlyAsync and GetApprovedRowsAsync on interface | VERIFIED | Both members declared (lines 37, 63) with XML doc comments. All IContentSiteIndexStore fakes in tests implement both (BlockedVideoStoreTests, CommandRunnerCorpusResetTests, RunDistillAsyncTests, FakeOrchestratorStores, FakeContentSiteIndexStore in Web.Tests). |
| `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` | ExportIndexAsync uses GetApprovedRowsAsync; distill upsert uses UpsertContentColumnsOnlyAsync | VERIFIED | Line 610: `GetApprovedRowsAsync`. Line 1052: `UpsertContentColumnsOnlyAsync`. No residual `GetAllRowsAsync` or `UpsertRowAsync` calls on the target code paths. |
| `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` | No approval_status property — Phase 42 golden shape unchanged | VERIFIED | File has no `approval_status` or `ApprovalStatus` reference. The `From(ContentSiteIndexRow row)` projection maps 11 fields, none being approval_status. |
| `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (ContentSiteIndexRow) | ApprovalStatus property with default "pending" | VERIFIED | Line 145: `public string ApprovalStatus { get; init; } = "pending";` |
| `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs` | 10 integration tests covering migration, grandfather, safe-upsert preservation, approved-only filter, DDL default | VERIFIED | Exactly 10 `[Fact]` tests. All key paths covered: ALTER on legacy schema, grandfather visible→approved / non-visible→pending, no-restamp on operator-changed status, new row→pending, existing row approval_status preservation, visible+evergreen admin fields preserved, hidden row preserved, GetApprovedRowsAsync filter, DDL default, reflection DDL constants. |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `ContentKbOrchestrator.ExportIndexAsync` | approved rows only | `_indexStore.GetApprovedRowsAsync(cancellationToken)` | WIRED | Line 610 confirmed by direct read |
| `ContentKbOrchestrator` distill path | safe upsert | `_indexStore.UpsertContentColumnsOnlyAsync(...)` | WIRED | Line 1052 confirmed by direct read |
| `UpsertContentColumnsOnlySql` INSERT | new rows land as pending | literal `'pending'` in VALUES | WIRED | Line 822 |
| `UpsertContentColumnsOnlySql` ON CONFLICT | admin fields preserved | admin columns absent from SET clause | WIRED | Lines 823-832 + comment line 833 |
| `EnsureSchemaAsync` | approval_status column added to legacy schemas | `ALTER TABLE ... ADD COLUMN approval_status ...` inside `!columns.Contains("approval_status")` | WIRED | Lines 84-90 |
| `EnsureSchemaAsync` | grandfather backfill | `UPDATE ... SET approval_status='approved' WHERE approval_status='pending' AND is_visible=@visible` | WIRED | Lines 94-104, runs on every EnsureSchemaAsync call (outside the column-existence gate), idempotent via WHERE clause |

---

### Data-Flow Trace (Level 4)

**ExportIndexAsync path:**

- `_indexStore.GetApprovedRowsAsync()` → SQL `WHERE approval_status='approved'` → real DB query (confirmed)
- Result rows → `ContentIndexExportRow.From(row)` projection → no approval_status in output
- Data flows: real approved rows only; pending/rejected rows excluded at the SQL layer

**Distill path:**

- `UpsertContentColumnsOnlyAsync(new ContentSiteIndexRow { ... })` → `UpsertContentColumnsOnlySql`
- New rows: INSERT sets `approval_status='pending'` via literal in VALUES
- Existing rows: ON CONFLICT SET clause does not touch `approval_status`; existing DB value is preserved
- Data flows correctly

---

### Behavioral Spot-Checks

Step 7b SKIPPED for this phase — the deliverable is a data-layer extension (no runnable API endpoint or CLI command added). The integration tests (real-SQLite `ContentSiteIndexStoreApprovalTests`) serve as the behavioral verification. Tests confirmed green per build output in summaries (Core 342/342).

---

### Probe Execution

Step 7c: No probes declared in PLAN or SUMMARY. No `scripts/*/tests/probe-*.sh` files relevant to this phase. SKIPPED.

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|------------|------------|-------------|--------|---------|
| REVQ-01 | 43-01-PLAN.md | approval_status column on content_site_index, self-healing migration | SATISFIED | Column in both DDL constants; ALTER migration; integration tests pass |
| PUB-01 | 43-01-PLAN.md | Safe content-only upsert overload preserving is_visible, is_evergreen, is_hidden, approval_status | SATISFIED | `UpsertContentColumnsOnlyAsync` + SQL confirmed; tests assert preservation |
| PUB-02 | 43-01-PLAN.md | Export filtered to approved rows only; pending/rejected never reach index-seed.json | SATISFIED | Orchestrator export path calls `GetApprovedRowsAsync`; `ContentIndexExportRow` excludes approval_status from JSON shape |

---

### Anti-Patterns Found

Scanned files modified in this phase:

- `ContentSiteIndexStore.cs` — no TBD/FIXME/XXX/placeholder markers; no empty returns; no hardcoded stubs
- `IContentSiteIndexStore.cs` — no anti-patterns; `DeleteAllRowsAsync` default-throws is pre-existing (not introduced this phase)
- `ContentKbOrchestrator.cs` — two-line change (610 + 1052); no issues
- `ContentArtifactSpec.cs` (ContentSiteIndexRow) — `ApprovalStatus` property has correct default; no issues
- `ContentSiteIndexStoreApprovalTests.cs` — 10 real-SQLite facts; no stubs or placeholders

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | — | — | — |

No blockers or warnings found.

---

### Human Verification Required

One item cannot be verified programmatically and is noted for post-deploy confirmation:

**1. Postgres self-healing ALTER — live migration column presence**

**Test:** Deploy to Render (Postgres), run the application, check `information_schema.columns WHERE table_name='content_site_index'` for `approval_status`.
**Expected:** Column present after first app startup with existing Postgres data.
**Why human:** CI runs SQLite-only; no Postgres test harness exists for this phase. The code logic is structurally identical to the already-working is_visible / is_evergreen / is_hidden ALTER blocks (`GetTableColumnsAsync` handles Postgres via `information_schema.columns`). Risk is LOW but unconfirmed until first Postgres deployment.

---

### SC4 Deviation Analysis

SC4 states "pre-migration rows treated as pending (no data loss on migration)". D-01 (locked in CONTEXT.md) grandfathers visible rows to `'approved'` instead of leaving them as `'pending'`. This diverges from the literal SC4 wording but honors SC4's parenthetical intent "(no data loss on migration)" — treating all pre-migration rows as pending would silently drop the live ~86-row published seed from the next export, which is data loss in operational terms.

The test `EnsureSchemaAsync_Grandfather_SetsApprovedForVisibleRows_PendingForOthers` asserts:
- visible legacy row → `"approved"` (grandfathered to preserve export membership)
- non-visible legacy row → `"pending"` (queued for review, consistent with SC4 intent)

This deviation is documented, intentional, and correct for the operational context. No override entry is needed — the CONTEXT.md decision record (D-01) is the authoritative documentation.

---

### Gaps Summary

No gaps. All 4 success criteria are verified by direct code inspection. The one human verification item (Postgres live migration) is LOW risk given structural parity with the three existing self-healing ALTER blocks that are already proven in production.

---

_Verified: 2026-06-13T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
