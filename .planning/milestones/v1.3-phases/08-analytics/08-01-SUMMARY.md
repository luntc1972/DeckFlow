---
phase: 08-analytics
plan: 01
subsystem: database
tags: [postgres, npgsql, sha256, analytics, ip-hashing, upsert, unnest]

# Dependency graph
requires:
  - phase: 07-harvest
    provides: HarvestRunStore IServiceProvider-lazy-DI pattern (D-14), DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection, RelationalDatabaseConnection abstractions
  - phase: 05-security
    provides: CF-Connecting-IP priority extraction pattern (BUG-02), FeedbackStore SHA-256 IP hashing
provides:
  - IpHasher static helper at DeckFlow.Web/Security/IpHasher.cs — single SHA-256+salt site for all IP hashing (CF-Connecting-IP > XFF > RemoteIpAddress priority)
  - RequestMetricEvent sealed record — immutable analytics event produced by middleware
  - IRequestMetricsStore interface — EnsureSchemaAsync + UpsertBatchAsync contracts
  - RequestMetricsStore Postgres-only store — request_metrics + request_metric_ip_seen schema + unnest bulk UPSERT
affects: [08-02-flusher, 08-03-middleware, 08-04-admin-dashboard, 08-05-di-registration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "IpHasher: static helper centralises CF-Connecting-IP > X-Forwarded-For > RemoteIpAddress priority + SHA-256 + FEEDBACK_IP_SALT resolution"
    - "RequestMetricsStore: mirrors HarvestRunStore IServiceProvider? optional ctor pattern (D-14) to prevent circular DI"
    - "unnest(@arrays) bulk UPSERT: single Postgres round-trip for N events; 4 array parameters instead of 4×N positional"
    - "EnsureSchemaAsync double-check SemaphoreSlim gate: same shape as FeatureFlagStore and HarvestRunStore"

key-files:
  created:
    - DeckFlow.Web/Security/IpHasher.cs
    - DeckFlow.Web/Services/Analytics/RequestMetricEvent.cs
    - DeckFlow.Web/Services/Analytics/IRequestMetricsStore.cs
    - DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs
  modified:
    - DeckFlow.Web/Services/FeedbackStore.cs

key-decisions:
  - "IpHasher.ResolveSaltAsync uses same feedback_meta table as FeedbackStore — single salt row, no new table needed"
  - "RequestMetricsStore.EnsureSchemaAsync silently no-ops on SQLite (sets _schemaReady=true and returns) — analytics is paid-tier Postgres-only per D-01"
  - "ipHashes array typed as string?[] not string[] to allow null entries flowing through unnest; ip_seen UPSERT filters WHERE ip_hash IS NOT NULL AND ip_hash <> ''"
  - "Removed System.Data (unused after System.Security.Cryptography + System.Text removal) from FeedbackStore to maintain 0-warning build"

patterns-established:
  - "Pattern: all IP hashing in DeckFlow.Web goes through IpHasher.Hash — no inline SHA256.HashData calls"
  - "Pattern: analytics store ctor takes IServiceProvider? services = null — do not inject buffer/flusher directly"

requirements-completed: [ANLY-01, ANLY-03]

# Metrics
duration: 25min
completed: 2026-05-03
---

# Phase 8 Plan 01: Analytics Foundation Summary

**IpHasher extracted as single SHA-256+salt+CF-Connecting-IP site; Postgres analytics schema (request_metrics + request_metric_ip_seen) defined with unnest bulk UPSERT contracts**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-03T20:42:00Z
- **Completed:** 2026-05-03T21:07:00Z
- **Tasks:** 2
- **Files modified:** 5 (4 created, 1 modified)

## Accomplishments

- Extracted `IpHasher` from FeedbackStore — CF-Connecting-IP > X-Forwarded-For > RemoteIpAddress priority now has a single implementation, closing duplication risk identified in 08-CONTEXT.md D-13
- Defined `RequestMetricEvent`, `IRequestMetricsStore`, and `RequestMetricsStore` — Wave 2 (flusher) and Wave 3 (middleware) now have stable contracts to build against
- `EnsureSchemaAsync` creates both D-01 tables idempotently; `UpsertBatchAsync` runs both UPSERTs in one Postgres transaction via unnest — no per-row round trips

## Task Commits

1. **Task 1: Extract IpHasher + refactor FeedbackStore** - `3f6835f` (feat)
2. **Task 2: RequestMetricEvent + IRequestMetricsStore + RequestMetricsStore** - `863b21b` (feat)

**Plan metadata:** see docs commit below

## Files Created/Modified

- `DeckFlow.Web/Security/IpHasher.cs` — Static helper: `HashRequestIp` (CF priority), `Hash` (SHA-256+salt), `ResolveSaltAsync` (env > feedback_meta > generate)
- `DeckFlow.Web/Services/Analytics/RequestMetricEvent.cs` — Sealed record: RouteKey, DayUtc, StatusClass (short), IsError, IpHash
- `DeckFlow.Web/Services/Analytics/IRequestMetricsStore.cs` — Interface: EnsureSchemaAsync + UpsertBatchAsync
- `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs` — Postgres-only store with D-14 IServiceProvider? ctor, SemaphoreSlim schema gate, unnest UPSERT
- `DeckFlow.Web/Services/FeedbackStore.cs` — HashIpInternal delegates to IpHasher.Hash; ResolveSaltAsync local method removed; unused usings removed

## Decisions Made

- `IpHasher.ResolveSaltAsync` uses the existing `feedback_meta` table rather than a new analytics-specific table — keeps salt resolution to one row and avoids schema divergence between feedback and analytics IP hashes.
- `RequestMetricsStore.EnsureSchemaAsync` silently no-ops on SQLite (sets `_schemaReady = true`) rather than throwing — local-dev SQLite starts cleanly without analytics tables.
- `ipHashes` array declared as `string?[]` to pass nullable entries through Npgsql unnest; the ip_seen UPSERT filters `WHERE ip_hash IS NOT NULL AND ip_hash <> ''` to skip null/empty entries.

## Deviations from Plan

None — plan executed exactly as written. The `System.Data` using removal from FeedbackStore was a logical extension of the `System.Security.Cryptography` + `System.Text` removals (all three became unused after IpHasher extraction) and was verified before removing.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required. DI registration and startup smoke-test are Wave 5 (plan 08-05).

## Known Stubs

None — this plan delivers contracts and schema DDL only; no UI or data-serving code.

## Threat Flags

None beyond the mitigations already in the plan's threat model (T-08-01 through T-08-04 all addressed):
- Only `ip_hash text` in both tables — no `ip`, `ip_raw`, or `raw_ip` columns.
- All SQL values bound via `NpgsqlParameter` — no string interpolation.

## Self-Check: PASSED

- `DeckFlow.Web/Security/IpHasher.cs` — exists, contains CF-Connecting-IP, SHA256.HashData, ResolveSaltAsync, FEEDBACK_IP_SALT, feedback_meta
- `DeckFlow.Web/Services/Analytics/RequestMetricEvent.cs` — exists
- `DeckFlow.Web/Services/Analytics/IRequestMetricsStore.cs` — exists
- `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs` — exists, contains CREATE TABLE IF NOT EXISTS request_metrics, PRIMARY KEY (route_key, day_utc, status_class), CREATE TABLE IF NOT EXISTS request_metric_ip_seen, PRIMARY KEY (route_key, day_utc, ip_hash), ON CONFLICT ... DO UPDATE, ON CONFLICT ... DO NOTHING, BeginTransactionAsync, IServiceProvider? _services
- `FeedbackStore.cs` — contains IpHasher.Hash, using DeckFlow.Web.Security, no private ResolveSaltAsync
- Build: 0 warnings, 0 errors (verified twice)
- Commits: 3f6835f (Task 1), 863b21b (Task 2)

## Next Phase Readiness

Wave 2 (08-02): flusher Channel + BackgroundService — depends on `IRequestMetricsStore` contract, now locked.
Wave 3 (08-03): analytics middleware — depends on `RequestMetricEvent` record and `IpHasher.HashRequestIp`, both now available.
Wave 5 (08-05): DI registration — `RequestMetricsStore` is registerable; IServiceProvider? ctor prevents circular dependency with flusher.

---
*Phase: 08-analytics*
*Completed: 2026-05-03*
