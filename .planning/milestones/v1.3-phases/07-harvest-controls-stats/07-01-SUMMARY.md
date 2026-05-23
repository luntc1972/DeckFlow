---
phase: 07-harvest-controls-stats
plan: 01
subsystem: database
tags: [harvest, persistence, postgres, sqlite, schema, redeploy-survival, feature-flag-seed, d-02-reaper, d-13-invalidation, d-17-commander-name]

# Dependency graph
requires:
  - phase: 06-admin-shell-feature-flags
    provides: FeatureFlagStore seed pattern, RelationalDatabaseConnection plumbing, BasicAuth-gated /Admin shell
provides:
  - IHarvestRunStore + HarvestRunStore (sealed PG/SQLite impl with D-02 startup reaper)
  - IHarvestScheduleStore + HarvestScheduleStore (sealed single-row UPSERT)
  - HarvestRunRow + HarvestScheduleSnapshot record types + HarvestRunKind/HarvestRunState enums
  - Forward-declared IHarvestStatsAggregator interface (Invalidate-only stub; Plan 06 fleshes out with GetAsync)
  - deck_queue.commander_name TEXT NULL column (D-17 additive migration)
  - feature_flags seed row 'harvest.cron.enabled' default-on (B3 — unblocks SC #4)
affects: [07-02-jobservice-pg-migration, 07-03-schedule-cache-and-tick, 07-04-admin-controller-and-views, 07-05-status-ajax-and-ts-poll, 07-06-stats-aggregator-and-panel, 07-07-di-wiring-and-startup]

# Tech tracking
tech-stack:
  added: []  # No new libraries; reuses Microsoft.Data.Sqlite, Npgsql, RelationalDatabaseConnection
  patterns:
    - "Optional cross-plan dependency injection: nullable IHarvestStatsAggregator? stats = null lets Plan 01 ship before Plan 06 defines the consumer"
    - "Forward-declared stub interface co-located with consumer (HarvestRunStore.cs hosts IHarvestStatsAggregator until Plan 06 amends it)"
    - "Schema gate also runs the D-02 reaper inside the same SemaphoreSlim — first call per process atomically reaps orphaned redeploy rows"

key-files:
  created:
    - DeckFlow.Web/Services/Harvest/HarvestRunModels.cs
    - DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs
    - DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs
    - DeckFlow.Web/Services/Harvest/HarvestRunStore.cs
    - DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs
  modified:
    - DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs (added commander_name additive migration)
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs (added harvest.cron.enabled seed row + updated XML doc)

key-decisions:
  - "IHarvestStatsAggregator stub lives in HarvestRunStore.cs (not a separate file) — Plan 06 amends it in place to add GetAsync without breaking Plan 01's Invalidate hook"
  - "harvest_runs uses TEXT for SQLite UUID storage (id.ToString()) and Npgsql native Guid for Postgres — branched at AddParameter sites"
  - "Reaper baked into EnsureSchemaAsync rather than a separate startup task — guarantees orphaned rows are reconciled before any HTTP request lands (atomic with schema bootstrap)"
  - "Both stores share the feedback DB via DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection — same DB file as feature_flags (D-07)"
  - "harvest_schedule single-row enforces id=1 via PK + CHECK constraint; UPSERT can never accidentally create id=2"

patterns-established:
  - "Optional cross-plan DI parameter pattern: trailing nullable interface = null on every ctor variant lets later plans wire dependencies without circular surgery"
  - "Reaper-in-schema-gate pattern: orphan reconciliation runs once per process in the same SemaphoreSlim that bootstraps the table"
  - "FeatureFlagStore-mirror shape: 3 ctor variants (path / RelationalDatabaseConnection / IWebHostEnvironment), volatile _schemaReady fast-path, double-checked SemaphoreSlim gate"

requirements-completed: [HARV-07]

# Metrics
duration: ~25min
completed: 2026-05-03
---

# Phase 07 Plan 01: Harvest Stores Schema + Reaper Summary

**Two new sealed persistence stores (HarvestRunStore + HarvestScheduleStore) with PG/SQLite-branched schema, the D-02 startup reaper baked into EnsureSchemaAsync, the D-17 commander_name additive migration on deck_queue, and the harvest.cron.enabled feature-flag seed default-on so SC #4 is provable on a fresh DB.**

## Performance

- **Duration:** ~25 min (executor wall-clock; parallel worktree)
- **Completed:** 2026-05-03T15:54:46Z
- **Tasks:** 4
- **Files created:** 5
- **Files modified:** 2
- **Commits:** 4 (one per task)

## Accomplishments
- Persistence foundation for every other Phase 7 plan landed: run-store + schedule-store contracts and impls compile cleanly with `dotnet build` exits 0, 0 warnings, 0 errors.
- D-02 startup reaper wired inside `HarvestRunStore.EnsureSchemaAsync` — non-terminal rows (`Queued`/`Running`/`Stopping`) are flipped to `Failed` with `error_message='interrupted by redeploy'` and `completed_utc=now()` on first call per process. Idempotent (zero rows on fresh DB or already-terminal state).
- D-13 explicit cache invalidation hook in place: `HarvestRunStore` ctors take an optional `IHarvestStatsAggregator? stats = null`; both write methods (`InsertQueuedAsync`, `UpdateStateAsync`) call `_stats?.Invalidate()` after the SQL write succeeds. Plan 06 now wires its aggregator without touching this file.
- D-17 `commander_name TEXT NULL` column on `deck_queue` via additive migration (existing rows stay NULL, no backfill).
- B3 unblocked: `harvest.cron.enabled` default-on row added to FeatureFlagStore seed list, preserved across redeploy via `ON CONFLICT (key) DO NOTHING`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Interfaces + record types (HarvestRunModels, IHarvestRunStore, IHarvestScheduleStore)** — `f13b9cc` (feat)
2. **Task 2: Implement HarvestRunStore + HarvestScheduleStore (D-02 reaper, D-13 invalidation)** — `0905f67` (feat)
3. **Task 3: Add commander_name column to deck_queue (D-17 additive migration)** — `1f73320` (feat)
4. **Task 4: Seed harvest.cron.enabled flag default-on (B3)** — `5de8eb4` (feat)

_Note: Tasks were executed without separate test commits — VSTest is unreliable in WSL per CLAUDE.md, so verification relied on `dotnet build DeckFlow.sln` clean exit + grep checks per the plan's `<verify>` blocks. Plans 02-07 will exercise these stores at runtime; integration test fixtures land in Plan 02._

## Files Created/Modified

### Created (Task 1, committed in `f13b9cc`)
- `DeckFlow.Web/Services/Harvest/HarvestRunModels.cs` — `HarvestRunKind` / `HarvestRunState` enums, `HarvestRunRow` + `HarvestScheduleSnapshot` sealed records
- `DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs` — 7-method run-store contract
- `DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs` — 3-method schedule-store contract

### Created (Task 2, committed in `0905f67`)
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` — Sealed PG/SQLite impl. Includes the forward-declared `IHarvestStatsAggregator` stub interface (Plan 06 will amend with `GetAsync` per the plan's Note block). Three ctor variants mirror `FeatureFlagStore`. Schema gate runs CREATE + indexes + reaper UPDATE inside one `SemaphoreSlim`. Two write methods call `_stats?.Invalidate()` after success.
- `DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` — Sealed single-row UPSERT. Schema gate seeds id=1 default-Off (interval=NULL, paused=FALSE) via `ON CONFLICT (id) DO NOTHING`. `GetAsync` throws defensively if seed row missing (should never occur).

### Modified (Task 3, committed in `1f73320`)
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — `EnsureDeckQueueColumnsAsync` gained parallel `hasCommanderName` branch; `ALTER TABLE deck_queue ADD COLUMN commander_name TEXT NULL;` runs once per fresh schema. Existing `skipped` migration untouched.

### Modified (Task 4, committed in `5de8eb4`)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — `PostgresSeedSql` and `SqliteSeedSql` constants gained third row (`'harvest.cron.enabled', TRUE` / `1`). XML doc summary updated to list all three seeded flags.

## Key SQL Strings (provenance for Plans 02-07)

### D-02 reaper (Postgres form)
```sql
UPDATE harvest_runs
   SET state='Failed',
       error_message='interrupted by redeploy',
       completed_utc = now()
 WHERE state IN ('Queued','Running','Stopping');
```
SQLite form swaps `now()` for `datetime('now')`.

### harvest_runs CREATE TABLE (Postgres form)
```sql
CREATE TABLE IF NOT EXISTS harvest_runs (
  id                       UUID PRIMARY KEY,
  kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
  state                    TEXT NOT NULL CHECK (state IN ('Queued','Running','Stopping','Succeeded','Failed','Cancelled')),
  requested_utc            TIMESTAMPTZ NOT NULL DEFAULT now(),
  started_utc              TIMESTAMPTZ NULL,
  completed_utc            TIMESTAMPTZ NULL,
  duration_seconds         INT NOT NULL,
  decks_processed          INT NOT NULL DEFAULT 0,
  additional_decks_found   INT NOT NULL DEFAULT 0,
  error_message            TEXT NULL,
  url                      TEXT NULL
);
CREATE INDEX IF NOT EXISTS ix_harvest_runs_state         ON harvest_runs(state);
CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_utc   ON harvest_runs(started_utc DESC);
```
SQLite form: `UUID` → `TEXT`, `TIMESTAMPTZ` → `TEXT`, `now()` → `(datetime('now'))`, `INT` → `INTEGER`. CHECK constraints retained on both.

### harvest_schedule CREATE + seed (Postgres form)
```sql
CREATE TABLE IF NOT EXISTS harvest_schedule (
  id              INT PRIMARY KEY CHECK (id = 1),
  interval_hours  INT NULL CHECK (interval_hours IS NULL OR interval_hours IN (2,4,8,24)),
  paused          BOOLEAN NOT NULL DEFAULT FALSE,
  updated_utc     TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
VALUES (1, NULL, FALSE, now())
ON CONFLICT (id) DO NOTHING;
```

### deck_queue.commander_name migration
```sql
ALTER TABLE deck_queue ADD COLUMN commander_name TEXT NULL;
```
Identical on both PG and SQLite. Idempotent — guarded by `GetTableColumnsAsync` lookup.

### FeatureFlagStore seed amendment (Postgres form, after Task 4)
```sql
INSERT INTO feature_flags (key, enabled) VALUES
  ('scryfall.tagger.enabled', TRUE),
  ('page.help.enabled', TRUE),
  ('harvest.cron.enabled', TRUE)
ON CONFLICT (key) DO NOTHING;
```
SQLite form uses `1` instead of `TRUE`. ON CONFLICT (key) DO NOTHING preserves operator changes on redeploy.

### IHarvestStatsAggregator stub (forward declaration in HarvestRunStore.cs)
```csharp
public interface IHarvestStatsAggregator
{
    void Invalidate();
}
```
Plan 06 amends this surface with `GetAsync` (per the plan's NOTE block — "the stub keeps Plan 01 buildable in isolation; Plan 06 is responsible for adding the GetAsync member without breaking the existing Invalidate surface").

## Decisions Made

- **IHarvestStatsAggregator stub location.** Per the plan's NOTE block, the stub interface lives at the top of `HarvestRunStore.cs` (not a sibling file). Plan 06 will amend the same file to add `GetAsync` so the Invalidate surface stays stable.
- **SQLite Guid storage.** `harvest_runs.id` is `TEXT` on SQLite, populated via `id.ToString()`. `RelationalDatabaseConnection.AddParameter` is type-agnostic (parameter.Value = object), so the branch happens at the call site in HarvestRunStore.
- **`GetTotalSucceededCountAsync` return type.** Plan listed `long` so we used `ExecuteScalarAsync` + a switch expression (long/int/Convert.ToInt64) to handle PG's `bigint` and SQLite's `INTEGER` paths uniformly.
- **`ReadTimestamp` extracted as private static helper.** Two stores duplicate the helper rather than relocating to a shared static class — the existing project convention is per-store private helpers (matches `FeatureFlagStore.ReadBool` pattern).
- **`HarvestScheduleStore.GetAsync` throws on missing seed row.** Defensive `InvalidOperationException` if the seed row is somehow absent — should be impossible because EnsureSchemaAsync runs the seed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] HarvestRunStore.GetLastSuccessUtcAsync uses `ExecuteScalarAsync`, not a reader**
- **Found during:** Task 2 (HarvestRunStore implementation)
- **Issue:** The plan's `<action>` block described "Reads with the `ReadTimestamp` helper (cross-provider). Returns null when no successful run exists." `MAX(completed_utc)` over zero rows returns `(null)` from `ExecuteScalarAsync`, not from a `DbDataReader.IsDBNull` check. Using a reader would have required boxing the scalar into a row, which is wasted work.
- **Fix:** Used `ExecuteScalarAsync()` + null/`DBNull` short-circuit + a private `ReadTimestampValue(object)` overload of the timestamp helper that takes a raw boxed value (so it's reusable from both reader and scalar paths).
- **Files modified:** `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs`
- **Verification:** Build clean; helper retains the four-way switch (DateTime / DateTimeOffset / string / fallback) so behavior is preserved.
- **Committed in:** `0905f67` (Task 2 commit)

**2. [Rule 2 - Missing Critical] `GetTotalSucceededCountAsync` cross-provider scalar conversion**
- **Found during:** Task 2 (HarvestRunStore implementation)
- **Issue:** Plan said "Returns long" but Postgres returns `bigint` (long), SQLite returns `INTEGER` (long for COUNT, but `int` is possible for some scalar paths). A direct `(long)raw` cast would throw on SQLite's int.
- **Fix:** Added a switch expression handling `null` / `DBNull` / `long` / `int` / `Convert.ToInt64` fallback. Same pattern used elsewhere in the codebase (`FeatureFlagStore.ReadBool`).
- **Files modified:** `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs`
- **Verification:** Build clean; exhaustive switch covers all observed scalar return types.
- **Committed in:** `0905f67` (Task 2 commit)

**3. [Rule 2 - Missing Critical] `ParseHarvestKind` defensive throw on unknown values**
- **Found during:** Task 2 (HarvestRunStore implementation)
- **Issue:** SQL CHECK constraint already enforces `kind IN ('bulk','url')`, but if a future migration adds a third value and the C# enum lags, silently coercing to `Bulk` or `Url` would corrupt stats. Plan didn't specify the helper.
- **Fix:** Private `static HarvestRunKind ParseHarvestKind(string raw)` switch expression with `_ => throw new InvalidOperationException($"Unknown harvest_runs.kind value '{raw}'.")` fallback.
- **Files modified:** `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs`
- **Verification:** Build clean.
- **Committed in:** `0905f67` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (1 blocking, 2 missing critical)
**Impact on plan:** All three deviations are local helpers / fault-tolerance improvements within HarvestRunStore.cs — no scope creep, no new files, no public surface change. Plan's `<verify>` block all green.

## Issues Encountered

- **WSL build slow.** First `dotnet build` after writing two new sealed types took 32s (cold restore + TS compile + zip extension target). Subsequent build (after Task 3 + Task 4 edits) was 29s. Acceptable for parallel worktree mode.

## User Setup Required

None — no external service configuration required. Schema bootstrap runs on first `EnsureSchemaAsync` call during startup; no env vars added in this plan.

## Verification Results

All `<verify>` automated checks from the plan ran green:

| Check | Required | Actual |
| --- | --- | --- |
| `grep -c "CREATE TABLE IF NOT EXISTS harvest_runs"` (HarvestRunStore.cs) | ≥ 1 | **2** (PG + SQLite consts) |
| `grep -c "CREATE TABLE IF NOT EXISTS harvest_schedule"` (HarvestScheduleStore.cs) | ≥ 1 | **2** (PG + SQLite consts) |
| `grep -c "interrupted by redeploy"` (HarvestRunStore.cs) | ≥ 1 | **2** (PG + SQLite reaper consts) |
| `grep -c "ON CONFLICT (id) DO NOTHING"` (HarvestScheduleStore.cs) | ≥ 1 | **4** (PG seed + SQLite seed + PG/SQLite UPSERT pairs) |
| `grep -c "_connectionInfo.IsPostgres"` (HarvestRunStore.cs) | ≥ 1 | **6** |
| `grep -c "ArgumentNullException.ThrowIfNull"` (HarvestRunStore.cs) | ≥ 1 | **1** |
| `grep -c "IHarvestStatsAggregator? stats = null"` (HarvestRunStore.cs) | ≥ 1 | **3** (one per ctor variant) |
| `grep -c "_stats?.Invalidate"` (HarvestRunStore.cs) | ≥ 2 | **2** (one per write method) |
| `grep -c "ALTER TABLE deck_queue ADD COLUMN commander_name TEXT"` (CategoryKnowledgeRepository.cs) | ≥ 1 | **1** |
| Non-comment ALTERs in CategoryKnowledgeRepository.cs | = 2 | **2** (`skipped`, `commander_name`) |
| `grep -c "harvest.cron.enabled"` (FeatureFlagStore.cs) | ≥ 2 | **3** (XML doc + PG seed + SQLite seed) |
| `dotnet build DeckFlow.sln` | exit 0 | **0 Warning(s), 0 Error(s)** |

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries beyond the operator-only admin DB. STRIDE register entries T-07-01 through T-07-06, T-07-34 already cover this plan's surface.

## Next Phase Readiness

- **Plan 02 (jobservice PG migration):** Both stores are DI-ready (3-ctor shape). `HarvestRunStore.InsertQueuedAsync` + `UpdateStateAsync` are the entry points the migrated `ArchidektCacheJobService` will call. Plan 02 can register both stores as singletons in Program.cs and start writing.
- **Plan 03 (schedule cache + tick):** `IHarvestScheduleStore.GetAsync` always returns a snapshot (seed guarantees the row exists). Plan 03's cache layer can hold the snapshot and call `SaveAsync` on operator change. The `harvest.cron.enabled` flag is seeded default-on so SC #4 is provable on first boot.
- **Plan 06 (stats aggregator):** Forward-declared `IHarvestStatsAggregator` stub is in place. Plan 06 amends the same interface (in `HarvestRunStore.cs`) to add `GetAsync(...)` returning a `HarvestStatsSnapshot`. The `Invalidate()` method already wired into both write methods means D-13 explicit invalidation lands automatically once Plan 06 registers a real impl in DI.
- **Plan 07 (DI wiring):** No DI registration was done in this plan — `Program.cs` untouched. Plan 07 will register both stores plus the stats aggregator in one place per the existing DI conventions.

## Self-Check: PASSED

Created files exist:
- `DeckFlow.Web/Services/Harvest/HarvestRunModels.cs` — FOUND (committed `f13b9cc`)
- `DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs` — FOUND (committed `f13b9cc`)
- `DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs` — FOUND (committed `f13b9cc`)
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` — FOUND (committed `0905f67`)
- `DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` — FOUND (committed `0905f67`)

Modified files exist:
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — modified, committed `1f73320`
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — modified, committed `5de8eb4`

Commits exist (verified via `git log --oneline`):
- `f13b9cc` — FOUND
- `0905f67` — FOUND
- `1f73320` — FOUND
- `5de8eb4` — FOUND

---
*Phase: 07-harvest-controls-stats*
*Completed: 2026-05-03*
