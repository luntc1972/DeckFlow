# Phase 8: Analytics — Discussion Log

**Date:** 2026-05-03
**Mode:** default (single-batch — orchestrator asked the highest-leverage question per area in one AskUserQuestion call given operator's just-finished Phase 7.1 emergency fatigue)

This log records the full Q&A path for audit / retrospective. The canonical decisions live in `08-CONTEXT.md`. This file is human-reference only.

---

## Setup

**Domain stated to user:**
> Per-request metrics middleware writing to a Postgres `request_metrics` table via a write-behind buffer; admin page at `/Admin/analytics` with top-routes table + per-row inline SVG sparklines + time-window filter.

**Locked from ANLY-01..06 + 5 SCs (not discussed):** Postgres `request_metrics`; Channel<T> + BackgroundService; FEEDBACK_IP_SALT for IP hashing, no ip_raw column; static-asset exclusion list `/css/*` `/js/*` `/lib/*` `/extensions/*`; inline SVG sparklines no JS lib; today/7d/30d/all-time windows; route templates not raw paths; `/Admin/analytics` URL.

**Carrying forward (presented):** RelationalDatabaseConnection + dialect, EnsureSchemaAsync, BackgroundService + singleton-and-hosted-service, AddDeckFlow{Subsystem}() extension, _AdminLayout.cshtml, BasicAuth gate, **Phase 7.1 lesson on container-startup smoke-test**.

---

## Q1 — Gray-area selection (multi-select)

**Question:** Which gray areas do you want to lock in for Phase 8 Analytics? (Sparkline shape will get a sensible default — bar chart, 14 days, omit empty days.)

**Options presented:**
1. Aggregation grain + retention (Recommended)
2. Route template extraction strategy
3. Error-rate definition
4. Write-behind buffer policy

**User selected:** All four.

**Note:** Sparkline shape was offered as a 5th gray area in the orchestrator's first attempt, but the AskUserQuestion API caps at 4 options. Sparkline shape was downgraded to a default (bar / 14 daily bars / omit empty days, captured as D-18 in CONTEXT) and the four substantive areas were preserved.

---

## Q2 — Aggregation grain + retention

**Question:** request_metrics schema grain + retention?

**Options:**
1. **Per-(route, day) UPSERT (Recommended)** — Middleware UPSERTs `(route_key, day_utc, status_class)` with hit_count++, errors++. Compact (~thousands of rows/day max). Top-routes query is a simple SUM/GROUP BY. Forever-retention is fine; cleanup deferred to v1.2.
2. Per-request rows + 30-day retention — Every request → one row. Rich querying. Forever-retention impractical; needs daily cleanup BackgroundService.
3. Hybrid — raw 7d + rollup 30/90/forever — Most flexible, most code, probably overengineered for current scale.

**User selected:** Option 1 (per-(route, day) UPSERT, recommended).

**Captured as:** D-01..D-04 in CONTEXT.md. D-03 makes the unique-IP side-table call (`request_metric_ip_seen`) since the user's option text said "via a side table or HLL" — orchestrator picked side table as the simpler default. D-04 makes status_class a smallint (2/4/5).

---

## Q3 — Route key extraction

**Question:** route_key extraction — what should the operator see in the dashboard?

**Options:**
1. **Endpoint.DisplayName + fallback (Recommended)** — `HttpContext.GetEndpoint()?.DisplayName`, fallback `__unmatched__`. Matches Serilog request log format the operator already reads.
2. RouteData controller/action only — Cleaner-looking but loses info for attribute-routed actions.
3. RoutePattern.RawText (raw URL template) — Closer to the curl URL but less readable for conventional-routed pages.

**User selected:** Option 1 (Endpoint.DisplayName, recommended).

**Captured as:** D-05, D-06.

---

## Q4 — Error-rate definition

**Question:** Which HTTP responses count toward the error-rate column?

**Options:**
1. 5xx only (Recommended) — True server-fault rate. Cleanest 'are errors spiking' signal.
2. **5xx + 4xx (excluding 404)** — Counts auth failures (401/403), rate-limit (429), validation/antiforgery (400) but excludes 404s. Catches abuse spikes too. Noisier.
3. Any non-2xx/3xx (all errors) — Includes 404s. Probably too noisy from extension probes / scanners.

**User selected:** Option 2 (5xx + 4xx excluding 404) — DEPARTED from recommendation.

**Captured as:** D-07. Implementation: `is_error = status >= 400 && status != 404 && status < 600` at middleware time.

**Why operator picked this over the recommendation:** not stated explicitly, but the operator's pattern (just lived through a Plan 07.1 incident where 403 antiforgery + 403 same-origin failures DID reflect a real bug) supports the call — abuse + auth-failure spikes ARE actionable signal, even though they're noisier than pure 5xx.

---

## Q5 — Write-behind buffer policy

**Question:** Channel<T> capacity + backpressure policy?

**Options:**
1. **Capacity 10k, drop-oldest, flush every 5s or 100 rows (Recommended)** — Lossy under sustained burst >2000 req/s but never blocks request thread.
2. Capacity 1k, block-briefly (200ms), flush every 1s or 50 rows — Less lossy at cost of occasional 200ms outliers.
3. Capacity 100k, drop-newest, flush every 10s or 500 rows — Largest buffer; coarser flush.

**User selected:** Option 1 (10k / drop-oldest / 5s or 100 rows, recommended).

**Captured as:** D-08, D-09. D-10 adds drop-accounting via Serilog WARN every ~60s (orchestrator-added — consistent with existing logging conventions, not a separate user decision).

---

## Orchestrator-Added Decisions (Claude's Discretion)

These were not user-chosen but follow directly from the locked decisions and CLAUDE.md project conventions:

- **D-11:** Static-asset exclusion at middleware top via StartsWithSegments — straightforward implementation of ANLY-06.
- **D-12:** Middleware position AFTER UseRouting() and BEFORE UseEndpoints/MapControllers — required for HttpContext.GetEndpoint() to be populated. Phase 7.1 invariant on UseForwardedHeaders ordering preserved.
- **D-13:** IP capture priority CF-Connecting-IP > X-Forwarded-For first hop > RemoteIpAddress, hashed with FEEDBACK_IP_SALT — leverages today's Phase 7.1 lesson that CF-Connecting-IP is the most trustworthy source under Render+Cloudflare.
- **D-14:** AddDeckFlowAnalytics() extension; IRequestMetricsStore takes IServiceProvider per the post-dc66a38 pattern.
- **D-15:** Container-startup smoke-test required before merge — directly enforces the Phase 7.1 errata lesson.
- **D-16..D-18:** Admin page UI shape (controller, table columns, sparkline shape) — follows Phase 6/7 admin-page conventions and the user's "GitHub contribution graph but inline-row-sized" specific.

---

## Deferred Items (not discussed in depth, captured to backlog)

- ANLY-NEXT-01: Retention/cleanup BackgroundService (revisit when row count > ~100k or storage > 100MB)
- ANLY-NEXT-02: Per-IP session drill-down (PII)
- ANLY-NEXT-03: p95/p99 latency tracking (Render dashboard covers it)
- ANLY-NEXT-04: Referer breakdown
- ANLY-NEXT-05: Outbound-API error-rate summary
- ANLY-NEXT-06: Free-form date-range picker
- ANLY-NEXT-07: Redis / external metrics store

These came from ROADMAP's `## v1.2+ Requirements` section (Analytics polish) — already in the project's deferred backlog. Recorded here for completeness; CONTEXT.md `<deferred>` mirrors them.

---

## Specifics surfaced

- "Sparklines should look like the GitHub contribution graph but inline-row-sized."
- Render dashboard log format is what the operator reads daily; route_key strings should match.
- Phase 7.1 emergency taught us that Render's KnownProxies don't honor X-Forwarded-Proto by default — CF-Connecting-IP is the most trustworthy IP source.

---

## Total turns

- 1 multi-select setup question (Q1)
- 1 batched four-question turn (Q2 + Q3 + Q4 + Q5 in one AskUserQuestion call) — efficient given operator just completed an emergency phase

5 user decisions captured. Orchestrator added 13 derived decisions (D-04 through D-18 minus the user-chosen ones) following from the locked answers.

---

*Phase: 08-analytics*
*Discussion gathered: 2026-05-03*
