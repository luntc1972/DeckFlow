---
status: complete
quick_id: 260507-ner
date: 2026-05-07
commit: b72f87a
---

## 260507-ner Summary

**Final commit on `main`:** `b72f87a` — `feat(admin-analytics): auto-refresh page when request_metrics revision changes`. Pre-dispatch plan commit at `37c69f7`.

**Pattern source:** mirrors 260507-m8k commit `9698551` (admin-harvest auto-refresh) shipped earlier the same session.

**Files modified (2):**
- `DeckFlow.Web/Controllers/Admin/AdminAnalyticsController.cs` — added `[HttpGet("status")]` action, `GetMetricsRevisionAsync` helper, `AnalyticsStatusPayload` private record. Injected `IMemoryCache`. SameOriginRequestValidator gate preserved; 5s memory-cache TTL matches `RequestMetricsFlusher.FlushInterval`.
- `DeckFlow.Web/Views/AdminAnalytics/Index.cshtml` — added `<noscript><meta http-equiv="refresh" content="60" /></noscript>` and `@section Scripts` block referencing `/js/admin-analytics.js`.

**Files created (1):**
- `DeckFlow.Web/wwwroot/ts/admin-analytics.ts` — always-on 15s poller. Captures `lastRevision` baseline on first poll, `window.location.reload()` on any subsequent change. AbortController + 10s fetch timeout. Non-OK response reschedules at next interval rather than freezing. Hard fetch exception stops polling permanently.

**SQL revision query:**
```sql
SELECT COALESCE(MAX(day_utc)::text, ''), COALESCE(SUM(hit_count), 0)
FROM request_metrics;
```
- No parameters required (read-only aggregate over operator-owned table).
- Postgres-only — SQLite local-dev path returns stable `"|0"` token (no exception, no reload loop).
- Query failure path logs at error level and returns stable `"|err"` token (avoids reload churn on transient hiccups).

**Token format:** `"{maxDay}|{sumHits}"` (e.g., `"2026-05-07|125834"`). Format mirrors harvest's `{startedTicks}|{completedTicks}|{count}` pipe-delimited shape for codebase consistency.

**Why this token works:**
- `request_metrics` schema has NO `updated_utc` column (verified before planning). Schema is `(route_key text, day_utc date, status_class smallint, hit_count bigint, error_count bigint)` PK over the first three.
- `SUM(hit_count)` increments on every flushed batch — request hot-path → buffer → 5s flusher → UPSERT increments hit_count. Token flips whenever new traffic lands.
- `MAX(day_utc)` rolls forward at midnight UTC, providing a natural daily breakpoint.
- Cheap aggregate: `ix_request_metrics_day_utc` index covers MAX; SUM does a full scan but the table is small (admin-only, bounded by route × day × status_class cardinality — hundreds of rows in steady state).

**Verification:**
- **Pass 1 — `dotnet build DeckFlow.sln`:** CLEAN. 0 errors. 10 warnings (all `NU1900` NuGet vulnerability-data fetch — network/firewall, baseline noise unrelated to code).
- **Pass 2 — static contract checks:**
  - `metricsRevision` symbol present in `AdminAnalyticsController.cs` (5 occurrences), `admin-analytics.ts` (3 occurrences), and rebuilt `wwwroot/js/admin-analytics.js` (2 occurrences).
  - `<meta http-equiv="refresh" content="60" />` at `Views/AdminAnalytics/Index.cshtml:7`.
  - `<script src="~/js/admin-analytics.js"` at `Views/AdminAnalytics/Index.cshtml:59`.
  - Route `/Admin/Analytics/status` resolves cleanly: `[HttpGet("status")]` on `[Route("Admin/Analytics")]` controller — no collision with existing `[HttpGet("")]` Index.

**Live verification gate (deferred to operator post-deploy on deckflow.gg):**
1. Load `/Admin/Analytics` (any range — today/7d/30d/all).
2. From a different tab/session, hit any route on the site to generate a request (e.g., `/`, `/sync`, `/lookup`).
3. Wait ≤15s. Original `/Admin/Analytics` tab should auto-reload via `window.location.reload()`.
4. After reload, the Top Routes list should reflect the new request (hit count ticks up on the existing route or a new route appears in the list).
5. Confirm `?range=today` (or whichever range was selected) survives the reload.
6. Disable JS, reload `/Admin/Analytics`, confirm 60s `<noscript>` meta-refresh fires.

**Risks for operator manual verification:**
- `SUM(hit_count)` over `request_metrics` is bounded by route × day × status_class cardinality — hundreds of rows in steady state. If table grows large enough that the SUM becomes slow, switch the token derivation to `(MAX(day_utc), COUNT(1) over request_metric_ip_seen)` or similar lighter aggregate.
- `IMemoryCache` 5s TTL absorbs poll fan-out — even with multiple operator browsers open, the SUM query runs at most once per 5s.
- Range query string preserved by `window.location.reload()` default behavior — no special handling required in the TS poller.
- `IRequestMetricsStore` deliberately not extended — write-only by design (Phase 8 D-15 decision). Revision read goes directly to Postgres in the controller, mirroring `LoadRowsAsync`.

**Deviations from plan:** none material. Codex's local `dotnet build DeckFlow.sln` failed in its sandbox (workload resolver SDK directories missing) but compiled the new TS via direct `tsc -p tsconfig.json`. Orchestrator re-ran `dotnet build DeckFlow.sln` on the host workspace — CLEAN.
