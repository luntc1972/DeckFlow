# Feature Research — v1.1 Admin Console

**Domain:** Single-operator admin console (BasicAuth), ASP.NET 10 + Razor, Postgres-backed
**Researched:** 2026-05-02
**Confidence:** HIGH — derived from live codebase, existing service contracts, and known operator constraints

---

## A. /Admin Landing Shell

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Sidebar nav with links to Feedback / Harvest / Analytics / Flags | Any admin shell has persistent section navigation | LOW | Vertical `<nav>` in a two-column layout; active page gets `.is-active` class |
| Active-page indicator | Without it the operator doesn't know where they are | LOW | CSS class on matching `<a>` — compare `Request.Path` in partial |
| Section heading / page title area in main content | Standard chrome; establishes hierarchy | LOW | `<h1>` in each child view's content area |
| Shared admin `_Layout` (separate from public layout) | Admin pages should not show theme picker, public nav, or footer; reduces visual noise | MEDIUM | New `_AdminLayout.cshtml`; `Layout = "_AdminLayout"` in each admin view |
| BasicAuth gate carries over all admin routes | Already exists; must remain consistent | LOW | Existing `BasicAuthMiddleware` branch on `/Admin/*` covers all new routes automatically |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Section-level status badges in sidebar (e.g. "2 new" on Feedback, "running" on Harvest) | Operator sees at-a-glance what needs attention without navigating each page | MEDIUM | Sidebar partial accepts a view-model with per-section counts; injected via `IViewComponentResult` or passed from a shared base controller |
| Future-slot placeholder nav items (greyed-out) | Communicates roadmap without dead links; prevents "where does X go?" confusion | LOW | `<a class="admin-nav__link is-disabled" aria-disabled="true">` with tooltip |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Collapsible sidebar | "Saves space" | Single operator on desktop; adds JS toggle state, CSS transitions, breakpoint edge cases for no real gain | Fixed-width sidebar (~180px); hide on mobile via `site-mobile.css` override |
| Breadcrumb trail | Feels "proper admin" | With only 4 sections at one level deep, breadcrumbs are noise | Page `<h1>` + sidebar active indicator is sufficient |
| Role-based visibility of nav items | Looks forward-thinking | Multi-user RBAC explicitly out of scope for v1.1; single operator, one auth level | Leave all nav items visible; revisit if multi-user admin added |
| Top horizontal nav instead of sidebar | Familiar from public layout | Only 4–6 items now but admin sections will grow; horizontal nav doesn't scale past ~6 | Sidebar scales to 15+ items without layout change |

---

## B. /Admin/Harvest — Controls

### Current service contract (what already exists)

`IArchidektCacheJobService.EnqueueAsync(TimeSpan duration)` — max 1 hour, rejects if job already active/queued. Returns whether a new job started. No cancel, no pause, no cron, no single-URL path currently exist on the interface.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Run-now button with preset duration selector (15 / 30 / 60 min) | Operator needs one-click harvest with sensible cap; presets cover 95% of cases | LOW | `<select>` + POST form; presets map to `TimeSpan`; existing `EnqueueAsync` already enforces ≤1h cap |
| "Already running" guard with current-job status displayed | Prevents accidental double-submit; service already rejects duplicates | LOW | Poll or page-reload to show active job state; display `ArchidektCacheJobStatus` fields |
| Cancel active job | Operator must be able to stop a runaway harvest | MEDIUM | Needs a `CancelJobAsync(Guid)` on `IArchidektCacheJobService`; internally fires a `CancellationTokenSource` linked to the job; **graceful stop after current deck** (not mid-HTTP kill) is the right semantic — avoids partial deck writes |
| Job status display (state, decks processed, elapsed time, error message) | Without it the operator can't tell if harvest is working | LOW | Surface `ArchidektCacheJobStatus` fields already on the record; auto-refresh via `<meta http-equiv="refresh">` or a 5s JS `fetch` poll is sufficient — no SSE needed |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Single Archidekt URL harvest | Operator can seed one specific deck/commander without running a full sweep | HIGH | Needs new `EnqueueSingleUrlAsync(string url, TimeSpan? timeout)` path in `IArchidektCacheJobService`; URL validated against Archidekt domain before enqueue |
| Cron schedule (friendly "every N hours" picker) | Reduces manual run-now clicks for regular harvests | HIGH | Store schedule in `feature_flags` table or new `harvest_schedule` row; `IHostedService` timer checks schedule; friendly picker (Every 2h / 4h / 8h / 24h / Off) generates a stored interval, NOT a free-form crontab string |
| Pause/resume schedule (not pause in-flight run) | Lets operator freeze overnight harvests without deleting schedule | LOW | Single boolean `harvest_paused` flag; pause means "skip next scheduled fire"; current in-flight run continues |
| Duration cap free-text input | Power-user escape hatch beyond 15/30/60 | LOW | Add optional number input beside preset `<select>`; only one of preset or custom sent; validate server-side against 1h cap |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Free-form crontab string input | "Maximum flexibility" | Single operator; crontab syntax is error-prone; produces support burden; hard to display "next run" cleanly | Friendly interval picker (Every N hours / Off); covers all real ops needs |
| Mid-HTTP-request kill (hard cancel) | "Instant stop" | Kills in-flight Archidekt API call mid-response; risks partial deck write to `category_knowledge` DB; RestSharp + Polly retry may ghost-retry after kill | Graceful stop: set a cancellation flag checked between deck iterations; current deck finishes, then job exits; display "stopping…" state |
| Pause in-flight run | "Freeze and resume later" | In-process state cannot survive app restart (Render redeploys); resuming mid-sweep requires checkpoint persistence | Pause schedule only; let current run finish; restart is cheap |
| Multiple concurrent jobs | "Parallel harvest" | Single Render Starter tier (512MB RAM); concurrent Archidekt API calls risk rate-limiting; existing service serializes by design | Queue one job; second enqueue returns existing-job status |

---

## C. /Admin/Harvest — Stats

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Total decks processed (all-time) | Baseline health metric | LOW | `ICategoryKnowledgeStore.GetProcessedDeckCountAsync()` already exists |
| Total unique cards / observations in DB | Shows knowledge base size | LOW | Add `GetObservationCountAsync()` to `ICategoryKnowledgeStore`; single `COUNT(1)` |
| Recent runs log (last 10: started, duration, decks processed, state, error) | Operator needs audit trail without reading logs | MEDIUM | In-memory `ConcurrentDictionary<Guid, ArchidektCacheJobStatus>` in `ArchidektCacheJobService` already holds job records; expose last-N sorted by `RequestedUtc` desc; persist across restarts = defer (Postgres `harvest_runs` table is a differentiator) |
| Last run timestamp + next scheduled run | Operators check "did it run?" first | LOW | Derive from recent-runs log + stored schedule interval |
| Storage size (MB used by `category_knowledge` table) | Render Basic Postgres has 256MB limit; operator must know when they're approaching it | MEDIUM | `SELECT pg_size_pretty(pg_total_relation_size('category_knowledge'))` for Postgres; `PRAGMA page_count * page_size` for SQLite fallback; exposed via `IRelationalDialect` |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Top-N commanders by deck count | Shows what the knowledge base knows best; helps operator decide which commanders to harvest more | MEDIUM | `SELECT commander, COUNT(*) FROM category_knowledge GROUP BY commander ORDER BY COUNT(*) DESC LIMIT 20`; simple table render |
| Harvest velocity (decks/min for last run) | Lets operator tune duration caps based on actual throughput | LOW | `DecksProcessed / elapsed_minutes` from `ArchidektCacheJobStatus`; display in recent-runs log |
| Persist run history to Postgres (`harvest_runs` table) | In-memory history lost on redeploy; Render redeploys on every push | MEDIUM | New `harvest_runs` table (job_id, state, started_utc, completed_utc, decks_processed, error_msg); written by `ArchidektCacheJobService` on job completion; enables trend display |
| Error-rate trend across last 10 runs | Early warning for Archidekt API changes or rate-limit degradation | LOW | Derived from persisted run history; count failed vs succeeded; only useful after persist is built |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Per-commander deck-count distribution chart (visual) | "Nice to see" | Requires a JS charting library (Chart.js etc.) — conflicts with "minimal JS, no SPA" constraint; adds a dependency for marginal ops value | Plain sorted table of top commanders is sufficient; operator can export to spreadsheet if they want charts |
| Real-time streaming progress bar | "Watch it work" | SSE or WebSocket for a single-operator admin adds infra complexity; Render Starter doesn't support persistent connections well | 5-second meta-refresh or lightweight JS `fetch` poll of `/Admin/harvest/status` JSON endpoint; shows decks-processed counter |
| Historical DB size trend graph | "Proactive capacity planning" | One operator, one Postgres instance; manual check is sufficient | Show current size + warn at 80% of 256MB threshold |

---

## D. /Admin/Analytics

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Per-route page view counts (route + day + hit count) | Basic usage insight; answers "is anyone using X?" | MEDIUM | New `page_views` table (`route TEXT, date DATE, hits INT`); `ON CONFLICT DO UPDATE` upsert per request; increment via middleware or base controller filter |
| Unique IP count per route per day | Distinguishes one power user from 100 casual users | MEDIUM | Hash+salt IP before storing (GDPR hygiene, same pattern as existing feedback IP salt); `unique_ips INT` column alongside `hits` |
| Time-window filter (today / last 7d / last 30d) | Without filter, all-time aggregates obscure recent trends | LOW | GET query param `?window=7`; SQL `WHERE date >= NOW() - INTERVAL '7 days'` |
| Error rate per route (4xx + 5xx count) | Shows broken pages before users complain | MEDIUM | Add `errors INT` column to `page_views`; increment in exception handler or result filter on non-2xx |
| Top routes table sorted by hits desc | Default view; answers "what do people actually use?" | LOW | Single `SELECT route, SUM(hits), SUM(unique_ips) FROM page_views WHERE ... GROUP BY route ORDER BY SUM(hits) DESC` |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Daily sparkline per route (last 7 days) | Trend at a glance; is usage growing or dying? | MEDIUM | 7 data points per route; render as inline SVG `<polyline>` — no JS charting library needed; pure Razor + CSS |
| Referer breakdown (Archidekt vs Moxfield vs direct) | Shows which import source drives engagement; informs which integrations to invest in | MEDIUM | Store `referer_bucket` (archidekt / moxfield / direct / other) derived from `Referer` header at log time; group by in analytics query |
| Admin-visible "zero-use routes" list | Routes with 0 hits in last 30d are candidates for deprecation | LOW | Filter where `SUM(hits) = 0` in window; simple callout section on analytics page |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Per-IP session drill-down | "See what a specific user did" | Single-operator tool; PII exposure risk even with hashed IPs; disproportionate complexity for ops value | Aggregate unique-IP count per route is sufficient; raw session replay is out of scope |
| p95 response time tracking | "Performance monitoring" | Requires timing middleware + percentile aggregation; Render dashboard already shows response-time metrics | Use Render dashboard for latency; admin analytics covers usage patterns, not perf profiling |
| Google Analytics / external beacon | "Industry standard" | Public repo constraint — embedding analytics means a third-party script on a tool handling user deck data; trust issue; also adds CSP complexity | Self-hosted `page_views` table is cleaner, privacy-respecting, and already on-stack |
| Real-time hit counter | "Live dashboard" | WebSocket/SSE on Render Starter; complexity for zero marginal value to a single operator checking once a day | Daily-granularity data is sufficient; page refreshes on demand |

---

## E. /Admin/Flags

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| List all flags (key, type, current value, last modified) | Core table; no flags page without this | LOW | `feature_flags` table: `key TEXT PK, value_text TEXT, last_modified_utc TIMESTAMPTZ` |
| Toggle bool flags inline (on/off) | Bool flags (kill switches, feature gates) are the primary use case | LOW | POST form per flag row; `ValidateAntiForgeryToken`; update `value_text` + `last_modified_utc` |
| Hot reload — app reads flag value without restart | Core value of runtime flags | MEDIUM | `IMemoryCache` with short TTL (30s) wrapping a `SELECT` from `feature_flags`; or polling `IHostedService` refreshing a singleton; flag consumers call `IFeatureFlagService.IsEnabled(string key)` |
| Flag descriptions / purpose notes | Operator needs to know what each flag does 3 months later | LOW | `description TEXT` column on `feature_flags`; shown in list; editable inline or via seed migration |
| New flag creation (key + bool value + description) | Flags need to be created without a deployment | MEDIUM | POST form; server-side validation: key alphanumeric+hyphens, no spaces; duplicate key = 400 with error |
| Flag deletion (with confirmation) | Deprecated flags accumulate otherwise | LOW | POST with hidden `_method` or separate form; confirm step via `data-confirm` attribute + simple JS; alternatively a separate DELETE form |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Audit log (who set what, when, previous value) | Single operator today, but "why is Tagger disabled?" is a real question 6 weeks after the fact | MEDIUM | `flag_audit_log` table (`key, old_value, new_value, changed_utc`); append on every update; show last-5 changes per flag in expanded row |
| String/integer flag types (beyond bool) | Rate-limit caps, max-deck-count thresholds configurable at runtime | MEDIUM | `flag_type ENUM('bool','string','int')` column; UI renders appropriate input per type; `IFeatureFlagService` has `GetString(key)` / `GetInt(key)` overloads |
| Scheduled flip (enable at time T, disable at time T+N) | Planned maintenance windows; beta feature on/off by date | HIGH | `enabled_from TIMESTAMPTZ, enabled_until TIMESTAMPTZ` nullable columns; flag service checks wall clock; UI shows datetime pickers — adds significant complexity |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Percentage rollout / canary flags | "A/B test features" | No meaningful user segmentation possible with BasicAuth single-operator model; anonymous public users can't be bucketed stably | Kill switches and bool gates cover all v1.1 use cases; revisit if multi-user auth added |
| Scoped flags per role or IP range | "Fine-grained targeting" | RBAC/multi-user is explicitly out of scope; IP scoping adds table complexity and false-positive risk with dynamic IPs | Bool flags with audit log is sufficient for current ops model |
| SDK / client-side flag evaluation | "Flags in TypeScript too" | Requires exposing flag state to browser; public flag names visible in source; security surface for a single-operator tool | Server-side only; Razor views read flag values before render; no client-side SDK needed |
| Flag import/export (JSON dump) | "Backup flags" | Postgres backup covers this; a separate export adds UI complexity for zero gain | Use Render Postgres backup; flags are recoverable from audit log |

---

## Feature Dependencies

```
Admin Landing Shell (_AdminLayout + sidebar partial)
    └──required-by──> /Admin/Harvest (controls + stats)
    └──required-by──> /Admin/Analytics
    └──required-by──> /Admin/Flags
    └──required-by──> /Admin/Feedback (must be re-wrapped in new shell)

/Admin/Harvest persist run history (harvest_runs table)
    └──required-by──> Error-rate trend
    └──required-by──> Last-run / next-run display (accurate after restart)

/Admin/Analytics page_views table + middleware
    └──required-by──> Daily sparkline (needs per-day rows)
    └──required-by──> Referer breakdown (needs referer_bucket column)

/Admin/Flags feature_flags table + IFeatureFlagService
    └──required-by──> Hot reload (IMemoryCache TTL consumer)
    └──required-by──> Audit log (flag_audit_log table, written on update)
    └──enhances──> /Admin/Harvest (pause-schedule flag can be a feature flag row)

IArchidektCacheJobService cancel support
    └──required-by──> Harvest cancel button
    └──independent-of──> Stats panel (stats work without cancel)
```

### Dependency Notes

- **Landing shell first:** `_AdminLayout.cshtml` + sidebar partial must exist before any child page ships. All four admin pages reference it. Feedback re-wrap into the new shell should happen in the same phase as the shell build.
- **Flags table early:** `feature_flags` table should be created in schema migration alongside the shell phase. Harvest pause-schedule and Tagger kill-switch are priority consumers and should not wait for the full Flags UI.
- **Analytics middleware before analytics page:** The `page_views` increment must run before there is any data to display. Ship middleware + table in one phase; display page in the same or next phase.
- **Harvest cancel is independent:** Stats panel (totals, recent runs) can ship before cancel is implemented. Cancel requires interface changes to `IArchidektCacheJobService`.

---

## MVP Definition (v1.1 scope = what ships)

### Phase 1 — Shell + Flags table foundation
- [ ] `_AdminLayout.cshtml` with sidebar nav (Feedback / Harvest / Analytics / Flags) — required-by all
- [ ] Re-wrap `/Admin/Feedback` in new shell — avoids a split look
- [ ] `feature_flags` table migration + `IFeatureFlagService` with 30s cache — unblocks Tagger kill-switch and harvest pause flag immediately
- [ ] Flags list + toggle UI — operators want Tagger kill-switch on day 1

### Phase 2 — Harvest controls + stats
- [ ] Run-now with preset durations (15/30/60 min) POST form — wraps existing `EnqueueAsync`
- [ ] Active job status display (state, decks processed, elapsed, error) — 5s JS poll or meta-refresh
- [ ] Cancel support on `IArchidektCacheJobService` — graceful stop between deck iterations
- [ ] Stats panel: total decks, total observations, storage size (Postgres + SQLite), recent-runs log (in-memory last 10)
- [ ] Top-20 commanders table

### Phase 3 — Analytics
- [ ] `page_views` table + increment middleware (route, date, hits, unique_ip_count, errors)
- [ ] Top routes table with time-window filter
- [ ] Daily sparkline per route (inline SVG, no JS library)

### Add After Validation
- [ ] Persist harvest run history to Postgres `harvest_runs` table — enables trend data post-redeploy
- [ ] Single Archidekt URL harvest — useful but adds new service interface path
- [ ] Cron/interval schedule for harvest — "every N hours" picker + `IHostedService` timer
- [ ] Flags audit log (`flag_audit_log` table)
- [ ] String/integer flag types

### Defer to v1.2+
- [ ] Referer breakdown on analytics — needs column + backfill plan
- [ ] Scheduled flag flips — significant complexity, no immediate use case
- [ ] Sidebar status badges (live job count, unread feedback count) — polish, not blocking

---

## Feature Prioritization Matrix

| Feature | Operator Value | Implementation Cost | Priority |
|---------|---------------|---------------------|----------|
| `_AdminLayout` shell + sidebar | HIGH | LOW | P1 |
| Re-wrap Feedback in shell | HIGH | LOW | P1 |
| `feature_flags` table + hot reload service | HIGH | MEDIUM | P1 |
| Flags list + bool toggle UI | HIGH | LOW | P1 |
| Harvest run-now form (preset durations) | HIGH | LOW | P1 |
| Active job status display | HIGH | LOW | P1 |
| Harvest cancel (graceful) | HIGH | MEDIUM | P1 |
| Harvest stats: totals + storage size | HIGH | MEDIUM | P1 |
| Top-20 commanders table | MEDIUM | LOW | P2 |
| Analytics: page_views middleware + table | HIGH | MEDIUM | P1 |
| Analytics: top routes + time-window filter | HIGH | LOW | P1 |
| Daily sparkline (inline SVG) | MEDIUM | MEDIUM | P2 |
| Persist harvest run history to Postgres | MEDIUM | MEDIUM | P2 |
| Single-URL harvest | MEDIUM | HIGH | P2 |
| Cron/interval harvest schedule | MEDIUM | HIGH | P2 |
| Flags audit log | MEDIUM | MEDIUM | P2 |
| String/int flag types | LOW | MEDIUM | P3 |
| Scheduled flag flips | LOW | HIGH | P3 |
| Sidebar status badges | LOW | MEDIUM | P3 |

---

## Sources

- Live codebase: `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — existing job contract
- Live codebase: `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — existing admin pattern
- Live codebase: `DeckFlow.Web/Views/AdminFeedback/Index.cshtml` — existing admin view structure
- Live codebase: `DeckFlow.Web/Views/Shared/_Layout.cshtml` — public layout baseline
- Live codebase: `DeckFlow.Web/wwwroot/css/site-common.css` — layout token conventions
- `.planning/PROJECT.md` — constraints (Razor-only, 512MB RAM cap, single-operator BasicAuth, no SPA, public repo)

---
*Feature research for: v1.1 Admin Console (DeckFlow)*
*Researched: 2026-05-02*
