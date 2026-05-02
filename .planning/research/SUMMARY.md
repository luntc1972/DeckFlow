# Research Summary — v1.1 Admin Console

**Project:** DeckFlow v1.1 Admin Console
**Domain:** Brownfield ASP.NET 10 MVC admin console extension (single-operator, BasicAuth, Postgres-backed)
**Researched:** 2026-05-02
**Confidence:** HIGH

---

## Executive Summary

The v1.1 Admin Console is a brownfield extension of an existing deployed ASP.NET 10 / Razor / Postgres app. It adds four admin sections (Landing Shell, Harvest controls, Analytics, Flags) under the existing BasicAuth path-prefix gate. The architectural approach is conservative: one new NuGet package (NCrontab 3.4.0), all other capabilities built from BCL types and patterns already proven in the codebase (`BackgroundService` + `Channel<T>`, `IMemoryCache`, `IRelationalDialect`, `EnsureSchemaAsync`). No SPA, no charting library, no external scheduler, no third-party feature flag service.

The recommended build order is Shell first, then Harvest + Flags (both depend only on the shell), then Analytics. Shell must come first because all four admin pages reference `_AdminLayout.cshtml` and the sidebar partial — building any feature page without it creates throwaway scaffolding. Feature flags should be wired in Phase 1 because the `feature_flags` table seeds kill-switches for live features (Tagger) that must default-on, and because the Harvest pause flag is an immediate consumer.

The dominant risk is the Phase 4 trap identified in the v1.0 post-mortem: features that pass `dotnet build` and unit tests but fail on the actual Render deployment due to Cloudflare header topology, in-process singleton state, or Postgres network behavior. Each phase must include a mandatory live verification step — not optional UAT — with specific pass/fail criteria defined before coding starts. The second highest risk is silent data corruption from two known SQL dialect divergences (ambiguous upsert column references; `EXISTS` cast mismatch) — every new SQL block must be verified against Postgres before the phase closes.

---

## Key Findings

### Recommended Stack

The stack requires exactly one new NuGet package: **NCrontab 3.4.0** (pure cron expression parser, netstandard1.0, zero runtime overhead). Everything else is built from BCL types and patterns already in the repo. `System.Threading.Channels` (already used by `ArchidektCacheJobService`) handles the harvest job queue and the analytics write-behind buffer. `IMemoryCache` (already registered) handles the feature flag 30-second poll cache. Inline SVG via Razor server-side coordinate math handles sparklines — no Chart.js, no D3.

Rejected alternatives are fully documented in STACK.md. Key rejections: Quartz.NET (heavyweight, own thread pool, own job store — duplicates existing `Channel` pattern), `Microsoft.FeatureManagement.AspNetCore` (Azure App Configuration transitive dep, 400KB+, IConfiguration coupling — 6-10 boolean flags need ~100 lines not a framework), Hangfire (memory leak history, dashboard conflicts, Postgres schema overhead), Chart.js/ApexCharts (JS dependency for sparklines solvable in 15 lines of Razor math).

**New packages:**
- `NCrontab` 3.4.0 — cron expression parsing for harvest scheduler; add to `DeckFlow.Web.csproj` only

**BCL-built capabilities (no new package):**
- Feature flags — `IFeatureFlagStore` + singleton `IFeatureFlagCache` with 30s `PeriodicTimer` poll
- Analytics accumulator — `Channel.CreateBounded<MetricEvent>(2000, DropOldest)` + `BackgroundService` flusher
- Job cancel — `CancellationTokenSource` linked to `stoppingToken`; graceful stop after current deck
- Sparklines — server-rendered `<polyline>` in a Razor partial; `(i, maxVal - counts[i])` coordinate math

### Expected Features

**Must have (table stakes per section):**

Shell:
- `_AdminLayout.cshtml` with sidebar nav (Feedback / Harvest / Analytics / Flags) — all child pages depend on it
- Folder-scoped `Views/Admin/_ViewStart.cshtml` — eliminates per-view `Layout =` assignments
- Active-link indicator via `aria-current="page"` on matching sidebar anchor
- `Views/AdminFeedback/_ViewStart.cshtml` to re-wrap existing feedback page (zero view renames)
- Admin layout CSS in `site-common.css` only — never in any guild theme or `site.css`

Harvest:
- Run-now with preset duration selector (15/30/60 min) POST form
- Active-job status display with 5-second poll (state, decks processed, elapsed, error)
- Cancel active job (graceful — stop after current deck, not mid-HTTP-kill)
- Stats: total decks, total observations, storage size (Postgres + SQLite dialects), in-memory recent-runs log (last 10)
- Top-20 commanders by deck count

Analytics:
- `request_metrics` table + `RequestMetricsMiddleware` (route template key, not raw path; write-behind channel)
- Top routes table with time-window filter (today / 7d / 30d)
- Daily sparkline per route (inline SVG, server-rendered)

Flags:
- `feature_flags` table with seed rows for all kill-switch flags (must default-on when row missing)
- List all flags with inline bool toggle (`[ValidateAntiForgeryToken]` on every POST)
- Hot reload: 30s poll cache + explicit `IMemoryCache.Remove` on admin write (not TTL-expiry wait)
- New flag creation (key + bool + description); flag deletion with confirm step

**Should have (add after validation):**
- Persist harvest run history to Postgres `harvest_runs` table — in-memory history lost on Render redeploy
- Single Archidekt URL harvest — new `EnqueueSingleUrlAsync` on `IArchidektCacheJobService`
- Interval/cron harvest schedule — friendly "Every N hours" picker stored in `harvest_schedules` table
- Flags audit log (`flag_audit_log` table — append on every update)
- String/int flag types beyond bool

**Defer to v1.2+:**
- Referer breakdown on analytics (needs column + backfill plan)
- Scheduled flag flips (significant complexity, no immediate use case)
- Sidebar status badges with live counts (polish, not blocking)
- Collapsible sidebar, breadcrumbs, role-based nav visibility (anti-features)

### Architecture Approach

The admin console extends the existing Controller-per-feature MVC pattern with one new layout layer and three new service abstractions. `_AdminLayout.cshtml` + folder-scoped `_ViewStart.cshtml` replace the public layout for all admin views without touching the root `_ViewStart.cshtml`. A new `IHarvestAdminService` wrapper isolates the admin controller from `IArchidektCacheJobService` internals — the controller gets a clean interface for RunNow / Cancel / Stats / Schedule. `IFeatureFlagCache` is a singleton holding an `ImmutableDictionary<string, bool>` snapshot refreshed every 30 seconds by a `BackgroundService`; flag reads are synchronous, zero I/O on the hot path. `RequestMetricsMiddleware` sits between `UseSerilogRequestLogging()` and `UseAuthorization()`, writes to a bounded `Channel<MetricEvent>` without awaiting, and the `RequestMetricsFlushService` drains and batch-INSERTs every 5 seconds.

**Major components:**
1. `Views/Shared/_AdminLayout.cshtml` + `_AdminSidebar.cshtml` — layout shell; folder `_ViewStart` eliminates per-view overrides
2. `IHarvestAdminService` / `HarvestAdminService` — wraps `IArchidektCacheJobService` + `ICategoryKnowledgeStore` + `IHarvestRunStore`; sole harvest controller dependency
3. `RequestMetricsMiddleware` + `RequestMetricsFlushService` — write-behind analytics accumulator; positioned after `UseRouting()` so route template is available
4. `IFeatureFlagCache` / `FeatureFlagCache` + `FeatureFlagRefreshService` — singleton flag dict, 30s background refresh, immediate invalidation on admin write
5. Four new Postgres tables (`harvest_runs`, `feature_flags`, `request_metrics`, `harvest_schedules`) following `EnsureSchemaAsync` + dual-dialect SQL pattern from existing stores
6. Three new admin controllers under `[Route("Admin/...")]` — covered by existing `UseWhen(/Admin)` BasicAuth branch with zero changes to auth wiring

**Existing classes touched (minimal):**
- `ArchidektCacheJobService` — add `CancelCurrentJobAsync()` + per-job `CancellationTokenSource` (Phase 2)
- `DeckFlowDatabaseConnectionFactory` — add `CreateAdminConnection()` reusing same DB (Phase 2)
- `Program.cs` — register new services, add `UseMiddleware<RequestMetricsMiddleware>()`, extend `ValidateDatabaseConnectionsAsync` (Phases 2-4)
- `site-common.css` — add `.admin-shell`, `.admin-sidebar`, `.admin-content` layout rules (Phase 1 only)

### Critical Pitfalls

1. **Phase 4 trap — static checks pass, live fails (G1)** — Define a live verification step before coding each phase. Four mandatory live checks: analytics IP reads `CF-Connecting-IP` (not "unknown"), flag cache invalidates within 2 seconds of admin write, harvest cancel transitions to Failed within one HTTP timeout budget, cron schedule fires within 60 seconds of scheduled UTC time. If a live check fails, stop and replan — do not press forward as in v1.0 Phase 4.

2. **Guild theme CSS leaking into admin pages (A1)** — `_AdminLayout.cshtml` must load only `site-common.css` plus a neutral `admin.css`; never reference `site.css` or any guild stylesheet. Admin layout selectors go in `site-common.css` only. Verify by loading `/Admin` under three guild themes and confirming `--accent-strong` is not a guild-specific hue.

3. **Analytics route cardinality blow-up (C1 + C2)** — Use `RouteData.Values[controller]/[action]` as the route key, never raw `Request.Path`. Direct `await INSERT` per request is forbidden — always write-behind channel. Both errors combine into a table with millions of near-unique rows and visible p95 latency regression.

4. **Feature flag default-off kills live feature (D3 + D2)** — Kill-switch flags for live features (Tagger) must default-on when the row is missing. `EnsureSchemaAsync` must seed these rows with `INSERT ... ON CONFLICT DO NOTHING`. Cache must be explicitly invalidated (`IMemoryCache.Remove`) on admin write — do not wait for 30-second TTL expiry.

5. **Archidekt importer's legacy Polly loop swallowing cancellation (B3)** — `ArchidektApiDeckImporter` uses legacy `AsyncRetryPolicy` directly (CLAUDE.md-confirmed). Audit cancellation token threading before implementing harvest cancel. Hard pre-condition: without it, operator cancel waits for all retries on the current card batch to complete before the job stops.

6. **SQLite/Postgres SQL dialect divergence (E2)** — Known project pattern: qualify upsert columns with table name (`page_hits.hit_count + EXCLUDED.hit_count`); use `COUNT(1)` not `EXISTS`. Local SQLite passes both; Postgres rejects ambiguous references at runtime. Every new SQL block requires Postgres verification before the phase closes.

---

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Admin Shell + Flags Foundation

**Rationale:** All four admin pages depend on `_AdminLayout.cshtml` and the sidebar partial — no child page can ship without it without creating throwaway scaffolding. The `feature_flags` table comes here too: kill-switch seed rows gate live features (Tagger is live in v1.0), and the harvest pause flag is a near-immediate consumer. Stub actions for all sidebar links must exist by end of this phase to avoid guild-themed 404s behind BasicAuth.

**Delivers:**
- `_AdminLayout.cshtml` + `_AdminSidebar.cshtml` + `Views/Admin/_ViewStart.cshtml`
- Admin landing page (`/Admin`) with stub actions for all sidebar sections
- `Views/AdminFeedback/_ViewStart.cshtml` — re-wraps existing feedback in new shell (zero view renames)
- `site-common.css` admin layout selectors (`.admin-shell`, `.admin-sidebar`, `.admin-content`)
- `feature_flags` table + `IFeatureFlagStore` + `IFeatureFlagCache` (30s poll) + `FeatureFlagRefreshService`
- `/Admin/Flags` list + bool toggle UI with `[ValidateAntiForgeryToken]` — establishes antiforgery pattern for all subsequent admin forms
- Seed rows for all kill-switch flags in `EnsureSchemaAsync`

**Avoids:** A1 (guild CSS leak), A2 (dead sidebar links), A3 (BasicAuth bypass), F1 (admin form CSRF), D3 (default-off kills Tagger)

**Live verification:** Load `/Admin` under 3 guild themes — neutral palette confirmed. GET every sidebar link — 200. `curl` without credentials — 401. Disable flag via admin, reload public page within 2 seconds — feature off.

---

### Phase 2: Harvest Controls + Stats

**Rationale:** Harvest is the most operationally critical section (the only way to populate the knowledge base) and the most architecturally complex (per-job CTS, service wrapper, schedule storage, background scheduler). Doing it while Phase 1 context is fresh reduces re-read cost. Stats ship with controls — the in-memory recent-runs log requires no new DB table for the table-stakes version, but `harvest_runs` persistence is included here because Render redeploys wipe in-memory state.

**Delivers:**
- `IHarvestAdminService` + `HarvestAdminService` wrapper (RunNow / Cancel / Stats / Schedule)
- `HarvestAdminController` + `Views/Admin/Harvest.cshtml`
- Run-now POST form (preset 15/30/60 min durations); active-job status (5-second JS poll)
- Cancel support: `CancelCurrentJobAsync()` on `IArchidektCacheJobService` + per-job `CancellationTokenSource`
- Stats panel: total decks, observations, storage size (both dialects), top-20 commanders
- `harvest_runs` table + `IHarvestRunStore` (persist run history across Render redeploys)
- `harvest_schedules` single-row table + `HarvestSchedulerService` + friendly "Every N hours" picker
- `DeckFlowDatabaseConnectionFactory.CreateAdminConnection()` (reuses same DB)
- New stores added to `ValidateDatabaseConnectionsAsync`

**Pre-condition:** Audit `ArchidektApiDeckImporter` cancellation token threading before designing cancel UI (pitfall B3).

**Avoids:** B1 (orphaned task on redeploy), B2 (double-run cron race), B3 (cancel not propagating), B4 (cron UTC foot-gun), B5 (cron string injection), E1 (new stores missing from startup validation), E2 (SQL dialect divergence), E3 (ALTER TABLE without IF NOT EXISTS)

**Live verification:** Start harvest, push deploy, confirm clean exit within 30s. Start harvest, cancel, confirm `Failed` within one HTTP timeout. Set cron 2 minutes ahead, confirm fires within 60 seconds.

---

### Phase 3: Analytics

**Rationale:** Analytics depends only on the shell (Phase 1) and is independent of Harvest and Flags. Placing it after Harvest means the middleware captures real job-trigger data from day one, giving non-trivial data to validate against. All analytics pitfalls (C1-C4) are detectable on day one if live verification criteria are defined before coding.

**Delivers:**
- `RequestMetricsMiddleware` + `RequestMetricsFlushService` (write-behind bounded channel)
- `request_metrics` table + `IRequestMetricsStore` (dual-dialect; route template key; hashed IP)
- `AnalyticsAdminController` + `Views/Admin/Analytics.cshtml`
- Top routes table with time-window filter (today / 7d / 30d)
- Daily sparkline per route (server-rendered `<polyline>`, no JS library)
- Middleware registered after `UseSerilogRequestLogging()`, before `UseAuthorization()`
- Store added to `ValidateDatabaseConnectionsAsync`

**Avoids:** C1 (high-cardinality route keys), C2 (per-request synchronous DB write), C3 (static assets in analytics), C4 (raw IP / PII), E1 (startup validation gap), E2 (SQL dialect divergence)

**Live verification:** After 5 minutes of use, `SELECT DISTINCT route_key FROM request_metrics LIMIT 20` — confirm template strings not literal paths. `SELECT COUNT(*) FROM request_metrics WHERE route_key LIKE '%css%'` — must be 0. Confirm `ip_hash` column only; no raw IPs visible. p95 on Render dashboard must not regress vs pre-analytics baseline.

---

### Phase 4: Deferred Polish

**Rationale:** Items that require Phases 1-3 stable and data-populated before they add value. The Phase 4 label is a deliberate callback to the v1.0 post-mortem — every item here still requires live verification, not just `dotnet build clean`.

**Candidates (P2/P3 from FEATURES.md):**
- Flags audit log (`flag_audit_log` table)
- String/int flag types beyond bool
- Sidebar status badges (live job count, unread feedback count)
- Referer breakdown on analytics (column addition + backfill plan)
- Error-rate trend across last N harvest runs (requires persisted `harvest_runs` data)

---

### Phase Ordering Rationale

- Shell first: `_AdminLayout` + sidebar is a shared dependency for all four admin sections. No child page can ship without it.
- Flags in Phase 1: Kill-switch seed rows gate live features already in production. Simplest new schema, immediate operational value.
- Harvest second: Highest operational value, highest complexity, benefits from fresh context. Stats and cancel ship together.
- Analytics third: Independent of Harvest/Flags. Placing it after Harvest means real job-trigger data exists for validation.
- Phase 4 polish last: Requires Phases 1-3 stable and data-populated before trend/audit features are worth building.

### Research Flags

**Phases needing deeper investigation during planning:**

- **Phase 2 (pre-condition):** Audit `ArchidektApiDeckImporter` cancellation token threading before designing cancel UI. Code-read task — hard pre-condition for cancel reliability (pitfall B3).
- **Phase 2 (cron sub-task):** `HarvestSchedulerService` combining `Channel` reads with 60-second ticks needs a concrete implementation plan before coding. STACK.md pseudo-pattern is a starting point.
- **Phase 3 (middleware position):** Exact insertion point in `Program.cs` pipeline needs confirmation against current line numbers before writing the registration.

**Phases with standard patterns (skip research-phase):**

- **Phase 1 (shell + flags):** Razor folder-scoped `_ViewStart`, `[ValidateAntiForgeryToken]`, `EnsureSchemaAsync` pattern, `IMemoryCache` wrapping — all established in existing stores/views.
- **Phase 3 (sparklines):** SVG coordinate math is fully specified in STACK.md. Zero additional research needed.
- **Phase 4 (polish):** Audit log and string flag types are additive schema changes following identical patterns to Phase 1 flags work.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Single new package (NCrontab); all other patterns derived from live codebase. Rejections documented with version-specific rationale. |
| Features | HIGH | Derived from live codebase contracts + operator constraints + explicit anti-feature reasoning. Priority matrix grounded in actual `IArchidektCacheJobService` interface gaps. |
| Architecture | HIGH | All findings from direct source reading. File paths, DI patterns, pipeline positions sourced from actual `Program.cs` and existing store implementations. |
| Pitfalls | HIGH | G1 is a live post-mortem. B3 flagged in CLAUDE.md. E2 is in project memory. D3 is a real production failure class. |

**Overall confidence: HIGH**

### Gaps to Address

- **B3 pre-condition:** Cancellation token threading in `ArchidektApiDeckImporter` must be confirmed before Phase 2 cancel design. Could change the cancel implementation approach if the token is not threaded through the legacy `AsyncRetryPolicy`.
- **Cron pattern scope:** v1.1 cron limited to three patterns (`0 * * * *`, `0 H * * *`, `0 H * * D`). Document the supported set before the Phase 2 cron sub-task.
- **`harvest_schedules` vs `feature_flags` row:** STACK.md originally suggested a feature flag row; ARCHITECTURE.md corrects this to a dedicated table with typed columns. Confirm at Phase 2 planning start.
- **Analytics p95 baseline:** No pre-analytics latency baseline exists. Capture Render dashboard baseline before deploying Phase 3 middleware to make "must not regress" verifiable.

---

## Sources

### Primary (HIGH confidence — live codebase)
- `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — existing job contract, channel pattern, cancellation threading
- `DeckFlow.Web/Program.cs` (lines 301, 313-314, 330-332, 421-438) — middleware pipeline ordering, `ValidateDatabaseConnectionsAsync`, `UseWhen` admin gate
- `DeckFlow.Web/Services/FeedbackStore.cs` / `AdminBruteForceTrackerStore.cs` — `EnsureSchemaAsync` pattern, dual-dialect SQL blocks
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` — `DeriveAdminPartitionKey`, `CF-Connecting-IP` read pattern
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — legacy `AsyncRetryPolicy` (CLAUDE.md-confirmed gap)
- `.planning/PROJECT.md` — constraints, out-of-scope items, v1.0 post-mortem Key Decisions table

### Secondary (HIGH confidence — official NuGet/docs)
- https://www.nuget.org/packages/NCrontab/ — v3.4.0, netstandard1.0 confirmed
- https://www.nuget.org/packages/quartz/ — v3.18.1 rejection rationale
- https://www.nuget.org/packages/Microsoft.FeatureManagement.AspNetCore/ — v4.5.0 rejection rationale
- https://alexplescan.com/posts/2023/07/08/easy-svg-sparklines/ — sparkline SVG coordinate math (verified)
- https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/ — Channel write-behind pattern

### Tertiary (project memory)
- `feedback_sqlite_postgres_sql_divergence.md` — confirmed EXISTS cast + ambiguous upsert column patterns
- v1.0 Phase 4 post-mortem (`04-ABANDONED.md`) — Phase 4 trap source documentation
- CLAUDE.md architecture note on `ArchidektApiDeckImporter` legacy Polly — B3 pitfall source

---
*Research completed: 2026-05-02*
*Ready for roadmap: yes*
