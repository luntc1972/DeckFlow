# Phase 8: Analytics - Research

**Researched:** 2026-05-03
**Domain:** ASP.NET Core 10 request-pipeline middleware, write-behind buffer (Channels), Postgres bulk UPSERT, server-side SVG sparklines
**Confidence:** HIGH

## Summary

Phase 8 ships a per-request analytics middleware that buffers `(route_key, day_utc, status_class, ip_hash)` tuples through a bounded `Channel<T>` and flushes them via a single `BackgroundService` into a Postgres `request_metrics` + `request_metric_ip_seen` pair. The admin page at `/Admin/Analytics` renders a top-routes table with inline SVG sparklines — no JS chart library. Every architectural choice is locked in CONTEXT.md (D-01..D-18); research below is strictly the HOW.

The codebase already has every pattern needed: `EnsureSchemaAsync` (FeatureFlagStore.cs), singleton-and-hosted-service dual registration (HarvestServiceCollectionExtensions.cs), BackgroundService loops with per-tick try/catch and `OperationCanceledException` handling (HarvestScheduleService.cs), `IServiceProvider` lazy resolution to break ctor cycles (HarvestRunStore.cs post-dc66a38), SHA-256 IP hashing with shared `FEEDBACK_IP_SALT` (FeedbackStore.cs), and the admin shell + neutral CSS + per-folder `_ViewStart` for `Views/AdminAnalytics/` (Phase 6 plan 01). The single new mechanism is the `System.Threading.Channels` write-behind buffer.

**Primary recommendation:** Mirror HarvestScheduleCache's BackgroundService shape for `RequestMetricsFlusher`; mirror FeatureFlagStore's `EnsureSchemaAsync` for `RequestMetricsStore`; extract `IpHasher` as a shared static helper in `DeckFlow.Web/Security/` (FeedbackStore consumes it too); place the analytics middleware as an inline lambda registered between `app.UseRouting()` and `app.UseAuthorization()` so `HttpContext.GetEndpoint()` is populated; render sparklines as Razor `@helper`-style C# loops emitting `<svg><rect>` per day.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01:** Per-(route_key, day_utc, status_class) UPSERT pattern. `INSERT ... ON CONFLICT (route_key, day_utc, status_class) DO UPDATE SET hit_count = hit_count + 1, error_count = error_count + EXCLUDED.error_count` per request. Compact (~thousands of rows/day max for a single-Render-instance app); top-routes query is a simple SUM/GROUP BY over a small table.

**D-02:** Forever retention; no cleanup BackgroundService in this phase. With per-(route, day, status_class) aggregation, table growth is bounded by route cardinality × 3 status classes × days. Cleanup deferred to v1.2 (track as ANLY-NEXT-01).

**D-03:** Unique-IP tracking via a side table `request_metric_ip_seen(route_key, day_utc, ip_hash)` PRIMARY KEY. Middleware does INSERT ... ON CONFLICT DO NOTHING per request; admin page joins via `SELECT route_key, day_utc, COUNT(*) FROM request_metric_ip_seen GROUP BY ...` to derive unique-IP counts. Simpler than HyperLogLog; no PG extension needed.

**D-04:** `status_class` is a smallint column with values 2, 4, 5 (representing 2xx/4xx/5xx). 3xx (redirects) collapse to 2 (treated as success). 1xx informational responses are not recorded.

**D-05:** `HttpContext.GetEndpoint()?.DisplayName` is the source. Yields strings like `DeckFlow.Web.Controllers.DeckController.Index (DeckFlow.Web)` for conventional-routed actions and the route template for attribute-routed endpoints. Match what Serilog request logs already emit.

**D-06:** Fallback `__unmatched__` for routes with no Endpoint (404s for unknown URLs, requests served by static files / favicon-misses that aren't already filtered by D-08).

**D-07:** 5xx + 4xx (excluding 404) count as errors for the error-rate column. 401/403/400/429 represent auth failures, abuse, validation noise, and rate-limit hits. 404 specifically excluded (extension probes / link-rot, not actionable). Computed at middleware time as `is_error = status >= 400 && status != 404 && status < 600`.

**D-08:** `Channel<RequestMetricEvent>` with capacity 10000, `FullMode=DropOldest`. On full, oldest unflushed records get dropped (newest preferred). Suits SC #5 (no p95 regression).

**D-09:** BackgroundService flusher: trigger on whichever fires first — 100 records buffered OR 5 seconds elapsed since last flush. Flush opens one transaction, batches all queued events into a single round-trip with parameter-array UPSERT. After flush, resets the elapsed timer.

**D-10:** Drop accounting via Serilog WARN every ~60 seconds (not per-drop). Flusher tracks dropped_total and emits one structured log line per minute when dropped_total > 0.

**D-11:** Filter at middleware top — before Endpoint resolution — by checking `request.Path.StartsWithSegments("/css")`, `"/js"`, `"/lib"`, `"/extensions"`, OR `"/favicon.ico"`. Plus reject `request.Path == "/_health"` if present. Returns immediately without buffering.

**D-12:** Place AFTER `app.UseRouting()` and BEFORE `app.UseEndpoints()`/`app.MapControllers()` so `HttpContext.GetEndpoint()` is populated. Phase 7.1 invariant on `UseForwardedHeaders` ordering preserved (analytics runs after, doesn't alter, that middleware).

**D-13:** Hash `CF-Connecting-IP` if present, else `X-Forwarded-For` first hop, else `request.HttpContext.Connection.RemoteIpAddress`. Same `FEEDBACK_IP_SALT` env var as `FeedbackStore`. Reuse the existing `IpHasher` helper if it exists; otherwise extract one in this phase.

**D-14:** `services.AddDeckFlowAnalytics()` extension method in `DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs`. Registers `IRequestMetricsStore` (singleton, takes `IServiceProvider` per Phase 7.1 lesson — NOT taking the buffer/flusher in ctor), `RequestMetricsBuffer` (singleton — wraps Channel), `RequestMetricsFlusher` (BackgroundService — calls `IRequestMetricsStore.UpsertBatchAsync`), and `IpHasher` if extracted.

**D-15:** Container-startup smoke-test required before merge. Phase 7.1 errata established that `dotnet build` clean ≠ DI graph clean. Run `dotnet run --project DeckFlow.Web` locally and confirm Kestrel reaches "Application started" without `InvalidOperationException`.

**D-16:** `/Admin/analytics` controller class: `AdminAnalyticsController` under `Controllers/Admin/` (BasicAuth-gated by existing middleware). Single `Index(string range = "7d")` action; range parameter persists via query string. Allowed values: today, 7d, 30d, all.

**D-17:** Top-routes table sorted by hit_count descending. Columns: route_key, hits, unique_ips, error_rate (formatted "X.X%"), sparkline. Top 50 rows shown; no pagination.

**D-18:** Sparkline shape: bar chart, 14 daily bars, omit empty days (gap rendered for days with zero traffic). Width budget per row: ~120px. Color: `var(--admin-fg-muted)`. Y-axis: linear, max=row's max-day hit count. No labels, no axes.

### Claude's Discretion

- Exact UPSERT SQL syntax (Postgres `INSERT ... ON CONFLICT (...) DO UPDATE` is locked; column order, naming, indexes are planner's call)
- Exact SVG path/rect generation algorithm (D-18 sets the contract; planner picks the rendering approach)
- BackgroundService cancellation semantics on app shutdown (graceful drain of buffer vs immediate stop) — planner's call; lean toward 2-second drain ceiling on `StopAsync`
- Whether `IpHasher` is extracted as a shared helper or stays per-store (depends on what FeedbackStore currently does — minor refactor, in-scope)
- Whether the admin page renders server-side fully or hydrates a small TS bit (server-side preferred — no SPA framework, matches Phase 6/7 admin-page convention)

### Deferred Ideas (OUT OF SCOPE)

- ANLY-NEXT-01: Retention/cleanup BackgroundService (revisit when row count > ~100k)
- ANLY-NEXT-02: Per-IP session drill-down (PII concern)
- ANLY-NEXT-03: p95/p99 latency tracking (Render dashboard already shows it)
- ANLY-NEXT-04: Referer breakdown
- ANLY-NEXT-05: Outbound-API error-rate summary
- ANLY-NEXT-06: Free-form date-range picker
- ANLY-NEXT-07: Redis / external metrics store
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ANLY-01 | Per-request middleware records (route template, day, count, unique-IP, error-rate) into a Postgres `request_metrics` table, using route template (not raw path) | D-01 schema + D-05 endpoint extraction; existing pattern: `HttpContext.GetEndpoint()?.DisplayName` (verified .NET 10 docs); existing FeatureFlagStore.cs `EnsureSchemaAsync` template for Postgres `CREATE TABLE IF NOT EXISTS` + indexes |
| ANLY-02 | Middleware uses a write-behind buffer (bounded `Channel` + `BackgroundService` flusher) so hot-path requests do not pay synchronous DB I/O latency | D-08/D-09 buffer policy; existing pattern: HarvestScheduleService.cs (60s `PeriodicTimer` BackgroundService loop with per-tick try/catch and `OperationCanceledException` handling) — flusher mirrors with whichever-fires-first batch+timer trigger |
| ANLY-03 | Unique-IP count uses hashed CF-Connecting-IP (existing `FEEDBACK_IP_SALT`) so no raw IPs are stored | D-13 IP capture priority (CF-Connecting-IP → X-Forwarded-For → Connection.RemoteIpAddress); existing FeedbackStore.cs `HashIpInternal` (SHA256(ip + "\|" + salt) → hex) is the reference implementation; salt resolution falls back to `feedback_meta` table if env var absent |
| ANLY-04 | `/Admin/analytics` lists top routes by hit count for a chosen time window (today / 7d / 30d / all-time) | D-16/D-17 controller + view; existing pattern: AdminHarvestController.cs (BasicAuth-gated via Program.cs branch, `[Route("Admin/...")]` + `[HttpGet("")]` Index, ViewModel with `AllowedXxx` SortedSet); per-folder `_ViewStart` for `Views/AdminAnalytics/` already exists from Phase 6 plan 01 |
| ANLY-05 | Each route row shows a daily sparkline rendered as inline SVG (no JS charting library, no external dependency) plus error-rate column | D-18 sparkline contract; pure server-side Razor C# loop emitting `<svg><rect>` per day. Approach detailed in "Inline SVG Sparkline" section below |
| ANLY-06 | Static-asset routes (`/css/*`, `/js/*`, `/lib/*`, `/extensions/*`) are excluded from `request_metrics` | D-11 path-prefix filter at middleware top, before Endpoint resolution; saves the Endpoint lookup cost AND keeps the table compact |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Per-request data capture | API/Backend (middleware) | — | Owns the request lifecycle; only the server-side pipeline can see `Endpoint.DisplayName`, `HttpResponse.StatusCode`, and CF-Connecting-IP simultaneously |
| Write-behind buffering | API/Backend (in-process Channel) | — | Per CONTEXT scope: single-Render-instance, intra-process. Redis/external buffer explicitly deferred (ANLY-NEXT-07) |
| Batch flush to Postgres | API/Backend (BackgroundService) | Database (Postgres UPSERT) | BackgroundService owns the cadence + transaction boundary; Postgres owns the atomicity of the (route, day, status) aggregation row |
| IP hashing | API/Backend (shared helper) | — | Must run server-side because CF-Connecting-IP only exists on inbound headers; salt is server-only (`FEEDBACK_IP_SALT`) |
| Top-routes query | Database (Postgres SUM/GROUP BY) | API/Backend (controller) | Aggregation is cheap on small per-(route, day, status) table; controller composes the query and shapes the view model |
| Sparkline rendering | API/Backend (server-side Razor SVG) | Browser (SVG render only) | D-18 explicitly forbids JS chart library; server has the per-day data already and emits final markup |
| Range filter (today/7d/30d/all) | API/Backend (controller query string) | — | Query-string-driven, no cookie/session state per D-16 |
| Static-asset exclusion | API/Backend (middleware path prefix check) | — | Pre-Endpoint short-circuit per D-11; the middleware itself is the only place that has both the path AND the buffer reference |

## Standard Stack

### Core (already in csproj)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Threading.Channels` | built-in (.NET 10 BCL) | Bounded write-behind buffer with `DropOldest` semantics | Hot-path safe — `TryWrite` always succeeds synchronously when configured with `DropOldest`; no allocation per write beyond the event record [VERIFIED: docs.microsoft.com/dotnet/core/extensions/channels] |
| `Microsoft.Extensions.Hosting` | built-in (.NET 10 BCL) | `BackgroundService` base class for `RequestMetricsFlusher` | Already used by `HarvestScheduleService`, `HarvestScheduleCache`, `FeatureFlagCache`, `ArchidektCacheJobService` — 4 in-tree references [VERIFIED: codebase grep] |
| Npgsql | 10.0.0 | Postgres driver — array parameters for batch UPSERT via `unnest` | Already in csproj per CLAUDE.md tech stack; supports `NpgsqlParameter` with `Value = TArray[]` for `unnest($1::text[], $2::date[], ...)` syntax [VERIFIED: npgsql.org/doc/performance.html] |
| Serilog | 4.2.0 | Structured logging for D-10 drop-rate WARN | Already in use; pattern: `_logger.LogWarning("Analytics.Buffer.Drops dropped={Dropped}/min", count)` |
| `Microsoft.AspNetCore.Http.Abstractions` | built-in | `HttpContext.GetEndpoint()` for D-05 route extraction | Available after `UseRouting()` middleware runs [VERIFIED: learn.microsoft.com/aspnet/core/fundamentals/routing] |

### Supporting (no new packages required)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Security.Cryptography.SHA256` | built-in | IP hashing (mirrors `FeedbackStore.HashIpInternal`) | Single shared helper in `IpHasher.HashAsync(ip, salt)` |

### Alternatives Considered (and rejected per CONTEXT)
| Instead of | Could Use | Tradeoff | Verdict |
|------------|-----------|----------|---------|
| `System.Threading.Channels` | `BlockingCollection<T>` | Older API, less ergonomic for async producer/consumer | Rejected — Channels is the modern .NET pattern for producer/consumer [CITED: devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels] |
| Per-row UPSERT in flush | `COPY` + staging table merge | Higher throughput at very large batches (>10k) | Rejected — D-09 caps batches at ~100, well inside per-row UPSERT comfort zone |
| HyperLogLog for unique-IPs | PG `hll` extension | Sublinear memory at scale | Rejected per D-03 — side table is simpler, no PG extension needed |
| OpenTelemetry / Prometheus | OTLP exporter | Standard observability tooling | Rejected explicitly in REQUIREMENTS.md "Out of Scope" |
| Inline JS chart library | Chart.js, ApexCharts | Better visual fidelity | Rejected per D-18 + REQUIREMENTS.md "Out of Scope" — server-side SVG is sufficient |

**No new NuGet installs required.** Every dependency is already in `DeckFlow.Web.csproj`.

**Version verification:**
- Npgsql 10.0.0 confirmed in CLAUDE.md tech stack section (line 57). Latest stable verified via published Microsoft docs. [VERIFIED: CLAUDE.md + npgsql.org]
- `Microsoft.Extensions.Hosting` 10.0.0 confirmed via existing `BackgroundService` consumers in DeckFlow.Web. [VERIFIED: codebase]
- Serilog 4.2.0 confirmed in CLAUDE.md tech stack section (line 60). [VERIFIED: CLAUDE.md]

## Architecture Patterns

### System Architecture Diagram

```
                    BROWSER REQUEST
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│ Program.cs middleware pipeline                                  │
│                                                                 │
│   UseForwardedHeaders     (Phase 7.1 invariant — DO NOT MOVE)  │
│   UseDeckFlowSecurityHeaders                                    │
│   UseHttpsRedirection                                           │
│   UseStaticFiles          (static files exit pipeline here)    │
│   UseRouting              ◄──── Endpoint populated after this   │
│         │                                                       │
│         ▼                                                       │
│   ┌─────────────────────────────────────────────┐              │
│   │  AnalyticsMiddleware (D-12 position)        │              │
│   │  ─────────────────────────────────          │              │
│   │  1. Path-prefix filter (D-11) ──fail──► next│              │
│   │  2. await next(context)        ──► request runs│           │
│   │  3. On return:                                │              │
│   │     - GetEndpoint()?.DisplayName ?? "__unmatched__"│         │
│   │     - status_class = code/100 (3xx→2)        │              │
│   │     - is_error = code≥400 && code≠404 && code<600│           │
│   │     - ip_hash = IpHasher.Hash(CF-Connecting-IP)│             │
│   │     - day_utc = DateOnly.FromDateTime(UtcNow)│              │
│   │     - buffer.TryWrite(RequestMetricEvent(...))│              │
│   │       ──drop── (Channel full, DropOldest)    │              │
│   └─────────────────────────────────────────────┘              │
│         │                                                       │
│         ▼                                                       │
│   UseSerilogRequestLogging                                      │
│   UseAuthorization                                              │
│   UseRateLimiter                                                │
│   UseWhen("/Admin", BasicAuthMiddleware)                        │
│   MapControllers                                                │
└─────────────────────────────────────────────────────────────────┘

                    SEPARATE THREAD (BackgroundService)

┌─────────────────────────────────────────────────────────────────┐
│ RequestMetricsFlusher : BackgroundService                       │
│                                                                 │
│  while (!stopping):                                             │
│     events = await ReadBatchAsync(                              │
│         maxCount=100, maxWait=5s)            ◄── D-09           │
│     try:                                                        │
│         await store.UpsertBatchAsync(events) ◄── D-01,D-03      │
│         track flushed_total                                     │
│     catch (Exception):                                          │
│         log; do NOT exit loop (T-07-14 pattern)                 │
│     if (one minute elapsed && dropped_total>0):                 │
│         _logger.LogWarning("Analytics.Buffer.Drops ...") ◄── D-10│
│         dropped_total = 0                                       │
│                                                                 │
│  StopAsync (cancellation):                                      │
│     drain remaining ≤ 2s, then exit (Claude's Discretion)       │
└─────────────────────────────────────────────────────────────────┘
                          │
                          ▼
              ┌────────────────────────────┐
              │ Postgres                   │
              │  request_metrics           │  ◄── INSERT ... ON CONFLICT
              │   PK (route_key, day_utc,  │      DO UPDATE SET
              │       status_class)        │      hit_count = hit_count + ...
              │  request_metric_ip_seen    │  ◄── INSERT ... ON CONFLICT
              │   PK (route_key, day_utc,  │      DO NOTHING
              │       ip_hash)             │
              └────────────────────────────┘
                          ▲
                          │ SUM/GROUP BY for top-routes table
                          │
              ┌────────────────────────────┐
              │ AdminAnalyticsController   │  /Admin/analytics?range=7d
              │  + AdminAnalyticsViewModel │  Razor view emits <svg><rect>
              │  + Views/AdminAnalytics/   │  per day, per row
              │       Index.cshtml         │
              └────────────────────────────┘
```

### Recommended Project Structure

All new code follows existing layout — no new top-level folders.

```
DeckFlow.Web/
├── Controllers/Admin/
│   └── AdminAnalyticsController.cs       # NEW (replaces existing placeholder stub)
├── Extensions/
│   └── AnalyticsServiceCollectionExtensions.cs  # NEW — AddDeckFlowAnalytics()
├── Infrastructure/
│   └── AnalyticsMiddleware.cs            # NEW (or inline lambda — see decision below)
├── Models/Admin/
│   └── AdminAnalyticsViewModel.cs        # NEW — Range, Routes (top-50)
├── Security/
│   └── IpHasher.cs                       # NEW — extracted shared helper
├── Services/Analytics/                   # NEW folder
│   ├── IRequestMetricsStore.cs
│   ├── RequestMetricsStore.cs            # EnsureSchemaAsync + UpsertBatchAsync
│   ├── RequestMetricsBuffer.cs           # Wraps Channel<RequestMetricEvent>
│   ├── RequestMetricsFlusher.cs          # BackgroundService — drains buffer
│   └── RequestMetricEvent.cs             # sealed record
└── Views/AdminAnalytics/
    ├── _ViewStart.cshtml                 # ALREADY EXISTS (Phase 6 plan 01)
    └── Index.cshtml                      # NEW — replaces existing placeholder
```

### Pattern 1: Bounded Channel + DropOldest (write-behind buffer)

**What:** A producer-consumer queue where producers (request middleware) never block; consumer (BackgroundService) drains in batches.

**When to use:** Hot-path → cold-path handoff where dropping data is preferable to blocking the request thread.

**Example:**
```csharp
// Source: docs.microsoft.com/en-us/dotnet/core/extensions/channels (verified 2026-05)
using System.Threading.Channels;

public sealed class RequestMetricsBuffer
{
    // D-08: capacity 10000, DropOldest. With DropOldest, TryWrite always succeeds
    // synchronously — the oldest unflushed item is silently evicted to make room.
    private static readonly BoundedChannelOptions Options = new(capacity: 10_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,   // Only RequestMetricsFlusher reads
        SingleWriter = false,  // Many concurrent requests write
        AllowSynchronousContinuations = false
    };

    private readonly Channel<RequestMetricEvent> _channel = Channel.CreateBounded<RequestMetricEvent>(Options);
    private long _droppedCount;

    public ChannelReader<RequestMetricEvent> Reader => _channel.Reader;

    public void Enqueue(RequestMetricEvent evt)
    {
        // With DropOldest, TryWrite returns true if the item was added (which is
        // always — the channel evicts the oldest entry when full).
        // To detect drops, compare reader.Count before/after, OR use the
        // itemDropped callback overload of CreateBounded (D-10 source for drop counter).
        if (!_channel.Writer.TryWrite(evt))
        {
            // DropOldest mode means this branch is unreachable in normal operation.
            // Defensive log only.
            Interlocked.Increment(ref _droppedCount);
        }
    }

    public long ConsumeDropCount() => Interlocked.Exchange(ref _droppedCount, 0L);
}
```

**Drop detection (D-10):** `Channel.CreateBounded<T>(BoundedChannelOptions, Action<T> itemDropped)` overload (added in .NET 6+, confirmed in current docs) calls a delegate every time DropOldest evicts an item. Use it to increment a `long` counter; the flusher reads + resets via `Interlocked.Exchange` once a minute. [VERIFIED: docs.microsoft.com/dotnet/api/system.threading.channels.channel.createbounded]

### Pattern 2: BackgroundService flusher with whichever-fires-first trigger

**What:** A `BackgroundService` that uses `ChannelReader.WaitToReadAsync` for await-when-empty semantics, plus a `PeriodicTimer` floor so a partial batch flushes within 5s even if traffic is light.

**When to use:** D-09 cadence — flush when 100 records buffered OR 5 seconds elapsed.

**Example:**
```csharp
// Source: derived from HarvestScheduleService.cs (in-tree pattern) +
//         docs.microsoft.com/dotnet/api/system.threading.channels.channelreader.waittoreadasync
public sealed class RequestMetricsFlusher : BackgroundService
{
    private const int BatchSize = 100;                          // D-09
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DropLogInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShutdownDrainCeiling = TimeSpan.FromSeconds(2);

    private readonly RequestMetricsBuffer _buffer;
    private readonly IServiceProvider _services;       // D-14 lazy resolve
    private readonly ILogger<RequestMetricsFlusher> _logger;
    private DateTimeOffset _lastDropLog = DateTimeOffset.MinValue;

    public RequestMetricsFlusher(
        RequestMetricsBuffer buffer,
        IServiceProvider services,
        ILogger<RequestMetricsFlusher> logger)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        _buffer = buffer;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _buffer.Reader;
        var batch = new List<RequestMetricEvent>(capacity: BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Block until at least one item is available, OR the timer fires.
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(FlushInterval);

                try
                {
                    if (await reader.WaitToReadAsync(flushCts.Token).ConfigureAwait(false))
                    {
                        // Drain available items up to BatchSize (TryRead is non-blocking).
                        while (batch.Count < BatchSize && reader.TryRead(out var evt))
                        {
                            batch.Add(evt);
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flush-interval timer fired; flush whatever (possibly empty) batch we have.
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                    batch.Clear();
                }

                // D-10: at most one WARN per minute when drops happened.
                MaybeLogDrops();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analytics.Flusher.TickFailure flusher tick threw; loop continues.");
                // T-07-14 pattern: never exit the loop on transient failure.
            }
        }

        // Claude's Discretion: drain remaining buffer with a 2-second ceiling on shutdown.
        await DrainOnShutdownAsync(reader, batch).ConfigureAwait(false);
    }

    private async Task FlushBatchAsync(List<RequestMetricEvent> batch, CancellationToken ct)
    {
        // D-14: lazy-resolve IRequestMetricsStore from IServiceProvider — avoids the
        // circular ctor graph that broke Render in dc66a38 (Phase 7.1 errata).
        using var scope = _services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRequestMetricsStore>();
        await store.UpsertBatchAsync(batch, ct).ConfigureAwait(false);
    }

    private async Task DrainOnShutdownAsync(ChannelReader<RequestMetricEvent> reader, List<RequestMetricEvent> batch)
    {
        using var deadline = new CancellationTokenSource(ShutdownDrainCeiling);
        try
        {
            while (reader.TryRead(out var evt))
            {
                batch.Add(evt);
                if (batch.Count >= BatchSize)
                {
                    await FlushBatchAsync(batch, deadline.Token).ConfigureAwait(false);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Analytics.Flusher.ShutdownDrainAborted some events not persisted.");
        }
    }

    private void MaybeLogDrops()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastDropLog < DropLogInterval) return;
        var dropped = _buffer.ConsumeDropCount();
        if (dropped > 0)
        {
            _logger.LogWarning("Analytics.Buffer.Drops dropped={Dropped} interval={Interval}s",
                dropped, DropLogInterval.TotalSeconds);
        }
        _lastDropLog = now;
    }
}
```

### Pattern 3: Postgres bulk UPSERT via UNNEST + arrays

**What:** Send N rows in a single round-trip by passing each column as a single array parameter.

**When to use:** Batch sizes between ~10 and ~1000 (well inside D-09's 100-row batch). 2.13× faster than per-row INSERT at batch size 1000 [CITED: tigerdata.com/blog/boosting-postgres-insert-performance].

**Example:**
```csharp
// Source: tigerdata.com/blog/boosting-postgres-insert-performance + npgsql.org/doc/performance.html
// (verified 2026-05). Mirrors FeatureFlagStore.cs PostgresUpsertSql column-name discipline
// per memory feedback_sqlite_postgres_sql_divergence.md.

public async Task UpsertBatchAsync(IReadOnlyList<RequestMetricEvent> events, CancellationToken ct)
{
    if (events.Count == 0) return;
    await EnsureSchemaAsync(ct).ConfigureAwait(false);

    // Pre-allocate column arrays.
    var routeKeys      = new string[events.Count];
    var dayUtcs        = new DateTime[events.Count]; // PG date — use DateTime.Date
    var statusClasses  = new short[events.Count];
    var errorIncrement = new int[events.Count];     // 1 if is_error else 0
    var ipHashes       = new string[events.Count];

    for (var i = 0; i < events.Count; i++)
    {
        var e = events[i];
        routeKeys[i]      = e.RouteKey;
        dayUtcs[i]        = e.DayUtc.ToDateTime(TimeOnly.MinValue);
        statusClasses[i]  = e.StatusClass;
        errorIncrement[i] = e.IsError ? 1 : 0;
        ipHashes[i]       = e.IpHash;
    }

    await using var conn = (NpgsqlConnection)await OpenConnectionAsync(ct).ConfigureAwait(false);
    await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

    // (1) request_metrics aggregate UPSERT
    await using (var cmd = conn.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO request_metrics (route_key, day_utc, status_class, hit_count, error_count)
            SELECT u.route_key, u.day_utc, u.status_class, COUNT(*)::bigint, SUM(u.error_inc)::bigint
              FROM unnest(@routeKeys, @dayUtcs, @statusClasses, @errorInc)
                AS u(route_key, day_utc, status_class, error_inc)
             GROUP BY u.route_key, u.day_utc, u.status_class
            ON CONFLICT (route_key, day_utc, status_class) DO UPDATE SET
              hit_count   = request_metrics.hit_count   + EXCLUDED.hit_count,
              error_count = request_metrics.error_count + EXCLUDED.error_count;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("routeKeys",     routeKeys));
        cmd.Parameters.Add(new NpgsqlParameter("dayUtcs",       dayUtcs));
        cmd.Parameters.Add(new NpgsqlParameter("statusClasses", statusClasses));
        cmd.Parameters.Add(new NpgsqlParameter("errorInc",      errorIncrement));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // (2) request_metric_ip_seen — INSERT ... ON CONFLICT DO NOTHING
    await using (var cmd = conn.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO request_metric_ip_seen (route_key, day_utc, ip_hash)
            SELECT u.route_key, u.day_utc, u.ip_hash
              FROM unnest(@routeKeys, @dayUtcs, @ipHashes)
                AS u(route_key, day_utc, ip_hash)
             WHERE u.ip_hash IS NOT NULL AND u.ip_hash <> ''
            ON CONFLICT (route_key, day_utc, ip_hash) DO NOTHING;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("routeKeys", routeKeys));
        cmd.Parameters.Add(new NpgsqlParameter("dayUtcs",   dayUtcs));
        cmd.Parameters.Add(new NpgsqlParameter("ipHashes",  ipHashes));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    await tx.CommitAsync(ct).ConfigureAwait(false);
}
```

**Why this shape over plain `INSERT ... VALUES (...), (...), ...`:**
- 1 round-trip regardless of batch size
- 1 prepared plan reuse (PG can cache it; per-row VALUES blows out the plan cache as N varies)
- No 32K parameter limit — 4 parameters total instead of 4×N
- Simpler C# (one array per column, not N positional parameters)

### Pattern 4: Inline SVG sparkline (bar chart, 14 days)

**What:** Server-side Razor C# loop that emits `<svg><rect>` per non-empty day.

**When to use:** D-18 — 14 daily bars, omit empty days, ~120px wide, neutral admin color.

**Example:**
```csharp
// Source: SVG 1.1 spec (developer.mozilla.org/en-US/docs/Web/SVG/Element/rect) — pure HTML5, no JS.
// Helper lives in the view model or as a static method in AdminAnalyticsViewModel.

public static string RenderSparkline(int[] hitsByDay /* length 14, days[0]=oldest */)
{
    const int width      = 120;
    const int height     = 24;
    const int barCount   = 14;
    const int gap        = 1;
    var barWidth   = (width - (barCount - 1) * gap) / (double)barCount; // ≈ 7.6 px

    var max = 0;
    foreach (var h in hitsByDay) if (h > max) max = h;
    if (max == 0)
    {
        // Empty row — render an invisible placeholder so the table column stays the same width.
        return $"<svg width=\"{width}\" height=\"{height}\" aria-hidden=\"true\"></svg>";
    }

    var sb = new StringBuilder(512);
    sb.Append("<svg width=\"").Append(width).Append("\" height=\"").Append(height)
      .Append("\" viewBox=\"0 0 ").Append(width).Append(' ').Append(height)
      .Append("\" role=\"img\" aria-label=\"14-day traffic sparkline\">");

    for (var i = 0; i < barCount; i++)
    {
        var v = hitsByDay[i];
        if (v <= 0) continue;          // D-18: omit empty days (gap is intentional signal)
        var barHeight = (int)Math.Round(v / (double)max * (height - 2));
        var x = Math.Round(i * (barWidth + gap), 2);
        var y = height - barHeight;
        sb.Append("<rect x=\"").Append(x.ToString(CultureInfo.InvariantCulture))
          .Append("\" y=\"").Append(y)
          .Append("\" width=\"").Append(barWidth.ToString("0.##", CultureInfo.InvariantCulture))
          .Append("\" height=\"").Append(barHeight)
          .Append("\" fill=\"currentColor\" />");
    }
    sb.Append("</svg>");
    return sb.ToString();
}

// In Razor view (Views/AdminAnalytics/Index.cshtml):
//   <td class="admin-sparkline">@Html.Raw(AdminAnalyticsViewModel.RenderSparkline(row.HitsByDay))</td>
//
// CSS in admin.css:
//   .admin-sparkline { color: var(--admin-fg-muted, #94a3b8); line-height: 0; }
//
// `currentColor` makes the bars inherit `.admin-sparkline { color: ... }` so theme tweaks
// happen in CSS, not in C# (D-18 color matches admin neutral palette).
```

**Accessibility:** `role="img"` + `aria-label` keeps the chart screen-reader-friendly. `aria-hidden` on empty-row placeholder so it isn't announced.

**Why `Html.Raw`:** the helper builds whitelisted markup (no user input ever flows in); Razor would otherwise HTML-encode `<svg>`. Safe because `RouteKey` and other tainted data never appear in the SVG body.

### Pattern 5: Middleware shape — inline lambda vs class

**Two options, both consistent with Program.cs style:**

**Option A — Inline lambda (recommended for simplicity):**
```csharp
// In Program.cs, between UseRouting() and UseAuthorization():
app.Use(async (context, next) =>
{
    // D-11: static asset short-circuit (ordered most-frequent-first)
    var path = context.Request.Path.Value;
    if (path is not null && (
        path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/extensions/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/_health", StringComparison.OrdinalIgnoreCase)))
    {
        await next(context);
        return;
    }

    await next(context);

    // D-05/D-06: route_key from Endpoint.DisplayName, fallback "__unmatched__"
    var endpoint = context.GetEndpoint();
    var routeKey = endpoint?.DisplayName ?? "__unmatched__";

    // D-04: status_class with 3xx → 2 collapse, 1xx skipped
    var status = context.Response.StatusCode;
    if (status < 200 || status >= 600) return;
    var statusClass = status switch
    {
        >= 200 and < 400 => (short)2,   // 2xx + 3xx
        >= 400 and < 500 => (short)4,
        _                => (short)5,
    };

    // D-07: error definition
    var isError = status >= 400 && status != 404 && status < 600;

    // D-13: IP capture priority via shared helper
    var ipHash = IpHasher.HashRequestIp(context, salt);

    var evt = new RequestMetricEvent(
        RouteKey:    routeKey,
        DayUtc:      DateOnly.FromDateTime(DateTime.UtcNow),
        StatusClass: statusClass,
        IsError:     isError,
        IpHash:      ipHash);

    buffer.Enqueue(evt);
});
```

**Option B — Class implementing `IMiddleware`:** gives a testable seam matching the rest of `DeckFlow.Web/Infrastructure/`. Recommended if the planner wants the same shape as `BasicAuthMiddleware`.

Either is fine; CONTEXT does not lock the choice.

### Pattern 6: Singleton-and-hosted-service DI registration (mirrors HarvestServiceCollectionExtensions)

```csharp
// DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs (NEW)
public static class AnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddDeckFlowAnalytics(this IServiceCollection services, IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(env);

        // Buffer: singleton, no dependencies — pure wrapper around Channel<T>.
        services.AddSingleton<RequestMetricsBuffer>();

        // Store: singleton, takes IServiceProvider lazily (D-14 — Phase 7.1 errata pattern).
        services.AddSingleton<IRequestMetricsStore>(sp => new RequestMetricsStore(
            sp.GetRequiredService<IWebHostEnvironment>(),
            sp));

        // Flusher: singleton + hosted service. Same dual-registration as HarvestScheduleCache.
        services.AddSingleton<RequestMetricsFlusher>();
        services.AddHostedService(sp => sp.GetRequiredService<RequestMetricsFlusher>());

        return services;
    }
}

// Program.cs additions:
//   builder.Services.AddDeckFlowAnalytics(builder.Environment);   // alongside AddDeckFlowHarvest
//
// And after app.Build(), before app.RunAsync():
//   await app.Services.GetRequiredService<IRequestMetricsStore>().EnsureSchemaAsync();
//   (mirrors lines 374-377 for harvest stores)
```

### Anti-Patterns to Avoid

- **Building Polly pipelines per call:** N/A — analytics is intra-process, no outbound HTTP.
- **Skipping `IServiceProvider` lazy resolution in the store:** Phase 7.1 dc66a38 errata proved that two singletons each taking the other in their ctors will pass `dotnet build` and crash on Render at `Application started`. The store MUST take `IServiceProvider` (not direct refs to buffer/flusher) per D-14. Local startup smoke-test mandatory per D-15.
- **Synchronous DB write inside the middleware:** That's the entire reason for the Channel — never `await store.UpsertAsync(...)` from the request thread.
- **Relying on `HttpContext.GetEndpoint()` before `UseRouting()`:** returns `null` always [VERIFIED: learn.microsoft.com/answers/questions/1350132]. D-12 placement is mandatory.
- **Storing raw IPs:** `request_metrics` and `request_metric_ip_seen` MUST have `ip_hash TEXT` only — verified by SC #3 (`SELECT ip_hash FROM request_metrics LIMIT 1` returns hash, no `ip_raw` column exists).
- **Putting analytics CSS into a guild theme `site-*.css`:** All admin CSS goes in `wwwroot/css/admin.css` per Phase 6 plan 01 D-05 single-stylesheet wall.
- **Adding a JS chart library:** Explicitly forbidden by D-18 + REQUIREMENTS.md "Out of Scope".

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Producer/consumer queue | Custom `ConcurrentQueue` + `SemaphoreSlim` | `System.Threading.Channels.BoundedChannel` with `DropOldest` | Channel handles back-pressure, drop semantics, and async wake-up correctly. The drop-callback overload gives D-10 drop accounting for free. |
| Background drain loop boilerplate | New `Thread` or `Task.Run` | `Microsoft.Extensions.Hosting.BackgroundService` | Already used by HarvestScheduleService, FeatureFlagCache, ArchidektCacheJobService. Integrates with `IHostApplicationLifetime` for graceful shutdown. |
| Bulk UPSERT | Per-row `INSERT ... ON CONFLICT` in a loop | `unnest(@arr1, @arr2, ...) ... ON CONFLICT DO UPDATE` | 2.13× faster, single round-trip, single prepared plan, no 32K-parameter limit. |
| IP hashing | New SHA-256 site | `IpHasher.HashRequestIp(httpContext, salt)` shared helper (extracted from FeedbackStore) | Single source of truth for salt resolution + CF-Connecting-IP priority order. Phase 7.1 already proved the salt env var → DB-stored fallback chain works. |
| Endpoint route extraction | Custom path-template lookup | `HttpContext.GetEndpoint()?.DisplayName` | Zero work — ASP.NET routing already populated this when `UseRouting()` ran. |
| Time-window date math | New helper | `DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-N))` | .NET 6+ built-in date type, no allocation. |
| SVG layout math | Hand-coded path strings or external library | C# `<rect>` emission with `currentColor` | Single sealed helper, ~30 lines, no dependencies, theme-aware via CSS. |
| Forwarded-header trust list | New `KnownProxies` config | Reuse existing `UseForwardedHeaders` middleware in Program.cs (already configured Phase 7.1) | The validator-scoped X-Forwarded-Proto pattern from Phase 7.1 CAT-FIX-01 is the precedent. |

**Key insight:** Every part of Phase 8 except the Channel buffer has a direct in-tree precedent. The buffer itself uses BCL primitives (`Channel<T>`) — no new package. Phase 8 is "wire 4 small services into existing patterns," not "design new infrastructure."

## Runtime State Inventory

> Phase 8 is greenfield code (analytics middleware + new tables). No rename / refactor / migration component beyond the optional `IpHasher` extraction.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — phase ADDS new tables (`request_metrics`, `request_metric_ip_seen`); no rename of existing data | None |
| Live service config | None — no n8n / external workflow / dashboard registration | None |
| OS-registered state | None — runs inside the existing Render container | None |
| Secrets/env vars | `FEEDBACK_IP_SALT` is **reused** (D-13). Already set in Render dashboard. No new secrets. If `IpHasher` is extracted as a shared helper, FeedbackStore must keep reading the same env var name — no rename | Verify env var is set on Render before merge (operator already confirmed for FeedbackStore) |
| Build artifacts / installed packages | None — no new NuGet packages, no new TS sources | None |

**The single migration concern:** if the planner extracts `IpHasher` as a shared helper, `FeedbackStore.HashIpInternal` callers must switch over in the same commit, or both code paths must keep working temporarily. Recommend: extract `IpHasher` first (its own task with passing tests), then point FeedbackStore at it (one-line refactor), then build the analytics middleware on top.

## Common Pitfalls

### Pitfall 1: Endpoint resolution returns null in middleware
**What goes wrong:** `context.GetEndpoint()` is null because the middleware ran before `UseRouting()`.
**Why it happens:** ASP.NET Core only populates the endpoint feature inside `UseRouting()` → `UseEndpoints()` (or implicit endpoint mapping via `MapControllers()`).
**How to avoid:** Strict middleware order per D-12 — analytics MUST run after `app.UseRouting()`. Verify by greppping Program.cs that the new `app.Use(...)` call is below `app.UseRouting()` and above the `app.Map*` calls.
**Warning signs:** Every row in `request_metrics` has `route_key = '__unmatched__'`. SC #1 fails (no template strings).

### Pitfall 2: Circular DI cycle — Application crashes on startup
**What goes wrong:** Two singletons depend on each other in their constructors. `dotnet build` passes; Render boot crashes with `InvalidOperationException: A circular dependency was detected`.
**Why it happens:** Phase 7.1 errata (commit dc66a38). MS DI does NOT short-circuit cycles when a parameter has a default value or is nullable.
**How to avoid:** D-14 — `IRequestMetricsStore` takes `IServiceProvider`, not direct refs to `RequestMetricsBuffer` or `RequestMetricsFlusher`. Resolve the buffer via `_services.GetRequiredService<RequestMetricsBuffer>()` only when needed (which for the store is never — store and buffer are independent). Mirror HarvestRunStore's lazy-resolve shape.
**Warning signs:** Local `dotnet run` crashes with `InvalidOperationException` before reaching "Application started." If the planner does NOT smoke-test locally, the first sign is Render's deploy log.

### Pitfall 3: Channel TryWrite returning false silently with default Wait mode
**What goes wrong:** Producer code assumes `TryWrite` always succeeds, but the planner forgot to set `FullMode = DropOldest` and used the default `Wait` mode.
**Why it happens:** `BoundedChannelOptions.FullMode` defaults to `Wait`, which makes `TryWrite` return `false` when the channel is full.
**How to avoid:** Explicitly set `FullMode = BoundedChannelFullMode.DropOldest` per D-08. Bonus: pass the `itemDropped` callback to `Channel.CreateBounded` so D-10 drop accounting is automatic.
**Warning signs:** `request_metrics` row counts plateau under load even though traffic continues. Drop counter is missing or never logs.

### Pitfall 4: SQLite vs Postgres SQL divergence on UPSERT
**What goes wrong:** Per `feedback_sqlite_postgres_sql_divergence.md` memory: column qualification differs between dialects (Postgres needs unqualified `EXCLUDED.col`; SQLite accepts both `excluded.col` and `EXCLUDED.col`).
**Why it happens:** Phase 8 is **PG-only** (D-01 narrative + admin-only feature gating per Phase 7 precedent), so the dual-dialect concern does not arise. But if the planner accidentally adds a SQLite path "for symmetry," the existing schema-divergence trap applies.
**How to avoid:** Do NOT add a SQLite path. Mirror HarvestStatsAggregator and HarvestRunStore's PG-only branches; assert `_connectionInfo.IsPostgres` at startup of `EnsureSchemaAsync` (or just call Postgres-only SQL since `RequestMetricsStore` only runs in Render where DB provider is always Postgres).
**Warning signs:** Local `dotnet run` against the default SQLite DB throws `SqliteException` on the UPSERT. Prevention: skip Phase 8 wiring in dev if `DECKFLOW_DATABASE_PROVIDER != "Postgres"`, OR document that local dev needs Postgres for analytics to function.

### Pitfall 5: X-Forwarded-Proto / IP capture under PaaS proxy
**What goes wrong:** The middleware reads `Connection.RemoteIpAddress` and gets Cloudflare's edge IP, not the real client IP. All unique-IP counts collapse to 1.
**Why it happens:** Phase 7.1 lesson: Render's `KnownProxies` does NOT honor `X-Forwarded-For` from Cloudflare's CIDR ranges by default. The Phase 7.1 fix (dc66a38) was to read `CF-Connecting-IP` directly, NOT to widen `KnownProxies`.
**How to avoid:** D-13 explicit priority — `CF-Connecting-IP` first, `X-Forwarded-For` first hop second, `Connection.RemoteIpAddress` last. Mirror Program.cs `DeriveCloudflareClientIp` (lines 400-409) inside the new `IpHasher.HashRequestIp` helper.
**Warning signs:** `SELECT COUNT(DISTINCT ip_hash) FROM request_metric_ip_seen WHERE day_utc = CURRENT_DATE` returns 1 (or a tiny number) even at peak traffic.

### Pitfall 6: Background flusher swallowed by an exception
**What goes wrong:** A transient PG failure throws inside `ExecuteAsync`; the BackgroundService loop exits silently. Buffer fills, drops climb, no flush ever happens again.
**Why it happens:** Without a per-tick try/catch, an exception bubbles out of `ExecuteAsync`, which terminates the service. The host does NOT auto-restart it.
**How to avoid:** Per-tick try/catch wrapping `FlushBatchAsync`, mirror HarvestScheduleService.cs:71-86 pattern. `OperationCanceledException` on `stoppingToken` propagates (normal shutdown); everything else is logged + loop continues.
**Warning signs:** Render logs show one `Analytics.Flusher.TickFailure` then nothing. `request_metrics` stops getting new rows. SC #5 indirectly fails because the buffer fills + drops everything.

### Pitfall 7: Cardinality blow-up from query strings or path params
**What goes wrong:** `route_key` ends up containing per-card-name or per-deck-id strings, exploding the table to one row per request.
**Why it happens:** Using `request.Path.Value` instead of `Endpoint.DisplayName`. The path has the variable substituted; the DisplayName has the template.
**How to avoid:** D-05 — use `Endpoint.DisplayName` exclusively. Add an integration smoke-test or a manual probe: hit `/Deck/Lookup` with two different cards, verify both rows aggregate under the same `route_key`.
**Warning signs:** SC #1 fails — `SELECT DISTINCT route_key FROM request_metrics` returns thousands of unique strings instead of ~30.

## Code Examples

(Embedded above in "Pattern 1" through "Pattern 6.")

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `BlockingCollection<T>` for producer/consumer | `System.Threading.Channels.BoundedChannel` | .NET Core 3.0 (2019) | Channels are async-first, lock-free reads, and have first-class drop policies |
| `ConfigureAwait(false)` on every await | Optional in .NET 8+ for non-library code | .NET 8 (2023) | App-level code does not need it; library code (`DeckFlow.Core`) still benefits. DeckFlow's existing services use `.ConfigureAwait(false)` consistently — keep matching that style |
| `IHostedService` direct implementation | `BackgroundService` base class | ASP.NET Core 3.0 | Handles cancellation/lifetime; only override `ExecuteAsync` |
| `DateTime` for date-only values | `DateOnly` | .NET 6 | Allocation-free, no time component, maps cleanly to PG `date` |
| Per-row `INSERT` in a loop for batches | `unnest(...)` array UPSERT | Long-standing PG idiom; benchmarks 2.13× faster at batch=1000 | Recommended by Tiger Data, Npgsql perf docs |
| Manual cookie management | `SocketsHttpHandler` `CookieContainer` | .NET 8 (Phase 5 BUG-01 in DeckFlow) | N/A here — analytics has no outbound HTTP |
| OpenTelemetry / Prometheus exporter | Bespoke Postgres write-behind | Project decision (REQUIREMENTS "Out of Scope") | Acceptable at single-instance Render scale; revisit if multi-instance |

**Deprecated/outdated:**
- Nothing in the current ASP.NET Core 10 docs deprecates `HttpContext.GetEndpoint()` — verified [VERIFIED: learn.microsoft.com/aspnet/core/fundamentals/routing?view=aspnetcore-10.0]
- `BoundedChannelOptions.FullMode` API is stable across .NET 6+ [VERIFIED: github.com/dotnet/runtime BoundedChannelFullMode source]

## Project Constraints (from CLAUDE.md)

- **Tech stack pinned:** ASP.NET 10 + Razor; no framework migration. Phase 8 stays inside the existing pipeline. ✓
- **Hosting:** Render Starter web (512MB RAM cap). Channel capacity 10000 × ~64 bytes per `RequestMetricEvent` ≈ 640KB worst-case — well inside budget. ✓
- **Theme system:** Layout CSS goes in `site-common.css`, NOT `site.css`. Analytics admin CSS goes in `wwwroot/css/admin.css` per Phase 6 D-05. ✓
- **HTTP resilience:** RestSharp + direct Polly v8 — N/A (analytics is intra-process, no outbound HTTP). ✓
- **Public repo:** No secrets in commits. `FEEDBACK_IP_SALT` already lives in Render dashboard. ✓
- **Testing:** VSTest unreliable in WSL — rely on `dotnet build` + manual harness + push-and-watch CI. D-15 startup smoke-test is the gate. ✓
- **Commits:** Plain default-author, no Co-Authored-By trailer; README updated when behavior changes; commit per logical change. ✓

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Channel.CreateBounded<T>(BoundedChannelOptions, Action<T> itemDropped)` overload exists in .NET 10 BCL | Pattern 1 (drop accounting) | If overload absent, planner must implement drop counting via `reader.Count` deltas — slightly fiddlier but no API blocker. **Verify by:** `dotnet --list-sdks` then `Channel.CreateBounded<int>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest }, x => { });` in a `dotnet script` snippet. (Verified present since .NET 6.) [ASSUMED] |
| A2 | Render's deploy log surfaces the `InvalidOperationException` from a circular DI cycle within ~30s of deploy | Pitfall 2 | Phase 7.1 dc66a38 confirmed this empirically — but the exact log latency on Render Starter could differ. Risk: planner relies on Render canary instead of local smoke-test, and the diagnostic loop is longer. Mitigation: D-15 mandates **local** smoke-test first. [ASSUMED] |
| A3 | DeckFlow has <30 distinct routes today, so top-50 with no pagination is sufficient | D-17 / view design | If route count grows past ~50 in a future phase, the table truncates silently. Mitigation: ANLY-NEXT pagination is a small follow-up. Low risk for v1.1. [ASSUMED] |
| A4 | Postgres single-row write to `request_metric_ip_seen` with `ON CONFLICT DO NOTHING` does not noticeably slow the batch UPSERT | Pattern 3 | At batch size 100 in one transaction, both UPSERTs together should be <10ms. Risk: under burst load (10k+ buffered events in 5s window), the second UPSERT could become the bottleneck. Mitigation: monitor flush latency in Serilog; if it climbs, split the IP-seen write into a separate background path or sample it. [ASSUMED] |
| A5 | `FEEDBACK_IP_SALT` env var is set in production; if absent, FeedbackStore falls back to `feedback_meta.ip_salt` row | D-13 IP capture | Verified by reading FeedbackStore.cs `ResolveSaltAsync` (lines 288-313) — env var is preferred, DB-stored value is fallback. `IpHasher` should reuse this resolution chain (factor `ResolveSaltAsync` into the helper). [VERIFIED: codebase] — actually verified, removing from assumptions risk |
| A6 | The 14-day sparkline can be served by querying `request_metrics` for the last 14 days and pivoting in C# without measurable cost at 50 routes × 14 days = 700 rows | D-18 / view rendering | Worst case 700 rows from a SUM/GROUP BY on a small table — sub-millisecond on PG Basic-256mb. [ASSUMED] |

**Items requiring user confirmation before execution:**
- A2 (Render deploy log latency) — operator should confirm whether Render's startup probe surfaces InvalidOperationException quickly or if it takes the full 60s healthcheck window.
- A4 (IP-seen UPSERT throughput) — accept as a known watch point; revisit if SC #5 baseline shows regression after merge.

## Open Questions (RESOLVED)

1. **Should the `range=all` filter use a separate query path?**
   - What we know: D-16 lists "today, 7d, 30d, all" as allowed values. SUM/GROUP BY across all-time on a per-(route, day, status) table is cheap (≤ a few thousand rows even after a year).
   - What's unclear: whether "all" should query without a `WHERE day_utc >= ...` clause, or set a sentinel like `range >= 9999d`.
   - Recommendation: omit the `WHERE` clause entirely for `range=all`. Query plan is identical at small table sizes; less code branching.
   - **RESOLVED:** Plan 04 Task 2 honors the recommendation — `LoadRowsAsync` switch sets `sinceClause = ""` for `range=all`, omitting the `WHERE day_utc >= ...` clause entirely. The `ipSqlClean` switch likewise drops the date predicate for the all-time branch.

2. **Should Phase 8 instrument the analytics middleware itself for self-observability?**
   - What we know: D-10 covers drop logging. Nothing covers flush latency or batch-size distribution.
   - What's unclear: whether the operator wants a `Analytics.Flusher.Batch flushed={N} elapsed={Ms}ms` log line per flush.
   - Recommendation: emit Information-level log per flush (already cheap). Provides SC #5 baseline data without needing a separate metrics page.
   - **RESOLVED — DECLINED:** Per-flush Information log is NOT adopted. Rationale: flusher fires every ~5 seconds (D-09), which would emit ~12 Information lines/minute under steady load — log spam in Render's console sink (Serilog rolling daily file at retainedFileCountLimit=14 would also bloat). D-10 already provides the only operator-relevant signal (drop-counter WARN throttled to one per ~60s). SC #5 baseline comes from Render dashboard p95 (Plan 05 Task 1), not from per-flush logs. Revisit only if we need flush-latency telemetry; can be added later behind a feature flag without schema impact.

3. **Pre-deployment: what is the current Render dashboard p95 baseline?**
   - What we know: STATE.md "Pending Todos" includes "Capture Render dashboard p95 baseline before deploying Phase 8 analytics middleware (SUMMARY.md gap)."
   - What's unclear: whether this baseline has been captured.
   - Recommendation: capture a 7-day p95 screenshot from Render dashboard BEFORE merging Phase 8 to give SC #5 a hard comparison target. Block the phase-close on it if necessary.
   - **RESOLVED:** Plan 05 Task 1 is the dedicated `checkpoint:human-action` that captures the pre-deploy Render dashboard p95 baseline before Plan 05 Task 2 (merge + deploy). Plan 05 Task 3 then compares post-deploy p95 against the captured baseline as part of SC #5 verification (≤ +20% tolerance gate). Phase-close is blocked on this capture per STATE.md pending-todo wording.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All builds | ✓ | 10.0 | — |
| Postgres (Basic-256mb on Render) | `request_metrics` writes & reads | ✓ (prod) | PG 15+ via Render Basic | Local dev: `DECKFLOW_DATABASE_PROVIDER=Postgres` env var + local PG container; otherwise feature is dormant in dev |
| Npgsql 10.0.0 | Bulk UPSERT via `unnest(...)` arrays | ✓ | 10.0.0 (already in csproj per CLAUDE.md) | — |
| `System.Threading.Channels` | Bounded write-behind buffer | ✓ | BCL (.NET 10) | — |
| Microsoft.Extensions.Hosting | `BackgroundService` base | ✓ | BCL (.NET 10) | — |
| Serilog 4.2.0 | Structured drop-rate WARN | ✓ | 4.2.0 already configured | — |
| `FEEDBACK_IP_SALT` env var | IP hashing salt | ✓ (prod) | Set in Render dashboard | DB-stored `feedback_meta.ip_salt` row (auto-generated on first FeedbackStore boot) |
| BasicAuth on `/Admin` | `/Admin/analytics` gate | ✓ | Active in Program.cs:334-336 | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** Local-dev PostgreSQL — analytics writes will throw on SQLite. Acceptable per Phase 7 precedent (admin-only PG-only features). If the planner wants local-dev parity, `DECKFLOW_DATABASE_PROVIDER=Postgres` + Docker `postgres:15` is the canonical workaround.

## Validation Architecture

> Project workflow disables Nyquist validation (`workflow.nyquist_validation: false`) — this section maps each Phase 8 success criterion to a verification approach so the planner can derive Dimension-8 verification tasks.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (per CLAUDE.md) — used for unit-test seams only; full-stack verification is manual + curl + Render |
| Config file | `DeckFlow.Web.Tests.csproj` (existing) |
| Quick run command | `dotnet build DeckFlow.sln` (project convention — VSTest unreliable in WSL) |
| Full suite command | `dotnet build DeckFlow.sln && dotnet run --project DeckFlow.Web` then manual curl probes |
| Phase gate | `dotnet build` clean + container-startup smoke-test (D-15) + Render canary green |

### Phase Requirements & Success Criteria → Verification Map

| Source | Behavior | Verification Type | Verification Action | Wave |
|--------|----------|------------------|---------------------|------|
| ANLY-01 / SC #1 | Route templates persisted (no high-cardinality blow-up) | Live-traffic SQL probe | After merge, run `psql -c "SELECT DISTINCT route_key FROM request_metrics"` against prod PG; expect ≤ 30 distinct strings, all matching either `MyApp.Web.Controllers.X.Y (...)` or attribute-route templates | Post-merge smoke |
| ANLY-06 / SC #2 | Static-asset routes excluded | Live-traffic SQL probe | `psql -c "SELECT COUNT(*) FROM request_metrics WHERE route_key LIKE '/css/%' OR route_key LIKE '/js/%' OR route_key LIKE '/lib/%' OR route_key LIKE '/extensions/%'"` returns 0 | Post-merge smoke |
| ANLY-03 / SC #3 | No raw IPs stored | Schema introspection | `psql -c "\d request_metrics"` and `psql -c "\d request_metric_ip_seen"` — verify NO column named `ip_raw`, `ip`, `client_ip`, or `remote_addr`; only `ip_hash TEXT` | Pre-merge schema review + post-merge `\d` |
| ANLY-04 / ANLY-05 / SC #4 | Top-routes table renders with sparklines, no JS chart lib | Build-time grep + manual page render | `grep -r "chart\.js\|apexcharts\|chartist\|d3" DeckFlow.Web/wwwroot/ DeckFlow.Web/Views/AdminAnalytics/` returns nothing. Open `https://www.deckflow.gg/Admin/analytics?range=7d` in browser, confirm table renders with inline `<svg>` per row (View Source check) | Pre-merge build check + post-merge manual |
| ANLY-02 / SC #5 | No p95 regression vs pre-analytics baseline | Render dashboard delta | Capture pre-merge 7-day p95 screenshot from Render dashboard. Post-merge, wait 7 days, capture again. Compare. Accept ≤ 5% regression as in-noise | Pre-merge baseline capture (mandatory per STATE.md pending todo); post-merge week-out |
| ANLY-02 / D-09 | Flusher does flush in batches, not per-row | Log inspection | Render logs: confirm `Analytics.Flusher.Batch` Information lines fire every 5s under load with `flushed=N` where N > 1 (proves batching, not per-row) | Post-merge log review |
| ANLY-02 / D-10 | Drop logging fires at most once per minute under burst | Log inspection | Synthetic load test: `ab -n 50000 -c 200 https://www.deckflow.gg/`. Confirm Render logs show `Analytics.Buffer.Drops dropped=N interval=60s` AT MOST once per 60-second window | Optional load probe (defer if SC #5 passes naturally) |
| D-15 | Container starts cleanly with new DI graph | Local smoke-test (mandatory per Phase 7.1 errata) | `dotnet run --project DeckFlow.Web` reaches `Application started. Press Ctrl+C to shut down.` log line within 10s without `InvalidOperationException` | Pre-merge — BLOCKING |
| D-12 | Middleware position correct | Code review + endpoint probe | `grep -n "AnalyticsMiddleware\|app.Use(.*context.*next" DeckFlow.Web/Program.cs` shows the line number is BETWEEN `app.UseRouting()` (currently :318) and `app.MapControllers()` (currently :338). Manual curl `/Deck/Lookup?card=Sol+Ring` should produce `route_key = 'DeckFlow.Web.Controllers.DeckController.Lookup ...'` not `'/Deck/Lookup'` | Pre-merge code review + post-merge SQL probe |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln` (project convention)
- **Per wave merge:** `dotnet run --project DeckFlow.Web` startup smoke-test + manual `curl http://localhost:5173/` to confirm middleware doesn't 500
- **Phase gate:** All 5 success-criterion probes above pass; Render p95 baseline captured pre-merge

### Wave 0 Gaps

- [ ] `IpHasher.cs` test seam: a small `IpHasherTests.cs` in `DeckFlow.Web.Tests` covering the priority order (CF-Connecting-IP > X-Forwarded-For > Connection.RemoteIpAddress) and salt-resolution fallback
- [ ] `RequestMetricsBufferTests.cs`: covers `Enqueue` returning success even when full (DropOldest semantics) and drop-count Interlocked accuracy
- [ ] `RequestMetricsFlusherTests.cs` (optional): drives a fake `IRequestMetricsStore`, asserts batch+timer trigger conditions
- [ ] No new framework install needed — xUnit 2.9.3 already in DeckFlow.Web.Tests

*(Tests are seam-level; full-stack validation is via the live-traffic SQL probes above per CLAUDE.md "VSTest unreliable in WSL" convention.)*

## Sources

### Primary (HIGH confidence)
- [.NET Channels documentation (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — BoundedChannelFullMode.DropOldest semantics, SingleReader/SingleWriter, drop callbacks
- [.NET routing documentation (Microsoft Learn, .NET 10)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0) — `HttpContext.GetEndpoint()`, `Endpoint.DisplayName`, middleware position vs `UseRouting`
- [Npgsql Performance docs](https://www.npgsql.org/doc/performance.html) — array parameters, batch operations, prepared statement reuse
- [BoundedChannelFullMode.cs source — dotnet/runtime](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Threading.Channels/src/System/Threading/Channels/BoundedChannelFullMode.cs) — authoritative DropOldest definition
- In-tree code (HIGH): `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`, `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs`, `DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs`, `DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs`, `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs`, `DeckFlow.Web/Services/FeedbackStore.cs`, `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`, `DeckFlow.Web/Security/SameOriginRequestValidator.cs`, `DeckFlow.Web/Program.cs`

### Secondary (MEDIUM confidence)
- [Boosting Postgres INSERT Performance by 2x With UNNEST (Tiger Data)](https://www.tigerdata.com/blog/boosting-postgres-insert-performance) — UNNEST 2.13× perf at batch=1000; recommended pattern
- [An Introduction to System.Threading.Channels (.NET Blog)](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) — modern producer/consumer idiom rationale
- [Postgres UNNEST cheat sheet (DEV.to)](https://dev.to/forbeslindesay/postgres-unnest-cheat-sheet-for-bulk-operations-1obg) — UNNEST + ON CONFLICT example syntax
- [How does UseRouting work? (Microsoft Q&A)](https://learn.microsoft.com/en-us/answers/questions/1350132/how-does-the-userouting-method-work-in-asp-net-cor) — confirmation that `GetEndpoint()` returns null before `UseRouting`

### Tertiary (LOW confidence — verified against primary source where used)
- [Channels in C# (Adrian Bailador)](https://adrianbailador.github.io/blog/42-channels-csharp/) — supplementary intro, cross-checked against MS Learn
- [Benchmarking PostgreSQL Batch Ingest (Tiger Data)](https://www.tigerdata.com/blog/benchmarking-postgresql-batch-ingest) — supplementary perf data

### Project memory consulted
- `feedback_di_optional_dep_does_not_break_cycle.md` — informs Pitfall 2 + D-14 lazy-resolve mandate
- `feedback_csrf_validator_under_proxy.md` — informs Pitfall 5 + D-13 CF-Connecting-IP priority
- `feedback_sqlite_postgres_sql_divergence.md` — informs Pitfall 4 (PG-only avoids the trap)
- `feedback_http_resilience_pattern.md` — confirmed N/A for Phase 8

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every package already in csproj, every pattern has an in-tree precedent
- Architecture: HIGH — locked in CONTEXT.md (D-01..D-18); research only confirms the HOW
- Pitfalls: HIGH — pitfalls 2, 4, 5 are documented project memory from Phases 5, 6, 7.1; pitfalls 1, 3, 6, 7 are well-known ASP.NET / Channels / Postgres patterns
- Validation: HIGH — every SC has a concrete probe + command

**Research date:** 2026-05-03
**Valid until:** 2026-06-03 (30 days — stable .NET 10 / Npgsql 10 / Postgres 15+ stack)

## RESEARCH COMPLETE

**Phase:** 8 - Analytics
**Confidence:** HIGH

### Key Findings

- Every Phase 8 building block has a direct in-tree precedent: `EnsureSchemaAsync` (FeatureFlagStore.cs), singleton-and-hosted-service registration (HarvestServiceCollectionExtensions.cs), BackgroundService loop with per-tick try/catch + OperationCanceledException handling (HarvestScheduleService.cs), `IServiceProvider` lazy resolution to break ctor cycles (HarvestRunStore.cs post-dc66a38), SHA-256 IP hashing with shared `FEEDBACK_IP_SALT` (FeedbackStore.cs lines 173-183 + 288-313), admin shell + neutral CSS + per-folder `_ViewStart` for `Views/AdminAnalytics/` (Phase 6 plan 01).
- Single new mechanism is `System.Threading.Channels.BoundedChannel<T>` with `FullMode = DropOldest`. The `Channel.CreateBounded` overload that accepts an `itemDropped` callback gives D-10 drop accounting for free.
- Recommended Postgres bulk UPSERT shape is `INSERT ... SELECT ... FROM unnest(@arr1, @arr2, ...) ... GROUP BY ... ON CONFLICT (route_key, day_utc, status_class) DO UPDATE SET hit_count = ... + EXCLUDED.hit_count` — 1 round-trip, 1 prepared plan, no parameter-count limit, 2.13× faster than per-row VALUES at batch=1000.
- Middleware MUST be placed between `app.UseRouting()` (Program.cs:318) and `app.MapControllers()` (Program.cs:338) per D-12 — verified that `HttpContext.GetEndpoint()` returns null before `UseRouting` runs.
- Phase 7.1 dc66a38 errata is the single highest-risk pitfall: `dotnet build` clean does NOT prove DI graph clean. Local container-startup smoke-test (D-15) is mandatory before push.

### File Created
`/mnt/c/users/chrislunt/source/personal/decksyncworkbench/.planning/phases/08-analytics/08-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | All deps already in csproj per CLAUDE.md; verified against Microsoft Learn + npgsql.org |
| Architecture | HIGH | Locked in CONTEXT.md D-01..D-18; research confirmed the HOW with in-tree precedents |
| Pitfalls | HIGH | 3 of 7 pitfalls are documented project memory from Phases 5/6/7.1; 4 are well-known |
| Validation Architecture | HIGH | Every SC mapped to a concrete probe + command |

### Open Questions (RESOLVED)
1. Should `range=all` use a different query path? **RESOLVED:** Plan 04 Task 2 omits the `WHERE day_utc >= ...` clause when `range=all` (per recommendation).
2. Should the flusher emit per-flush Information log? **RESOLVED — DECLINED:** Per-flush Information log not adopted (would log-spam every ~5s); D-10 drop-counter WARN at ~60s cadence remains the only flusher signal.
3. Has the Render p95 baseline been captured pre-merge? **RESOLVED:** Plan 05 Task 1 is the dedicated checkpoint:human-action that captures the baseline before Plan 05 Task 2 deploys; Plan 05 Task 3 compares post-deploy p95 against it (SC #5).

### Ready for Planning
Research complete. Planner can now create PLAN.md files. The recommended sequencing (per dependency order):
- Wave 1: `IpHasher` extraction + `FeedbackStore` swap (1 task) | `request_metrics` + `request_metric_ip_seen` schema + `RequestMetricsStore.EnsureSchemaAsync` + `UpsertBatchAsync` (1 task)
- Wave 2: `RequestMetricsBuffer` + `RequestMetricsFlusher` BackgroundService (1 task) | `AnalyticsMiddleware` + Program.cs middleware insertion (1 task)
- Wave 3: `AddDeckFlowAnalytics()` extension + Program.cs DI wiring + startup `EnsureSchemaAsync` await (1 task) | D-15 container-startup smoke-test (verification task)
- Wave 4: `AdminAnalyticsController` + `AdminAnalyticsViewModel` + `Views/AdminAnalytics/Index.cshtml` (sparkline rendering, range filter, top-50 table) (1 task)
- Wave 5: Live-traffic SC verification (5 SQL/grep probes; deferred-to-prod equivalents per Phase 6/7 precedent) + Render p95 baseline capture (1 task)
