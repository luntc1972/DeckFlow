# Phase 7: Harvest Controls + Stats - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-03
**Phase:** 7-harvest-controls-stats
**Areas discussed:** Job persistence + redeploy survivability, Run lifecycle (cancel/schedule/live status), Single-URL on-demand harvest UX, Stats panel data + caching

---

## Job persistence + redeploy survivability

### Q1: How should harvest_runs relate to the in-memory job tracker?

| Option | Description | Selected |
|--------|-------------|----------|
| PG = source of truth, drop in-memory dict | Service stops using ConcurrentDictionary; every state transition writes harvest_runs row directly. /Admin/Harvest reads PG. Optional 1-2s IMemoryCache TTL on status GET. | ✓ |
| Write-through cache | Keep ConcurrentDictionary as live status, write-through to PG. Two sources of truth = drift risk on redeploy. | |
| PG-only, no in-memory | Same as recommended but no caching at all. Every poll hits PG raw. | |

### Q2: How to handle 'Running' rows orphaned by Render redeploy mid-run?

| Option | Description | Selected |
|--------|-------------|----------|
| Startup sweep marks orphans Failed | EnsureSchemaAsync runs UPDATE ... WHERE state IN ('Queued','Running'). Single-instance Render makes this safe. | ✓ |
| Heartbeat column + reaper background loop | last_heartbeat_utc + separate reaper. Multi-instance ready but heavier. | |
| Lazy reap on next run-now | Don't reap on startup; cleanup on next manual trigger. Leaves bogus 'Running' visible. | |

### Q3: harvest_runs schema (minimum viable)?

| Option | Description | Selected |
|--------|-------------|----------|
| id, kind, state, requested/started/completed_utc, duration_seconds, decks_processed, additional_decks_found, error_message | Mirrors existing record + kind for HARV-02 distinction. | ✓ (extended in Q3.1 with `url` column from Area 3) |
| + triggered_by ('manual'/'schedule') | Adds source-of-trigger column. | |
| + url column | Captures Archidekt URL for kind='url'. | (folded into ✓ via Area 3 D-10) |

### Q4: Run-now duration cap?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep 60min hard cap, expose 15/30/60 presets in UI | Service already enforces 1h max. Admin form whitelists {900,1800,3600}; API endpoint stays freeform-up-to-3600. | ✓ |
| Tighten cap to 60min and add server-side preset whitelist | Reject any non-preset duration at the API too. | |
| Keep cap, freeform API + presets in UI | Same as recommended (this is what was selected). | |

---

## Run lifecycle — cancel, schedule, live status

### Q1: HARV-03 cancel propagation?

| Option | Description | Selected |
|--------|-------------|----------|
| Per-job linked CancellationTokenSource | _activeJobCts linked to host stoppingToken. Cancel API calls _activeJobCts.Cancel(). RunCacheSweepAsync's existing CT plumbing carries it. OperationCanceledException → state='Cancelled'. | ✓ |
| Cooperative bool flag in harvest_runs row | cancel_requested column + sweep polls between decks. Adds column + cadence question. | |
| Hybrid CTS + 'Stopping' state | CTS for cancellation, also write 'Stopping' interim state. | (interim state delegated to Claude's discretion in follow-up Q) |

### Q2: Schedule service shape?

| Option | Description | Selected |
|--------|-------------|----------|
| New HarvestScheduleService BackgroundService + harvest_schedule table | Single-row table; wakes every 60s; gated by harvest.cron.enabled flag. | ✓ |
| Reuse ArchidektCacheJobService with internal Timer | Mixes scheduling and execution responsibilities. | |
| Separate service + Off=0 sentinel instead of NULL | NOT NULL constraint variant. Less expressive. | |

### Q3: Schedule edit + cache invalidation?

| Option | Description | Selected |
|--------|-------------|----------|
| Postgres write + IHarvestScheduleCache.ReloadAsync | Mirrors Phase 6 D-10 pattern. Scheduler reads from cache.Snapshot() each tick. | ✓ |
| PG-only on every tick, no cache | Simpler, ~60 PG roundtrips/hour. | |
| Cache + PG NOTIFY/LISTEN | Multi-instance ready; overkill on Render Starter. | |

### Q4: Live status feedback during a run?

| Option | Description | Selected |
|--------|-------------|----------|
| fetch() AJAX poll every 3s while in (Queued/Running/Stopping); stop on terminal state; <noscript> meta-refresh fallback | TS module under wwwroot/ts/, same-origin gate. | ✓ |
| Plain meta-refresh every 5s on Razor page | Whole-page reload. Flickery. | |
| AJAX poll with adaptive cadence (1s first 30s, then 5s) | More TS code; better UX but overkill for internal page. | |

### Follow-up: Discuss 'Stopping' interim state row?

| Option | Description | Selected |
|--------|-------------|----------|
| More — ROADMAP SC #3 calls for explicit 'Stopping' | Roadmap requires "Stopping then Failed/Cancelled within 30s". | |
| Next area — Stopping state can be Claude's discretion | Locked enough; planner picks write point. | ✓ |

---

## Single-URL on-demand harvest UX (HARV-02)

### Q1: Sync (block + show) or async (status panel)?

| Option | Description | Selected |
|--------|-------------|----------|
| Sync — POST blocks, redirects to result page | 1-3s typical via existing ArchidektApiDeckImporter. TempData banner shows result. | ✓ |
| Async — same job machinery as bulk | Uniform UX but adds complexity (queue contention). | |
| Sync with timeout fallback to async | Most code paths, least value. | |

### Q2: How does single-URL write to harvest_runs?

| Option | Description | Selected |
|--------|-------------|----------|
| Always insert row with kind='url' + url column | Adds nullable url TEXT column; recent-runs log shows both kinds interleaved. | ✓ |
| Don't record URL harvests in harvest_runs | Breaks unified history. | |
| Record but hide kind='url' from default view by default | Adds UI surface for marginal value. | |

### Q3: Where is the URL form on /Admin/Harvest?

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated panel above stats, below Run-Now | Page topology: [Run Now] [Single-URL] [Schedule] [Stats]. | ✓ |
| Collapsed accordion under Run-Now | Saves space but adds friction. | |
| Separate sub-route /Admin/Harvest/url | Only worth it if URL harvest grows multi-step. | |

### Q4: URL harvest while bulk run is active?

| Option | Description | Selected |
|--------|-------------|----------|
| Allow — single URL bypasses channel queue | Single-deck import already runs concurrently with sweeps in production via PersistObservedCategoriesAsync. | ✓ |
| Reject with 409 Conflict if bulk active | Conservative; forces operator to wait or cancel. | |
| Queue — wait for bulk to finish then run | Adds another job state machine. | |

---

## Stats panel data + page topology (HARV-06)

### Q1: Live PG queries vs IMemoryCache?

| Option | Description | Selected |
|--------|-------------|----------|
| IMemoryCache 60s TTL on whole stats payload | Single cache key invalidated on harvest_runs insert/update. | ✓ |
| Live every page load — no caching | ~5-10 PG roundtrips per page view. Wasteful. | |
| Per-metric cache with different TTLs | Most code, marginal value. | |

### Q2: pg_database_size() on SQLite?

| Option | Description | Selected |
|--------|-------------|----------|
| Branch on IsPostgres dialect; SQLite returns null + UI shows 'N/A' | Mirrors Phase 6 D-07 / feedback memory pattern. | ✓ |
| Production-only metric — hide entirely on SQLite | Different UI per environment. | |
| PG-only — throw on SQLite | Won't work; tests use SQLite. | |

### Q3: Top-N commanders query shape + N value?

| Option | Description | Selected |
|--------|-------------|----------|
| Top 10, GROUP BY commander_name, deck count desc | New repo method GetTopCommandersAsync(int n=10). Index decision deferred to planner via EXPLAIN. | ✓ |
| Top 5, simpler query | Too sparse for stats panel. | |
| Top 20 with pagination | Stats panel shouldn't paginate. | |

### Q4: Full stats metric set for HARV-06?

| Option | Description | Selected |
|--------|-------------|----------|
| All 7 from REQ: total_decks, total_decks_30d, total_observations, top_10_commanders, recent_runs (10), pg_storage_size, last_success_utc, next_scheduled_utc | REQUIREMENTS.md HARV-06 enumerates. 7 distinct queries. | ✓ |
| Drop 30d-decks | Required by HARV-06. | |
| Add cards-harvested-today / observations-per-deck-avg | Scope creep — not in HARV-06. Defer. | |

---

## Claude's Discretion

- Whether to write an interim `Stopping` state row between cancel-request and `OperationCanceledException` landing (D-05). Both options satisfy ROADMAP SC #3's 30-second window.
- Exact `IMemoryCache` TTL on the status GET (D-01) — anywhere from 0s (no cache) to 2s.
- Whether `ArchidektCacheJobService` keeps its `BackgroundService` + `Channel` shape, or simplifies now that PG is source of truth.
- Whether the URL harvest controller action lives on `AdminHarvestController` or a sibling `AdminHarvestUrlController`.
- Adaptive poll cadence on cancel transition (e.g., briefly drop from 3s to 1s).
- Whether `harvest_schedule` row is seeded `NULL/FALSE` on EnsureSchemaAsync or absent until first save.
- Index choice on `category_knowledge(commander_name)` for top-N query — only if EXPLAIN warrants it.

## Deferred Ideas

- **Harvest run retention / cleanup** — bounded retention policy, future phase.
- **Error-rate alerting** — "3 of last 5 failed" banner, Phase 8 candidate.
- **Scheduler clock anchoring** — `anchor_utc` column for "always at top of hour", deferred.
- **Audit trail (who triggered)** — single-operator BasicAuth makes it moot; POLISH-tier.
- **Multi-active-job support** — out of scope.
- **Cron expression support** — fixed picker sufficient.
- **Email/Slack notification** — out of scope.
- **Per-commander harvest filtering** — feature creep.
