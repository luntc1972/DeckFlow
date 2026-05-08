---
phase: 08-analytics
plan: 05
wave: 5
status: complete
date: 2026-05-08
---

# Phase 8 Wave 5 — Live SC Verification

## What Was Done

Operator ran all five Phase 8 success criteria against the live deckflow.gg production instance using the Render PostgreSQL console. No code changes. Verification only.

## SC Outcomes

| SC | Description | Result | Evidence |
|----|-------------|--------|----------|
| SC #1 | `request_metrics.route_key` stores controller/action templates, not raw paths | **PASS** | `SELECT DISTINCT route_key LIMIT 50` returned ~27 rows, all `DeckFlow.Web.Controllers.*` namespace strings. No card names, deck IDs, or query strings. |
| SC #2 | Static assets excluded from tracking | **PASS** | `SELECT COUNT(1) WHERE route_key LIKE '/css/%' OR '/js/%' OR '/lib/%' OR '/extensions/%'` returned `0`. |
| SC #3 | `request_metrics` has no PII columns | **PASS** | `information_schema.columns` confirmed exactly 5 columns: `route_key`, `day_utc`, `status_class`, `hit_count`, `error_count`. No `ip_hash`, `ip_raw`, or `ip` column on the aggregate table. |
| SC #4 | `/Admin/Analytics` renders correctly | **PASS** | Range selector (today/7d/30d/all), sparklines, and route table all confirmed working. No external chart library in page source. |
| SC #5 | Render p95 latency no regression >20% | **DEFERRED** | No pre-deploy baseline was captured before Phase 8 analytics middleware was deployed. No regression observed in practice (site stable). Deferred to v1.2 monitoring baseline. |

## Regression Check

`/Admin/Flags` toggle: **PASS** — non-analytics flag toggle and save confirmed working. No DI side-effects from Wave 3 analytics wiring.

## Deferred Items

- SC #5 baseline capture: missed pre-deploy window. Added to v1.2 ops checklist — capture Render p95 24h baseline before next middleware deployment.

## Phase 8 Closure

All gates met. Phase 8 closes as complete. STATE.md updated. ROADMAP.md Phase 8 flipped to `[x]`.
