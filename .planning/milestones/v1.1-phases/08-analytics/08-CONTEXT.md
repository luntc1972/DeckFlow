# Phase 8: Analytics - Context

**Gathered:** 2026-05-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Per-request analytics middleware that records (route_template, day_utc, status_class) hit/error/unique-IP counts into a Postgres `request_metrics` table via a write-behind buffer (bounded `Channel<T>` + `BackgroundService` flusher). Admin-only page at `/Admin/analytics` renders a top-routes table filterable by today / 7d / 30d / all-time, with each row showing hit count, unique-IP count, error rate, and an inline SVG sparkline. No raw IPs stored (hashed via existing `FEEDBACK_IP_SALT`). No JS chart library. Postgres-only — analytics is admin-side and does not need a SQLite path. Phase delivers operator visibility into "which pages are used and where errors are spiking" without leaking PII or paying synchronous DB cost on the request hot path.

**Out of scope (and explicitly so):** per-IP session drill-down (PII concern), p95/p99 latency tracking (Render dashboard covers it), referer breakdown, outbound-API error-rate summary (Scryfall/Tagger/Spellbook), free-form date-range picker, retention/cleanup BackgroundService (deferred to v1.2 — per-(route, day) UPSERT keeps the table compact enough to skip cleanup).

</domain>

<decisions>
## Implementation Decisions

### Schema grain + retention (D-01..D-04)
- **D-01:** **Per-(route_key, day_utc, status_class) UPSERT pattern.** Middleware does one INSERT ... ON CONFLICT (route_key, day_utc, status_class) DO UPDATE SET hit_count = hit_count + 1, error_count = error_count + EXCLUDED.error_count per request. Compact (~thousands of rows/day max for a single-Render-instance app); top-routes query is a simple SUM/GROUP BY over a small table.
- **D-02:** **Forever retention; no cleanup BackgroundService in this phase.** With per-(route, day, status_class) aggregation, table growth is bounded by route cardinality × 3 status classes × days — manageable for years on a Basic-256mb Postgres instance. Cleanup deferred to v1.2 (HARV-NEXT-style polish; track as ANLY-NEXT-01 in REQUIREMENTS deferred section).
- **D-03:** **Unique-IP tracking via a side table** `request_metric_ip_seen(route_key, day_utc, ip_hash)` PRIMARY KEY. Middleware does INSERT ... ON CONFLICT DO NOTHING per request; admin page joins via `SELECT route_key, day_utc, COUNT(*) FROM request_metric_ip_seen GROUP BY ...` to derive unique-IP counts. Simpler than HyperLogLog; no PG extension needed.
- **D-04:** **Status_class is a smallint column** with values 2, 4, 5 (representing 2xx/4xx/5xx). 3xx (redirects) collapse to 2 (treated as success). 1xx informational responses are not recorded (very rare; no operator value).

### Route key extraction (D-05..D-06)
- **D-05:** **`HttpContext.GetEndpoint()?.DisplayName` is the source.** Yields strings like `DeckFlow.Web.Controllers.DeckController.Index (DeckFlow.Web)` for conventional-routed actions and the route template for attribute-routed endpoints. Match the strings already emitted by Serilog request logs the operator has been reading on Render — operator is already familiar with the format.
- **D-06:** **Fallback `__unmatched__` for routes with no Endpoint** (404s for unknown URLs, requests served by static files / favicon-misses that aren't already filtered by D-08). Provides a single bucket for noise. Helps detect spam / scanner traffic without exploding cardinality.

### Error-rate definition (D-07)
- **D-07:** **5xx + 4xx (excluding 404) count as errors** for the error-rate column. 401/403/400/429 represent auth failures, abuse, validation noise, and rate-limit hits — all signals the operator wants to see spike. 404 specifically excluded because it's dominated by extension probes and link-rot, not actionable. Computed at middleware time as `is_error = status >= 400 && status != 404 && status < 600`. This drives `error_count` in the UPSERT and the `error_rate = error_count / hit_count` derived column on the admin page.

### Write-behind buffer policy (D-08..D-10)
- **D-08:** **Channel<RequestMetricEvent> with capacity 10000, FullMode=DropOldest.** On full, oldest unflushed records get dropped (newest preferred). This protects the request hot path under burst — no synchronous wait, no thread blocking. Suits SC #5 (no p95 regression).
- **D-09:** **BackgroundService flusher: trigger on whichever fires first — 100 records buffered OR 5 seconds elapsed since last flush.** Flush opens one transaction, batches all queued events into a single round-trip with parameter-array UPSERT. After flush, resets the elapsed timer.
- **D-10:** **Drop accounting via Serilog WARN every ~60 seconds** (not per-drop — that would itself be a hot-path concern). Flusher tracks dropped_total and emits one structured log line per minute when dropped_total > 0. Operator sees in Render logs that bursts happened without log spam.

### Static asset exclusion (D-11)
- **D-11:** **Filter at middleware top — before Endpoint resolution** — by checking `request.Path.StartsWithSegments("/css")`, `"/js"`, `"/lib"`, `"/extensions"`, OR `"/favicon.ico"`. Plus reject `request.Path == "/_health"` if present. Returns immediately without buffering. Per ANLY-06.

### Middleware position in pipeline (D-12)
- **D-12:** **Analytics middleware sits AFTER `app.UseForwardedHeaders()` (so we read the post-promotion `request.Scheme`/`request.Host`) and AFTER `app.UseStaticFiles()` (so static files don't even reach the analytics check), BUT BEFORE `app.UseRouting()` would not work — we need Endpoint resolution. So: place AFTER `app.UseRouting()` and BEFORE `app.UseEndpoints()`/`app.MapControllers()`** so `HttpContext.GetEndpoint()` is populated. This is the canonical ASP.NET Core middleware position for endpoint-aware logging. The Phase 7.1 invariant on `UseForwardedHeaders` ordering is preserved (analytics runs after, doesn't alter, that middleware).

### IP capture (D-13)
- **D-13:** **Hash `CF-Connecting-IP` if present, else `X-Forwarded-For` first hop, else `request.HttpContext.Connection.RemoteIpAddress`.** Same `FEEDBACK_IP_SALT` env var as `FeedbackStore` (per ANLY-03). Reuse the existing `IpHasher` helper if it exists; otherwise extract one in this phase and have FeedbackStore consume it too (small refactor, in-scope per "shared salt" decision).

### DI registration + service shape (D-14..D-15)
- **D-14:** **`services.AddDeckFlowAnalytics()` extension method** in `DeckFlow.Web/Extensions/AnalyticsServiceCollectionExtensions.cs`. Registers `IRequestMetricsStore` (singleton, takes `IServiceProvider` per Phase 7.1 lesson — NOT taking the buffer/flusher in ctor), `RequestMetricsBuffer` (singleton — wraps Channel), `RequestMetricsFlusher` (BackgroundService — calls `IRequestMetricsStore.UpsertBatchAsync`), and `IpHasher` if extracted.
- **D-15:** **Container-startup smoke-test required before merge.** Phase 7.1 errata established that `dotnet build` clean ≠ DI graph clean. After all services land, run `dotnet run --project DeckFlow.Web` locally and confirm Kestrel reaches "Application started" without `InvalidOperationException`. Push-to-Render-and-watch is the canary if local startup is impractical.

### Admin page UI (D-16..D-18)
- **D-16:** **`/Admin/analytics` controller class:** `AdminAnalyticsController` under `Controllers/Admin/` (BasicAuth-gated by existing middleware). Single `Index(string range = "7d")` action; `range` parameter persists via query string (no cookie/session). Default 7d. Allowed values: today, 7d, 30d, all.
- **D-17:** **Top-routes table sorted by hit_count descending.** Columns: route_key, hits, unique_ips, error_rate (formatted "X.X%"), sparkline (inline SVG). Top 50 rows shown; no pagination in this phase (50 is enough — DeckFlow has <30 distinct routes).
- **D-18:** **Sparkline shape: bar chart, 14 daily bars, omit empty days** (gap rendered for days with zero traffic — operator sees the gap as signal). Width budget per row: ~120px. Color matches admin neutral theme (`var(--admin-fg-muted)` or equivalent). Y-axis: linear, max=row's max-day hit count. No labels, no axes — pure sparkline.

### Claude's Discretion
- Exact UPSERT SQL syntax (Postgres `INSERT ... ON CONFLICT (...) DO UPDATE` is locked; column order, naming, indexes are planner's call)
- Exact SVG path/rect generation algorithm (D-18 sets the contract; the planner picks the rendering approach)
- BackgroundService cancellation semantics on app shutdown (graceful drain of buffer vs immediate stop) — planner's call; lean toward 2-second drain ceiling on `StopAsync`
- Whether `IpHasher` is extracted as a shared helper or stays per-store (depends on what FeedbackStore currently does — minor refactor, in-scope)
- Whether the admin page renders server-side fully or hydrates a small TS bit (server-side is preferred — no SPA framework, matches Phase 6 / 7 admin-page convention)

</decisions>

<specifics>
## Specific Ideas

- "Sparklines should look like the GitHub contribution graph but inline-row-sized — at-a-glance traffic shape per route."
- Operator already reads Render dashboard logs daily; route_key format should match the strings Serilog emits there, no surprise renaming.
- The Phase 7.1 emergency proved Render's KnownProxies don't honor X-Forwarded-Proto. CF-Connecting-IP is set by Cloudflare directly and is the most trustworthy source — prefer it for IP hashing.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/ROADMAP.md` — Phase 8 entry: 5 success criteria + ANLY-01..06 list + out-of-scope items in v1.2+ deferred section
- `.planning/REQUIREMENTS.md` — ANLY-01..06 definitions + traceability table

### Reusable patterns (must read before designing equivalent)
- `.planning/phases/06-admin-shell-flags-foundation/06-02-SUMMARY.md` — `EnsureSchemaAsync` idempotent bootstrap pattern (Postgres + SQLite); request_metrics table will follow the PG path only
- `.planning/phases/06-admin-shell-flags-foundation/06-04-SUMMARY.md` — `BackgroundService` poller pattern (FeatureFlagPoller); RequestMetricsFlusher follows the same shape minus the polling clock — instead it awaits Channel.Reader
- `.planning/phases/07-harvest-controls-stats/07-03-SUMMARY.md` — singleton-and-hosted-service registration pattern (HarvestScheduleCache); RequestMetricsBuffer + Flusher pair uses the same shape
- `.planning/phases/07.1-categories-feature-flag-sameorigin-ajax-fix/07.1-02-SUMMARY.md` — Phase 4/5 invariant on `UseForwardedHeaders` ordering; analytics middleware must sit AFTER it and not modify it. Plus: PaaS proxy + scheme + IP capture lessons.
- `.planning/phases/06-admin-shell-flags-foundation/06-01-SUMMARY.md` — `_AdminLayout.cshtml` neutral admin shell; AdminAnalyticsController views must use it (no guild theme leakage)

### Source files to read before changing
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — schema bootstrap pattern
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` — Postgres + dialect pattern; **also note the IServiceProvider lazy-resolution pattern** post-dc66a38 fix (avoid recreating the circular DI bug)
- `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` — current DI extension shape post-fix; AddDeckFlowAnalytics() mirrors this
- `DeckFlow.Web/Program.cs` — middleware pipeline; analytics middleware position decided in D-12
- `DeckFlow.Web/Services/FeedbackStore.cs` — existing `FEEDBACK_IP_SALT` consumer; share the salt + helper
- `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — admin controller pattern
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — admin controller with stats panel + time-window filter (closest analog to AdminAnalyticsController)

### Project memory (lessons from prior phases)
- `~/.claude/projects/.../memory/feedback_di_optional_dep_does_not_break_cycle.md` — applies directly: D-14 services must not have circular ctor deps; D-15 mandates container-startup smoke-test
- `~/.claude/projects/.../memory/feedback_csrf_validator_under_proxy.md` — applies to D-13: PaaS proxy + IP capture; CF-Connecting-IP is the most trustworthy source
- `~/.claude/projects/.../memory/feedback_sqlite_postgres_sql_divergence.md` — applies if the planner decides analytics needs SQLite-too (it doesn't per D-01..D-04 — PG-only)
- `~/.claude/projects/.../memory/feedback_http_resilience_pattern.md` — N/A here (analytics is intra-process, no outbound HTTP)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EnsureSchemaAsync` pattern with `CREATE TABLE IF NOT EXISTS` + ON CONFLICT seed in `FeatureFlagStore.cs` and `HarvestRunStore.cs` — RequestMetricsStore copies the bootstrap shape (PG path only)
- `BackgroundService` from `Microsoft.Extensions.Hosting` — FeatureFlagPoller and HarvestScheduleService are the two reference implementations; RequestMetricsFlusher follows
- `RelationalDatabaseConnection` + `IRelationalDialect` — but analytics is PG-only, so we can call `Npgsql.NpgsqlConnection` directly OR go through the dialect (planner's call; dialect is more consistent, direct Npgsql is simpler)
- `_AdminLayout.cshtml` and admin neutral CSS — AdminAnalyticsController views inherit
- `AdminHarvestController` time-window filter pattern (last 24h / 7d / 30d on the harvest stats panel) — closest analog; AdminAnalyticsController copies the query-string-based selector
- BasicAuth gate on `/Admin/*` from Phase 6 — covers `/Admin/analytics` automatically; no auth work needed

### Established Patterns
- `services.AddDeckFlow{Subsystem}()` extension method — `AddDeckFlowFeatureFlags()`, `AddDeckFlowHarvest()` precedent; this phase adds `AddDeckFlowAnalytics()`
- Singleton-and-hosted-service for stateful BackgroundServices — register once as singleton via factory, register again via `AddHostedService(sp => sp.GetRequiredService<T>())` so the same instance is reachable for DI consumers AND owns the lifecycle
- Postgres-only feature gating — when a feature is admin-only and PG is the prod store, do not implement a SQLite path. Phase 7's harvest stats panel followed this; Analytics follows it too
- `IServiceProvider` lazy resolution to break ctor cycles (post-dc66a38 fix) — apply preemptively in IRequestMetricsStore so future cache-invalidation extensions don't reintroduce the cycle

### Integration Points
- `Program.cs` middleware pipeline: insert analytics middleware between `app.UseRouting()` and `app.UseEndpoints()`/`MapControllers()`. Phase 7.1 D-12 invariant on `UseForwardedHeaders` ordering preserved.
- `Program.cs` DI block: add `services.AddDeckFlowAnalytics()` call alongside `AddDeckFlowFeatureFlags()` and `AddDeckFlowHarvest()`
- `_AdminSidebar.cshtml` (or whichever partial Phase 6 plan 01 uses) — add `Analytics` link pointing to `/Admin/analytics`
- `FeedbackStore` IP-hashing site — share the helper if extracted as `IpHasher`

</code_context>

<deferred>
## Deferred Ideas

Captured during discussion. NOT in scope for Phase 8.

- **ANLY-NEXT-01: Retention/cleanup BackgroundService** — per-(route, day) UPSERT keeps table compact enough to defer. Revisit when row count > ~100k or storage > 100MB. Add to REQUIREMENTS.md v1.2+ deferred section.
- **ANLY-NEXT-02: Per-IP session drill-down** — explicit PII concern per ROADMAP out-of-scope. Defer with privacy review.
- **ANLY-NEXT-03: p95/p99 latency tracking** — Render dashboard already shows it; not duplicating.
- **ANLY-NEXT-04: Referer breakdown** — useful but separate concern; admin polish, not v1.1.
- **ANLY-NEXT-05: Outbound-API error-rate summary** (Scryfall, Tagger, Spellbook) — useful operational signal but separate from inbound request analytics; future phase.
- **ANLY-NEXT-06: Free-form date-range picker** — today/7d/30d/all is sufficient for v1.1; date-range picker is admin polish.
- **ANLY-NEXT-07: Redis / external metrics store** — current single-Render-instance scale doesn't need it; Postgres is fine.

</deferred>

---

*Phase: 08-analytics*
*Context gathered: 2026-05-03*
