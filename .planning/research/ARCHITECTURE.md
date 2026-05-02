# Architecture Research

**Domain:** ASP.NET 10 MVC admin console extension (brownfield)
**Researched:** 2026-05-02
**Confidence:** HIGH — all findings derived from reading actual source files

---

## A. Admin Sidebar Layout

### Decision: New `_AdminLayout.cshtml` under `Views/Shared/`

The existing `_ViewStart.cshtml` sets `Layout = "_Layout"` globally. Admin views must opt out of that and opt into a dedicated admin layout. The idiomatic Razor pattern is to override `Layout` at the top of each admin view (or in a folder-scoped `_ViewStart.cshtml`).

**Recommended approach: folder-scoped `_ViewStart.cshtml` + `Views/Shared/_AdminLayout.cshtml`**

```
DeckFlow.Web/Views/
├── AdminFeedback/          ← existing (move to Admin/ in this milestone)
├── Admin/                  ← new folder
│   ├── _ViewStart.cshtml   ← sets Layout = "_AdminLayout"  (new)
│   ├── Index.cshtml        ← landing shell  (new)
│   ├── Harvest.cshtml      (new)
│   ├── Analytics.cshtml    (new)
│   └── Flags.cshtml        (new)
└── Shared/
    ├── _Layout.cshtml      ← unchanged
    ├── _AdminLayout.cshtml ← new; includes _AdminSidebar partial
    └── _AdminSidebar.cshtml ← new sidebar nav partial
```

`Views/Admin/_ViewStart.cshtml` contains only:
```cshtml
@{ Layout = "_AdminLayout"; }
```

This eliminates per-view `Layout =` assignments and ensures every future admin view in the folder automatically uses the admin chrome. No `[Authorize]` drift risk — the layout choice is structural, not security.

**`_AdminLayout.cshtml` structure:**

- Inherits the same `<head>` block as `_Layout.cshtml`: `site-common.css`, theme stylesheet link, `site-mobile.css`
- Replaces the `page-frame` inner structure with a two-column `admin-shell` (sidebar + main)
- Keeps `page-header` (brand + theme picker) and `page-footer` from main layout — copy, do not share via partial (theme files are standalone forks; layout CSS goes in `site-common.css`)
- `_AdminSidebar.cshtml` rendered via `@Html.Partial("_AdminSidebar")` inside `_AdminLayout`
- Sidebar links: Feedback (`/Admin/Feedback`), Harvest (`/Admin/Harvest`), Analytics (`/Admin/Analytics`), Flags (`/Admin/Flags`)
- Active-link highlight: `asp-controller` + `asp-action` tag helpers emit `aria-current="page"` on the matching anchor; CSS targets `[aria-current="page"]` with `--accent-strong`

**What to add to `site-common.css`:** `.admin-shell`, `.admin-sidebar`, `.admin-sidebar__nav`, `.admin-sidebar__link`, `.admin-content` layout rules. Do not put these in any theme file or `site.css`.

**`AdminFeedback` view folder rename:** The existing `Views/AdminFeedback/` folder maps to `AdminFeedbackController` which routes under `Admin/Feedback`. Move the views to `Views/Admin/Feedback/` (or keep as `AdminFeedback/` and add a folder `_ViewStart.cshtml` that sets `Layout = "_AdminLayout"`). The latter (adding `_ViewStart.cshtml` to `Views/AdminFeedback/`) requires zero view renames and zero controller changes — preferred for minimal impact.

---

## B. BasicAuthMiddleware Gate — No Drift Risk

The gate is path-based, not attribute-based:

```csharp
// Program.cs:330-332
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/Admin"),
    branch => branch.UseMiddleware<BasicAuthMiddleware>("DeckFlow Admin"));
```

Any controller routed under `/Admin/*` is automatically covered. New controllers (`HarvestAdminController`, `AnalyticsAdminController`, `FlagsAdminController`) must:

1. Use `[Route("Admin/Harvest")]`, `[Route("Admin/Analytics")]`, `[Route("Admin/Flags")]` — the `/Admin` prefix is what triggers the middleware branch.
2. Never use `[Authorize]` attribute — `BasicAuthMiddleware` is custom middleware, not ASP.NET Core's policy-based auth system. Mixing the two would create confusion with no security benefit.

No change to `BasicAuthMiddleware.cs` or `Program.cs` is needed to cover the four new pages. The existing `UseWhen` branch covers all of them by path prefix.

**Invariants to preserve:**
- `IAdminBruteForceTrackerStore` throttle gate runs first in `BasicAuthMiddleware.InvokeAsync` before any auth parsing — do not restructure the middleware
- `DeriveAdminPartitionKey` reads `CF-Connecting-IP` — do not change partition derivation
- `UseForwardedHeaders()` runs before the admin branch in the pipeline (Program.cs:301) — this ordering must be preserved for any new middleware added before `UseWhen`

---

## C. Harvest Controls — Service Wrapping

### Decision: New `IHarvestAdminService` interface wrapping `IArchidektCacheJobService`

`ArchidektCacheJobService` already exposes `EnqueueAsync(TimeSpan)`, `GetJob(Guid)`, `GetActiveJob()`. The controller should not call these directly for two reasons:

1. The controller needs operations `ArchidektCacheJobService` does not have: cancel active job, pause/resume, cron schedule query/set, harvest stats (total decks, total cards, top commanders, storage size, last/next run). Adding these directly to `ArchidektCacheJobService` bloats the hosted service.
2. The test seam is cleaner on the new interface.

**New interface: `IHarvestAdminService`**

```
DeckFlow.Web/Services/HarvestAdminService.cs
```

```csharp
public interface IHarvestAdminService
{
    Task<HarvestAdminStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<HarvestEnqueueResult> RunNowAsync(TimeSpan duration, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(CancellationToken cancellationToken = default);
    Task<HarvestStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<HarvestSchedule?> GetScheduleAsync(CancellationToken cancellationToken = default);
    Task SetScheduleAsync(HarvestSchedule schedule, CancellationToken cancellationToken = default);
}
```

`HarvestAdminService` implementation:
- Constructor injects `IArchidektCacheJobService`, `ICategoryKnowledgeStore`, `IHarvestRunStore` (new — see section F)
- `RunNowAsync` delegates to `IArchidektCacheJobService.EnqueueAsync`
- `CancelAsync` — see cancel design below
- `GetStatsAsync` queries `ICategoryKnowledgeStore` for deck/card counts and queries `IHarvestRunStore` for run history
- `GetScheduleAsync` / `SetScheduleAsync` read/write the `harvest_schedules` table (see section G)

**Cancel mechanism:** `ArchidektCacheJobService` uses `Channel<ArchidektCacheJobStatus>` + `CancellationToken stoppingToken` (host shutdown only). There is no per-job cancellation today. To add cancel:

- Add `CancelCurrentJobAsync()` to `IArchidektCacheJobService` (minimal interface extension)
- Implementation: store a `CancellationTokenSource _jobCts` in `ArchidektCacheJobService`, linked to `stoppingToken` via `CancellationTokenSource.CreateLinkedTokenSource`. On `CancelCurrentJobAsync`, call `_jobCts.Cancel()` and replace `_jobCts` with a fresh linked source for the next job
- `ExecuteAsync` passes `_jobCts.Token` to `RunCacheSweepAsync` instead of `stoppingToken` directly
- `IHarvestAdminService.CancelAsync` delegates to `IArchidektCacheJobService.CancelCurrentJobAsync()`

**Pause/resume:** `ArchidektCacheJobService.RunCacheSweepAsync` is implemented inside `ICategoryKnowledgeStore`. Pause requires a cooperative checkpoint inside the sweep loop. Defer to its own sub-task — expose `PauseAsync`/`ResumeAsync` on `IArchidektCacheJobService` as no-ops initially and implement in the cron/jobs milestone. The controller renders a disabled "Pause" button when job is not running.

**CancellationTokenSource never exposed to the controller or view.** `IHarvestAdminService` returns `bool` from `CancelAsync` (true = was running and cancel signal sent; false = nothing active).

**DI registration:**

```csharp
// Program.cs — add after existing ArchidektCacheJobService registrations
builder.Services.AddSingleton<IHarvestAdminService, HarvestAdminService>();
```

Singleton because it wraps singletons (`IArchidektCacheJobService`, `ICategoryKnowledgeStore`).

**New controller:**

```
DeckFlow.Web/Controllers/Admin/HarvestAdminController.cs
```

```csharp
[Route("Admin/Harvest")]
public sealed class HarvestAdminController : Controller
{
    private readonly IHarvestAdminService _harvest;
    // ...
}
```

---

## D. Analytics Middleware

### Decision: Custom `RequestMetricsMiddleware` + write-behind in-memory buffer flushed on background timer

**Position in pipeline:** Between `UseSerilogRequestLogging()` and `UseAuthorization()` — after routing resolves (so endpoint metadata is available) but before controllers execute. Crucially, it runs after `UseForwardedHeaders()` so the IP read is correct, and it runs before and after `UseWhen(Admin branch)` — the path-based `UseWhen` does not affect this middleware since it sits on the main pipeline, not inside the branch.

```
UseForwardedHeaders()          ← scheme/IP resolved
UseExceptionHandler / HSTS
UseDeckFlowSecurityHeaders()
UseHttpsRedirection()
UseStaticFiles()
UseRouting()                   ← endpoint selected, route values available
UseSerilogRequestLogging()
[RequestMetricsMiddleware]     ← NEW: reads RouteData after routing
UseAuthorization()
UseRateLimiter()
UseWhen(/Admin → BasicAuth)
MapControllers()
```

Static files are excluded by `UseStaticFiles()` running before routing — static file responses short-circuit and never reach `RequestMetricsMiddleware`.

**Latency impact mitigation — write-behind buffer:**

```csharp
public sealed class RequestMetricsMiddleware
{
    // Bounded channel: if buffer is full, drop (never block request path)
    private static readonly Channel<MetricEvent> _buffer =
        Channel.CreateBounded<MetricEvent>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context);
        // After response: fire-and-forget enqueue — never awaited on hot path
        var route = context.GetRouteData();
        var routeKey = ExtractRouteKey(route, context.Request.Path);
        var ipHash = HashIp(context, _salt);
        _ = _buffer.Writer.TryWrite(new MetricEvent(routeKey, ipHash, context.Response.StatusCode, DateTimeOffset.UtcNow));
    }
}
```

A singleton `RequestMetricsFlushService : BackgroundService` drains the channel and batch-INSERTs to `request_metrics`. Batch size: 50 events or 5-second timer, whichever fires first.

**What NOT to do:** Direct `await INSERT` inside `InvokeAsync` on every request — that adds a DB round-trip to every page load. Even with connection pooling this would be visible at p95 on Render's shared Postgres.

**IP privacy:** Use `DeriveCloudflareClientIp` from `Program` (already internal/static) to get the raw IP, then SHA-256 hash it with a stable salt (reuse `FEEDBACK_IP_SALT` env var or add a dedicated `METRICS_IP_SALT`) — consistent with `FeedbackStore`'s `HashIpInternal` pattern.

**Route key normalization:** Use `context.GetEndpoint()?.DisplayName` or extract `controller`+`action` from `RouteData.Values` to produce stable keys like `Deck/ChatGptPackets` rather than raw path strings that vary by query parameter.

**Admin routes:** `/Admin/*` routes are included in metrics (useful: see harvest job trigger volume). The `RequestMetricsMiddleware` runs before the `BasicAuth` branch; admin-path metrics are counted regardless of auth outcome. This is correct — failed auth attempts are analytically interesting.

**New files:**

```
DeckFlow.Web/Infrastructure/RequestMetricsMiddleware.cs   ← new
DeckFlow.Web/Services/RequestMetricsFlushService.cs       ← new (BackgroundService)
DeckFlow.Web/Services/IRequestMetricsStore.cs             ← new interface + impl
```

**DI registration:**

```csharp
// Program.cs
builder.Services.AddSingleton<IRequestMetricsStore, RequestMetricsStore>();
builder.Services.AddHostedService<RequestMetricsFlushService>();
// middleware registered inline: app.UseMiddleware<RequestMetricsMiddleware>()
```

---

## E. Feature Flags

### Decision: Periodic poll (30s) with singleton `IFeatureFlagCache` + explicit invalidation on admin write

**Ruled out:**
- `IOptionsMonitor` — designed for config files / environment reload, not DB-backed runtime mutation
- Postgres `LISTEN/NOTIFY` — requires a persistent open connection; incompatible with Npgsql connection-pool lifecycle on Render's Basic-256mb tier; adds connection pressure for low-value use case (single operator, rare flag changes)

**Design:**

```csharp
public interface IFeatureFlagCache
{
    bool IsEnabled(string flagKey, bool defaultValue = true);
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}

public sealed class FeatureFlagCache : IFeatureFlagCache
{
    private volatile IReadOnlyDictionary<string, bool> _flags = new Dictionary<string, bool>();
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private readonly IFeatureFlagStore _store;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    // ...
}
```

`IsEnabled` is synchronous and reads the in-memory dict — zero I/O on the hot request path. Background `FeatureFlagRefreshService : BackgroundService` wakes every 30 seconds and calls `_store.LoadAllAsync()` to refresh `_flags`. On admin write (`FlagsAdminController` POSTs), `IFeatureFlagCache.InvalidateAsync()` is called directly — sets `_loadedAt = DateTimeOffset.MinValue` which forces the next refresh cycle to re-query immediately rather than waiting up to 30s.

**`IFeatureFlagStore` and `FeatureFlagStore`:**

```
DeckFlow.Web/Services/FeatureFlagStore.cs   ← new; same IRelationalDialect pattern as FeedbackStore
```

Schema uses both dialects (see section F). Store methods: `LoadAllAsync`, `SetAsync(key, enabled)`, `ListAsync`.

**Singleton lifetime:** `IFeatureFlagCache` is singleton — flag dict is shared across all requests, single in-memory copy. `IFeatureFlagStore` is singleton (consistent with `FeedbackStore`, `AdminBruteForceTrackerStore`).

**Usage pattern in services/controllers:**

```csharp
// Inject IFeatureFlagCache; call synchronously
if (!_flags.IsEnabled("tagger-enabled", defaultValue: true))
{
    return Array.Empty<string>();
}
```

No async, no DB hit per request.

**DI registration:**

```csharp
builder.Services.AddSingleton<IFeatureFlagStore, FeatureFlagStore>();
builder.Services.AddSingleton<IFeatureFlagCache, FeatureFlagCache>();
builder.Services.AddHostedService<FeatureFlagRefreshService>();
```

**New controller:**

```
DeckFlow.Web/Controllers/Admin/FlagsAdminController.cs
```

---

## F. New Postgres Tables — Schema and Migration

### Decision: Continue `EnsureSchemaAsync` pattern; no migration framework

FluentMigrator (or EF migrations) would require a migration runner at startup, a migrations assembly, and a `__EFMigrationsHistory` / `schemaversions` table. For four tables added in one milestone on a solo-operated app this overhead is not justified. The `EnsureSchemaAsync` + `CREATE TABLE IF NOT EXISTS` pattern is established and working.

**Dual-dialect SQL requirement (must not break SQLite parity):**

Each new store follows `AdminBruteForceTrackerStore`'s pattern: two `const string` SQL blocks (`Postgres*` / `Sqlite*`) selected via `_connectionInfo.IsPostgres`. Timestamp columns: `TIMESTAMPTZ` (Postgres) / `TEXT` (SQLite). Identity columns: `BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY` (Postgres) / `INTEGER PRIMARY KEY AUTOINCREMENT` (SQLite).

**Naming convention** (existing tables: `feedback`, `feedback_meta`, `category_knowledge`, `observations`, `admin_brute_force_buckets`): snake_case, singular or short plural, prefixed by domain.

**New tables:**

### `harvest_runs`
Records each completed/failed harvest job. Written by `HarvestAdminService` after `ArchidektCacheJobService` completes.

```sql
-- Postgres
CREATE TABLE IF NOT EXISTS harvest_runs (
  id               BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
  job_id           TEXT        NOT NULL,
  started_utc      TIMESTAMPTZ NOT NULL,
  completed_utc    TIMESTAMPTZ,
  state            TEXT        NOT NULL,   -- Succeeded | Failed | Cancelled
  decks_processed  INT         NOT NULL DEFAULT 0,
  decks_added      INT         NOT NULL DEFAULT 0,
  error_message    TEXT,
  duration_seconds INT         NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_harvest_runs_started ON harvest_runs(started_utc DESC);
```

SQLite version: `INTEGER PRIMARY KEY AUTOINCREMENT`, `TEXT` for timestamps.

Owner store: `IHarvestRunStore` / `HarvestRunStore` in `DeckFlow.Web/Services/HarvestRunStore.cs`.

### `feature_flags`
```sql
-- Postgres
CREATE TABLE IF NOT EXISTS feature_flags (
  flag_key        TEXT PRIMARY KEY,
  enabled         BOOLEAN     NOT NULL DEFAULT TRUE,
  description     TEXT,
  updated_utc     TIMESTAMPTZ NOT NULL
);
```

SQLite: `INTEGER` for `enabled` (0/1), `TEXT` for timestamps.

Owner store: `IFeatureFlagStore` / `FeatureFlagStore`.

### `request_metrics`
```sql
-- Postgres
CREATE TABLE IF NOT EXISTS request_metrics (
  id              BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
  route_key       TEXT        NOT NULL,
  recorded_day    DATE        NOT NULL,    -- truncated to day for aggregation
  ip_hash         TEXT,
  status_code     INT         NOT NULL,
  recorded_utc    TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_request_metrics_route_day ON request_metrics(route_key, recorded_day DESC);
```

SQLite: `TEXT` for `recorded_day` (ISO date string), `TEXT` for timestamps.

Note: `recorded_day` duplicates data from `recorded_utc` but exists for fast group-by-day aggregation without casting. This is the same tradeoff `FeedbackStore` makes with `created_utc`.

### `harvest_schedules`
See section G.

**Connection routing:**
All four new tables share the same `RelationalDatabaseConnection` as `FeedbackStore` (single Postgres DB on Render). `DeckFlowDatabaseConnectionFactory` gains:

```csharp
public static RelationalDatabaseConnection CreateAdminConnection(IWebHostEnvironment environment)
    => CreateFeedbackConnection(environment);   // same DB, separate logical tables
```

This keeps the Render `DECKFLOW_DATABASE_CONNECTION_STRING` env var pointing to one DB, consistent with current `CreateAdminThrottleConnection` routing.

**`ValidateDatabaseConnectionsAsync` (Program.cs:421):** Add `IHarvestRunStore`, `IFeatureFlagStore`, `IRequestMetricsStore` validation calls alongside existing feedback/knowledge store validation. Each store's `EnsureSchemaAsync` idempotently creates tables on first call.

---

## G. Cron Schedule Storage

### Decision: `harvest_schedules` table (not a feature_flags row)

A feature_flags row stores a boolean. A schedule stores a cron expression, a next-run timestamp, an enabled flag, and a max-duration cap — a distinct record type. Jamming it into `feature_flags` as a serialized blob breaks the typed interface and couples two unrelated concerns.

```sql
-- Postgres
CREATE TABLE IF NOT EXISTS harvest_schedules (
  id              INT         PRIMARY KEY DEFAULT 1,   -- single-row table
  cron_expression TEXT,                                -- NULL = disabled
  duration_cap_seconds INT    NOT NULL DEFAULT 3600,
  enabled         BOOLEAN     NOT NULL DEFAULT FALSE,
  next_run_utc    TIMESTAMPTZ,
  updated_utc     TIMESTAMPTZ NOT NULL
);
```

Single-row table (id=1 always). `cron_expression` nullable — NULL means no schedule configured. `IHarvestScheduleStore` (part of `HarvestRunStore.cs` or its own file) provides `GetAsync`/`SetAsync`.

**Survival across Render redeployments:** Stored in Postgres, not in-process memory. The `FeatureFlagRefreshService` / harvest scheduler reads from DB on startup. Process restarts are safe.

**Cron execution:** A new `HarvestSchedulerService : BackgroundService` wakes on a 60-second tick, reads `harvest_schedules`, evaluates whether `next_run_utc <= UtcNow`, and calls `IHarvestAdminService.RunNowAsync`. On each trigger it updates `next_run_utc` to the next occurrence using a minimal cron parser (no external library — parse only the five standard fields needed for daily/weekly/hourly patterns). For v1.1 scope, support only `0 * * * *` (hourly), `0 H * * *` (daily at hour H), `0 H * * D` (weekly). Full cron expression support is out of scope.

**DI:**

```csharp
builder.Services.AddSingleton<IHarvestRunStore, HarvestRunStore>();
builder.Services.AddHostedService<HarvestSchedulerService>();
```

---

## H. Component Dependencies and Build Order

```
Phase 1: Admin Shell (layout, sidebar, landing page, AdminFeedback re-skin)
    No new services. No new DB tables.
    Deliverables:
    - Views/Shared/_AdminLayout.cshtml  (new)
    - Views/Shared/_AdminSidebar.cshtml (new)
    - Views/Admin/_ViewStart.cshtml     (new)
    - Views/Admin/Index.cshtml          (new — landing shell)
    - Views/AdminFeedback/_ViewStart.cshtml (new — opts into _AdminLayout)
    - site-common.css additions: .admin-shell, .admin-sidebar, .admin-content
    TouchedFiles: _Layout.cshtml (no change), Program.cs (no change)

Phase 2: Harvest Controls
    Depends on: Phase 1 (admin shell renders harvest page)
    New DB tables: harvest_runs, harvest_schedules
    New files:
    - DeckFlow.Core/Storage/IRelationalDialect.cs — ADD harvest table SQL properties
      (or keep SQL inline in stores; keeping inline matches AdminBruteForceTrackerStore pattern — prefer inline)
    - DeckFlow.Web/Services/HarvestRunStore.cs          (new IHarvestRunStore)
    - DeckFlow.Web/Services/HarvestAdminService.cs      (new IHarvestAdminService)
    - DeckFlow.Web/Services/HarvestSchedulerService.cs  (new BackgroundService)
    - DeckFlow.Web/Controllers/Admin/HarvestAdminController.cs (new)
    - DeckFlow.Web/Views/Admin/Harvest.cshtml           (new)
    Modified files:
    - DeckFlow.Web/Services/ArchidektCacheJobService.cs — add CancelCurrentJobAsync + per-job CTS
    - DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs — add CreateAdminConnection
    - DeckFlow.Web/Program.cs — register IHarvestRunStore, IHarvestAdminService, HarvestSchedulerService; add ValidateDatabaseConnectionsAsync call

Phase 3: Feature Flags
    Depends on: Phase 1 (admin shell). Independent of Phase 2.
    New DB tables: feature_flags
    New files:
    - DeckFlow.Web/Services/FeatureFlagStore.cs         (new IFeatureFlagStore)
    - DeckFlow.Web/Services/FeatureFlagCache.cs         (new IFeatureFlagCache + FeatureFlagRefreshService)
    - DeckFlow.Web/Controllers/Admin/FlagsAdminController.cs (new)
    - DeckFlow.Web/Views/Admin/Flags.cshtml             (new)
    Modified files:
    - DeckFlow.Web/Program.cs — register flag services
    Usage wiring (ScryfallTaggerService, etc.) can proceed in parallel once IFeatureFlagCache is registered

Phase 4: Analytics
    Depends on: Phase 1 (admin shell). Independent of Phases 2 and 3.
    New DB tables: request_metrics
    New files:
    - DeckFlow.Web/Infrastructure/RequestMetricsMiddleware.cs (new)
    - DeckFlow.Web/Services/RequestMetricsFlushService.cs     (new BackgroundService)
    - DeckFlow.Web/Services/RequestMetricsStore.cs            (new IRequestMetricsStore)
    - DeckFlow.Web/Controllers/Admin/AnalyticsAdminController.cs (new)
    - DeckFlow.Web/Views/Admin/Analytics.cshtml               (new)
    Modified files:
    - DeckFlow.Web/Program.cs — UseMiddleware<RequestMetricsMiddleware>() after UseSerilogRequestLogging(), register store + BackgroundService
```

**Ordering rationale:**

- Phase 1 first — every admin page depends on the layout shell; building Harvest or Flags without a shell means throwaway scaffolding
- Phase 2 (Harvest) before Phase 3 (Flags) and Phase 4 (Analytics) — Harvest is the most complex (BackgroundService interaction, per-job CTS, schedule storage, multi-step controller) and benefits from being done while context is fresh; Flags and Analytics are more self-contained
- Phases 3 and 4 are independent of each other and can be parallelized across execution plans if desired
- Schema creation (`EnsureSchemaAsync`) is in each store; tables are created lazily on first access, so no explicit migration step is needed between phases

---

## System Overview — v1.1 Admin Request Path

```
Browser /Admin/*
    │
    ▼
UseForwardedHeaders()          [Program.cs:301]
    │
UseRouting()
    │
UseSerilogRequestLogging()
    │
RequestMetricsMiddleware       [NEW — Phase 4]
    │
UseAuthorization()
    │
UseRateLimiter()
    │
UseWhen(/Admin → BasicAuth)    [Program.cs:330-332 — unchanged]
    │  IAdminBruteForceTrackerStore.IsThrottledAsync()
    │  credential check
    │
MapControllers()
    │
    ├── Admin/Index             AdminController (new — Phase 1)
    ├── Admin/Feedback          AdminFeedbackController (existing — re-skinned Phase 1)
    ├── Admin/Harvest           HarvestAdminController (new — Phase 2)
    │       └── IHarvestAdminService
    │               └── IArchidektCacheJobService (singleton BackgroundService)
    │               └── IHarvestRunStore → harvest_runs, harvest_schedules tables
    ├── Admin/Analytics         AnalyticsAdminController (new — Phase 4)
    │       └── IRequestMetricsStore → request_metrics table
    └── Admin/Flags             FlagsAdminController (new — Phase 3)
            └── IFeatureFlagStore → feature_flags table
            └── IFeatureFlagCache.InvalidateAsync()

Background services (singleton hosted):
    ArchidektCacheJobService    [existing — gains per-job CTS in Phase 2]
    HarvestSchedulerService     [new — Phase 2]
    FeatureFlagRefreshService   [new — Phase 3; 30s poll]
    RequestMetricsFlushService  [new — Phase 4; channel drain + batch INSERT]
```

---

## Integration Points — Existing Classes Touched

| Class | File | Change | Phase |
|-------|------|--------|-------|
| `ArchidektCacheJobService` | `Services/ArchidektCacheJobService.cs` | Add `CancelCurrentJobAsync()` to interface + implementation; add per-job `CancellationTokenSource` | 2 |
| `IArchidektCacheJobService` | same file | Add `CancelCurrentJobAsync()` | 2 |
| `DeckFlowDatabaseConnectionFactory` | `Services/DeckFlowDatabaseConnectionFactory.cs` | Add `CreateAdminConnection()` method | 2 |
| `Program.cs` | `Program.cs` | Register new services (phases 2-4); add `UseMiddleware<RequestMetricsMiddleware>()` after `UseSerilogRequestLogging()` (phase 4); add new stores to `ValidateDatabaseConnectionsAsync` | 2-4 |
| `_ViewStart.cshtml` | `Views/_ViewStart.cshtml` | No change — admin folder has its own `_ViewStart` | — |
| `site-common.css` | `wwwroot/css/site-common.css` | Add admin layout CSS tokens | 1 |

---

## Anti-Patterns

### Using `[Authorize]` on new admin controllers

**What it does:** Adds ASP.NET Core policy-based auth on top of the custom `BasicAuthMiddleware`.
**Why wrong:** `BasicAuthMiddleware` is not integrated with `IAuthenticationScheme`. Adding `[Authorize]` without wiring a scheme causes 302 redirects to a non-existent login page. The `UseWhen` path-prefix gate is the sole and sufficient enforcement point.
**Do this instead:** No `[Authorize]` on any controller under `Controllers/Admin/`. Gate is the `UseWhen` branch only.

### Calling `IArchidektCacheJobService.EnqueueAsync` directly from the controller

**What it does:** Exposes `TimeSpan duration` parameter and job management directly to the controller.
**Why wrong:** Controller gains knowledge of job lifetime semantics; cancel/schedule/stats logic has no home; test seam is on the hosted service directly.
**Do this instead:** `IHarvestAdminService` is the controller's only dependency for harvest operations.

### Inline synchronous Postgres query inside `RequestMetricsMiddleware.InvokeAsync`

**What it does:** `await INSERT INTO request_metrics ...` on every request.
**Why wrong:** Adds measurable p95 latency; connection pool contention under bursty load; Render Basic-256mb Postgres has limited concurrent connections.
**Do this instead:** Fire-and-forget enqueue to a bounded `Channel<MetricEvent>`; `RequestMetricsFlushService` drains and batch-inserts asynchronously.

### Storing schedule as a feature_flags row

**What it does:** Saves `harvest_schedule` as a JSON blob in `feature_flags.description` or similar.
**Why wrong:** Conflates boolean kill-switches with structured scheduling data; breaks typed `IFeatureFlagStore` interface; schema change required to add duration cap or next-run timestamp.
**Do this instead:** Dedicated `harvest_schedules` single-row table with typed columns.

### Putting admin layout CSS into `site.css` or any guild theme file

**What it does:** Admin sidebar styles in `site.css`.
**Why wrong:** Guild themes are full standalone forks; `site.css` is overridden per theme; admin layout would break under 24 of 25 themes.
**Do this instead:** All new admin layout selectors go in `site-common.css` only.

---

*Architecture research for: DeckFlow v1.1 Admin Console (brownfield ASP.NET 10 MVC)*
*Researched: 2026-05-02*
