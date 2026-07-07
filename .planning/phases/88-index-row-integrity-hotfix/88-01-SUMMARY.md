---
phase: 88-index-row-integrity-hotfix
plan: 01
subsystem: database
tags: [sqlite, postgres, dapper, content-kb, approval-status, ddl-guard]

requires:
  - phase: 87-creator-model-hardening
    provides: content_site_index store + approval_status column
provides:
  - Approval-mirrored content-columns-only upsert (insert + update)
  - Approval-filtered public reads — browse list AND new GetPublishedByIdAsync
  - ContentKbController.Detail routed through the approval-filtered by-id read (pending rows 404)
  - ensureSchemaEnabled ctor switch that no-ops EnsureSchemaAsync for prod-pointed stores
  - Internal connection-factory test seam on the store
affects: [88-03, content-kb sync, DirectPush, ProdStoreFactory]

tech-stack:
  added: []
  patterns:
    - "Recording DbConnection/DbCommand decorator to assert exact SQL issued (no-DDL invariant)"
    - "ensureSchemaEnabled OFF switch as first statement in EnsureSchemaAsync (call sites untouched)"

key-files:
  created:
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreSchemaEnsureSwitchTests.cs
    - DeckFlow.Web/e2e/content-kb-pending-hidden.spec.ts
  modified:
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Web/Controllers/ContentKbController.cs
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreVisibilityTests.cs
    - DeckFlow.Web.Tests/ContentKbControllerTests.cs
    - "13 IContentSiteIndexStore fakes (new GetPublishedByIdAsync member)"

key-decisions:
  - "D-01/D-02: approval_status mirrored via @approvalStatus + EXCLUDED.approval_status (single + batch share the const)"
  - "D-04: both public reads filter approval_status='approved'; GetByIdAsync stays unfiltered for admin/Studio"
  - "D-09/D-11: EnsureSchemaAsync early-returns when ensureSchemaEnabled:false; zero-DDL proven by a recording connection"

patterns-established:
  - "Faithful fakes: Web/Studio FakeContentSiteIndexStore mirror the real approval+visibility serve filter"

requirements-completed: [SYNC-04, SYNC-06]

duration: 55min
completed: 2026-07-06
---

# Phase 88 Plan 01: Index-Row Integrity — Store Layer

**Closed the C1 (visible-while-pending) and C4 (DDL-against-prod) store-layer bugs: DirectPush upserts now mirror approval_status, both public read paths filter to approved+visible, and prod-pointed stores issue zero DDL — locked by a recording-connection test.**

## Performance

- **Duration:** ~55 min
- **Tasks:** 3 completed
- **Files modified:** 20 (incl. 13 fake implementers of the widened interface)

## Accomplishments

### Task 1 — Approval mirror (D-01/D-02)
- `UpsertContentColumnsOnlySql`: replaced the hardcoded `'pending'` INSERT literal with `@approvalStatus`, added `approval_status = EXCLUDED.approval_status` to the ON CONFLICT DO UPDATE SET. `BuildUpsertParameters` binds `row.ApprovalStatus`. Both single + batch entry points inherit mirror semantics via the shared const. `is_visible/is_hidden/is_evergreen` stay operator-owned (excluded). Trailing comment corrected (D-12).
- Old-behavior facts retargeted: `_NewRow_LandsAsPending` → `_NewRow_MirrorsSourceApproval`; `_ExistingRow_PreservesApprovalStatus` → `_ExistingPendingRow_HealsToApprovedFromApprovedSource`. Two "Preserves…Approved" facts rewritten to re-push from an approved source (approval mirrors, operator visibility/evergreen survive).

### Task 2 — Serve-side approval filter on BOTH reads (D-04 / Codex HIGH)
- `GetPublishedRowsAsync` WHERE now `is_visible AND approval_status='approved'`.
- New `GetPublishedByIdAsync(long id)` on interface + store (same shape as `GetByIdAsync` + approval/visibility filter). `GetByIdAsync` stays unfiltered.
- `ContentKbController.Detail` reads via `GetPublishedByIdAsync`; the redundant `!row.IsVisible` check dropped. A visible-but-pending row now 404s at `/content-kb/{id}`.
- Interface ripple: added the member to all 13 `IContentSiteIndexStore` implementers (2 real fakes filter approved+visible; throwing/orchestrator doubles mirror their existing style). Web/Studio real fakes updated so the serve filter is faithful.
- Tests: store case-matrices for both reads; web `Detail_ReturnsNotFound_WhenRowVisibleButPending`; swept VisibilityTests facts to set approval explicitly.

### Task 3 — Schema-ensure OFF switch + no-DDL invariant (D-09/D-11)
- Added `ensureSchemaEnabled = true` ctor param + `_ensureSchemaEnabled` field; `EnsureSchemaAsync` early-returns when off, before the `_schemaReady` fast-path — no call site touched.
- Added an `internal` connection-factory test-seam ctor; `OpenConnectionAsync` uses the override when supplied.
- REQUIRED recording-connection test: a prod-mode store driven through `GetAllRowsAsync` (DirectPush read) + `UpsertContentColumnsOnlyBatchAsync` (write) against a `RecordingDbConnection/RecordingDbCommand` pair issues **zero** CREATE/ALTER/DROP. Supplemental schema-less-file test + switch-ON default test included.

## Verification

- `dotnet test DeckFlow.Core.Tests` — 1107 passed, 0 failed, 0 warnings.
- `dotnet test DeckFlow.Web.Tests` — 1221 passed, 12 skipped (Postgres-only), 0 failed.
- `dotnet build DeckFlow.sln` — clean, 0 warnings.
- `scripts/format-check-changed.sh staged` — passed (changed lines).

## Deviations / Follow-ups

- **e2e (`content-kb-pending-hidden.spec.ts`)**: authored to seed a visible-but-pending row via the `sqlite3` CLI against the Development content DB, assert browse-omit + detail-404 + approved-control-renders. It self-skips when the KB flag is off or the DB is absent. Live validation is deferred to CI / operator (WSL e2e + live-server SQLite locking is the fragile path per CLAUDE.md); the invariant is already triple-proven at the xUnit layer (store matrices + web 404 + recording no-DDL). **Operator: run `npx --no-install playwright test content-kb-pending-hidden` against a fresh server on this branch to confirm.**
- **D-14 operator prod verification** (manual, at deploy): pre-audit + post-deploy re-check via the read-only Render MCP query per the plan's verification block.
