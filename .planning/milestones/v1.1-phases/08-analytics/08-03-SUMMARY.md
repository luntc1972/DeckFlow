---
phase: 08-analytics
plan: 03
subsystem: infra

tags: [middleware, di, channel, analytics, ip-hashing, aspnetcore]

# Dependency graph
requires:
  - phase: 08-analytics
    plan: 01
    provides: IpHasher.HashRequestIp, IRequestMetricsStore, RequestMetricEvent, DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection
  - phase: 08-analytics
    plan: 02
    provides: RequestMetricsBuffer.Enqueue, RequestMetricsFlusher (BackgroundService)

provides:
  - AnalyticsMiddleware (IMiddleware) at DeckFlow.Web/Infrastructure/AnalyticsMiddleware.cs
  - AnalyticsApplicationBuilderExtensions.UseAnalyticsMiddleware() at DeckFlow.Web/Infrastructure/AnalyticsApplicationBuilderExtensions.cs
  - AnalyticsServiceCollectionExtensions.AddDeckFlowAnalytics(IWebHostEnvironment) at DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs
  - AnalyticsSaltAccessor singleton (startup-populated volatile string holder) in same file
  - Program.cs wired: DI registration, middleware placement, EnsureSchemaAsync, salt resolution
affects: [08-04-admin-dashboard, 08-05-live-traffic]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AnalyticsMiddleware: IMiddleware + scoped DI — registered via AddScoped<AnalyticsMiddleware>() + app.UseMiddleware<AnalyticsMiddleware>() extension"
    - "AnalyticsSaltAccessor: volatile-read singleton populated once at startup, eliminates per-request DB I/O on hot path"
    - "Static-asset path-prefix filter before await next() — prevents cardinality blow-up on versioned asset URLs (D-11)"
    - "endpoint?.DisplayName ?? __unmatched__ — never Request.Path.Value for route_key (D-05/D-06, T-08-12)"
    - "salt resolution try/catch at startup — graceful SQLite no-op: feedback_meta missing logs WRN, app continues, ip_hash = null until Postgres deploy"

key-files:
  created:
    - DeckFlow.Web/Infrastructure/AnalyticsMiddleware.cs
    - DeckFlow.Web/Infrastructure/AnalyticsApplicationBuilderExtensions.cs
    - DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs
  modified:
    - DeckFlow.Web/Program.cs

key-decisions:
  - "AnalyticsSaltAccessor co-located in AnalyticsServiceCollectionExtensions.cs — accepted per CLAUDE.md precedent (result records co-located with services in CardLookupService.cs)"
  - "Salt resolution uses CreateHarvestStateConnection + CreateConnection() + OpenAsync() — parity with RequestMetricsStore ctor and future Plan 04 admin reads; CreateHarvestStateConnection has no OpenConnectionAsync method"
  - "Salt resolution try/catch wraps entire block — SQLite smoke-test: feedback_meta missing throws SqliteException, caught, WRN logged, ip_hash null; this is expected non-blocking behavior on local-dev and does NOT mask circular-DI failures"
  - "UseAnalyticsMiddleware() inserted after UseRouting() and before UseSerilogRequestLogging() — satisfies D-12 (after routing, before MapControllers) while keeping Serilog request logging below analytics"

patterns-established:
  - "Pattern: IMiddleware + scoped DI — always pair AddScoped<TMiddleware>() + UseMiddleware<TMiddleware>() for typed middleware in DeckFlow.Web"
  - "Pattern: startup-resolved singleton for hot-path config values (AnalyticsSaltAccessor) — avoids DB I/O per request"

requirements-completed: [ANLY-01, ANLY-02, ANLY-03, ANLY-06]

# Metrics
duration: 15min
completed: 2026-05-03
---

# Phase 8 Plan 03: Analytics Middleware + DI Extension + Program.cs Wiring Summary

**AnalyticsMiddleware wired into request pipeline with endpoint-aware route_key capture, static-asset filter, D-14 lazy-IServiceProvider DI graph, and D-15 smoke-test confirmed clean**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-03T21:00:00Z
- **Completed:** 2026-05-03T21:05:00Z
- **Tasks:** 2 (Task 3 is the human checkpoint — deferred per plan)
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments

- `AnalyticsMiddleware` captures `route_key` from `endpoint?.DisplayName ?? "__unmatched__"`, computes D-04 `status_class` (2/4/5), applies D-07 `is_error` (status >= 400 && != 404 && < 600), hashes IP via `IpHasher.HashRequestIp`, enqueues `RequestMetricEvent` into `RequestMetricsBuffer`; swallows all exceptions post-pipeline so analytics never propagates into requests (T-08-14)
- Static-asset path-prefix filter (`/css/`, `/js/`, `/lib/`, `/extensions/`, `/favicon.ico`, `/_health`) fires BEFORE `await next()` to prevent cardinality blow-up on high-frequency asset requests (D-11, ANLY-06)
- `AddDeckFlowAnalytics(IWebHostEnvironment)` registers the full analytics DI graph with D-14 lazy-IServiceProvider pattern — no circular singleton cycle; `dotnet build` clean and D-15 smoke-test confirms DI graph starts without `InvalidOperationException`
- `EnsureSchemaAsync` awaited at startup before `RunAsync`; salt resolved once at startup and cached in `AnalyticsSaltAccessor`

## Task Commits

1. **Tasks 1 + 2 combined** — `474d1bf` (feat) — all 3 new files + Program.cs in one atomic commit

**Plan metadata:** see docs commit below

## Files Created/Modified

- `DeckFlow.Web/Infrastructure/AnalyticsMiddleware.cs` — `public sealed class AnalyticsMiddleware : IMiddleware`; static-asset filter, endpoint DisplayName route_key, D-04/D-07 status logic, IpHasher, exception swallow
- `DeckFlow.Web/Infrastructure/AnalyticsApplicationBuilderExtensions.cs` — `UseAnalyticsMiddleware()` calling `app.UseMiddleware<AnalyticsMiddleware>()`
- `DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs` — `AddDeckFlowAnalytics()` + `AnalyticsSaltAccessor`
- `DeckFlow.Web/Program.cs` — added `using DeckFlow.Web.Security` + `using DeckFlow.Web.Services.Analytics`; `AddDeckFlowAnalytics(builder.Environment)` after `AddDeckFlowHarvest`; `UseAnalyticsMiddleware()` between `UseRouting()` and `UseSerilogRequestLogging()`; `EnsureSchemaAsync` + salt resolution block before `RunAsync`

## Decisions Made

- `AnalyticsSaltAccessor` co-located in `AnalyticsServiceCollectionExtensions.cs` rather than its own file — acceptable per CLAUDE.md precedent (result records co-located with services, e.g., `CardLookupService.cs`).
- `CreateConnection() + OpenAsync()` used directly for salt resolution — `RelationalDatabaseConnection` has no `OpenConnectionAsync()` method; adapted to match the pattern in `RequestMetricsStore.OpenConnectionAsync()`.
- Salt resolution wrapped in `try/catch` that logs WRN and continues — on local-dev SQLite, `feedback_meta` table doesn't exist (created lazily by FeedbackStore, not by analytics bootstrap). The WRN is expected and non-blocking. On Render Postgres the table exists from prior feedback activity.
- Middleware inserted immediately after `UseRouting()` (before `UseSerilogRequestLogging()`) — satisfies D-12 (endpoint resolved, before MapControllers) while keeping Serilog request logging as the next item in the chain.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `OpenConnectionAsync` does not exist on `RelationalDatabaseConnection`**
- **Found during:** Task 2 (Program.cs wiring — salt resolution block)
- **Issue:** Plan spec called `harvestConn.OpenConnectionAsync()` but `RelationalDatabaseConnection` only exposes `CreateConnection()` (returns `DbConnection`); no async open method on the record
- **Fix:** Changed to `harvestConn.CreateConnection()` + `await saltConnection.OpenAsync()` — matches the private `OpenConnectionAsync` helper pattern in `RequestMetricsStore`
- **Files modified:** `DeckFlow.Web/Program.cs`
- **Verification:** Build clean, smoke-test reached `Application started`
- **Committed in:** `474d1bf`

---

**Total deviations:** 1 auto-fixed (1 blocking — wrong API call)
**Impact on plan:** Minimal; single line change, semantically identical, factory parity preserved.

## D-15 Smoke-Test Result

**Status: PASS**

Command:
```
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:5299"
MTG_DATA_DIR=/tmp/deckflow-smoke DECKFLOW_DATABASE_PROVIDER=Sqlite
DECKFLOW_DISABLE_AUTO_BROWSER=true timeout 35 dotnet run --project DeckFlow.Web --no-launch-profile
```

Key log lines (in order):
```
[INF] Ensuring harvest store schemas during startup.
[INF] Harvest store schemas ensured during startup.
[INF] Ensuring analytics store schema during startup.
[INF] Analytics store schema ensured during startup.
[WRN] Analytics IP salt resolution failed; ip_hash will be null until next startup.
       (SQLite Error 1: 'no such table: feedback_meta' — expected on local SQLite, caught by try/catch)
[INF] Now listening on: http://localhost:5299
[INF] Application started. Press Ctrl+C to shut down.
```

PASS conditions verified:
- `Application started` — YES
- `Ensuring analytics store schema` + `Analytics store schema ensured` — YES
- `Analytics IP salt resolution failed` with WRN (not error, not crash) — expected on SQLite; try/catch works
- No `InvalidOperationException` — YES (grep returned empty)
- No `circular dependency` / `cannot be resolved` — YES (grep returned empty)

Note on salt WRN: `feedback_meta` table is created by `FeedbackStore.EnsureSchemaAsync`, which runs on first feedback submit. On Render Postgres it pre-exists from production use. On local SQLite smoke-test it is absent — the try/catch logs WRN and the app proceeds with `ip_hash = null` for analytics events. This is non-blocking and was anticipated in the plan.

## Issues Encountered

None beyond the `OpenConnectionAsync` API deviation documented above.

## User Setup Required

None — wave 3 is infrastructure-only. No new env vars required (salt uses existing `FEEDBACK_IP_SALT`).

## Known Stubs

None — no UI or data-serving code in this plan. Analytics middleware is fully wired; ip_hash will be populated in production where `feedback_meta` / `FEEDBACK_IP_SALT` exists.

## Threat Flags

None — no new network endpoints or auth paths introduced. Middleware sits in the existing pipeline. `UseForwardedHeaders()` position unchanged (Phase 7.1 invariant preserved; `app.UseForwardedHeaders()` is at line 305, `UseAnalyticsMiddleware()` is at line 319).

## Self-Check

- `DeckFlow.Web/Infrastructure/AnalyticsMiddleware.cs` — exists; contains `public sealed class AnalyticsMiddleware : IMiddleware`, `GetEndpoint()`, `endpoint?.DisplayName`, `__unmatched__`, `/css/`, `/js/`, `/lib/`, `/extensions/`, `/favicon.ico`, `/_health`, `IpHasher.HashRequestIp`, `_buffer.Enqueue`, `status != 404`, `DateOnly.FromDateTime`
- `DeckFlow.Web/Infrastructure/AnalyticsApplicationBuilderExtensions.cs` — exists; contains `UseAnalyticsMiddleware`, `app.UseMiddleware<AnalyticsMiddleware>()`
- `DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs` — exists; contains `AddDeckFlowAnalytics`, `AddSingleton<RequestMetricsBuffer>`, `AddSingleton<IRequestMetricsStore>(sp => new RequestMetricsStore(`, `AddSingleton<RequestMetricsFlusher>`, `AddHostedService(sp => sp.GetRequiredService<RequestMetricsFlusher>())`, `AnalyticsSaltAccessor`, `AddScoped<AnalyticsMiddleware>`
- `DeckFlow.Web/Program.cs` — contains `AddDeckFlowAnalytics(builder.Environment)`, `UseAnalyticsMiddleware()`, `GetRequiredService<IRequestMetricsStore>().EnsureSchemaAsync()`, `IpHasher.ResolveSaltAsync(saltConnection)`, `saltAccessor.SetSalt(salt)`, `CreateHarvestStateConnection`; UseRouting < UseAnalyticsMiddleware < MapControllers (ORDER OK); UseForwardedHeaders before UseAnalyticsMiddleware
- Build: `474d1bf` — 0 Warning(s), 0 Error(s)
- Smoke-test: Application started, no circular DI

## Next Phase Readiness

Wave 4 (08-04): Admin analytics dashboard — can read from `request_metrics` / `request_metric_ip_seen` tables. Middleware now populates the buffer on every production request.
Wave 5 (08-05): Live-traffic success criteria verification — analytics pipeline is fully wired; SC #1 (data visible in admin) and SC #2 (static assets excluded) ready to verify against live traffic.

---
*Phase: 08-analytics*
*Completed: 2026-05-03*
