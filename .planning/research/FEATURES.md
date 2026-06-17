# Feature Research

**Domain:** Local content-curation studio for a solo operator (harvest → review → publish pipeline)
**Researched:** 2026-06-13
**Confidence:** HIGH — grounded in the existing codebase and well-understood admin-tooling patterns

---

## Context: What already exists (do not re-spec)

The existing pipeline covers: YouTube caption harvest + Whisper fallback via `ContentKbCommandRunners`,
LLM distillation into summary/clips/tags via `RunDistillAsync`, per-video spend ledger
(`LlmSpendLedger`, `WhisperSpendLedger`), a `ContentSiteIndexStore` with `is_visible` /
`is_evergreen` columns, bulk publish/hide per source in the deployed `AdminContentKb` web UI,
admin block/hard-delete via `RunBlockVideoAsync`, and a seed-export-then-commit publish path via
`RunContentIndexExportAsync`. The `IYouTubeChannelVideoLister` uses YoutubeExplode (HTML scraping,
no API key) with serialized metadata lookups due to AngleSharp static-state concurrency bug.

The LOCAL tool in v1.7 wraps all of the above in a UI and adds: in-app video discovery/browse,
an explicit approve-before-publish queue, and a direct prod-DB push path. Every feature below is
categorized against those boundaries.

---

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Pipeline dependency | Notes |
|---------|--------------|------------|---------------------|-------|
| Paste URL/ID and trigger harvest+distill | Any curation tool must accept direct input; it's the escape hatch when browse doesn't find what you want | LOW | Thin wrapper — `RunHarvestAsync(videoIds:...)` + `RunDistillAsync(videoIds:...)` exist; new: call them from a UI form | Call existing CLI internals directly; almost pure plumbing |
| Dedup against already-harvested | Without this, re-submitting a URL silently duplicates or wastes LLM spend | LOW | `ResolveHarvestVideoIdAsync` already checks `GetVideoByYoutubeIdAsync`; skip-if-exists path is in the CLI | Surface "already harvested" status back to the UI — no new store logic needed |
| Distill-status tracking per video | Operator needs to know whether a video is pending / harvested / distilled / failed | LOW | `distill_status` column + `GetDistillStatusAsync` / `SetDistillStatusAsync` already exist on `IContentVideoStore` | Read existing column; add a status display in the review queue |
| Per-item preview in the review queue | Operator must see summary + clips + tags before deciding to approve | MEDIUM | Distilled output already stored (summary in `content_videos`, clips in `content_clips`, tags in `content_tags`); need a read-side service to assemble it | New read path but trivially composed from existing stores |
| Approve / reject individual items | Core action of a review queue; without it the tool is just a batch runner | MEDIUM | `is_visible` column on `content_site_index` covers "published" state; "approved" needs a new status column OR reuse `is_visible=false` with a distinct "reviewed but hidden" interpretation — requires design decision | See Design Notes below |
| LLM spend shown before distill runs | Operator will not trust a tool that surprises them with cost | LOW | `LlmSpendLedger.WouldExceedCapAsync` + dry-run path already implemented in `RunDistillAsync(dryRun:true)` | Call dry-run first; surface the projected cost before the confirm button is clickable |
| Post-action spend summary | Operator needs to know what was spent after a distill batch | LOW | `DistillCounts.LlmSpendUsd` is returned from `RunDistillAsync`; `LlmSpendLedger` has monthly totals | Render the returned counts in the UI; query ledger for YTD total |
| Publish approved entries to prod | Purpose of the whole tool; missing = no value delivered | HIGH | Two paths: (1) existing `RunContentIndexExportAsync` → commit → Render auto-deploy (already works); (2) direct prod-DB write (NEW authenticated path — does not exist today) | High complexity because path 2 requires a new connection management story; path 1 is LOW complexity reuse |
| "What will change" diff before publish | Publishing without a preview is dangerous; every deploy tool shows a diff | MEDIUM | Export JSON exists; a local diff between current export and prod state requires either a snapshot or a DB query to prod | Medium because prod-DB query is a new capability; commit-path diff is cheap (git diff) |
| Blocked-video management in UI | Already exists as CLI commands; operator expects UI parity | LOW | `RunBlockVideoAsync` + `RunUnblockVideoAsync` + `RunListBlockedAsync` all exist; thin controller wrapper | New controller action, no new store logic |
| List existing sources + enable/disable | Source management is a prerequisite to any discovery workflow | LOW | `ContentSourceStore.ListEnabledSourcesAsync` + `SetEnabledAsync` exist | Thin form wrapper; already done via add-source CLI |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Pipeline dependency | Notes |
|---------|-------------------|------------|---------------------|-------|
| Channel browse UI — list N most recent videos with thumbnail + duration + harvested status | Saves the operator from copying video IDs manually; makes discovery faster than the CLI | MEDIUM | `IYouTubeChannelVideoLister.ListRecentAsync` exists (YoutubeExplode, no API key); thumbnails require a direct `img src` to `i.ytimg.com` (no store dependency); harvested-status requires a `GetVideosByYoutubeIdsAsync` batch read | The AngleSharp concurrency bug means discovery calls must stay serialized (MetadataLookupConcurrency = 1). Duration + title + published date already in `YouTubeChannelVideo`. |
| Multi-select checklist → batch harvest+distill | Reduces N round-trips to one form submit for a set of videos | MEDIUM | Piggybacks on existing `--video-ids` / `ParseVideoIds` path in `RunHarvestAsync` and `RunDistillAsync`; need a multi-select form + progress feedback | Complexity is in progress feedback, not pipeline logic |
| Creator search by handle/URL (not just pre-configured sources) | Operator discovers new creators without first running `add-source` | MEDIUM | `IYouTubeChannelVideoLister.ListRecentAsync` accepts any channel URL, handle, or ID — no source DB entry required | Differentiator because the CLI requires pre-configured sources; the UI can allow ad-hoc browse |
| YouTube Data API v3 search by keyword / creator name | Full-text creator search without knowing the handle | HIGH | NEW — YoutubeExplode does not expose search; requires a YouTube Data API v3 key (quota: 10,000 units/day free tier); search = 100 units/call | This is genuinely new build. Quota exhaustion is a real risk on free tier. Scope carefully — keyword search of videos costs 100 units each; channel search costs 100 units. |
| Real-time progress feedback during harvest+distill | Long-running operations need visible progress; a spinner with no feedback feels broken | HIGH | No progress-push mechanism exists; would need SignalR, SSE, or a polling status endpoint (like the existing `AdminHarvest/Status` polling pattern) | The existing harvest controller uses a 1-second polling endpoint + JS loop — reuse that pattern. SSE is simpler than SignalR for a local single-user tool. |
| Inline tag editing before publish | Operator can fix incorrect LLM tags before they go to prod | MEDIUM | Tags stored in `content_tags`; `ContentTagVocabulary` exists for validation; need an edit form + `DeleteTagAsync` + `InsertTagAsync` calls | Vocabulary enforcement is already built; UI is new but bounded |
| Direct prod-DB push path | Skip the commit → wait-for-deploy cycle (typically 2-4 min on Render) | HIGH | NEW — prod write is not an existing capability; requires a separate authenticated connection config, a secrets-safe storage mechanism, and the same `ContentSiteIndexStore` write path pointed at Render Postgres | High complexity + security surface; see Anti-Features for the scheduler variant |
| Post-publish verification — query prod after push to confirm rows landed | Closes the loop on "did it work?" | MEDIUM | Would reuse the prod-DB connection (if implemented) with a read-only SELECT; or check the deployed `/content-kb` page via HTTP | Medium if prod-DB exists; LOW via HTTP scrape |

### Anti-Features (Explicitly Exclude)

| Feature | Why Requested | Why Exclude | What to Do Instead |
|---------|---------------|-------------|---------------------|
| Scheduled / cron harvest from the local UI | "Run automatically while I'm away" | Local tool is not always running; a local cron is fragile and defeats the review-before-publish guarantee. The deployed site already has no scheduler by design (PROJECT.md explicitly deferred it every milestone). | Keep harvest manual-trigger only; the review queue is the value, not automation |
| YouTube Data API v3 video search (keyword search across all of YouTube) | "Find MTG content I haven't heard of" | 100 quota units per search call; free tier = 10,000 units/day. Full-text across YouTube is a quota bomb. One mistake in testing exhausts the daily budget. | Scope Data API to channel/handle resolution only (cheaper); use creator browse via known handles for discovery |
| Rebuild or replace the LLM distillation pipeline | "Better prompts / different model" | The distiller (`LlmDistillationProviderFactory`, `RunDistillAsync`) is stable and tested. Re-implementing it inside the local tool creates two code paths to maintain. | Call the existing `RunDistillAsync` internals directly; change prompts in the Core layer if needed |
| Real-time preview of the LLM distill output as it generates | "See the summary as it streams" | Streaming token-by-token from the LLM through a local UI adds SSE/WebSocket complexity and the existing distillation is a batch of 3 API calls (classify + summarize + extract clips), not a streaming response | Run distill to completion, then show the review queue; the latency (~10-30s per video) is acceptable for a local curation tool |
| Multi-user / role-based access | "Other team members could use this" | Single operator (Chris Lunt). Adding auth to a local tool adds complexity with zero current value. The deployed admin pages already use BasicAuth for the only multi-user surface. | Keep the local tool unauthenticated (local network only) or reuse BasicAuth if exposed remotely |
| "Publish to staging first, then prod" | "Test before real publish" | No staging environment exists; Render has one service on main branch. A staging path adds config surface for zero benefit. | Use the dry-run diff as the staging equivalent |
| Export to formats other than JSON seed | "CSV / spreadsheet export" | No downstream consumer for alternative formats; adds complexity | The existing JSON seed is the canonical format; the deployed site reads it directly |
| Infinite scroll on the commander grid | "Feels modern" | The existing grid has server-side `GetPagedProcessedCommandersAsync` and a numbered pagination control. Infinite scroll breaks keyboard navigation, makes "go to page N" impossible, and complicates accessibility. | Use "load-page-on-demand" AJAX — numbered pages, load content async on click; see Grid Paging section |

---

## Design Notes

### Approve/reject state model

The existing `content_site_index` has `is_visible` (published to the deployed site) but no
"reviewed/approved" column. There are two design options:

**Option A — Reuse is_visible as the approve gate.** After distill, new entries start
`is_visible=false`. The review queue shows all `is_visible=false` entries with distilled status.
Approve = flip `is_visible=true`. Reject = block + hard-delete (existing `RunBlockVideoAsync`).
Pros: no schema change. Cons: "hidden intentionally" and "not yet reviewed" are
indistinguishable.

**Option B — Add an `approval_status` column** (`pending`, `approved`, `rejected`) to
`content_site_index`. Publish = set `is_visible=true` on approved entries. Pros: explicit state
machine, queryable. Cons: schema migration + self-healing migration (same pattern as
`is_evergreen` self-healing ALTER in v1.5).

Recommendation: Option B. The explicit status column is worth the schema migration cost because
the pipeline now has four meaningful states: harvested-not-distilled, distilled-pending-review,
approved (ready to publish), and rejected. The self-healing ALTER pattern is established.

### YouTube discovery — YoutubeExplode vs Data API v3

The existing `YouTubeChannelVideoLister` uses YoutubeExplode (HTML scraping, no API key, free).
It supports: channel URL/handle/ID resolution, listing N most recent, and fetching by explicit
video IDs. The AngleSharp concurrency bug constrains it to serialized calls.

The Data API v3 adds: keyword search for channels, video search within a channel, quota-metered
calls (100 units per search). For v1.7's discovery UX, YoutubeExplode browse covers the core
case (paste a channel handle, list recent videos). Data API v3 is only needed for "find a creator
by name" search — which is a differentiator, not table stakes. Recommend scoping Data API v3
to channel-search-by-name only, and protecting it behind a quota guard showing remaining
units before each call.

### Grid paging — AJAX on-demand vs current full-page-reload

The `AdminHarvestController.Index` loads the entire page including `GetDistinctProcessedCommanderCountAsync`
+ `GetPagedProcessedCommandersAsync` on every request, regardless of which page is requested.
The slow initial load is likely the count query (cross-join aggregate) running without the
benefit of pagination on the count itself. Three options:

**Option 1 — Numbered AJAX pages (recommended).** Initial page load skips the grid entirely and
renders a skeleton/placeholder. A JS call to `GET /Admin/Harvest/commanders?page=N` returns
a partial HTML table (Razor partial or JSON). Page navigation clicks fire AJAX, replace the
table body, update the pagination control. Benefits: keyboard-navigable, accessible, no
position-loss on page reload, works without JS (degrade to full-page query param). This
matches the existing pattern of `AdminHarvest/Status` polling — the codebase already has AJAX
fetch + DOM-replace TypeScript.

**Option 2 — "Load more" button.** Appends next page to the bottom. Easier to implement but
loses the ability to jump to a specific page. Not recommended for a data grid (defeats
"show me page 5 of alphabetically sorted commanders").

**Option 3 — Infinite scroll.** Anti-feature (see above).

Recommendation: Option 1. A new `GET /Admin/Harvest/commanders` JSON or partial-HTML endpoint;
existing `GetPagedProcessedCommandersAsync` drives it unchanged; JS replaces the grid section
on page-click. Low implementation risk.

---

## Feature Dependencies

```
Paste URL / harvest+distill
    └──requires──> Dedup check (already in pipeline)
    └──requires──> Distill status tracking (already in store)

Channel browse UI
    └──requires──> IYouTubeChannelVideoLister.ListRecentAsync (exists)
    └──requires──> Harvested-status lookup by youtube_id (new batch read method needed)

Multi-select batch harvest
    └──requires──> Channel browse UI (the selection surface)
    └──requires──> Paste URL/ID path (same CLI internals)

Review/approve queue
    └──requires──> Per-item preview read path (new read service, existing store)
    └──requires──> Approve/reject state model (schema change recommended — Option B)
    └──requires──> Inline tag editing (optional enhancement, depends on queue)

Publish approved entries
    └──requires──> Review/approve queue (approved state)
    └──requires──> Either: seed-export + commit path (exists) OR direct prod-DB write (new)

"What will change" diff
    └──requires──> Publish path choice (diff shape differs by path)
    └──requires──> Prod state readable (either from prod-DB or via HTTP check)

Direct prod-DB push
    └──requires──> Prod connection config (new, secrets-safe)
    └──requires──> ContentSiteIndexStore write path (exists, just needs Postgres connection)

Post-publish verification
    └──requires──> Direct prod-DB push OR HTTP scrape of deployed site

YouTube Data API v3 creator search
    └──requires──> API key config (new)
    └──requires──> Quota guard UI

Lazy/AJAX grid paging (AdminHarvest commander grid)
    Independent — no dependency on Content KB pipeline
    └──requires──> New partial endpoint (GET /Admin/Harvest/commanders)
    └──requires──> JS page-click handler (new TS, reuses fetch+DOM-replace pattern)
```

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Reuse vs New Build |
|---------|------------|---------------------|----------|--------------------|
| Paste URL/ID → harvest+distill | HIGH | LOW | P1 | ~90% reuse — thin controller wrapping existing CLI internals |
| Dedup / already-harvested surfacing | HIGH | LOW | P1 | 100% reuse — `ResolveHarvestVideoIdAsync` logic |
| LLM spend shown before distill | HIGH | LOW | P1 | 95% reuse — `RunDistillAsync(dryRun:true)` exists |
| Distill status tracking / review queue scaffold | HIGH | MEDIUM | P1 | 70% reuse — store reads exist; UI + approval state is new |
| Per-item preview (summary/clips/tags) | HIGH | MEDIUM | P1 | 60% reuse — store reads exist; assembly service is new |
| Approve / reject individual items | HIGH | MEDIUM | P1 | 40% reuse — `is_visible` exists; approval_status column is new |
| Seed-export + commit publish path | HIGH | LOW | P1 | 100% reuse — `RunContentIndexExportAsync` exists; UI wrapper is new |
| Blocked-video management in UI | MEDIUM | LOW | P1 | 100% reuse — `RunBlockVideoAsync/Unblock/List` exist |
| Channel browse UI (by known handle) | HIGH | MEDIUM | P2 | 70% reuse — `ListRecentAsync` exists; thumbnail display + harvested-status badge are new |
| "What will change" diff before publish | HIGH | MEDIUM | P2 | 50% reuse — export JSON exists; diff vs prod state is new |
| Post-action spend summary | MEDIUM | LOW | P2 | 100% reuse — `DistillCounts` + ledger query |
| Multi-select batch harvest | MEDIUM | MEDIUM | P2 | 70% reuse — `--video-ids` path exists; multi-select form + progress feedback are new |
| Inline tag editing | MEDIUM | MEDIUM | P2 | 60% reuse — vocabulary + store exist; edit form is new |
| Lazy/AJAX grid paging (AdminHarvest) | MEDIUM | LOW | P2 | 80% reuse — `GetPagedProcessedCommandersAsync` exists; new partial endpoint + JS |
| Direct prod-DB push path | HIGH | HIGH | P2 | 30% reuse — `ContentSiteIndexStore` write path exists; prod connection config + secrets management are new |
| Real-time harvest+distill progress | MEDIUM | HIGH | P3 | 20% reuse — polling endpoint pattern exists in AdminHarvest; wiring to Content KB pipeline is new |
| Creator search by handle/URL (ad-hoc, no pre-config) | MEDIUM | LOW | P3 | 95% reuse — `ListRecentAsync` already accepts any URL/handle/ID |
| YouTube Data API v3 channel search by name | LOW | HIGH | P3 | New build — quota management, API key, new HTTP client |
| Post-publish verification | MEDIUM | MEDIUM | P3 | 50% reuse if prod-DB exists; LOW otherwise (HTTP check) |

**Priority key:** P1 = in-scope for v1.7 MVP, P2 = should complete in v1.7 if schedule allows, P3 = defer unless trivial

---

## MVP Definition

### Launch With (v1.7 core)

- [ ] Paste URL/ID → harvest+distill with spend preview — validates the UI wraps the CLI cleanly
- [ ] Distill-status tracking visible in queue — operator can see what state each video is in
- [ ] Per-item review queue (summary + clips + tags preview) — core value of "review before publish"
- [ ] Approve / reject per item — the gate before publish
- [ ] Blocked-video management in UI — parity with existing CLI block/unblock/list
- [ ] Seed-export + commit publish path — the known-good publish mechanism already working in prod
- [ ] LLM spend shown before distill — prevents surprise cost; uses existing dry-run path
- [ ] Lazy/AJAX grid paging for AdminHarvest commander grid — independent bug fix, low risk

### Add After Core Scaffold (v1.7 complete)

- [ ] Channel browse UI (known handle/URL) — adds discovery without Data API quota risk
- [ ] Multi-select batch harvest — quality-of-life once browse works
- [ ] "What will change" diff before publish — safety for publish operations
- [ ] Direct prod-DB push path — skip the deploy cycle; only after commit path is proven
- [ ] Inline tag editing — refinement once queue is working

### Future Consideration (v1.8+)

- [ ] Real-time progress feedback (SSE/polling) — high complexity, nice-to-have for long runs
- [ ] YouTube Data API v3 creator search by name — quota risk, low marginal value given handle browse
- [ ] Post-publish verification — useful once direct push exists
- [ ] Source management CRUD in UI — currently CLI-only; low priority since sources change rarely

---

## Sources

- Codebase: `DeckFlow.CLI/ContentKbCommandRunners.cs` — all harvest/distill/block/export internals
- Codebase: `DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs` + `YouTubeChannelVideoLister.cs` — YoutubeExplode-based channel listing, serialized concurrency constraint
- Codebase: `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — existing paged grid pattern, AJAX status polling pattern
- Codebase: `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` — existing `is_visible` / bulk-publish UI
- Codebase: `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `is_visible`, `is_evergreen` schema; self-healing ALTER migration pattern
- Codebase: `DeckFlow.Core/Content/IContentVideoStore.cs` — `distill_status` tracking surface
- PROJECT.md v1.7 target features — "dual publish paths", "review/approve queue", "Data API v3"
- Admin tooling UX conventions: numbered pagination + AJAX replacement is the established pattern in this codebase (see `AdminHarvest/Status` 1-second polling + DOM-replace)

---
*Feature research for: DeckFlow v1.7 Local Harvest & Publish Studio*
*Researched: 2026-06-13*
