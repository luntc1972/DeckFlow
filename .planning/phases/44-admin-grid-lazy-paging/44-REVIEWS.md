---
phase: 44
reviewers: [codex]
reviewed_at: 2026-06-14T04:15:59Z
plans_reviewed: [44-01-PLAN.md, 44-02-PLAN.md, 44-03-PLAN.md]
reviewer_models: { codex: gpt-5.4 (medium) }
---

# Cross-AI Plan Review — Phase 44 (Admin Grid Lazy Paging)

> Self-reviewer (claude) skipped for independence. gemini/cursor not installed.
> Sole external reviewer: Codex (gpt-5.4, medium).

## Codex Review

## Plan 44-01

### 1. Summary
Solid, narrowly scoped repo/index plan. It follows the phase decision to verify-first, then consolidate the overlapping commander indexes into one partial expression index, and it includes both test updates and explicit EXPLAIN evidence.

### 2. Strengths
- Scope is tight and aligned to `GRID-02` only.
- Task ordering is good: test first, DDL change second, EXPLAIN proof last.
- The plan correctly avoids inventing a fake SQLite/Postgres branch where one is not needed.
- It explicitly preserves the existing startup/schema-init pattern instead of mixing in a one-off migration style.

### 3. Concerns
- `MEDIUM`: The plan drops the two old indexes before proving the new one exists in the same run, while `EnsureSchemaAsync` still swallows DB exceptions. If create fails after drop, the app can silently regress to no commander index.
- `LOW`: The EXPLAIN acceptance text is slightly brittle. SQLite may report temp B-tree usage in addition to index usage for `COUNT(DISTINCT ...)` / grouped queries, so “must contain exactly X and no scan” can become tool-output-sensitive rather than outcome-sensitive.

### 4. Suggestions
- Create the new index first, then drop the old pair only after successful creation, if the current startup pattern allows that safely.
- If keeping the broad exception swallow, add at least one test or summary note that explicitly calls out the silent-failure tradeoff.
- Loosen EXPLAIN acceptance to “no full table scan of `deck_queue`; planner references the new index for the filtered/grouped access path.”

### 5. Risk Assessment
`MEDIUM`

---

## Plan 44-02

### 1. Summary
This plan gets the architecture mostly right: move the slow grid path off `Index`, introduce a dedicated partial endpoint, and add a slim paging view model. The biggest problem is a requirements/security contradiction that should be resolved before execution.

### 2. Strengths
- Clean separation of concerns between `Index` shell render and commander-grid fetch path.
- Good reuse of the existing same-origin guard pattern instead of inventing new endpoint security behavior.
- `CommandersGridViewModel` is a reasonable way to avoid overloading `AdminHarvestViewModel`.
- Pagination clamp logic is explicitly preserved.

### 3. Concerns
- `HIGH`: The plan conflicts with the phase context on direct navigation behavior. Earlier context says direct browser navigation to `/Admin/Harvest/commanders` should 403; this plan later states bare no-header direct-nav may return the partial and says not to test 403 there. That is not a small wording issue; it changes realized endpoint behavior.
- `MEDIUM`: SC1 says `Index` must stop calling both `GetDistinctProcessedCommanderCountAsync` and `GetPagedProcessedCommandersAsync`, but the proposed test only proves the paged query was not called. The count half is unverified because the fake store has no call count.
- `MEDIUM`: The plan adds nontrivial pagination rendering in Razor but has no automated coverage for zero-row output or numbered/windowed pagination correctness.
- `LOW`: Every page fetch still does count + rows sequentially. That is acceptable for scope, but it keeps the count query hot on every pagination click.

### 4. Suggestions
- Resolve the 403 contract explicitly before execution: either amend the success criterion to “cross-origin 403, bare direct-nav allowed by validator design,” or change endpoint behavior so direct-nav truly 403s.
- Extend `FakeCategoryKnowledgeStore` with an explicit call counter for `GetDistinctProcessedCommanderCountAsync`, then assert both query methods remain untouched in `Index`.
- Add at least one rendering-oriented test for `_CommandersGrid` behavior:
  - zero rows => `admin-empty`
  - multi-page dataset => numbered links + current page `<strong aria-current="page">`
- If count-query cost remains visible after index work, consider deferring a cache/fetch-once strategy to a later phase note.

### 5. Risk Assessment
`HIGH`

---

## Plan 44-03

### 1. Summary
The client-side shape is mostly right: placeholder on initial render, auto-fetch, delegated pagination, inline error/retry, and CSS scoped to the admin shell. Two implementation details need correction before execution.

### 2. Strengths
- Scope is appropriate and respects the phase split: server in Plan 02, client wiring here.
- Event delegation on a stable container is the right choice for repeated `innerHTML` swaps.
- CSS placement is disciplined and aligned with the admin-shell design contract.
- Error/retry handling is included instead of assuming fetches always succeed.

### 3. Concerns
- `HIGH`: The plan specifies `scrollIntoView({ behavior: 'instant' ... })` for reduced-motion. `instant` is not a standard `ScrollBehavior` value in TypeScript DOM typings; `auto` is the safe value. This can fail compile or force a cast.
- `MEDIUM`: The task text appears to scroll the section into view after any successful load, including the initial automatic page-1 fetch. That can cause an unwanted jump on first page load. Scrolling should happen only for user-initiated pagination/retry.
- `LOW`: There is no automated coverage for the placeholder markup or TS behavior. Build-only verification is acceptable here, but it leaves regression detection to manual smoke testing.

### 4. Suggestions
- Change reduced-motion behavior from `'instant'` to `'auto'`.
- Pass a flag into `loadCommandersGrid(container, page, { scrollIntoView: boolean })` so initial auto-load does not scroll, but pagination/retry does.
- Consider a tiny server-render test or view assertion for the placeholder in `Index.cshtml`; it would raise confidence without much cost.
- Keep the generic error message, but log or surface non-200 status separately in dev if debugging becomes difficult.

### 5. Risk Assessment
`MEDIUM`

---

## Overall
The phase decomposition is good and the plans are close, but I would not start execution until two issues are corrected:

1. The `/Admin/Harvest/commanders` 403 contract needs one clear, final definition.
2. Plan 44-03 needs the reduced-motion scroll behavior fixed from `'instant'` to `'auto'`.

After that, the remaining gaps are mostly test-strength issues, not architectural blockers.

---

## Consensus Summary

Single external reviewer (Codex). No cross-reviewer consensus possible; findings below are Codex's.

### Strengths
- Phase decomposition is sound: repo/index (01) → server endpoint split (02) → client wiring (03).
- Each plan is tightly scoped to its requirement; test-first ordering in 01.
- Reuses existing same-origin guard and admin-shell CSS contract instead of inventing new patterns.

### Concerns (priority order)
- **HIGH — 403 contract contradiction (Plan 44-02):** CONTEXT says direct browser nav to `/Admin/Harvest/commanders` should 403; Plan 02 says bare no-header direct-nav may return the partial and not to test 403. Realized endpoint behavior is ambiguous. Resolve before execution.
- **HIGH — non-standard ScrollBehavior (Plan 44-03):** `scrollIntoView({ behavior: 'instant' })` — `'instant'` is not a valid `ScrollBehavior` in TS DOM typings; use `'auto'`. May fail compile / force a cast.
- **MEDIUM — drop-before-create index risk (Plan 44-01):** old indexes dropped before new one proven created, while `EnsureSchemaAsync` swallows exceptions → silent regression to no commander index if create fails.
- **MEDIUM — SC1 count half unverified (Plan 44-02):** test only proves paged query not called; `GetDistinctProcessedCommanderCountAsync` call has no counter in fake store.
- **MEDIUM — initial auto-load scroll jump (Plan 44-03):** scroll-into-view fires on initial page-1 fetch too; should only scroll on user-initiated pagination/retry.
- **MEDIUM — no render-path coverage (Plan 44-02):** zero-row / numbered-pagination Razor output untested.

### Recommended pre-execution fixes
1. Pin one final definition of the `/Admin/Harvest/commanders` 403 contract (amend SC or change endpoint behavior).
2. Change reduced-motion scroll `'instant'` → `'auto'` in Plan 44-03.
3. Optional but advised: create-then-drop index order; add count-call counter to fake store; pass `scrollIntoView` flag to skip initial-load scroll.

Codex verdict: architecture is close; only items 1–2 are blockers, rest are test-strength gaps.
