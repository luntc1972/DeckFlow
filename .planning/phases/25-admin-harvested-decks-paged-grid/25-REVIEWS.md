---
phase: 25
reviewers: [codex]
reviewed_at: 2026-05-24T22:03:47Z
plans_reviewed: [25-01-PLAN.md, 25-02-PLAN.md]
---

# Cross-AI Plan Review — Phase 25

> Reviewers invoked: **Codex** (authoritative per project workflow). Claude skipped (self — running inside Claude Code CLI). Gemini / OpenCode / Qwen / Cursor not installed.

## Codex Review

## Summary

Both plans are directionally sound and mostly achieve AHD-01: they preserve the Core/Web boundary, use server-side paging, reuse existing admin table patterns, and address the Render memory/perf constraints. I would not execute them unchanged, though. The biggest issues are a likely compile break from missing `ICategoryKnowledgeStore` test implementors, ambiguous Razor placement that can nest admin panels, unstable paging order on tied `inserted_utc`, and a probable wasted `GetTopCommandersAsync` query after the top-10 UI is removed.

## Strengths

- Good Core/Web separation: Core returns named tuples, Web maps to `HarvestedDeckRow`.
- Server-side paging is correctly centered on `LIMIT @limit OFFSET @offset`; no full-table load.
- Controller-level upper clamp for `page=999999` directly addresses the large-OFFSET DoS risk.
- Index plan matches the actual hot paths and uses dual-dialect `CREATE INDEX IF NOT EXISTS`.
- Reuses existing admin UI primitives instead of adding CSS.
- Test plan covers repository paging, empty states, null fields, index creation, and controller clamp behavior.

## Concerns

- **HIGH**: Plan 25-01 updates only [FakeCategoryKnowledgeStore](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs:1), but there are inline `ICategoryKnowledgeStore` fakes in [CommanderCategoryServiceTests.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web.Tests/CommanderCategoryServiceTests.cs:74) and [CategorySuggestionServiceTests.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs:215). Adding a new interface member will break build unless all implementors are updated.

- **MEDIUM**: `ORDER BY inserted_utc DESC` is not stable. `AddDeckIdsAsync` uses one timestamp for a batch, so many rows can tie. OFFSET paging can duplicate or skip rows between pages. Use `ORDER BY inserted_utc DESC, deck_id DESC` and consider indexing `(processed, inserted_utc, deck_id)`.

- **MEDIUM**: Plan 25-02 says add a new `<section class="admin-harvest__panel">` “in place” of the top-10 block, but that block is inside the existing Stats panel in [Index.cshtml](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Views/AdminHarvest/Index.cshtml:189). The new panel should be a sibling after the Stats `</section>`, not nested inside it.

- **MEDIUM**: After removing the top-10 UI, `GetTopCommandersAsync(10)` appears to have no remaining view consumer. Keeping it in [HarvestStatsAggregator.cs](/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs:66) preserves an unnecessary grouped query on the cold path. Verify consumers; if none, remove it from the stats payload or defer that query.

- **MEDIUM**: The Postgres `reltuples` query should qualify schema. `WHERE relname = 'card_category_observations'` can match the wrong relation if duplicate names exist across schemas. Prefer `to_regclass('public.card_category_observations')` or join `pg_namespace`.

- **LOW**: Repository-level paging does not defensively clamp `page` / `pageSize`; only the Web store does. Since the repository method is public Core API, it should also guard `page = Math.Max(page, 1)` and `pageSize = Math.Max(pageSize, 1)`.

- **LOW**: The controller-test plan says the fake should record the clamped page, but does not explicitly add `LastPagedDeckPage` / `LastPagedDeckPageSize`. Make that an acceptance criterion.

## Suggestions

- Update Plan 25-01 Task 2 to include every `ICategoryKnowledgeStore` implementor found by `rg "ICategoryKnowledgeStore" DeckFlow.Web.Tests`.
- Change the paged query to:
  ```sql
  ORDER BY inserted_utc DESC, deck_id DESC
  LIMIT @limit OFFSET @offset;
  ```
- Decide explicitly whether `TopCommanderRow` remains part of `HarvestStatsPayload`. If the top-10 list is gone, remove the query now unless another UI still needs it.
- Make the Harvested Decks panel placement explicit: close the Stats section first, then add a sibling panel.
- Qualify the `reltuples` lookup by schema and keep the `<= 0` fallback.
- Add repository tests for tied timestamps and invalid page/pageSize if the repo clamps defensively.

## Risk Assessment

**Overall risk: MEDIUM.** The architecture is solid and the plans are close, but the missing test fakes are a concrete build risk, and the unstable sort can create real paging defects at scale. Fix those before execution and the phase drops to low risk.

---

## Consensus Summary

Single external reviewer (Codex). No cross-reviewer consensus to synthesize; findings below are Codex's, surfaced verbatim as the authoritative gate.

### Agreed Strengths
- Core/Web boundary preserved (Core returns tuples, Web maps to `HarvestedDeckRow`).
- Server-side `LIMIT/OFFSET` paging — no full-table load (Render 512MB cap respected).
- Controller upper-clamp addresses large-OFFSET DoS (T-25-02).
- Dual-dialect `CREATE INDEX IF NOT EXISTS` matches hot paths.

### Blocking Concerns (must resolve before execute)
- **HIGH** — New `ICategoryKnowledgeStore` member will break build: inline fakes in `CommanderCategoryServiceTests.cs:74` and `CategorySuggestionServiceTests.cs:215` not covered by Plan 25-01 (only `FakeCategoryKnowledgeStore` updated).

### Other Concerns
- **MEDIUM** — Unstable paging sort: `ORDER BY inserted_utc DESC` ties (batch shares one timestamp) → OFFSET dup/skip. Add `, deck_id DESC` tiebreaker; index `(processed, inserted_utc, deck_id)`.
- **MEDIUM** — Razor placement: new panel must be a sibling AFTER Stats `</section>`, not nested inside it (`Index.cshtml:189`).
- **MEDIUM** — `GetTopCommandersAsync(10)` likely orphaned after top-10 removal — wasted grouped query on cold path. Verify consumers; remove/defer.
- **MEDIUM** — Postgres `reltuples` query unqualified by schema; prefer `to_regclass('public.card_category_observations')`.
- **LOW** — Repository (public Core API) should defensively clamp `page`/`pageSize`, not only the Web store.
- **LOW** — Controller-test fake should explicitly record clamped page (`LastPagedDeckPage`/`LastPagedDeckPageSize` as acceptance criterion).

### Divergent Views
None (single reviewer).
