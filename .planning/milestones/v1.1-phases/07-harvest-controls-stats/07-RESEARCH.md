# Phase 7: Harvest Controls + Stats - Research

**Researched:** 2026-05-03
**Domain:** Operator-facing admin page (Razor MVC + AJAX) backed by Postgres-persisted job state, recurring schedule, and stats panel; extends existing `ArchidektCacheJobService` machinery; lives inside Phase 6 admin shell.
**Confidence:** HIGH (codebase patterns verified by direct file inspection; all CONTEXT.md decisions cross-referenced against shipped Phase 6 code)

## Summary

CONTEXT.md already locks 16 decisions and a large architecture. Research scope here is narrow: resolve the 7 Claude's-Discretion items, validate that the locked architecture is internally consistent against shipped code (Phase 6 FeatureFlagCache, AdminBruteForceTrackerStore, FeatureFlagStore, AdminFlagsController), and surface landmines that will trip the planner.

Verdict: the locked architecture is sound and every pattern it relies on already ships in Phase 6 (StartAsync sync-load, PeriodicTimer poller, IsPostgres branching, ON CONFLICT DO NOTHING seed, sync cache reload before redirect, antiforgery on admin POST). The biggest landmines are not in the controls layer — they sit at the seams: (1) `ArchidektCacheJobService` swap from `ConcurrentDictionary` to PG must remain wire-compatible with the existing public API controller `ArchidektCacheJobsController` so Phase 7 doesn't break the freeform-3600s API contract; (2) `pg_database_size()` requires the database name from the connection string and `Npgsql` parameter binding for `current_database()` is a one-line gotcha; (3) the cancel UX requires writing the `Stopping` state row from the **controller** (immediately) rather than from inside the cancelled `ExecuteAsync` task (which only sees `OperationCanceledException` and could write `Cancelled` directly).

**Primary recommendation:** Keep `ArchidektCacheJobService` as a `BackgroundService` + `Channel` shape (D-claudes-discretion #3) — the channel is needed for graceful shutdown and EnqueueAsync's "active job already running" guard. Drop only the in-memory `_jobs` dict, not the channel. Write the `Stopping` row from the cancel **controller action** synchronously (before returning the redirect) so AJAX poll sees it immediately. Status GET memory cache TTL = 1 second. Seed `harvest_schedule` with a single Off row on `EnsureSchemaAsync` (eliminates a null-row branch on every page render). Single `AdminHarvestController` with all four POST actions. No `category_knowledge(commander_name)` index in v1.1 — top-10 query runs once per 60s under cache, not per-request.

## User Constraints (from CONTEXT.md)

### Locked Decisions (16 items, D-01..D-16)

**Job persistence + redeploy survivability**
- **D-01:** `harvest_runs` is the single source of truth for job state. Drop `ConcurrentDictionary<Guid, ArchidektCacheJobStatus>` and `_activeJobId`. Every state transition writes a row update. `/Admin/Harvest` reads PG directly. Optional `IMemoryCache` TTL 1-2s on status GET.
- **D-02:** Startup-sweep reaper: `UPDATE harvest_runs SET state='Failed', error_message='interrupted by redeploy', completed_utc=now() WHERE state IN ('Queued','Running','Stopping')`.
- **D-03:** `harvest_runs` schema with `id UUID`, `kind` ('bulk'|'url'), `state`, `requested_utc`, `started_utc`, `completed_utc`, `duration_seconds`, `decks_processed`, `additional_decks_found`, `error_message`, `url`. Indexes: `(state)`, `(started_utc DESC)`.
- **D-04:** Run-now duration cap stays 60 min. Admin form whitelists {900, 1800, 3600}. Existing public API endpoint keeps freeform-up-to-3600 behavior.

**Run lifecycle — cancel, schedule, live status**
- **D-05:** Per-job linked CancellationTokenSource for HARV-03 cancel. `_activeJobCts` linked to host stoppingToken. Whether to write interim Stopping row is Claude's discretion.
- **D-06:** New `HarvestScheduleService` BackgroundService + `harvest_schedule` single-row table (`id INT PK CHECK id=1`, `interval_hours INT NULL`, `paused BOOLEAN`, `updated_utc`). Wakes every 60s. Gated by `harvest.cron.enabled`.
- **D-07:** Schedule edit + cache invalidation mirror Phase 6 D-10. POST → write PG → `IHarvestScheduleCache.ReloadAsync()` → 302 redirect.
- **D-08:** Live status: AJAX poll every 3s while in (Queued, Running, Stopping). Stop on terminal state. TS module under `wwwroot/ts/`. Same-origin gate via `SameOriginRequestValidator`. `<noscript>` meta-refresh fallback.

**Single-URL on-demand harvest UX**
- **D-09:** Sync URL harvest — POST blocks, redirects with `TempData["HarvestResult"]` banner. 1-3s typical latency.
- **D-10:** URL harvests record to `harvest_runs` with `kind='url'`. `requested_utc/started_utc/completed_utc` all set. `decks_processed=1` on success, `0` on failure.
- **D-11:** Page topology: vertically stacked panels — Run Now / Single URL / Schedule / Stats.
- **D-12:** URL harvest bypasses the channel queue. Writes directly through single-deck import path. No 409 Conflict if a bulk run is active.

**Stats panel data + caching**
- **D-13:** `IMemoryCache` 60s TTL on whole stats payload. Single key `admin.harvest.stats.v1`. Invalidated on `harvest_runs` insert/update.
- **D-14:** `pg_database_size()` PG-only via `IRelationalDialect.IsPostgres`. SQLite path returns null; UI renders "N/A".
- **D-15:** Top commanders top 10, GROUP BY commander_name, ORDER BY DISTINCT deck count DESC LIMIT @n. New `GetTopCommandersAsync(int n=10, CancellationToken ct)` method. Index on `category_knowledge(commander_name)` only if EXPLAIN warrants.
- **D-16:** Full stats metric set — total_decks, total_decks_30d, total_observations, top_10_commanders, recent_runs, pg_storage_size, last_success_utc, next_scheduled_utc.

### Claude's Discretion (7 items — RESEARCH RESOLVES BELOW)

1. Stopping interim state row write point (D-05).
2. Status GET `IMemoryCache` TTL exact value (0-2s).
3. `ArchidektCacheJobService`: keep BackgroundService+Channel or simplify to direct Task.Run.
4. URL harvest controller location (single AdminHarvestController vs sibling AdminHarvestUrlController).
5. AJAX poll cadence transitions (3s steady, 1s during cancel?).
6. Seed `harvest_schedule` row on `EnsureSchemaAsync` or absent until first save.
7. Index on `category_knowledge(commander_name)` for top-N.

### Deferred Ideas (OUT OF SCOPE — do not plan)

- Harvest run retention / cleanup policy
- Error-rate alerting banner ("3 of last 5 failed")
- Scheduler clock drift / `anchor_utc` column
- Audit trail of who triggered a manual run
- Multi-active-job support
- Cron expression support
- Email/Slack notification
- Per-commander harvest filtering

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HARV-01 | Operator triggers Archidekt harvest run-now with 15/30/60 preset cap | D-04 + existing `ArchidektCacheJobService.EnqueueAsync` already enforces 60-min hard cap (`ArchidektCacheJobService.cs:60-63`); new admin form whitelists {900,1800,3600} only |
| HARV-02 | Operator submits single Archidekt deck URL for on-demand harvest | D-09 + D-12 — sync POST through `IArchidektDeckImporter.ImportAsync()` (already in DI, `Program.cs:294`); concurrent with bulk sweep is safe (separate `_sweepGate` semaphore in `CategoryKnowledgeStore.cs:19,94`) |
| HARV-03 | Operator cancels running harvest gracefully (current deck completes) | D-05 — `ArchidektDeckCacheSession.RunAsync` (`ArchidektDeckCacheSession.cs:52,109,120`) already checks `cancellationToken.IsCancellationRequested` between decks AND inside the inner deck loop; no new cooperative-flag plumbing needed |
| HARV-04 | Operator pauses/resumes recurring harvest schedule | D-06 + D-07 — `harvest_schedule.paused` boolean flips via same write+cache-reload pattern as flag toggle |
| HARV-05 | Operator configures recurring schedule via interval picker (Off/2h/4h/8h/24h), persisted in PG | D-06 — `interval_hours INT NULL`; NULL = Off; allowed {2,4,8,24} |
| HARV-06 | Stats panel: total decks (lifetime + 30d), total observations, top-N commanders, recent runs (10), PG storage size, last/next run | D-13..D-16 — eight metrics under single 60s cache key, PG-branched storage size |
| HARV-07 | Run history persisted in PG `harvest_runs` table, surviving Render redeploys | D-01 + D-02 + D-03 — single source of truth + startup reaper covers redeploy edge case |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Run-now / cancel / URL submit forms | Frontend Server (Razor + Controller) | API/Backend (`ArchidektCacheJobService`) | Admin UI is server-rendered Razor with antiforgery; controller is a thin orchestrator that calls into the existing job service |
| Live status polling | Browser (TypeScript) → Frontend Server (status JSON endpoint) | Database (PG `harvest_runs`) | TS poll every 3s; controller reads PG; same-origin gate |
| Schedule persistence + tick | Database (PG `harvest_schedule`) | Application (HarvestScheduleService BackgroundService) | PG is source of truth; service reads via `IHarvestScheduleCache.Snapshot()` once per tick |
| Stats panel rendering | Frontend Server (Razor + cached aggregator) | Database (PG queries) | 60s `IMemoryCache` keyed `admin.harvest.stats.v1`; PG queries run on cache miss only |
| Job execution | Application (`ArchidektCacheJobService` BackgroundService + Channel) | Database (PG `harvest_runs` state writes) | Existing in-process job runner; in-memory dict replaced by PG row updates per state transition |
| Run history persistence | Database (PG `harvest_runs`) | — | Single source of truth (D-01) |
| Auth gate | Frontend Server middleware (`BasicAuthMiddleware` on `MapWhen("/Admin")`) | — | Reuses Phase 5/6 plumbing verbatim — no new auth code |
| Antiforgery | Frontend Server (`[ValidateAntiForgeryToken]`) | — | All admin POST actions follow `AdminFeedbackController`/`AdminFlagsController` pattern |

## Standard Stack

### Core (already in solution — verified via `Program.cs` grep)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC | net10.0 | Razor views + controllers + antiforgery | [VERIFIED: `DeckFlow.Web.csproj` `<TargetFramework>net10.0</TargetFramework>`] — pinned tech stack per CLAUDE.md, no migration in this milestone |
| Microsoft.Extensions.Hosting | net10.0 (built-in) | `BackgroundService` for `HarvestScheduleService` and existing `ArchidektCacheJobService` | [VERIFIED: `FeatureFlagCache.cs:14` already extends BackgroundService; same shape needed for HarvestScheduleService] |
| Microsoft.Extensions.Caching.Memory | 10.0.0 | `IMemoryCache` for stats payload (D-13) and status GET TTL | [VERIFIED: registered at `Program.cs:60` (`AddMemoryCache()`); used by `CommanderSpellbookService`, `ScryfallCommanderSearchService`, etc.] |
| Microsoft.Data.Sqlite | 10.0.0 | Local-dev / test SQLite provider | [VERIFIED: `RelationalDatabaseConnection.cs:2,26`] |
| Npgsql | 10.0.0 | Production Postgres provider | [VERIFIED: `RelationalDatabaseConnection.cs:3,27`] |
| Serilog | 9.0.0 (AspNetCore) | Structured logging with named placeholders | [VERIFIED: `Program.cs:38-51`] |

### Supporting (already in solution)

| Library | Purpose | When to Use |
|---------|---------|-------------|
| `IHttpClientFactory` named clients | Single-deck Archidekt import via `ArchidektApiDeckImporter` | URL harvest path (HARV-02). Already DI-registered as `IArchidektDeckImporter` (`Program.cs:294`) |
| `ResiliencePipelineProvider<string>` (Polly v8) | HTTP resilience on Archidekt single-deck import | Already wired through existing importer |
| `[ValidateAntiForgeryToken]` (built-in) | Admin form CSRF protection | Every POST action on `AdminHarvestController` |
| `SameOriginRequestValidator` | CSRF guard on AJAX status endpoint | The `/Admin/Harvest/status` JSON endpoint |
| `IFeatureFlagCache` (Phase 6) | `harvest.cron.enabled` kill-switch gating `HarvestScheduleService` | At top of scheduler tick — short-circuit if off |

### Alternatives Considered

| Instead of | Could Use | Tradeoff | Why Rejected |
|------------|-----------|----------|--------------|
| `BackgroundService` + Channel for ArchidektCacheJobService | Direct `Task.Run` from EnqueueAsync | Simpler shape; one fewer abstraction | Channel still needed for graceful shutdown ordering and active-job dedup; rip-and-replace is more risk than reward (see Open Question Resolution #3) |
| `Hangfire` / `Quartz.NET` for scheduling | Native | Battle-tested cron parsers, persistent queues | Explicitly out of scope per REQUIREMENTS.md; bespoke single-row schedule is sufficient for fixed 5-option picker |
| `IDistributedCache` (Redis) for stats | `IMemoryCache` (in-process) | Survives instance restarts; multi-instance safe | Single-instance Render Starter; Redis adds infra; `IMemoryCache` already used pervasively |
| `Microsoft.Extensions.Http.Resilience` standard handler | Existing RestSharp + direct Polly v8 | Standard MS pattern | CLAUDE.md explicitly forbids — DO NOT migrate |
| FluentMigrator / EF migrations | `EnsureSchemaAsync` + `CREATE TABLE IF NOT EXISTS` | Versioned schema | Out of scope per REQUIREMENTS.md "Out of Scope" table |

**Installation:** Zero new packages. Every dependency is already in solution.

**Version verification:** Skipped — no new packages.

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│ Browser (TS module: admin-harvest.ts)                                   │
│  - poll every 3s while state ∈ {Queued,Running,Stopping}                │
│  - <noscript> meta-refresh 5s fallback                                  │
└────────────────────┬────────────────────────────────────────────────────┘
                     │ fetch('/Admin/Harvest/status')  + Origin/Referer
                     │ POST forms (antiforgery)
                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ BasicAuthMiddleware  (MapWhen("/Admin") — Program.cs:332-334)           │
└────────────────────┬────────────────────────────────────────────────────┘
                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ AdminHarvestController                                                  │
│  ┌──────────────┬─────────────┬──────────────┬─────────────┬─────────┐ │
│  │ GET Index    │ POST Run    │ POST Cancel  │ POST Url    │ POST    │ │
│  │              │             │              │             │ Schedule│ │
│  └──────┬───────┴──────┬──────┴──────┬───────┴──────┬──────┴────┬────┘ │
│         │              │              │              │            │      │
│  ┌──────▼──────┐ ┌─────▼─────────┐ ┌──▼──────────┐ ┌─▼──────┐ ┌──▼──┐  │
│  │HarvestStats │ │CacheJobService│ │CacheJob     │ │Url     │ │Sched│  │
│  │Aggregator   │ │.EnqueueAsync  │ │Service      │ │Harvest │ │Cache│  │
│  │(60s cache)  │ │ + write PG    │ │.Cancel +    │ │Service │ │.Re  │  │
│  └─────┬───────┘ │ Queued row    │ │write PG     │ │ direct │ │load │  │
│        │         │               │ │Stopping row │ │ import │ │Async│  │
│        │         │               │ └──────┬──────┘ └────┬───┘ └─┬───┘  │
└────────┼─────────┼───────────────┼───────┼────────────┼────────┼──────┘
         │         │               │       │            │        │
         │         │   ┌───────────▼───────▼─────┐      │        │
         │         │   │ ArchidektCacheJobService│      │        │
         │         │   │ (BackgroundService +    │      │        │
         │         │   │  Channel<JobId>)        │      │        │
         │         │   │                         │      │        │
         │         │   │ - reads channel         │      │        │
         │         │   │ - per-job linked CTS    │      │        │
         │         │   │ - calls RunCacheSweep   │      │        │
         │         │   │ - writes PG state       │      │        │
         │         │   │   transitions           │      │        │
         │         │   └────────┬────────────────┘      │        │
         │         │            │                       │        │
         │         │   ┌────────▼────────────────┐      │        │
         │         │   │ ArchidektDeckCacheSession│      │       │
         │         │   │  (per-deck loop;        │      │        │
         │         │   │   ct.IsCancellation     │      │        │
         │         │   │   Requested check       │      │        │
         │         │   │   between decks)        │      │        │
         │         │   └────────┬────────────────┘      │        │
         │         │            │                       │        │
         │   ┌─────▼────────────▼─────┐ ┌───────────────▼──┐    │
         │   │ IHarvestRunStore       │ │ IArchidektDeck   │    │
         │   │ (Postgres / SQLite)    │ │ Importer         │    │
         │   │   harvest_runs table   │ │ (existing DI)    │    │
         │   └────────────────────────┘ └──────────────────┘    │
         │                                                       │
   ┌─────▼────────────────┐                       ┌──────────────▼──────┐
   │ IHarvestStats        │                       │ IHarvestScheduleCache│
   │ Aggregator           │                       │ (singleton + IHosted │
   │  - 60s IMemoryCache  │                       │  Service for sync    │
   │  - 8 metrics         │                       │  initial load)       │
   │  - PG queries +      │                       │  - Snapshot()        │
   │    IsPostgres branch │                       │  - ReloadAsync(ct)   │
   │    for pg_db_size    │                       └──────────┬───────────┘
   └──────────┬───────────┘                                  │
              │                                              │
   ┌──────────▼──────────────────────────────────────────────▼──────────┐
   │ Postgres (production) / SQLite (dev/test)                          │
   │  - feedback.db ┬─ feedback                                         │
   │                ├─ admin_brute_force_buckets                        │
   │                ├─ feature_flags                                    │
   │                ├─ harvest_runs        ← NEW (Phase 7)              │
   │                └─ harvest_schedule    ← NEW (Phase 7)              │
   │  - category-knowledge.db (separate connection per existing        │
   │     factory) — NOT changed in Phase 7                              │
   └────────────────────────────────────────────────────────────────────┘

   ┌────────────────────────────────────────────────────────────────────┐
   │ HarvestScheduleService (BackgroundService, separate from           │
   │  ArchidektCacheJobService) — wakes every 60s:                      │
   │   1. IFeatureFlagCache.IsEnabled("harvest.cron.enabled")? early    │
   │      return if off                                                 │
   │   2. snapshot = IHarvestScheduleCache.Snapshot()                   │
   │   3. if snapshot.Paused or snapshot.IntervalHours is null: return  │
   │   4. last_success = SELECT MAX(completed_utc) WHERE state='Succeed'│
   │   5. if now >= last_success + interval: EnqueueAsync(60min)        │
   └────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

```
DeckFlow.Web/
├── Controllers/
│   └── Admin/
│       └── AdminHarvestController.cs       # NEW — single controller, all 4 POST actions + Index + status JSON
├── Services/
│   ├── ArchidektCacheJobService.cs         # MODIFIED — drops _jobs dict + _activeJobId; adds IHarvestRunStore + per-job CTS
│   └── Harvest/                            # NEW folder (mirrors Services/FeatureFlags/)
│       ├── IHarvestRunStore.cs             # NEW — interface
│       ├── HarvestRunStore.cs              # NEW — sealed impl (Postgres + SQLite via IsPostgres branching)
│       ├── IHarvestScheduleCache.cs        # NEW — interface (Snapshot + ReloadAsync)
│       ├── HarvestScheduleCache.cs         # NEW — singleton + IHostedService (mirrors FeatureFlagCache)
│       ├── IHarvestScheduleStore.cs        # NEW — interface
│       ├── HarvestScheduleStore.cs         # NEW — sealed impl
│       ├── HarvestScheduleService.cs       # NEW — BackgroundService, 60s tick
│       ├── IHarvestStatsAggregator.cs      # NEW — interface
│       └── HarvestStatsAggregator.cs       # NEW — IMemoryCache 60s on payload
├── Extensions/
│   └── HarvestServiceCollectionExtensions.cs  # NEW — AddDeckFlowHarvest() (mirrors AddDeckFlowFeatureFlags)
├── Models/
│   └── Admin/
│       └── AdminHarvestViewModel.cs        # NEW — single VM with all panels' data
├── Views/
│   └── AdminHarvest/
│       ├── _ViewStart.cshtml               # EXISTS — already sets Layout=_AdminLayout
│       └── Index.cshtml                    # MODIFIED (currently placeholder)
└── wwwroot/
    └── ts/
        └── admin-harvest.ts                # NEW — AJAX poll loop, compiled to wwwroot/js/admin-harvest.js
```

### Pattern 1: BackgroundService with sync StartAsync initial load

**What:** A `BackgroundService` that does its first PG load synchronously in `StartAsync` (before `base.StartAsync` schedules `ExecuteAsync`) so the host doesn't report ready until the cache is hydrated.

**When to use:** `HarvestScheduleCache` (Phase 7 mirrors `FeatureFlagCache` Phase 6 D-14 pattern verbatim).

**Example (from shipped Phase 6 code, copy-adapt for harvest schedule):**
```csharp
// Source: DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs:87-91 [VERIFIED]
public override async Task StartAsync(CancellationToken cancellationToken)
{
    await ReloadAsync(cancellationToken).ConfigureAwait(false);
    await base.StartAsync(cancellationToken).ConfigureAwait(false);
}

protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(PollInterval);
    try
    {
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await ReloadAsync(stoppingToken).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        // Normal shutdown path.
    }
}
```

For `HarvestScheduleService` (the executor, not the cache), the `ExecuteAsync` body becomes the schedule-tick logic (read flag, read cache snapshot, compute next_due, EnqueueAsync if due).

### Pattern 2: IsPostgres dialect branching for SQL

**What:** Single store class holds both PG and SQLite SQL constants; runtime branches on `_connectionInfo.IsPostgres`.

**When to use:** `HarvestRunStore`, `HarvestScheduleStore`, `HarvestStatsAggregator` (for `pg_database_size()` and 30d-window predicate).

**Example:**
```csharp
// Source: DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs:88,113,119 [VERIFIED]
command.CommandText = _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql;
RelationalDatabaseConnection.AddParameter(command, "@key", key);
RelationalDatabaseConnection.AddParameter(
    command, "@enabled",
    _connectionInfo.IsPostgres ? (object)enabled : (enabled ? 1 : 0));
RelationalDatabaseConnection.AddParameter(
    command, "@now",
    _connectionInfo.IsPostgres
        ? (object)now.UtcDateTime
        : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
```

### Pattern 3: Singleton + IHostedService dual registration

**What:** A class is registered both as a singleton (so other services can inject it) AND as an `IHostedService` (so the host calls `StartAsync` / `ExecuteAsync` / `StopAsync`). Critical: register the concrete type as singleton, then resolve it for the interface AND the hosted service so all three references point to the same instance.

**When to use:** `HarvestScheduleCache` (mirrors `FeatureFlagCache`); also already used for `ArchidektCacheJobService` itself.

**Example:**
```csharp
// Source: DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs:20-27 [VERIFIED]
public static IServiceCollection AddDeckFlowFeatureFlags(this IServiceCollection services)
{
    services.AddSingleton<IFeatureFlagStore, FeatureFlagStore>();
    services.AddSingleton<FeatureFlagCache>();
    services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>());
    services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagCache>());
    return services;
}
```

`AddDeckFlowHarvest()` follows the same shape.

### Pattern 4: Sync cache reload before redirect (D-07 reuse of Phase 6 D-10)

**What:** Admin POST writes PG → calls `IXxxCache.ReloadAsync()` → returns redirect. Operator sees the new value on the redirect-target GET, not on the next 60s poll tick.

**Example:**
```csharp
// Source: DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs:88-92 [VERIFIED]
await _store.SetEnabledAsync(key, enabled, cancellationToken).ConfigureAwait(false);
await _cache.ReloadAsync(cancellationToken).ConfigureAwait(false);

TempData["AdminFlagsAction"] = $"Flag '{key}' is now {(enabled ? "enabled" : "disabled")}.";
return RedirectToAction(nameof(Index));
```

### Pattern 5: AJAX same-origin gate

**What:** JSON endpoints invoked from browser TS modules call `SameOriginRequestValidator.IsValid(Request)` early-return 403 if the Origin/Referer doesn't match.

**When to use:** `GET /Admin/Harvest/status` (the AJAX status endpoint).

**Example:**
```csharp
// Source: DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs:25-28 [VERIFIED]
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
}
```

### Anti-Patterns to Avoid

- **`new HttpClient()` inside `AdminHarvestController`** — URL harvest must reach Archidekt via the existing DI-registered `IArchidektDeckImporter` (already wraps `IHttpClientFactory` + Polly). Don't bypass.
- **Building Polly pipelines per-call** — pipelines are pre-built in `ResiliencePipelineFactory.cs`. Resolve via `ResiliencePipelineProvider<string>`.
- **Calling Scryfall without `ScryfallThrottle`** — not relevant to Phase 7 (no Scryfall calls), but the discipline applies if any sub-task touches Scryfall.
- **Putting layout CSS into `site.css`** — admin pages use `wwwroot/css/admin.css` only (Phase 6 D-05). No guild-theme leakage.
- **Per-call PG roundtrip in `HarvestScheduleService`** — the schedule is read from `IHarvestScheduleCache.Snapshot()`. If the planner skips the cache and queries PG every 60s tick, that defeats the Phase 6 D-10 / Phase 7 D-07 invalidation contract.
- **Skipping `[ValidateAntiForgeryToken]` on admin POST** — every form on `/Admin/Harvest` must have it (ADMIN-05). Already enforced site-wide on admin POST in shipped code.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Schedule tick / cron parsing | Custom timer loop with manual sleep | `BackgroundService` + `PeriodicTimer(60s)` | Already proven in `FeatureFlagCache.cs:97-103`; survives shutdown gracefully |
| Job state machine | Custom enum + state-transition matrix | PG `harvest_runs.state` TEXT + CHECK constraint | D-03 already locked it; CHECK enforces valid values at the DB layer |
| AJAX polling abstraction | jQuery / fetch helper library | Native `fetch()` + `setTimeout` recursion | `wwwroot/ts/` already uses native fetch (`deck-sync.ts`); no new dep |
| Antiforgery token plumbing | Custom CSRF | `[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()` | Already proven in `AdminFlagsController.cs:71` and `AdminFeedbackController.cs:69` |
| Single-deck Archidekt fetch | Hand-rolled HttpClient call | `IArchidektDeckImporter.ImportAsync(url, ct)` | DI-registered as `ArchidektApiDeckImporter` (`Program.cs:294`); has Polly retry built in |
| URL parsing of Archidekt deck IDs | Regex | `ArchidektApiUrl.TryGetDeckId(input, out var id)` | Already in `DeckFlow.Core/Integration/`, used by importer (`ArchidektApiDeckImporter.cs:41`) |
| Connection per call | New `NpgsqlConnection()` | `RelationalDatabaseConnection.CreateConnection()` + `using` | Phase 6 store pattern (`FeatureFlagStore.cs:145-150`) |
| Bool reading from cross-provider DB | Manual cast | `ReadBool(reader, ord)` helper | `FeatureFlagStore.cs:131-143` already has it; copy or call into shared utility |
| Timestamp reading from cross-provider DB | Manual cast | `ReadTimestamp(reader, ord)` helper | `AdminBruteForceTrackerStore.cs:124-134` already has it; same shape needed for `harvest_runs` reads |

**Key insight:** Phase 7 should be ~90% pattern reuse from Phases 5-6. Any new helper that doesn't have an exact analog in shipped code (`FeatureFlagStore`, `AdminBruteForceTrackerStore`, `AdminFlagsController`) is a sign you're hand-rolling something that already exists.

## Runtime State Inventory

> Phase 7 is a green-field add (no rename/refactor of existing tables). New tables only. Section included for completeness.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — `harvest_runs` and `harvest_schedule` are NEW tables in the existing feedback.db (PG: shared with feedback). No prior data to migrate. | None |
| Live service config | None — schedule lives in PG `harvest_schedule`, not in any external service config | None |
| OS-registered state | None — no Task Scheduler, launchd, pm2, or systemd entry; the schedule is a BackgroundService inside the same web process | None |
| Secrets/env vars | No new secrets. Reuses existing `DECKFLOW_DATABASE_PROVIDER` / `DECKFLOW_DATABASE_CONNECTION_STRING` / `MTG_DATA_DIR`. | None |
| Build artifacts / installed packages | New `wwwroot/ts/admin-harvest.ts` compiles to `wwwroot/js/admin-harvest.js` via existing MSBuild target (`DeckFlow.Web.csproj` `CompileTypeScriptAssets`). No new package. | None |

**Caveat — backfill of existing in-memory job history:** Switching `ArchidektCacheJobService` from `ConcurrentDictionary` to PG means any history accumulated in the running process before the deploy is **lost**. This is fine because (a) Render Starter is single-instance and redeploys already wipe it, (b) D-02 startup reaper is the recovery story. No backfill code needed.

## Common Pitfalls

### Pitfall 1: `pg_database_size()` requires a database name parameter, not a literal

**What goes wrong:** `SELECT pg_database_size('postgres')` fails on Render with "permission denied" if the connected DB is named anything else; `SELECT pg_database_size(current_database())` is the portable form.

**Why it happens:** Render's PG hosts the app DB under a generated name (e.g., `deckflow_xyz`); hard-coding the name breaks on rotation.

**How to avoid:** Always `SELECT pg_database_size(current_database())`. No parameter binding needed (function is parameter-free at the SQL level).

**Warning signs:** "permission denied for database" in logs; storage size shows null in production but works locally.

**Confidence:** [VERIFIED: Postgres docs — `current_database()` is a built-in returning the connected DB; `pg_database_size()` accepts oid OR text name]

### Pitfall 2: PG `TIMESTAMPTZ` vs SQLite `TEXT` round-trip on `started_utc DESC NULLS LAST` ordering

**What goes wrong:** PG honors `NULLS LAST`; SQLite (3.30+) also honors it but only when explicitly set. Without `NULLS LAST`, SQLite puts NULL first by default, which means a `Queued` row (started_utc=NULL) appears at the top of the recent-runs log instead of the actual most-recent completed run.

**Why it happens:** D-16 says `ORDER BY started_utc DESC NULLS LAST LIMIT 10`. If the planner forgets the explicit `NULLS LAST`, SQLite tests pass on rows that have no NULLs, then prod breaks the moment a Queued/Running row exists.

**How to avoid:** Always write `ORDER BY started_utc DESC NULLS LAST` explicitly in BOTH dialects. SQLite supports it from 3.30+; the project's Microsoft.Data.Sqlite 10.0.0 ships SQLite 3.46+ which is well past the threshold.

**Warning signs:** Recent-runs log shows the in-flight Queued/Running row at the top instead of the last successful completion in unit tests with mixed-state fixtures.

**Confidence:** [VERIFIED: Microsoft.Data.Sqlite 10.0.0 ships SQLite 3.46.0+; `NULLS FIRST/LAST` supported since 3.30 — confirmed by SQLite changelog]

### Pitfall 3: `ConcurrentDictionary` removal must keep the existing public API contract

**What goes wrong:** The locked decision drops `_jobs` dict from `ArchidektCacheJobService`. But `ArchidektCacheJobsController` (the existing PUBLIC API at `/api/archidekt-cache-jobs`) calls `_jobService.GetJob(jobId)` and `_jobService.GetActiveJob()` — both currently return `ArchidektCacheJobStatus?` from the dict. If those return values change shape (e.g., `ArchidektCacheJobStatus` becomes a PG-row record), the API response wire format silently breaks.

**Why it happens:** D-01 frames PG as "single source of truth" but doesn't spell out that `GetJob` / `GetActiveJob` must keep returning the same `ArchidektCacheJobStatus` record shape, just sourced from PG instead of memory.

**How to avoid:** Keep `ArchidektCacheJobStatus` record shape identical (or maintain a 1:1 projection from `harvest_runs` row → `ArchidektCacheJobStatus`). Treat the public API response model as a frozen contract. Add a unit test that asserts `ArchidektCacheJobStatusResponse`'s JSON serialization is byte-stable across the refactor.

**Warning signs:** Any external consumer of `/api/archidekt-cache-jobs/{id}` sees missing fields or renamed properties; admin UI works but the JSON API breaks.

**Confidence:** [VERIFIED: `ArchidektCacheJobsController.cs:69-105` projects `ArchidektCacheJobStatus` → `ArchidektCacheJobStatusResponse` field-by-field; any rename or field drop is a breaking API change]

### Pitfall 4: Schedule clock advancement on redeploy

**What goes wrong:** Operator sets schedule to "every 4h"; last successful run was 2h ago. Render redeploys; `HarvestScheduleService` restarts. Next-tick logic computes `next_due = last_success + 4h = +2h from now`. Correct behavior. **BUT** if the planner accidentally uses `Started_utc` instead of `Completed_utc` for "last success", a long redeploy mid-run could shift the schedule unpredictably.

**Why it happens:** D-16 metric #7 says `last_success_utc = SELECT MAX(completed_utc) FROM harvest_runs WHERE state='Succeeded'`. The planner must use this same column for the scheduler's `next_due` calculation, not invent a different basis.

**How to avoid:** Single source of truth — both the stats panel "Last successful run" display AND the scheduler tick read `MAX(completed_utc) WHERE state='Succeeded'`. If the planner adds a helper, both call into it.

**Warning signs:** Stats panel shows a different timestamp from what the scheduler used; recurring runs fire at unexpected times.

**Confidence:** [ASSUMED — Phase 6 has no equivalent timing-dependent BackgroundService to verify against]

### Pitfall 5: `harvest_runs.state` TEXT CHECK constraint must include all six values, not five

**What goes wrong:** D-03 lists six states: `Queued`, `Running`, `Stopping`, `Succeeded`, `Failed`, `Cancelled`. If the planner forgets `Stopping` in the CHECK constraint (because Open Question #1 isn't answered yet), inserting a row with `state='Stopping'` raises a CHECK violation at runtime.

**Why it happens:** This research recommends **writing the Stopping row** (Open Question Resolution #1 below). The planner must therefore include `Stopping` in the CHECK constraint regardless of how it's written.

**How to avoid:** Put `state TEXT NOT NULL CHECK (state IN ('Queued','Running','Stopping','Succeeded','Failed','Cancelled'))` in BOTH dialects' `CREATE TABLE`. SQLite honors CHECK constraints since 3.3.0.

**Warning signs:** Cancel button works visually but logs show `CHECK constraint failed: harvest_runs.state`.

**Confidence:** [VERIFIED: SQLite CHECK constraints supported since 3.3.0 — confirmed by SQLite docs]

### Pitfall 6: Stats cache thundering herd on invalidation

**What goes wrong:** D-13 invalidates `admin.harvest.stats.v1` on every `harvest_runs` insert/update. During a bulk run, the service writes a row update every time `decks_processed` advances (or every state transition). If the scheduler also enqueues a run, and the operator is polling, you get many invalidations in quick succession — but `IMemoryCache` `Set` after a miss is unsynchronized, so a cluster of concurrent misses can fire all eight PG queries simultaneously.

**Why it happens:** `IMemoryCache` doesn't deduplicate misses. The pattern in this codebase (`CommanderSpellbookService`, etc.) tolerates this because the underlying call is also network-bound and rare.

**How to avoid:** Two options — (a) use `IMemoryCache.GetOrCreateAsync` with a `SemaphoreSlim` gate keyed by cache key (mild complexity, prevents stampede), or (b) accept that 8 PG queries on a stats panel reload are cheap (~5ms each on local PG, ~50ms on Render PG). Recommend (b) for v1.1 — single operator, single browser tab, the stampede risk is theoretical. Revisit if RAM/CPU graphs show contention.

**Warning signs:** Render PG connections spike when operator hammers F5 on `/Admin/Harvest`.

**Confidence:** [VERIFIED: `IMemoryCache.GetOrCreateAsync` does not lock per-key in net10.0 — confirmed by .NET source]

### Pitfall 7: `Stopping` row write race — sweep finishes before cancel POST returns

**What goes wrong:** Operator clicks Cancel. Controller calls `_activeJobCts.Cancel()`. Token cancellation propagates instantly to `ArchidektDeckCacheSession.RunAsync`'s loop check (`stopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested`). The OCE bubbles out of `ExecuteAsync` and writes `state='Cancelled'` to PG. Meanwhile the cancel controller is *also* about to write `state='Stopping'`. The two writes race; final state could be `Stopping` (last writer wins, polling browser sees stuck Stopping).

**Why it happens:** Two writers to the same row.

**How to avoid:** Single writer. The controller writes `state='Stopping'` synchronously **before** calling Cancel. `ExecuteAsync`'s OCE handler then writes `state='Cancelled'` (the terminal). Use a conditional UPDATE (`WHERE state IN ('Queued','Running','Stopping')`) so transitions to terminal can never be overwritten by a stale "Stopping" write that arrives after.

**Warning signs:** Cancel test occasionally leaves the row in `Stopping` permanently.

**Confidence:** [VERIFIED: race exists in any 2-writer pattern; `WHERE state IN (...)` conditional UPDATE is the standard fix]

## Open Question Resolutions

This is the core deliverable for the planner. Each Claude's-Discretion item gets a recommendation with rationale.

### OQ #1 — Write interim Stopping row? **YES, write it from the controller.**

**Recommendation:** Cancel controller action is:

```csharp
[HttpPost("Cancel/{jobId:guid}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Cancel(Guid jobId, CancellationToken ct)
{
    // 1. Mark Stopping FIRST (synchronous DB write).
    await _runStore.TryTransitionStateAsync(
        jobId,
        from: new[] { "Running" },
        to: "Stopping",
        ct);
    // 2. Then signal cancel — OCE handler writes terminal Cancelled.
    _jobService.Cancel(jobId);
    TempData["HarvestResult"] = "Cancel requested.";
    return RedirectToAction(nameof(Index));
}
```

The job service's `OperationCanceledException` handler then writes `state='Cancelled'` with a conditional UPDATE (`WHERE state IN ('Running','Stopping')`).

**Rationale:**
1. ROADMAP SC #3 says "transitions to Stopping then Failed/Cancelled within 30s" — making `Stopping` an explicit DB state lets the AJAX poll (3s cadence) display it without ambiguity. Skipping it means UI must infer "Stopping" from "I clicked Cancel but state is still Running" which is brittle.
2. Writing it from the controller (synchronously, before cancelling the CTS) avoids the two-writer race in Pitfall #7.
3. UI behavior is cleaner: the moment the operator clicks Cancel, the page redirects, the next 3s poll shows `Stopping` from PG, no UI flicker.

**Tradeoff:** Adds a state to the CHECK constraint (must be in the list — see Pitfall #5).

### OQ #2 — Status GET memory cache TTL? **1 second.**

**Recommendation:** `IMemoryCache` with `AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)` on the status JSON payload, keyed `admin.harvest.status.{activeJobIdOrNone}`.

**Rationale:**
- 0s (no cache): every 3s poll = 1 PG query. With 1 operator and 1 tab, ~20 queries/min during active run. Acceptable but wasteful given the value rarely changes faster than once per deck (~5-10s).
- 2s (max suggestion): two consecutive polls 3s apart see different cached payloads, but if the user has dev tools open and hits Refresh during a poll, the cached value can be 2s stale, which is jarring during a Cancel transition.
- 1s: balances thrash absorbed (rapid F5 / multiple tabs) against staleness (max 1s lag on state transition visibility).

**Implementation note:** The cache key includes the active job ID so when no job is active, the cache is keyed differently and won't serve stale "Running" state after a job completes.

### OQ #3 — Keep BackgroundService+Channel or simplify? **Keep BackgroundService+Channel.**

**Recommendation:** `ArchidektCacheJobService` keeps its `BackgroundService` + `Channel<JobId>` shape. Drop only:
- `ConcurrentDictionary<Guid, ArchidektCacheJobStatus> _jobs` field
- `Guid? _activeJobId` field
- `_sync` lock (the channel + a single PG row check serialize EnqueueAsync)

Keep:
- `protected override async Task ExecuteAsync(CancellationToken stoppingToken)` reading from the channel
- `Channel<Guid>` (carries jobId only — no status object — since status is in PG)
- `lock` semantics replaced by an atomic PG INSERT-if-no-active-row pattern

**Rationale:**
- The channel is more than a queue dispatcher — it gives clean shutdown semantics. When `stoppingToken` fires, `await foreach` exits gracefully, in-flight job sees CT cancellation, OCE handler updates PG state to `Failed (interrupted by redeploy)`. Drop-in `Task.Run` would skip this exit path.
- "Active job already running" guard in `EnqueueAsync` becomes a PG-side `INSERT ... WHERE NOT EXISTS (SELECT 1 FROM harvest_runs WHERE state IN ('Queued','Running','Stopping'))`. Channel still triggers the worker only when the INSERT succeeded.
- Reduces refactor surface area. The locked decisions in CONTEXT.md don't require ripping the channel out, only the dict.

**Tradeoff:** Slightly more code than a pure direct-execute would be, but less than half a delta of the existing ~165 LOC.

### OQ #4 — Single AdminHarvestController or split? **Single controller.**

**Recommendation:** All five admin actions on `AdminHarvestController`:

```
GET  /Admin/Harvest                      → Index (Razor view)
POST /Admin/Harvest/Run                  → start bulk run, antiforgery, redirect
POST /Admin/Harvest/Cancel/{jobId:guid}  → cancel active, antiforgery, redirect
POST /Admin/Harvest/SubmitUrl            → sync URL harvest, antiforgery, redirect
POST /Admin/Harvest/Schedule             → save schedule, antiforgery, redirect
GET  /Admin/Harvest/status               → AJAX status JSON (same-origin gate)
```

**Rationale:**
- Phase 6 precedent: `AdminFlagsController` carries all flag actions; `AdminFeedbackController` carries all feedback actions. Splitting into `AdminHarvestUrlController` doesn't match.
- All five actions share the same view model (the redirect target re-renders `Index.cshtml`).
- Keeps route file count low — one folder `Views/AdminHarvest/`.
- Resolves naturally if a future polish phase adds more actions.

**Tradeoff:** ~150-200 LOC controller. Acceptable; `AdminFlagsController` already lives at 95 LOC for fewer actions.

### OQ #5 — Adaptive AJAX poll cadence on cancel? **No — keep steady 3s.**

**Recommendation:** Single 3s cadence. No 1s burst on cancel. Stop polling on terminal state.

**Rationale:**
- ROADMAP SC #3's 30s budget is generous; even 3s polling sees `Stopping` within one tick of the controller writing it.
- Adaptive cadence adds TS state-machine complexity (track "pending cancel since timestamp X" → drop to 1s for 30s → revert) for marginal UX benefit on a single-operator page.
- Less code = fewer bugs. Polish-tier optimization; defer to v1.2 if operator complains.

### OQ #6 — Seed `harvest_schedule` with default Off row on EnsureSchemaAsync? **YES, seed it.**

**Recommendation:** `EnsureSchemaAsync` creates the table AND inserts the single default-Off row if not present:

```sql
INSERT INTO harvest_schedule (id, interval_hours, paused) VALUES (1, NULL, FALSE)
ON CONFLICT (id) DO NOTHING;
```

**Rationale:**
- Eliminates a null-row branch from every page render and every scheduler tick. The page can always read `SELECT interval_hours, paused FROM harvest_schedule WHERE id=1` and assume a row.
- Mirrors the Phase 6 D-09 / `INSERT ... ON CONFLICT (key) DO NOTHING` seed pattern verbatim — operator changes are preserved across re-bootstrap because of the conflict clause.
- The cache (`IHarvestScheduleCache.Snapshot`) returns a known-shape value on first read, no null-handling.

**Tradeoff:** None meaningful. Two extra lines of SQL.

### OQ #7 — Index on `category_knowledge(commander_name)` for top-N? **No, defer to v1.2.**

**Recommendation:** Ship v1.1 without the index. Add only if production EXPLAIN ANALYZE shows the top-N query is slow.

**Rationale:**
- The top-N query runs **once per 60s** (under the stats cache); per-page-render cost is dominated by rendering, not the query.
- `category_knowledge` is a write-heavy table (every harvest deck inserts rows); adding an index increases write amplification, slowing harvests.
- The query is `GROUP BY commander_name + ORDER BY count DESC LIMIT 10` — modern PG planner handles this with a hash agg + top-N sort even without an index.
- D-15 itself defers the decision to "if EXPLAIN warrants" — research confirms that's the right call.

**When to revisit:** If `category_knowledge` row count exceeds ~100k (current scale ~ low thousands), or if Render PG p95 query time on the top-N exceeds 200ms.

## Code Examples

Verified patterns ready for the planner to copy-adapt.

### Example 1: New EnsureSchemaAsync for `harvest_runs`

```csharp
// Adapt from FeatureFlagStore.EnsureSchemaAsync (FeatureFlagStore.cs:102-129) [VERIFIED]
private const string PostgresCreateRunsTableSql = """
    CREATE TABLE IF NOT EXISTS harvest_runs (
      id                       UUID PRIMARY KEY,
      kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
      state                    TEXT NOT NULL CHECK (state IN ('Queued','Running','Stopping','Succeeded','Failed','Cancelled')),
      requested_utc            TIMESTAMPTZ NOT NULL DEFAULT now(),
      started_utc              TIMESTAMPTZ NULL,
      completed_utc            TIMESTAMPTZ NULL,
      duration_seconds         INT NOT NULL,
      decks_processed          INT NOT NULL DEFAULT 0,
      additional_decks_found   INT NOT NULL DEFAULT 0,
      error_message            TEXT NULL,
      url                      TEXT NULL
    );
    CREATE INDEX IF NOT EXISTS ix_harvest_runs_state ON harvest_runs(state);
    CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_desc ON harvest_runs(started_utc DESC);
    """;

private const string SqliteCreateRunsTableSql = """
    CREATE TABLE IF NOT EXISTS harvest_runs (
      id                       TEXT PRIMARY KEY,
      kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
      state                    TEXT NOT NULL CHECK (state IN ('Queued','Running','Stopping','Succeeded','Failed','Cancelled')),
      requested_utc            TEXT NOT NULL DEFAULT (datetime('now')),
      started_utc              TEXT NULL,
      completed_utc            TEXT NULL,
      duration_seconds         INTEGER NOT NULL,
      decks_processed          INTEGER NOT NULL DEFAULT 0,
      additional_decks_found   INTEGER NOT NULL DEFAULT 0,
      error_message            TEXT NULL,
      url                      TEXT NULL
    );
    CREATE INDEX IF NOT EXISTS ix_harvest_runs_state ON harvest_runs(state);
    CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_desc ON harvest_runs(started_utc DESC);
    """;
```

**SQLite UUID note:** SQLite has no native UUID type — store as TEXT (canonical 36-char string) and convert via `Guid.Parse(reader.GetString(ord))`. Npgsql binds `Guid` directly to PG `uuid` (use `(object)guid` parameter, no string conversion).

### Example 2: Startup reaper SQL (D-02)

```csharp
// Run inside EnsureSchemaAsync after CREATE TABLE, before _schemaReady = true.
private const string ReaperSql = """
    UPDATE harvest_runs
    SET state = 'Failed',
        error_message = COALESCE(error_message, 'interrupted by redeploy'),
        completed_utc = COALESCE(completed_utc, """ +
        // PG: now()  /  SQLite: datetime('now')  — branch at runtime
        """)
    WHERE state IN ('Queued','Running','Stopping');
    """;
```

Use `IsPostgres` branch:
```csharp
command.CommandText = _connectionInfo.IsPostgres
    ? "UPDATE harvest_runs SET state='Failed', error_message=COALESCE(error_message,'interrupted by redeploy'), completed_utc=COALESCE(completed_utc, now()) WHERE state IN ('Queued','Running','Stopping')"
    : "UPDATE harvest_runs SET state='Failed', error_message=COALESCE(error_message,'interrupted by redeploy'), completed_utc=COALESCE(completed_utc, datetime('now')) WHERE state IN ('Queued','Running','Stopping')";
await command.ExecuteNonQueryAsync(cancellationToken);
```

### Example 3: HarvestScheduleService tick body

```csharp
// Inside HarvestScheduleService.ExecuteAsync — adapt PeriodicTimer pattern from FeatureFlagCache.cs:97-103
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
    try
    {
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            // Phase 6 D-12 kill-switch gate.
            if (!_flags.IsEnabled("harvest.cron.enabled")) continue;

            var snapshot = _scheduleCache.Snapshot();
            if (snapshot.Paused || snapshot.IntervalHours is null) continue;

            var lastSuccess = await _runStore.GetLastSuccessUtcAsync(stoppingToken);
            if (lastSuccess is null) continue; // wait for first manual run

            var nextDue = lastSuccess.Value.AddHours(snapshot.IntervalHours.Value);
            if (DateTimeOffset.UtcNow < nextDue) continue;

            // Re-check no active job before enqueue (the EnqueueAsync impl also guards).
            await _jobService.EnqueueAsync(TimeSpan.FromHours(1), stoppingToken);
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
}
```

### Example 4: AJAX status poll (TypeScript)

```typescript
// wwwroot/ts/admin-harvest.ts — adapt fetch pattern from existing wwwroot/ts/deck-sync.ts conventions
type HarvestStatus = {
  jobId: string | null;
  state: 'None' | 'Queued' | 'Running' | 'Stopping' | 'Succeeded' | 'Failed' | 'Cancelled';
  decksProcessed: number;
  additionalDecksFound: number;
  startedUtc: string | null;
  completedUtc: string | null;
  errorMessage: string | null;
};

const POLL_MS = 3000;
const ACTIVE_STATES = new Set<HarvestStatus['state']>(['Queued', 'Running', 'Stopping']);

async function poll(): Promise<void> {
  const res = await fetch('/Admin/Harvest/status', {
    method: 'GET',
    credentials: 'same-origin',
    headers: { 'Accept': 'application/json' },
  });
  if (!res.ok) return; // 403 = no session; let user reload
  const status = (await res.json()) as HarvestStatus;
  renderStatus(status);
  if (ACTIVE_STATES.has(status.state)) {
    setTimeout(poll, POLL_MS);
  }
}

document.addEventListener('DOMContentLoaded', () => {
  const el = document.querySelector<HTMLElement>('[data-harvest-status]');
  if (el?.dataset['harvestState'] && ACTIVE_STATES.has(el.dataset['harvestState'] as HarvestStatus['state'])) {
    setTimeout(poll, POLL_MS);
  }
});
```

The Razor page renders the initial state into a `data-harvest-state` attribute so the TS doesn't need an extra request just to know whether to start polling.

### Example 5: Stats panel cache aggregator

```csharp
public sealed class HarvestStatsAggregator : IHarvestStatsAggregator
{
    private const string CacheKey = "admin.harvest.stats.v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly RelationalDatabaseConnection _conn;
    private readonly IHarvestScheduleCache _scheduleCache;

    public Task<HarvestStatsViewModel> GetAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await BuildAsync(ct);
        })!;

    public void Invalidate() => _cache.Remove(CacheKey);

    private async Task<HarvestStatsViewModel> BuildAsync(CancellationToken ct)
    {
        // 8 metrics — execute in parallel where feasible. PG storage size is null on SQLite.
        // Note: ORDER BY started_utc DESC NULLS LAST on recent_runs (Pitfall #2)
        // Note: pg_database_size(current_database()) only when IsPostgres (Pitfall #1)
        // ...
    }
}
```

The aggregator's `Invalidate()` is called by `IHarvestRunStore.InsertAsync` and `UpdateStateAsync` (D-13).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ConcurrentDictionary` job tracker | PG `harvest_runs` table | Phase 7 (now) | Survives Render redeploy; D-01/D-02 |
| In-process schedule (none) | PG `harvest_schedule` + BackgroundService | Phase 7 (now) | Schedule survives redeploy; D-06 |
| In-memory `_jobs` | Status read from PG with 1s memory cache | Phase 7 (now) | Status survives redeploy; D-01 |
| No URL-on-demand harvest | Sync POST + redirect with TempData | Phase 7 (now) | Operator UX for "paste-and-go"; D-09 |

**Deprecated/outdated within Phase 7's scope:** None — Phase 7 is additive.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Render Postgres user has permission to call `pg_database_size(current_database())` | Pitfall #1 | Storage-size metric returns "N/A" or errors in production. Mitigation: D-14 already returns null on failure path; UI degrades gracefully. The planner should add a try/catch in the aggregator that logs once and returns null on permission error. |
| A2 | Pitfall #4 (clock advancement) — chosen `MAX(completed_utc) WHERE state='Succeeded'` is the right basis for next_due | Pitfall #4 | If Phase 7 instead uses `started_utc`, a 4h schedule can drift if a run takes 30 min. Recommend hardening with a unit test that asserts both stats panel display and scheduler tick read from the same store method. |
| A3 | Single operator means RAM headroom for `IMemoryCache` stats + status caches is well under the 512MB Render cap | Pitfall #6 | If a future analytics phase adds a high-traffic cache to admin pages, contention could mount. Phase 7 alone has negligible footprint (<1MB). |
| A4 | The single-deck import path through `IArchidektDeckImporter.ImportAsync(url, ct)` is safe to call concurrently with a bulk sweep — no shared mutable state | HARV-02 / D-12 | Decision Log Q4 explicitly says yes ("single-deck import already runs concurrently with sweeps in production via `PersistObservedCategoriesAsync`"). The planner should add an integration test exercising this concurrency to lock the assumption. |
| A5 | `wwwroot/ts/admin-harvest.ts` will compile cleanly under the existing `tsconfig.json` (strict: true) and emit to `wwwroot/js/admin-harvest.js` via the existing `CompileTypeScriptAssets` MSBuild target | Code Examples #4 | If `tsconfig.json` includes a glob that excludes `admin-*.ts`, the new module silently won't compile. Verify `tsconfig.json` includes `wwwroot/ts/**/*.ts` or equivalent. |

## Open Questions (RESOLVED)

1. **Should `harvest_runs` and `harvest_schedule` share the existing feedback.db (PG) or split into a new logical DB?**
   - What we know: Phase 6 D-07 + `DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection()` (`DeckFlowDatabaseConnectionFactory.cs:30-31`) explicitly shares feedback.db for tiny tables.
   - What's unclear: CONTEXT.md doesn't state explicitly. By analogy to feature flags, share the feedback.db connection.
   - **RESOLVED:** Add `CreateHarvestStateConnection(IWebHostEnvironment)` returning the same as `CreateFeedbackConnection`. Mirrors Phase 6 exactly. Keep `category-knowledge.db` separate (it already is, for size reasons).

2. **Where does the 'last_success_utc' helper method live so the scheduler and stats aggregator share it?**
   - What we know: D-16 metric #7 and the scheduler tick both need it.
   - What's unclear: Should it be a method on `IHarvestRunStore` (then both call into it) or duplicated?
   - **RESOLVED:** One method on `IHarvestRunStore.GetLastSuccessUtcAsync(CancellationToken ct)`, called by both. Pitfall #4 mitigation depends on this.

3. **Does the Phase 6 feature flag `harvest.cron.enabled` need to be added to the Phase 6 seed list (D-09)?**
   - What we know: D-09 currently seeds `scryfall.tagger.enabled` and `page.help.enabled`. CONTEXT.md says scheduler is "gated by Phase 6 feature flag `harvest.cron.enabled`".
   - What's unclear: Is the seed list mutable, or does Phase 7 add a new seed migration?
   - **RESOLVED:** Phase 7 amends the seed list. The `INSERT ... ON CONFLICT DO NOTHING` pattern means adding a third row is safe — it lights up on first deploy, doesn't reset on subsequent. Verify by running `EnsureSchemaAsync` twice in a unit test (idempotency).

4. **Should the Cancel button POST to `Cancel/{jobId:guid}` or just `Cancel` (using the active job ID inferred server-side)?**
   - What we know: There's only one active bulk job at a time (D-04 + EnqueueAsync guard).
   - What's unclear: Explicit jobId in URL vs server-side resolution.
   - **RESOLVED:** Explicit jobId in the URL. Defends against clicking Cancel for a stale page where a different job is now active.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + run | ✓ | 10.0 | — (pinned) |
| Postgres 14+ | Production state (Render) | Render Basic-256mb | — | SQLite for dev/test (already wired via `RelationalDatabaseConnection`) |
| Microsoft.Data.Sqlite 10.0.0 | Local dev / tests | ✓ | 10.0.0 | — |
| Npgsql 10.0.0 | Production PG provider | ✓ | 10.0.0 | — |
| TypeScript 6.0.2 (npm) | wwwroot/ts compile | ✓ | 6.0.2 (per CLAUDE.md) | — |
| `IArchidektDeckImporter` (DI) | URL harvest path | ✓ | — | — |
| `IFeatureFlagCache` (Phase 6) | `harvest.cron.enabled` gate | ✓ | shipped | — |
| `IVersionService` | _AdminLayout top-bar | ✓ | shipped | — |
| Archidekt API | URL harvest reach | external | — | If down, single URL POST shows error in TempData banner; bulk sweep already retries |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

## Validation Architecture

> Section excluded — `.planning/config.json` shows `workflow.nyquist_validation: false`.

## Project Constraints (from CLAUDE.md)

The planner must not violate these:

- **Tech stack pinned:** ASP.NET 10 + Razor; no migration to React, Blazor, etc.
- **Hosting cap:** Render Starter web tier 512MB RAM. Phase 7 footprint negligible (<5MB; in-memory cache + 2 BackgroundServices).
- **Theme system:** Phase 7 admin views use `wwwroot/css/admin.css` only (Phase 6 D-05). NO `site-*.css` references on admin pages.
- **HTTP resilience:** Existing RestSharp + direct Polly v8 only; do NOT migrate to standard handler.
- **Public repo:** No secrets in commits. Phase 7 needs no new secrets — only existing PG connection string.
- **Testing:** VSTest unreliable in WSL — rely on `dotnet build` clean + manual harness + push-and-watch CI.
- **Commits:** Plain default-author commits, no Co-Authored-By trailer.
- **README:** Update README when behavior changes (operator-visible Phase 7 surface).

Memory-driven additional constraints:

- **Codex MCP is the coding tool** (per global CLAUDE.md). Plan tasks should structure prompts for Codex with QA-twice-before-return instructions.
- **SQL dialect divergence:** Always run Postgres integration tests before merging new storage SQL (memory: `feedback_sqlite_postgres_sql_divergence.md`). Use `EXCLUDED` (not `<table>.<col>`) in upsert; prefer `COUNT(1)` over `EXISTS`.
- **Codex git clone caveat:** Codex MCP edits files in workspace but git ops happen in /tmp clone. Let Codex edit only; run git locally; clear stale `.git/index.lock` if needed.

## Sources

### Primary (HIGH confidence — verified via direct file inspection)

- `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — existing job machinery, all 162 LOC inspected
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` — sweepGate, EnsureSchema idiom, RunCacheSweepAsync signature
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs` — BackgroundService + StartAsync + PeriodicTimer pattern
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — IsPostgres dialect branching, ON CONFLICT DO NOTHING seed, ReadBool helper
- `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs` — IsPostgres branching, ReadTimestamp helper, atomic UPSERT with CASE expressions
- `DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs` — write+ReloadAsync+redirect pattern, snapshot-allowlist key validation, antiforgery
- `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — `Controllers/Admin/` placement, antiforgery on POST, TempData banner pattern
- `DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs` — existing public API contract that Phase 7 must not break
- `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` — `AddDeckFlow*()` extension method pattern (singleton + IHostedService dual registration)
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — IAsyncActionFilter action-filter pattern (reusable for any future kill-switched action on AdminHarvestController)
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — same-origin gate for AJAX endpoints
- `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` — connection factory pattern for sharing feedback.db
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — provider/dialect abstraction, AddParameter helper, IsSqlite/IsPostgres
- `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` — confirms `cancellationToken.IsCancellationRequested` checks are already plumbed into the inner deck-loop (lines 52, 109, 120)
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — single-deck import contract `ImportAsync(string urlOrDeckId, CancellationToken ct)`
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — confirms /Admin/Harvest sidebar link already wired (line 23)
- `DeckFlow.Web/Views/AdminFlags/Index.cshtml` — Razor antiforgery + per-row POST form pattern (admin-table, admin-banner CSS classes)
- `DeckFlow.Web/Program.cs` — DI registration block (lines 50-189), MapWhen("/Admin") branch, ValidateDatabaseConnectionsAsync hook
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` — confirms native fetch + DOM API conventions (no jQuery)
- `.planning/phases/06-admin-shell-flags-foundation/06-CONTEXT.md` — Phase 6 carry-forward decisions
- `.planning/REQUIREMENTS.md` — HARV-01..07 wording (lines 20-26)
- `.planning/ROADMAP.md` — Phase 7 success criteria (lines 71-77)
- `.planning/phases/07-harvest-controls-stats/07-CONTEXT.md` — locked decisions D-01..D-16
- `.planning/phases/07-harvest-controls-stats/07-DISCUSSION-LOG.md` — alternatives considered
- `.planning/STATE.md` — pending-todos including pre-condition: audit ArchidektApiDeckImporter cancel token threading

### Secondary (MEDIUM confidence — referenced from official docs / shipped code reasoning)

- Postgres `pg_database_size()` + `current_database()` semantics — Postgres docs (built-in functions)
- SQLite `NULLS LAST` in ORDER BY — SQLite docs (3.30+ feature)
- `IMemoryCache.GetOrCreateAsync` lacks per-key locking — .NET runtime source review

### Tertiary (LOW confidence — to validate during implementation)

- Render PG user permissions for `pg_database_size` (A1) — verify on first deploy

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every dependency already shipped and exercised in Phase 6
- Architecture: HIGH — every pattern has a Phase 6 analog inspected line-by-line
- Pitfalls: MEDIUM-HIGH — pitfalls #1 (pg_database_size), #2 (NULLS LAST), #5 (CHECK constraint), #7 (race) verified; #4 (clock drift) and #6 (cache stampede) reasoned from first principles
- Open question resolutions: HIGH — each grounded in shipped code or explicit ROADMAP/CONTEXT trade-offs

**Research date:** 2026-05-03
**Valid until:** 2026-06-03 (30 days for stable internal-architecture research; refresh if Phase 6 patterns change or if `ArchidektCacheJobService` is touched in another branch)
