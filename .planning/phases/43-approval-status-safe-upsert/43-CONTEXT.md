# Phase 43: Approval Status + Safe Upsert - Context

**Gathered:** 2026-06-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Add the data-layer prerequisites for the review/publish workflow (v1.7 Local Harvest & Publish Studio):
1. An `approval_status` column (`pending` / `approved` / `rejected`) on `content_site_index`, added via the established self-healing ALTER migration pattern (SQLite + Postgres), default `pending` (REVQ-01).
2. A safe content-only upsert overload on `IContentSiteIndexStore` that refreshes content/nav columns WITHOUT clobbering admin-set fields (PUB-01).
3. Seed export filtered to `approved` rows only, so `pending`/`rejected` content never ships to the public repo or prod (PUB-02).

**In scope:** the column + self-healing migration + backfill, the `UpsertContentColumnsOnlyAsync` overload, `GetApprovedRowsAsync`, wiring the distill index-write to the safe overload (new rows → `pending`), wiring the export read to the approved-only query, and integration tests for each.

**Out of scope (own phases):** the review-queue UI (Phase 46 / REVQ-02/03), the commit-publish path (Phase 46 / PUB-03), the direct prod-DB+SCP path (Phase 47 / PUB-04/05), the harvest+distill UI (Phase 45). This phase is data-layer only — no UI.

</domain>

<decisions>
## Implementation Decisions

### Migration backfill (REVQ-01, SC4)
- **D-01:** Pre-existing `content_site_index` rows are GRANDFATHERED on migration: the self-healing ALTER backfills `approval_status='approved'` for rows where `is_visible=TRUE`, and `'pending'` for all others. This preserves the live ~86-row published seed (export filters to `approved`) while still queuing never-published rows for review. This deliberately diverges from a literal reading of ROADMAP SC4 ("pre-migration rows treated as pending") because all-pending would silently drop every published row from the next seed export — the divergence honors SC4's parenthetical intent "(no data loss on migration)". Implement the backfill as part of the same self-healing `EnsureSchemaAsync` step that adds the column (idempotent: only runs the UPDATE when the column was just added, OR guard so re-runs don't re-stamp operator-changed statuses).
- **D-02:** Column default is `pending` (new INSERTs with no explicit status become pending). DEFAULT applied at the DDL level so any future insert path is safe.

### Safe content-only upsert (PUB-01)
- **D-03:** New method `UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, ...)` on `IContentSiteIndexStore`. Its `ON CONFLICT ... DO UPDATE` updates ONLY content/nav columns: `source, title, video_url, artifact_path, published_utc, indexed_utc, archetype_tags, bracket_tags, card_category_tags`. It NEVER writes the 4 admin-preserved fields on UPDATE: `is_visible`, `is_hidden`, `is_evergreen`, `approval_status`.
- **D-04:** On INSERT (brand-new natural key), the overload sets `approval_status='pending'` (new content needs review) and leaves the admin booleans at their column defaults. On UPDATE (existing row re-distilled), `approval_status` is PRESERVED (not reset). Rationale: videos rarely change; re-distill is a deliberate, operator-targeted action on a specifically-selected video, so auto-requeuing is unnecessary — the operator who re-distills can manually re-review if they choose.
- **D-05:** Additive surface — the new overload sits ALONGSIDE the existing `UpsertRowAsync` and `UpsertRowPreservingVisibilityAsync` (lowest blast radius). Do NOT rename/replace the existing methods in this phase. (Consolidation of the three upsert variants can be a later cleanup.)
- **D-06:** "pin state" in PUB-01 maps to the existing `is_evergreen` column (the priority/pin concept); there is no separate pin column and none is added here. The admin-preserved set is exactly {`is_visible`, `is_hidden`, `is_evergreen`, `approval_status`}.

### Approved-only export (PUB-02)
- **D-07:** New `GetApprovedRowsAsync()` on `IContentSiteIndexStore` returning only rows `WHERE approval_status='approved'`, preserving today's row ORDER from `GetAllRowsAsync` (so the seed JSON byte-shape stays stable — the Phase 42 golden test pins serialization shape, but row membership now changes by design).
- **D-08:** The orchestrator export path (`ContentKbOrchestrator.ExportIndexAsync`, currently `GetAllRowsAsync` at line 610) switches to `GetApprovedRowsAsync`. This is the intended behavior change: rejected/pending rows never reach `index-seed.json`.

### Distill wiring
- **D-09:** The distill index-write (`ContentKbOrchestrator` `UpsertRowAsync` at line 1052) switches to `UpsertContentColumnsOnlyAsync`, so re-distilling a video refreshes its content without clobbering an operator's visibility/evergreen/approval state, and a brand-new distilled row lands as `pending`.

### Claude's Discretion
- Exact `EnsureSchemaAsync` idempotency mechanism for the one-time backfill (e.g. only backfill in the same branch that just added the column, vs a guarded `WHERE approval_status IS NULL`) — planner/researcher choose the safest re-runnable form consistent with the existing is_visible/is_evergreen/is_hidden ALTER blocks.
- Whether `ContentSiteIndexRow` gains an `ApprovalStatus` property (likely yes, to round-trip the value) and its placement — must not break the Phase 42 `ContentIndexExportRow` byte-shape (export row is a separate projection; do not add approval_status to the EXPORTED JSON shape).
- Test placement (DeckFlow.Core.Tests integration tests against real SQLite; Postgres column-presence assertion may be SQLite-only in CI if no PG test harness exists — note skip explicitly).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` §"Phase 43: Approval Status + Safe Upsert" — goal + 4 success criteria
- `.planning/REQUIREMENTS.md` — REVQ-01, PUB-01, PUB-02 (and REVQ-02/03, PUB-03/04/05 for downstream context only — NOT this phase)

### Data layer (the code being changed)
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `EnsureSchemaAsync` self-healing ALTER pattern (is_visible @57-64, is_evergreen @66-73, is_hidden @75-82); `UpsertSql` / `UpsertPreservingVisibilitySql`; `GetAllRowsAsync` @240; `GetPublishedRowsAsync` @47 (WHERE is_visible precedent for GetApprovedRowsAsync)
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — interface to extend (UpsertRowAsync, UpsertRowPreservingVisibilityAsync, GetAllRowsAsync, GetPublishedRowsAsync)
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs` — distill upsert call `UpsertRowAsync` @1052 (switch to safe overload); export read `GetAllRowsAsync` @610 (switch to GetApprovedRowsAsync)
- `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` — EXPORT projection; its byte-shape is pinned by the Phase 42 golden test — do NOT add approval_status to the exported JSON

### Pattern precedents
- `.planning/phases/42-orchestrator-extraction/42-05-SUMMARY.md` — golden-fixture test that pins `index-seed.json` byte-shape (membership-change in this phase is intentional; shape must stay)
- Recent `is_hidden` tri-state migration (commit `f629582`, memory [[project_kb_tristate_visibility]]) — most recent example of the self-healing ALTER + admin-visibility pattern

</canonical_refs>

<code_context>
## Reusable Assets & Patterns

- **Self-healing ALTER:** `EnsureSchemaAsync` already does `columns.Contains("x")` → `ADD COLUMN x BOOLEAN/INTEGER ... DEFAULT ...` with a Postgres-vs-SQLite type branch. `approval_status` follows the same shape but as TEXT/VARCHAR with `DEFAULT 'pending'`. Add the column block + a one-time grandfather UPDATE alongside the existing three.
- **Filtered read precedent:** `GetPublishedRowsAsync` (WHERE is_visible) is the direct template for `GetApprovedRowsAsync` (WHERE approval_status='approved').
- **Upsert precedent:** `UpsertPreservingVisibilitySql` already shows a content-update-that-preserves-some-admin-fields. The new overload extends that idea to preserve ALL 4 admin fields.
- **Test seam:** DeckFlow.Core.Tests has real-SQLite integration tests for the store; mirror them for the column-present / preserve-on-upsert / approved-only-filter assertions (Phase 42 added shared FakeOrchestratorStores but those are in-memory fakes — these new tests want the REAL ContentSiteIndexStore against SQLite).

</code_context>

<deferred>
## Deferred Ideas
- Consolidating the three upsert variants (`UpsertRowAsync` / `UpsertRowPreservingVisibilityAsync` / `UpsertContentColumnsOnlyAsync`) into one parameterized method — cleanup, not this phase (D-05 keeps them additive).
- Re-distill auto-requeue (reset approval_status→pending on content change) — explicitly rejected for now (D-04); revisit only if video content churn proves higher than expected.
- A dedicated Postgres integration-test harness for the migration (CI currently SQLite-only) — note as a coverage gap, address if/when a PG test path exists.

</deferred>

<scope_fence>
## Scope Fence
- Data layer only. NO UI (review queue = Phase 46, harvest/distill UI = Phase 45).
- NO publish/commit/SCP logic (Phases 46/47).
- NO change to the EXPORTED JSON byte-shape (approval_status is a DB/filter concern, not an exported field).
- NO rename/removal of existing upsert methods (additive only).
- NO new NuGet packages.
</scope_fence>
