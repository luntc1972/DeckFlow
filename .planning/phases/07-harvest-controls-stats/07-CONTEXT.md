# Phase 7: Harvest Controls + Stats - Context

**Gathered:** 2026-05-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver `/Admin/Harvest` — operator-facing page (inside the Phase 6 admin shell) that lets the operator (a) trigger an Archidekt bulk harvest with a 15/30/60-minute preset cap, (b) submit a single Archidekt deck URL for inline harvest, (c) cancel a running harvest gracefully, (d) configure and pause/resume a recurring harvest schedule (Off / 2h / 4h / 8h / 24h) persisted to Postgres, and (e) view a stats panel covering total decks, observations, top-10 commanders, recent runs (last 10), Postgres storage size, last successful run, and next scheduled run. All run history and schedule state persist in Postgres tables (`harvest_runs`, `harvest_schedule`) so they survive Render redeploys.

This phase ships the **operator surface and durability layer** for the existing `ArchidektCacheJobService` machinery. Analytics middleware lands in Phase 8.

</domain>

<decisions>
## Implementation Decisions

### Job persistence + redeploy survivability
- **D-01:** `harvest_runs` is the **single source of truth** for job state. `ArchidektCacheJobService` drops its `ConcurrentDictionary<Guid, ArchidektCacheJobStatus>` and `_activeJobId` field. Every state transition (Queued → Running → Succeeded/Failed/Cancelled) writes a row update. `/Admin/Harvest` reads PG directly. The status GET endpoint may use `IMemoryCache` with a 1–2 second TTL to absorb tight polling loops; planner picks the exact TTL.
- **D-02:** **Startup-sweep reaper.** On `EnsureSchemaAsync`, run `UPDATE harvest_runs SET state='Failed', error_message='interrupted by redeploy', completed_utc=now() WHERE state IN ('Queued','Running','Stopping')`. Single-instance Render means any non-terminal row at startup is by definition orphaned. No heartbeat column, no separate reaper service.
- **D-03:** **`harvest_runs` schema:**
  - `id UUID PRIMARY KEY`
  - `kind TEXT NOT NULL CHECK (kind IN ('bulk','url'))`
  - `state TEXT NOT NULL` — values: `Queued`, `Running`, `Stopping`, `Succeeded`, `Failed`, `Cancelled`
  - `requested_utc TIMESTAMPTZ NOT NULL DEFAULT now()`
  - `started_utc TIMESTAMPTZ NULL`
  - `completed_utc TIMESTAMPTZ NULL`
  - `duration_seconds INT NOT NULL` — for `kind='url'` set to `0` (sync) or actual elapsed
  - `decks_processed INT NOT NULL DEFAULT 0`
  - `additional_decks_found INT NOT NULL DEFAULT 0`
  - `error_message TEXT NULL`
  - `url TEXT NULL` — populated only for `kind='url'`
  - Indexes: `(state)` for active-job lookup; `(started_utc DESC)` for recent-runs log.
- **D-04:** **Run-now duration cap stays 60 min.** `ArchidektCacheJobService.EnqueueAsync` already enforces this. Admin form posts only one of `{900, 1800, 3600}` seconds; controller validates against that whitelist. The existing public API endpoint (`POST /api/archidekt-cache-jobs`) keeps its current freeform-up-to-3600 behavior — admin form is the strict path.

### Run lifecycle — cancel, schedule, live status
- **D-05:** **Per-job linked CancellationTokenSource for HARV-03 cancel.** `ArchidektCacheJobService` holds a `_activeJobCts` (linked to host `stoppingToken`). `Cancel` API calls `_activeJobCts.Cancel()`. `RunCacheSweepAsync` already accepts a `CancellationToken`; `ArchidektDeckCacheSession.RunAsync` already loops between decks, so the token check happens naturally between deck imports. `OperationCanceledException` propagates → state row updated to `Cancelled`. Whether to write an interim `Stopping` state row between cancel-request and OCE landing is **Claude's discretion** — ROADMAP SC #3 says the page must transition to "Stopping then Failed/Cancelled within 30s"; planner picks the cleanest write point.
- **D-06:** **New `HarvestScheduleService` (BackgroundService) + `harvest_schedule` single-row table.** Schema:
  - `id INT PRIMARY KEY CHECK (id = 1)` (single-row enforcement)
  - `interval_hours INT NULL` — `NULL` = Off; allowed values when set: `{2, 4, 8, 24}`
  - `paused BOOLEAN NOT NULL DEFAULT FALSE`
  - `updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()`

  `HarvestScheduleService` wakes every 60s, reads `IHarvestScheduleCache.Snapshot()`, computes `next_due = last_success_utc + interval_hours`, and calls `EnqueueAsync(60min)` if `now >= next_due AND NOT paused AND interval_hours IS NOT NULL`. Whole scheduler is gated by the Phase 6 feature flag `harvest.cron.enabled` (kill switch).
- **D-07:** **Schedule edit + cache invalidation mirror Phase 6 D-10.** Admin POST writes `harvest_schedule` row → calls `IHarvestScheduleCache.ReloadAsync()` → returns 302 to `/Admin/Harvest`. Scheduler reads from cache `Snapshot()` each tick (no per-tick PG roundtrip). Pause = same write path with `paused=TRUE`. Resume = `paused=FALSE`. Off = `interval_hours=NULL`.
- **D-08:** **Live status: AJAX poll every 3s while `state IN (Queued, Running, Stopping)`.** Stop polling on `Succeeded`/`Failed`/`Cancelled`. TypeScript module under `wwwroot/ts/` posts `fetch('/Admin/Harvest/status')` returning JSON. Same-origin gate via `SameOriginRequestValidator`. Page includes a `<noscript>` `<meta http-equiv="refresh" content="5">` fallback for no-JS users.

### Single-URL on-demand harvest UX (HARV-02)
- **D-09:** **Sync URL harvest — POST blocks, redirects with TempData banner.** Operator submits Archidekt URL; controller calls existing single-deck import path (via `IArchidektDeckImporter` or equivalent) inline; redirects to `/Admin/Harvest` with `TempData["HarvestResult"] = "Harvested {commander}: {n} new observations"` (or error on failure). No polling, no separate panel. Typical latency 1–3s.
- **D-10:** **URL harvests record to `harvest_runs` with `kind='url'`** and the `url` column populated. `requested_utc`/`started_utc`/`completed_utc` are all set since the operation is sync. `decks_processed=1` on success, `0` on failure. Recent-runs log (HARV-06) shows bulk and URL harvests interleaved by `started_utc DESC`.
- **D-11:** **Page topology:** single page `/Admin/Harvest` with vertically stacked panels in this order:
  1. **Run Now** (preset 15/30/60 dropdown + Run button + live status block)
  2. **Single URL** (URL input + Submit button + last-result banner)
  3. **Schedule** (interval picker Off/2h/4h/8h/24h + Pause/Resume button + next-run timestamp)
  4. **Stats** (counts, top commanders, recent runs, storage, last/next run)
- **D-12:** **URL harvest bypasses the channel queue** — it does not call `ArchidektCacheJobService.EnqueueAsync`. It writes directly through `CategoryKnowledgeStore.PersistObservedCategoriesAsync` (or the appropriate single-deck import method). The `_sweepGate` semaphore in `RunCacheSweepAsync` is bulk-only; single-deck import already runs concurrently with sweeps in production. No 409 Conflict if a bulk run is active.

### Stats panel data + caching (HARV-06)
- **D-13:** **`IMemoryCache` 60s TTL on the whole stats payload.** Single cache key `admin.harvest.stats.v1` holding a record with all metrics. Cache is invalidated explicitly on `harvest_runs` insert/update (so an operator who just clicked Run Now sees fresh state). Aligns with existing `IMemoryCache` usage in `CommanderSpellbookService` and others.
- **D-14:** **`pg_database_size()` is PG-only, branched via `IRelationalDialect.IsPostgres`** (mirrors Phase 6 D-07 / `feedback_sqlite_postgres_sql_divergence.md`). SQLite path returns `null`; Razor view renders `"N/A"` for the storage-size row only. All other metrics work on both providers.
- **D-15:** **Top commanders: top 10, deck-count GROUP BY.** New repository method `GetTopCommandersAsync(int n = 10, CancellationToken ct)` returning `IReadOnlyList<TopCommanderRow(string CommanderName, int DeckCount)>`. SQL shape:
  ```sql
  SELECT commander_name, COUNT(DISTINCT deck_id) AS deck_count
  FROM category_knowledge
  WHERE commander_name IS NOT NULL
  GROUP BY commander_name
  ORDER BY deck_count DESC
  LIMIT @n
  ```
  Works on both providers. Planner adds `(commander_name)` index only if `EXPLAIN ANALYZE` on prod-sized data shows it's needed.
- **D-16:** **Full stats metric set (HARV-06):**
  1. `total_decks` — `SELECT COUNT(DISTINCT deck_id) FROM category_knowledge`
  2. `total_decks_30d` — same with `WHERE first_seen_utc >= now() - interval '30 days'` (PG) / `julianday('now') - 30` (SQLite)
  3. `total_observations` — `SELECT COUNT(1) FROM category_knowledge`
  4. `top_commanders` — D-15 query, N=10
  5. `recent_runs` — `SELECT * FROM harvest_runs ORDER BY started_utc DESC NULLS LAST LIMIT 10`
  6. `pg_storage_size` — `SELECT pg_database_size(current_database())` (PG only; SQLite returns null)
  7. `last_success_utc` — `SELECT MAX(completed_utc) FROM harvest_runs WHERE state='Succeeded'`
  8. `next_scheduled_utc` — computed: `last_success_utc + (harvest_schedule.interval_hours hours)`, or `null` if Off/paused/no prior success

### Claude's Discretion
- Whether to write an interim `Stopping` state row between cancel-request and `OperationCanceledException` landing (D-05). Both options satisfy ROADMAP SC #3's 30-second window.
- Exact `IMemoryCache` TTL on the status GET (D-01) — anywhere from 0s (no cache) to 2s. Pick what feels snappy without thrashing PG.
- Whether `ArchidektCacheJobService` keeps its current `BackgroundService` + `Channel` shape or simplifies to a direct-execute pattern now that PG is the source of truth. Channel was useful for the in-memory dict; with PG, a simpler "check for active row, refuse if present, else INSERT + spawn Task.Run" pattern may be cleaner. Planner decides.
- Whether the URL harvest controller action lives on `AdminHarvestController` (with all other admin harvest actions) or a sibling `AdminHarvestUrlController`. One controller is fine for v1.1.
- Exact poll cadence on `/Admin/Harvest/status` if user cancels — could drop from 3s to 1s briefly to confirm `Stopping → Cancelled` transition lands fast for the SC #3 demo. Optional polish.
- Whether `harvest_schedule` row is seeded with `interval_hours=NULL, paused=FALSE` on `EnsureSchemaAsync` (default Off), or absent until first save. Seeded-default is simpler for the page-render path (no null-row branch).
- Index choice on `category_knowledge(commander_name)` for the top-N query (D-15) — only if planner sees query plan evidence it's needed.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/PROJECT.md` — milestone definition, constraints (RAM cap, public repo, no framework migration)
- `.planning/REQUIREMENTS.md` §HARV-01..07 — locked requirements
- `.planning/ROADMAP.md` §"Phase 7" — goal, depends-on, success criteria (5 items)
- `.planning/phases/07-harvest-controls-stats/07-CONTEXT.md` — this file
- `.planning/phases/06-admin-shell-flags-foundation/06-CONTEXT.md` — Phase 6 decisions carried forward (D-10 hot-reload pattern, feature flag plumbing, `_AdminLayout` shell)

### Codebase patterns to follow
- `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — existing background-job machinery being extended/replaced
- `DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs` — existing public API; admin counterpart added at `/Admin/Harvest`
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs:91` (`RunCacheSweepAsync`) — long-running loop with `_sweepGate`; already accepts `CancellationToken`
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` — `EnsureSchemaAsync` idiom for new `harvest_runs` + `harvest_schedule` tables
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` (Phase 6) — admin chrome for all `/Admin/*` views; `Views/AdminHarvest/_ViewStart.cshtml` sets it
- `DeckFlow.Web/Services/FeatureFlagCache.cs` (Phase 6) — `IFeatureFlagCache` pattern; mirror for `IHarvestScheduleCache`
- `DeckFlow.Web/Infrastructure/AdminBruteForceTrackerStore.cs` — `IsPostgres` dialect-branching example for D-14
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — guard on the AJAX status endpoint
- `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — `Controllers/Admin/` placement precedent + `[ValidateAntiForgeryToken]` on POST
- `DeckFlow.Web/wwwroot/ts/` — TypeScript module placement; compiled to `wwwroot/js/` via MSBuild

### Project conventions / build constraints
- `CLAUDE.md` §Constraints — tech stack pinned (ASP.NET 10 + Razor), no framework migration, public repo (no secrets), commits plain default-author (no Co-Authored-By trailer), 512MB RAM cap on Render
- `CLAUDE.md` §"HTTP / Resilience Conventions" — services follow public-ctor + internal-test-ctor pattern; new `IHarvestScheduleCache` matches
- `feedback_sqlite_postgres_sql_divergence.md` (memory) — qualify upsert columns with table name, prefer `COUNT(1)` over `EXISTS`, run Postgres integration tests before shipping new storage SQL

### Memory / prior-decision context
- Phase 6 D-10 — "synchronous in-process reload" pattern reused for schedule cache
- Phase 6 D-12 — `IFeatureFlagCache.IsEnabled("harvest.cron.enabled")` gates `HarvestScheduleService`
- Phase 6 D-14 — singleton + `IHostedService` dual registration pattern for `IFeatureFlagCache`; reuse for `IHarvestScheduleCache` if planner picks the same shape

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`ArchidektCacheJobService`** (`DeckFlow.Web/Services/ArchidektCacheJobService.cs:36`) — `BackgroundService` + `Channel<ArchidektCacheJobStatus>`. Currently in-memory only; D-01 converts state ownership to PG. The `EnqueueAsync` 60-min cap (`ArchidektCacheJobService.cs:60-63`) already matches HARV-01.
- **`ArchidektCacheJobsController`** (`DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs`) — public API stays as-is; new `AdminHarvestController` lives under `Controllers/Admin/` and shares the underlying service.
- **`CategoryKnowledgeStore.RunCacheSweepAsync`** (`DeckFlow.Web/Services/CategoryKnowledgeStore.cs:91`) — already takes `CancellationToken`; `_sweepGate` semaphore guarantees one sweep at a time. D-05 cancel rides existing token plumbing.
- **`EnsureSchemaAsync` idiom** in `CategoryKnowledgeStore`/`FeedbackStore`/`FeatureFlagStore` (Phase 6) — `harvest_runs` + `harvest_schedule` follow the same `CREATE TABLE IF NOT EXISTS` + idempotent seed pattern.
- **`IFeatureFlagCache`** (Phase 6) — gates `HarvestScheduleService`; the singleton+IHostedService dual-registration pattern is the model for `IHarvestScheduleCache`.
- **`_AdminLayout.cshtml`** (Phase 6) — shell complete; new view dir `Views/AdminHarvest/` slots in.
- **Antiforgery (`[ValidateAntiForgeryToken]`)** already wired on admin POSTs (Phase 6 D-15) — reuse on every form on `/Admin/Harvest`.

### Established Patterns
- **One service interface + sealed implementation per file** — apply to `IHarvestScheduleCache` / `HarvestScheduleCache`, `IHarvestRunStore` / `PostgresHarvestRunStore`, `HarvestScheduleService` (BackgroundService).
- **Public DI ctor + internal test ctor** with `[InternalsVisibleTo("DeckFlow.Web.Tests")]` — apply to all new services.
- **Razor views per controller folder, PascalCase `.cshtml`** — `Views/AdminHarvest/Index.cshtml`. `_ViewStart.cshtml` sets `Layout = "_AdminLayout"`.
- **Structured Serilog logging** — named placeholders (`{JobId}`, `{IntervalHours}`); never interpolation.
- **Same-origin gate on AJAX endpoints** — `SameOriginRequestValidator.IsValid(Request)` early-return 403.
- **`IRelationalDialect.IsPostgres` branch** — mandatory for `pg_database_size()`, optional for `interval '30 days'` vs SQLite `julianday()` (planner picks per-query).
- **Hosted background work + singleton facade dual registration** (`Program.cs:178-180` for `ArchidektCacheJobService`) — pattern for `HarvestScheduleService`.

### Integration Points
- `DeckFlow.Web/Program.cs:50-189` — register `IHarvestRunStore`, `IHarvestScheduleCache` (Singleton + IHostedService), `HarvestScheduleService` (IHostedService).
- `DeckFlow.Web/Program.cs` startup DB validation block — call `IHarvestRunStore.EnsureSchemaAsync()` (which runs the D-02 reaper) and `IHarvestScheduleCache.LoadAsync()` before Kestrel binds, alongside other stores.
- `ArchidektCacheJobService` ctor — accepts `IHarvestRunStore` (replaces `ConcurrentDictionary` writes); existing `IHostedService` registration unchanged.
- `Program.cs` `MapWhen("/Admin")` BasicAuth branch (Phase 5/6) — new `AdminHarvestController` slots in, no middleware change.
- TypeScript: new `wwwroot/ts/admin-harvest.ts` compiles to `wwwroot/js/admin-harvest.js` via existing MSBuild target.

</code_context>

<specifics>
## Specific Ideas

- **"Page must survive Render redeploys"** is the durability anchor — if the operator clicks Run Now, sees "Running", and Render redeploys mid-sweep, the post-deploy page must show the run as `Failed (interrupted by redeploy)`, not a stuck `Running` row. D-02 startup-sweep is non-negotiable.
- **"Single URL = paste-and-go"** — operator workflow is: copy Archidekt URL from a tournament list, paste into the form, hit submit, see "Harvested {commander}: 47 new observations" banner. Latency tolerance is ~3s; anything slower needs spinner UX. Sync path (D-09) chosen specifically for this.
- **Cron is gated by `harvest.cron.enabled`** — operator can disable the entire scheduler from `/Admin/Flags` without a deploy, matching the Phase 6 kill-switch demo philosophy. Default ON.
- **Top-10 commanders is a "what's working" signal** — operator wants to see at a glance which commanders the harvest is finding most. 10 is enough to spot-check; 5 too sparse, 20 too noisy.
- **"Storage size matters"** — 256MB Postgres tier on Render Basic is the constraint. Operator needs to see usage trend before hitting the cap. Hence `pg_database_size()` even though it's PG-only.

</specifics>

<deferred>
## Deferred Ideas

- **Harvest run retention / cleanup** — `harvest_runs` will grow unbounded. v1.1 keeps everything; future phase adds `DELETE WHERE completed_utc < now() - interval '90 days'` policy.
- **Error-rate alerting** — surface "3 of last 5 runs failed" banner. Belongs in Phase 8 (analytics) or a future ops phase.
- **Scheduler clock drift on redeploy** — `next_scheduled_utc` is computed from `last_success_utc + interval`; redeploys don't shift it. If operators want "always run at top of hour", add an `anchor_utc` column later.
- **Audit trail (who triggered manual run)** — single-operator BasicAuth means it's always the same user. POLISH-02-equivalent for harvest.
- **Multi-active-job support** — current contract is "one bulk job at a time". URL harvests can run alongside (D-12). Multi-bulk would require deeper queue work.
- **Cron expression support** — fixed 5-option picker (Off/2h/4h/8h/24h) is sufficient for HARV-05. Cron-style strings deferred indefinitely.
- **Email/Slack notification on harvest completion** — out of scope for v1.1.
- **Per-commander harvest filtering** ("only harvest decks playing X") — feature creep; defer.

</deferred>

---

*Phase: 7-harvest-controls-stats*
*Context gathered: 2026-05-03*
