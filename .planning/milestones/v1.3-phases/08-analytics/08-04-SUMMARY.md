---
phase: 08-analytics
plan: 04
subsystem: admin-ui

tags: [admin, analytics, npgsql, svg-sparkline, razor, no-js]

# Dependency graph
requires:
  - phase: 08-analytics
    plan: 01
    provides: RequestMetricsStore, request_metrics + request_metric_ip_seen tables, DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection
  - phase: 08-analytics
    plan: 02
    provides: RequestMetricsFlusher writes to Postgres
  - phase: 08-analytics
    plan: 03
    provides: AnalyticsMiddleware populates request_metrics on every request

provides:
  - AdminAnalyticsViewModel at DeckFlow.Web/Models/Admin/AdminAnalyticsViewModel.cs
  - AdminAnalyticsController at DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs
  - Views/AdminAnalytics/Index.cshtml (top-routes table + sparklines)
  - admin.css additions: .admin-sparkline, .admin-analytics-table, .admin-range-selector

affects: [08-05-live-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Direct NpgsqlConnection from DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection — no IRequestMetricsStore growth; write-only store contract preserved"
    - "RenderSparkline: C# StringBuilder emitting inline SVG <rect> elements — no JS chart library (D-18)"
    - "range whitelist via SortedSet<string>.Contains before SQL switch — no user input interpolated into SQL (T-08-18)"
    - "SQLite local-dev graceful empty: connInfo.IsPostgres check at top of LoadRowsAsync; returns Array.Empty without error"

key-files:
  created:
    - DeckFlow.Web/Models/Admin/AdminAnalyticsViewModel.cs
  modified:
    - DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs
    - DeckFlow.Web/Views/AdminAnalytics/Index.cshtml
    - DeckFlow.Web/wwwroot/css/admin.css

key-decisions:
  - "Controller queries Postgres directly via NpgsqlConnection (mirrors HarvestStatsAggregator) rather than extending IRequestMetricsStore — store stays write-only; query surface lives in the controller"
  - "DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection has no OpenConnectionAsync — used CreateConnection() + OpenAsync() matching RequestMetricsStore private helper (same deviation as Plan 03)"
  - "admin.css token adaptation: plan spec referenced --admin-fg-muted / --admin-line but admin.css uses --muted / --border; adapted to actual tokens with hardcoded fallbacks"
  - "ipSql built as switch over whitelisted range value (not string-concat) — each branch is a full hardcoded SQL string parameterized via @routeKeys only (T-08-18)"

requirements-completed: [ANLY-04, ANLY-05]

# Metrics
duration: 10min
completed: 2026-05-03
---

# Phase 8 Plan 04: Admin Analytics Dashboard Summary

**AdminAnalyticsController + ViewModel + RenderSparkline helper + Razor view + admin.css additions — top-routes table with inline SVG sparklines, range filter, no JS chart library**

## Performance

- **Duration:** ~10 min
- **Completed:** 2026-05-03
- **Tasks:** 3 (all complete in single commit)
- **Files modified:** 4 (1 created, 3 modified)

## Accomplishments

- `AdminAnalyticsViewModel` with `AllowedRanges` SortedSet (today/7d/30d/all), `RouteRow` sealed record (RouteKey, Hits, UniqueIps, ErrorRate, HitsByDay int[14]), `RenderSparkline` static helper emitting inline SVG `<rect>` bars via StringBuilder — no JS library, `currentColor` for CSS theming, empty days omitted per D-18
- `AdminAnalyticsController` replaces 15-line stub: 3 direct Npgsql queries (top-50 routes by hits, unique-IP counts, 14-day per-route sparkline), graceful SQLite no-op, range whitelist validation, structured error logging
- `Views/AdminAnalytics/Index.cshtml` replaces placeholder: range-selector nav (4 links, `is-active` class), conditional empty state, `<table class="admin-table admin-analytics-table">` with route/hits/unique-IPs/error-rate/sparkline columns, `@Html.Raw(RenderSparkline(...))` per row
- `admin.css` gains `.admin-sparkline { color: var(--muted); line-height: 0; }`, `.admin-analytics-table`, `.admin-range-selector`, `.admin-page-header`, `.admin-empty` — scoped to admin.css only, no guild theme touched

## Task Commits

1. **Tasks 1 + 2 + 3 combined** — `b7ef767` (feat) — all 4 files in one atomic commit

## Files Created/Modified

- `DeckFlow.Web/Models/Admin/AdminAnalyticsViewModel.cs` — `public sealed class AdminAnalyticsViewModel`; AllowedRanges SortedSet, Range + Routes required properties, RouteRow sealed record, RenderSparkline (SVG via StringBuilder), FormatErrorRate
- `DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` — replaced stub; `[Route("Admin/Analytics")]`, `Index(string range="7d", CancellationToken)`, `LoadRowsAsync` private helper with 3 Npgsql queries
- `DeckFlow.Web/Views/AdminAnalytics/Index.cshtml` — replaced placeholder; `@model AdminAnalyticsViewModel`, range selector, top-50 table, `@Html.Raw(RenderSparkline(row.HitsByDay))`
- `DeckFlow.Web/wwwroot/css/admin.css` — appended Phase 8 analytics section (no existing rules modified)

## Decisions Made

- Controller queries DB directly (not via `IRequestMetricsStore`) — store contract stays write-only; admin read surface belongs in the controller layer matching `HarvestStatsAggregator` pattern.
- `CreateConnection() + OpenAsync()` instead of a non-existent `OpenConnectionAsync()` — same adaptation as Plan 03; fully documented in 08-03-SUMMARY.md.
- CSS tokens adapted from plan spec (`--admin-fg-muted` → `--muted`, `--admin-line` → `--border`, `--admin-accent-bg` → `--panel`) — plan spec assumed new tokens but admin.css already has a clean token set; adaptation maintains visual parity with rest of admin shell.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Missing `using DeckFlow.Web.Services;` in controller**
- **Found during:** Task 2 build verification
- **Issue:** `DeckFlowDatabaseConnectionFactory` lives in namespace `DeckFlow.Web.Services`; plan spec did not include this using directive
- **Fix:** Added `using DeckFlow.Web.Services;` to controller usings
- **Files modified:** `DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs`
- **Committed in:** `b7ef767`

**2. [Rule 2 - Adaptation] CSS token names differ from plan spec**
- **Found during:** Task 3 (reading admin.css before writing)
- **Issue:** Plan spec referenced `--admin-fg-muted`, `--admin-line`, `--admin-accent-bg` but admin.css defines `--muted`, `--border`, `--panel`
- **Fix:** Used actual token names with hardcoded fallback values (e.g. `color: var(--muted, #94a3b8)`) so the page is legible even if token resolution fails
- **Files modified:** `DeckFlow.Web/wwwroot/css/admin.css`
- **Committed in:** `b7ef767`

---

**Total deviations:** 2 auto-fixed (1 missing using, 1 token name adaptation)
**Impact on plan:** Minimal; both are trivial one-file fixes.

## Known Stubs

None — controller queries live Postgres tables. On SQLite local-dev the page renders an empty state ("No request metrics recorded yet") which is correct and intentional per D-01 (analytics is Postgres-only).

## Threat Flags

None — no new network endpoints beyond `/Admin/Analytics` which is covered by existing `app.UseWhen("/Admin", BasicAuth)` branch (T-08-17 mitigated). SQL injection surface closed: range value is whitelisted before use; route_key array is parameterized (T-08-18). XSS surface closed: `@row.RouteKey` is Razor HTML-encoded; `@Html.Raw` receives only integer-derived SVG output (T-08-19).

## Self-Check

- `DeckFlow.Web/Models/Admin/AdminAnalyticsViewModel.cs` — exists; contains `public sealed class AdminAnalyticsViewModel`, `AllowedRanges`, `"today"`, `"7d"`, `"30d"`, `"all"`, `public sealed record RouteRow`, `RouteKey`, `Hits`, `UniqueIps`, `ErrorRate`, `HitsByDay`, `public static string RenderSparkline`, `<svg`, `<rect`, `currentColor`, `aria-label`
- `DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` — exists; contains `[Route("Admin/Analytics")]`, `public sealed class AdminAnalyticsController`, `Index(string range = "7d"`, `AllowedRanges.Contains`, `request_metrics`, `request_metric_ip_seen`, `COUNT(DISTINCT ip_hash)`, `TopRouteLimit = 50`, `SparklineDays = 14`, `ORDER BY hits DESC`; does NOT contain `[Authorize]`
- `DeckFlow.Web/Views/AdminAnalytics/Index.cshtml` — exists; contains `@model DeckFlow.Web.Models.Admin.AdminAnalyticsViewModel`, `RenderSparkline(row.HitsByDay)`, `Last 14 days`, `"today"`, `"7d"`, `"30d"`, `"all"`, `Hits`, `Unique IPs`, `Error rate`; does NOT contain `<script`
- `DeckFlow.Web/wwwroot/css/admin.css` — contains `.admin-sparkline`, `.admin-analytics-table`, `.admin-range-selector`
- Build: `b7ef767` — 0 Warning(s), 0 Error(s)

## Self-Check: PASSED

## Next Phase Readiness

Wave 5 (08-05): Live-traffic verification — `/Admin/Analytics` is now deployed-ready. SC #1 (top-routes table visible) and SC #4 (sparklines render) are verifiable on deckflow.gg after Wave 5 deploy confirms live data flowing from middleware through to the admin page.

---
*Phase: 08-analytics*
*Completed: 2026-05-03*
