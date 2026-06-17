# Phase 43: Approval Status + Safe Upsert — Discussion Log

**Date:** 2026-06-13
**Mode:** discuss (default)

> Human-reference audit trail. Not consumed by downstream agents (see 43-CONTEXT.md for the locked decisions).

## Gray areas analyzed

Phase is data-layer only with prescriptive ROADMAP success criteria. Codebase scout surfaced: clean self-healing ALTER pattern in `ContentSiteIndexStore.EnsureSchemaAsync`; existing `UpsertRowPreservingVisibilityAsync` + `GetPublishedRowsAsync` precedents; distill upserts via `UpsertRowAsync` (line 1052); export reads via `GetAllRowsAsync` (line 610); no pin column (is_evergreen is the pin concept).

## Questions & answers

### Q1 — Migration backfill default for pre-existing rows
**Options:** Grandfather visible→approved / Grandfather ALL→approved / All→pending (literal SC4)
**Chosen:** Grandfather visible→approved
**Note:** The footgun: export filters to approved-only, so all-pending would drop the live ~86-row seed. Grandfather is_visible=TRUE→approved preserves the published seed; honors SC4's "(no data loss on migration)" over its literal "treated as pending." → D-01.

### Q2 — approval_status on re-distill / re-harvest (UPDATE path)
**Options:** Preserve (stays approved) / Reset to pending on update
**Chosen (user clarification):** "videos do not change very often if at all, only need to re-distill if a video is specifically selected" → PRESERVE on update.
**Note:** Re-distill is deliberate + operator-targeted; auto-requeue unnecessary. Operator can manually re-review. → D-04.

### Q3 — Is approval_status in the admin-preserved set for the safe overload?
**Options:** Preserve all 4 admin fields / Preserve visibility not approval
**Chosen:** Preserve all 4 admin fields (is_visible, is_hidden, is_evergreen, approval_status).
**Note:** Consistent with Q2-preserve. → D-03.

### Q4 — New-row write path + existing method fate
**Options:** Distill switches to new overload (additive) / Rename-replace preserving-visibility one
**Chosen:** Distill switches to new additive overload; existing methods untouched.
**Note:** Lowest blast radius; consolidation deferred. → D-05, D-09.

## Deferred ideas
- Consolidate the 3 upsert variants (cleanup, later).
- Re-distill auto-requeue (rejected for now).
- Postgres migration integration-test harness (CI is SQLite-only).

## Claude's discretion
- EnsureSchemaAsync backfill idempotency mechanism.
- ContentSiteIndexRow.ApprovalStatus property (round-trip) — must not alter the exported ContentIndexExportRow byte-shape.
- Test placement (real-SQLite integration tests).
