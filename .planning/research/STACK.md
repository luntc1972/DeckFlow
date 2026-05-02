# Technology Stack — v1.1 Admin Console Additions

**Project:** DeckFlow v1.1 Admin Console
**Researched:** 2026-05-02
**Scope:** NEW packages only. Existing stack (ASP.NET 10, Razor, Postgres/Npgsql 10, RestSharp 114, Polly 8.6.6, Serilog 9, Markdig 0.38, Swashbuckle 7, IMemoryCache) is validated and unchanged.

---

## 1. Cron Scheduling

**Recommendation: NCrontab 3.4.0 + existing BackgroundService/Channel pattern**

Do NOT add Quartz.NET. Do NOT add Coravel. Do NOT add Hangfire.

### Rationale

`ArchidektCacheJobService` already extends `BackgroundService` and uses `System.Threading.Channels` (already in the repo at lines 1-2 of that file). The only missing piece is cron-expression parsing — computing "time until next run" from a cron string. NCrontab 3.4.0 does exactly that: it is a pure cron-expression parser with zero runtime overhead, no threads, no timers, no DI registrations. It targets netstandard1.0, published 2025-09-13, confirmed compatible with .NET 10.

Quartz.NET 3.18.1 (latest, published 2026-04-25) requires its own `ISchedulerFactory`, `IJobDetail`, `ITrigger`, `IScheduler` abstraction layer, plus a hosted service thread pool, and optionally a database persistence store. All of that duplicates what the existing `Channel<ArchidektCacheJobStatus>` + `BackgroundService` already does. Quartz brings 200+ KB of assembly, its own job store, and its own retry/misfires model that conflicts with the DeckFlow Polly pipeline pattern.

Coravel is a full-feature scheduler + queue + mailer; 50 KB of overhead, last substantial commit 2023, not worth the dependency for one cron expression.

Hangfire (InMemory) has real-world reports of memory leaks and retention issues; its dashboard would conflict with the admin sidebar; its Postgres storage option would add a separate schema and polling loop — all wrong for a 512MB Starter tier with one operator.

### Integration

Add `NCrontab` 3.4.0 to `DeckFlow.Web.csproj`:

```xml
<PackageReference Include="NCrontab" Version="3.4.0" />
```

Extend `ArchidektCacheJobService` with a `_cronSchedule` field (nullable `CrontabSchedule`). In `ExecuteAsync` replace `Channel.ReadAllAsync(stoppingToken)` with a hybrid loop:

```csharp
// Pseudo-pattern — not final code
while (!stoppingToken.IsCancellationRequested)
{
    var nextRun = _cronSchedule?.GetNextOccurrence(DateTime.UtcNow);
    var delay = nextRun.HasValue
        ? nextRun.Value - DateTime.UtcNow
        : Timeout.InfiniteTimeSpan;

    var triggered = await WaitForJobOrDelayAsync(delay, stoppingToken);
    // ... execute job
}
```

`SetCronSchedule(string expr)` validates with `CrontabSchedule.TryParse(expr)` and stores the result. Store last/next run times in the same in-memory `ConcurrentDictionary<Guid, ArchidektCacheJobStatus>` pattern, extended with `LastRunUtc` and `NextRunUtc` fields. No new DB table needed for schedule state (single-operator, process-restarts reset it; cron expression lives in a Postgres feature-flag row — see section 4).

**What NOT to add:** Quartz.AspNetCore, Coravel, Hangfire, Hangfire.InMemory, Hangfire.PostgreSql.

---

## 2. Feature Flags

**Recommendation: Roll-own — `IFeatureFlagStore` + `IOptionsMonitor`-style hot-reload poller. Do NOT add Microsoft.FeatureManagement.**

### Rationale

`Microsoft.FeatureManagement.AspNetCore` 4.5.0 (published 2026-04-23, targets net8.0+, compatible with net10.0) supports a custom `IFeatureDefinitionProvider` that could back flags from Postgres. However:

- The interface requires mapping DeckFlow's simple `bool enabled` rows to `FeatureDefinition` objects with `IEnumerable<FeaturFilterConfiguration>` — significant ceremony for kill switches and boolean gates.
- The `IFeatureManager.IsEnabledAsync(string)` call path re-evaluates on every invocation but still depends on `IConfiguration`, meaning the custom provider still gets called via the configuration pipeline — the caching and reload semantics are opaque (GitHub issue #367 confirms caching inside the provider is the caller's responsibility).
- The package pulls in Azure App Configuration SDK as a transitive dependency — 400KB+ extra for a feature that can be done in ~100 lines.

For DeckFlow's use case (6-10 boolean flags, one operator, kill switches + a Tagger toggle + beta gates) a purpose-built store is less surface area and easier to reason about.

### Design

`IFeatureFlagStore` (new interface in `DeckFlow.Web/Services/`):

```csharp
public interface IFeatureFlagStore
{
    ValueTask<bool> IsEnabledAsync(string flag, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default);
    Task SetAsync(string flag, bool enabled, string? description, CancellationToken ct = default);
}
```

`FeatureFlag` is a `sealed record` with `string Key`, `bool Enabled`, `string? Description`, `DateTimeOffset UpdatedUtc`.

Postgres schema (added to `EnsureSchemaAsync` in the existing dialect pattern):

```sql
CREATE TABLE IF NOT EXISTS feature_flags (
    key         TEXT PRIMARY KEY,
    enabled     BOOLEAN NOT NULL DEFAULT TRUE,
    description TEXT,
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Hot reload: a singleton `FeatureFlagCache` wraps the store and polls every 30 seconds via a `PeriodicTimer` inside a `BackgroundService`. `IsEnabledAsync` reads from an `ImmutableDictionary<string, bool>` snapshot, replaced atomically on each poll. Default-open (returns `true`) if flag not found, except explicit kill-switch flags which should default-closed — document per flag. No IConfiguration involved.

Registration in `Program.cs`:

```csharp
builder.Services.AddSingleton<IFeatureFlagStore, PostgresFeatureFlagStore>();
builder.Services.AddSingleton<FeatureFlagCache>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagCache>());
```

Controllers and services inject `FeatureFlagCache` (or `IFeatureFlagStore` for the admin write path).

**What NOT to add:** Microsoft.FeatureManagement, Microsoft.FeatureManagement.AspNetCore, LaunchDarkly, ConfigCat, Azure App Configuration.

---

## 3. Sparkline / Chart Rendering

**Recommendation: Server-rendered inline SVG via Razor — zero packages, zero JS libraries.**

### Rationale

The analytics page needs daily sparklines for per-route request counts: a small line chart over 7-30 data points per route. Pure SVG math requires only subtraction and division. The approach (verified from alexplescan.com, 2023):

1. Y-flip each value: `yCoord = maxValue - value` (SVG origin is top-left).
2. Set `viewBox="0 0 {count-1} {maxValue}"` — SVG scales coordinates automatically.
3. Emit a `<polyline>` with `points="{i},{yFlipped} ..."` (or `<path>` for filled area).

The entire sparkline is a Razor partial or Display Template with ~15 lines of C# coordinate math and ~5 lines of SVG markup. No JS at runtime. No HTTP round-trip. No external package. Renders correctly in all browsers. Scales infinitely with guild themes via `stroke: var(--accent-strong)`.

### Implementation

Add `Views/Shared/DisplayTemplates/Sparkline.cshtml` (or inline in the analytics partial). The view model passes `IReadOnlyList<int> DailyCounts`. The template emits:

```html
<svg viewBox="0 0 @(counts.Count - 1) @maxVal" preserveAspectRatio="none"
     width="120" height="30" class="sparkline">
  <polyline points="@string.Join(" ", points)" fill="none"
            stroke="var(--accent-strong)" stroke-width="1.5" />
</svg>
```

Where `points` is computed server-side as `(i, maxVal - counts[i])` pairs.

**What NOT to add:** Chart.js, ApexCharts, Recharts, D3.js, Telerik, Syncfusion, any npm chart package.

---

## 4. Long-Running Cancellable Job Pattern

**Recommendation: Extend existing `BackgroundService` + `System.Threading.Channels` — no new package.**

### Rationale

`ArchidektCacheJobService` already uses this exact pattern (confirmed in source). It already has `EnqueueAsync`, `GetJob`, `GetActiveJob`, and `ExecuteAsync` over `Channel.CreateUnbounded`. What it lacks for v1.1:

- **Cancel:** expose a per-job `CancellationTokenSource` linked to the `stoppingToken`. Store it alongside the job status in the `ConcurrentDictionary`. `CancelJobAsync(Guid)` calls `cts.Cancel()`.
- **Pause/Resume:** add a `SemaphoreSlim(1,1)` as a pause gate. `PauseJobAsync` acquires the semaphore; `ResumeJobAsync` releases it. The job loop checks `await _pauseGate.WaitAsync(ct)` inside its inner iteration.
- **Single-URL harvest:** add `EnqueueSingleUrlAsync(string archidektUrl, CancellationToken)` to `IArchidektCacheJobService`; enqueues a job with `DurationSeconds = 0` and a `TargetUrl` field on the status record.
- **Stats panel:** `GetStatsAsync()` on `IArchidektCacheJobService` returns totals from the knowledge store plus last/next run times from the job state. No new persistence needed except the `feature_flags` row for the cron expression (see section 2).

`System.Threading.Channels` is a BCL type (no NuGet needed). `BackgroundService` is in `Microsoft.Extensions.Hosting` (already transitive). `CancellationTokenSource` is BCL.

`ArchidektCacheJobStatus` record gets extended fields: `CancellationTokenSource? Cts`, `bool IsPaused`, `string? TargetUrl`.

**What NOT to add:** Hangfire, MediatR, any message broker, any actor framework.

---

## 5. Per-Request Metrics Persistence

**Recommendation: Bounded `Channel<PageHitEvent>` accumulator + singleton `BackgroundService` flusher — direct INSERT batches every 5 seconds. No external metrics package.**

### Rationale

Direct INSERT per request on the hot path is wrong at Render Starter tier: under any burst (bot crawl, shared-link spike), one INSERT per request will queue up against a Basic-256mb Postgres instance with low connection count. OpenTelemetry + Prometheus exporter is overkill — it adds a collector sidecar or a pull endpoint, neither of which DeckFlow has budget or ops complexity for.

The right pattern is the same one already used by `ArchidektCacheJobService`: a `Channel.CreateBounded<PageHitEvent>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest })` as a ring buffer. The middleware writes to the channel (non-blocking `TryWrite`); a `BackgroundService` drains and batch-INSERTs every 5 seconds using Dapper-style raw SQL via the existing `IRelationalDatabaseConnection` dialect.

A dropped event under load is acceptable — DeckFlow analytics is operational telemetry for one operator, not billing. `DropOldest` with capacity 2000 events is ~50KB at ~25 bytes/event, negligible RAM.

### Schema

```sql
CREATE TABLE IF NOT EXISTS page_hits (
    id          BIGSERIAL PRIMARY KEY,        -- Sqlite: INTEGER PRIMARY KEY AUTOINCREMENT
    route       TEXT NOT NULL,
    hit_day     DATE NOT NULL,
    hit_count   INTEGER NOT NULL DEFAULT 1,
    unique_ips  INTEGER NOT NULL DEFAULT 0,
    error_count INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS uix_page_hits_route_day ON page_hits (route, hit_day);
```

The flusher drains the channel into a `Dictionary<(string route, DateOnly day), PageHitAccumulator>` then upserts one row per (route, day) key. Postgres upsert:

```sql
INSERT INTO page_hits (route, hit_day, hit_count, unique_ips, error_count)
VALUES (@route, @day, @hitCount, @uniqueIps, @errorCount)
ON CONFLICT (route, hit_day) DO UPDATE
SET hit_count   = page_hits.hit_count + EXCLUDED.hit_count,
    unique_ips  = page_hits.unique_ips + EXCLUDED.unique_ips,
    error_count = page_hits.error_count + EXCLUDED.error_count;
```

SQLite equivalent uses `INSERT OR REPLACE` with pre-read — follow existing `IRelationalDialect` branch pattern.

### Middleware

`PageHitMiddleware : IMiddleware` (registered as scoped, added in `Program.cs` before `MapControllers`). Reads `HttpContext.Request.Path` (normalised to controller route template), checks `context.Response.StatusCode` after `await _next(context)` to determine error flag, hashes IP to a `bool isNewIp` check against an `IMemoryCache` sliding 24h key. `TryWrite` to the channel — fire and forget.

### Registration

```csharp
// In Program.cs, DI section
builder.Services.AddSingleton<PageHitChannel>();   // wraps Channel<PageHitEvent>
builder.Services.AddSingleton<PageHitFlusher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PageHitFlusher>());
builder.Services.AddScoped<PageHitMiddleware>();
// In middleware pipeline, after UseRouting, before MapControllers:
app.UseMiddleware<PageHitMiddleware>();
```

**What NOT to add:** OpenTelemetry SDK, prometheus-net, App.Metrics, Application Insights, any OTEL collector, any time-series database.

---

## Summary — New Packages

| Package | Version | Purpose | Add to |
|---------|---------|---------|--------|
| `NCrontab` | 3.4.0 | Cron expression parsing for harvest schedule | `DeckFlow.Web.csproj` |

That is the only NuGet addition for v1.1.

All other capabilities (feature flags, sparklines, cancellable jobs, metrics accumulator) are built from BCL types and existing project patterns.

---

## Package Reference Block

```xml
<!-- Add to DeckFlow.Web/DeckFlow.Web.csproj ItemGroup -->
<PackageReference Include="NCrontab" Version="3.4.0" />
```

---

## What NOT to Add (explicit exclusions)

| Rejected Package | Reason |
|-----------------|--------|
| `Quartz` / `Quartz.AspNetCore` | Heavyweight scheduler; own thread pool; own job store; duplicates existing Channel+BackgroundService pattern |
| `Coravel` | Full feature scheduler+queue+mailer; 2023 last major commit; over-kill for one cron expression |
| `Hangfire` / `Hangfire.PostgreSql` | Adds schema, polling loop, dashboard conflicts with admin sidebar; memory leak history |
| `Microsoft.FeatureManagement.AspNetCore` | Azure App Configuration transitive dep; 400KB+; IConfiguration coupling; ceremony exceeds value for 6-10 boolean flags |
| `LaunchDarkly` / `ConfigCat` | External SaaS; network dependency for a kill switch; secrets; overkill |
| `Chart.js` / `ApexCharts` / `D3.js` | Client-side JS charting; violates "minimal JS" constraint; sparklines need no JS |
| `OpenTelemetry.*` / `prometheus-net` | Metrics collection for external collectors; no collector infra exists; overkill for in-app analytics |
| `App.Metrics` | Same concern as above; last major activity 2022 |
| `MediatR` | No pub/sub or CQRS need; Channel covers the decoupling that matters here |

---

## Sources

- NCrontab NuGet: https://www.nuget.org/packages/NCrontab/ (v3.4.0, published 2025-09-13, netstandard1.0)
- Quartz.NET NuGet: https://www.nuget.org/packages/quartz/ (v3.18.1, published 2026-04-25, net8.0+)
- Microsoft.FeatureManagement.AspNetCore NuGet: https://www.nuget.org/packages/Microsoft.FeatureManagement.AspNetCore/ (v4.5.0, published 2026-04-23)
- IFeatureDefinitionProvider interface: https://learn.microsoft.com/en-us/dotnet/api/microsoft.featuremanagement.ifeaturedefinitionprovider
- Custom DB ConfigurationProvider pattern: https://gavilan.blog/2025/03/29/write-a-custom-configuration-provider-that-connects-to-a-database-asp-net-core/
- SVG sparkline coordinate math: https://alexplescan.com/posts/2023/07/08/easy-svg-sparklines/
- System.Threading.Channels background processing: https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/
- Quartz.NET Context7 docs: https://context7.com/quartznet/quartznet
- Microsoft.FeatureManagement Context7 docs: https://context7.com/microsoft/featuremanagement-dotnet
