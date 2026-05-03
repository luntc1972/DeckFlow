---
phase: 07-harvest-controls-stats
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs
  - DeckFlow.Web/Services/Harvest/HarvestRunStore.cs
  - DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs
  - DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs
  - DeckFlow.Web/Services/Harvest/HarvestRunModels.cs
  - DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
autonomous: true
requirements: [HARV-07]
tags: [harvest, persistence, postgres, sqlite, schema, redeploy-survival, feature-flag-seed]

must_haves:
  truths:
    - "harvest_runs table is created on first call to EnsureSchemaAsync (D-01, D-03, D-07)"
    - "harvest_schedule table is created and seeded with single row id=1, interval_hours=NULL, paused=FALSE on EnsureSchemaAsync (D-06, D-07)"
    - "On EnsureSchemaAsync, every non-terminal harvest_runs row is reaped to state='Failed' with error_message='interrupted by redeploy' (D-02)"
    - "deck_queue gains a nullable commander_name TEXT column via additive migration (D-17)"
    - "All schema and parameter binding branches on IRelationalDialect.IsPostgres for PG vs SQLite divergence (D-14, S-3)"
    - "Both stores expose public DI ctor + internal test ctor with InternalsVisibleTo (S-6)"
    - "HarvestRunStore ctor accepts optional `IHarvestStatsAggregator? stats = null` so write-side methods can call _stats?.Invalidate() after successful writes (D-13 explicit cache invalidation)"
    - "FeatureFlagStore seed list adds `harvest.cron.enabled` default-on so SC #4 (scheduler runs at chosen interval) is provable on a fresh DB (D-06, Phase 6 D-09 carry-forward)"
  artifacts:
    - path: "DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs"
      provides: "Run-store contract: InsertQueuedAsync, UpdateStateAsync, GetActiveAsync, GetRecentAsync, GetLastSuccessUtcAsync, ReapInterruptedRunsAsync, EnsureSchemaAsync"
      contains: "interface IHarvestRunStore"
    - path: "DeckFlow.Web/Services/Harvest/HarvestRunStore.cs"
      provides: "Sealed PG/SQLite-dialect impl of IHarvestRunStore with D-02 reaper inside EnsureSchemaAsync; write methods call _stats?.Invalidate() (D-13)"
      contains: "sealed class HarvestRunStore"
    - path: "DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs"
      provides: "Schedule-store contract: GetAsync, SaveAsync, EnsureSchemaAsync"
      contains: "interface IHarvestScheduleStore"
    - path: "DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs"
      provides: "Sealed single-row UPSERT impl, seeds id=1 default-Off row"
      contains: "sealed class HarvestScheduleStore"
    - path: "DeckFlow.Web/Services/Harvest/HarvestRunModels.cs"
      provides: "Public sealed record types: HarvestRunRow, HarvestScheduleSnapshot, enums HarvestRunKind/HarvestRunState"
      contains: "public sealed record HarvestRunRow"
    - path: "DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs"
      provides: "Existing repository — EnsureDeckQueueColumnsAsync gains commander_name additive-migration branch"
      contains: "commander_name"
    - path: "DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs"
      provides: "Seed list (PostgresSeedSql / SqliteSeedSql) gains a third row 'harvest.cron.enabled' default-on so the scheduler kill-switch is enabled on first boot (B3)"
      contains: "harvest.cron.enabled"
  key_links:
    - from: "HarvestRunStore.EnsureSchemaAsync"
      to: "harvest_runs PG/SQLite tables"
      via: "CREATE TABLE IF NOT EXISTS + indexes + reaper UPDATE"
      pattern: "CREATE TABLE IF NOT EXISTS harvest_runs"
    - from: "HarvestScheduleStore.EnsureSchemaAsync"
      to: "harvest_schedule single-row table"
      via: "CREATE TABLE IF NOT EXISTS + ON CONFLICT DO NOTHING seed"
      pattern: "INSERT INTO harvest_schedule .* VALUES .1, NULL, FALSE"
    - from: "CategoryKnowledgeRepository.EnsureDeckQueueColumnsAsync"
      to: "deck_queue.commander_name column"
      via: "ALTER TABLE … ADD COLUMN guarded by GetTableColumnsAsync"
      pattern: "ALTER TABLE deck_queue ADD COLUMN commander_name"
    - from: "FeatureFlagStore.PostgresSeedSql / SqliteSeedSql"
      to: "feature_flags row 'harvest.cron.enabled' enabled=TRUE/1"
      via: "ON CONFLICT (key) DO NOTHING tail of seed multi-row INSERT"
      pattern: "harvest.cron.enabled"
---

<objective>
Land the **persistence and schema foundation** for Phase 7. Three stores join the solution (IHarvestRunStore, IHarvestScheduleStore, plus the supporting record types) and one existing repository gains a commander column. The startup reaper (D-02) is wired inside HarvestRunStore.EnsureSchemaAsync so the moment the cache StartAsync (Plan 03) runs, orphaned redeploy rows are reconciled before any HTTP request lands. The FeatureFlagStore seed list gains the `harvest.cron.enabled` default-on row so the Plan 03 scheduler kill-switch is enabled on first boot of a fresh database (B3 — without this seed SC #4 is unprovable).

Purpose: every other Phase 7 plan reads or writes through these contracts. Without them the controller, scheduler, stats aggregator, and AJAX status endpoint are all blocked.

Output:
- Two new sealed stores under `DeckFlow.Web/Services/Harvest/` mirroring the Phase 6 FeatureFlagStore shape.
- Public records and enums consumed by every later plan.
- D-17 commander_name column live on deck_queue (additive migration; existing rows stay NULL).
- D-02 reaper baked into EnsureSchemaAsync — fires once per process lifetime via `_schemaReady` gate.
- Phase 6 FeatureFlagStore seed amended with `harvest.cron.enabled` default-on (B3).
- HarvestRunStore ctor takes `IHarvestStatsAggregator? stats = null` so Plan 06's invalidator wires in without circular-dependency surgery (D-13).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/REQUIREMENTS.md
@.planning/STATE.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-RESEARCH.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/06-admin-shell-feature-flags/06-CONTEXT.md
@DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore.cs
@DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
@DeckFlow.Web/Infrastructure/AdminBruteForceTrackerStore.cs
@DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
@DeckFlow.Core/Storage/RelationalDatabaseConnection.cs

<interfaces>
<!-- Authoritative contracts every later plan binds to. -->

From DeckFlow.Web/Services/Harvest/HarvestRunModels.cs (NEW — Task 1):
```csharp
public enum HarvestRunKind { Bulk, Url }

public enum HarvestRunState { Queued, Running, Stopping, Succeeded, Failed, Cancelled }

public sealed record HarvestRunRow(
    Guid Id,
    HarvestRunKind Kind,
    HarvestRunState State,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    int DurationSeconds,
    int DecksProcessed,
    int AdditionalDecksFound,
    string? ErrorMessage,
    string? Url);

public sealed record HarvestScheduleSnapshot(
    int? IntervalHours,
    bool Paused,
    DateTimeOffset UpdatedUtc);
```

From DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs (NEW — Task 1):
```csharp
public interface IHarvestRunStore
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default);
    Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default);
    Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default);
}
```

From DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs (NEW — Task 1):
```csharp
public interface IHarvestScheduleStore
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task<HarvestScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default);
}
```

From DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs (existing — modified at Task 3):
```csharp
// Existing additive-migration idiom at lines 86-97 — `EnsureDeckQueueColumnsAsync(DbConnection connection, CancellationToken)`.
// Currently checks for `skipped` column. Plan adds a parallel check for `commander_name`.
```

From DeckFlow.Core/Storage/RelationalDatabaseConnection.cs (existing — read-only context):
```csharp
public sealed class RelationalDatabaseConnection
{
    public bool IsPostgres { get; }
    public bool IsSqlite { get; }
    public DbConnection CreateConnection();
    public static void AddParameter(DbCommand command, string name, object? value);
    public string ExtractSqlitePath();
    public static RelationalDatabaseConnection FromSqlitePath(string path);
}
```

From DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs (existing — modified at Task 4):
```csharp
// Phase 6 D-09 seed pattern (lines 172-184). Plan 07 adds a third row to BOTH the
// PostgresSeedSql and SqliteSeedSql multi-row INSERT statements:
//   ('harvest.cron.enabled', TRUE)   -- PG
//   ('harvest.cron.enabled', 1)      -- SQLite
// ON CONFLICT (key) DO NOTHING preserves operator changes on redeploy (FLAG-01).
```
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Create record types + interface files (HarvestRunModels, IHarvestRunStore, IHarvestScheduleStore)</name>
  <files>DeckFlow.Web/Services/Harvest/HarvestRunModels.cs, DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs, DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs</files>
  <behavior>
    - HarvestRunModels.cs compiles: defines two enums (HarvestRunKind {Bulk, Url}, HarvestRunState {Queued, Running, Stopping, Succeeded, Failed, Cancelled}) and two public sealed records (HarvestRunRow, HarvestScheduleSnapshot) under namespace `DeckFlow.Web.Services.Harvest`.
    - IHarvestRunStore.cs declares the seven Task-returning methods listed in <interfaces>; every async method ends in `Async` and takes `CancellationToken cancellationToken = default` as the LAST parameter.
    - IHarvestScheduleStore.cs declares three Task-returning methods (EnsureSchemaAsync, GetAsync, SaveAsync).
    - File-scoped namespace, one public type per file (record types co-locate with HarvestRunModels.cs by exception per the existing `CardLookupResult` precedent — that's the canonical "results bag" idiom).
    - XML doc comment on every public type and method (matches `IFeatureFlagStore.cs` lines 1-34).
  </behavior>
  <action>
    Create three files in `DeckFlow.Web/Services/Harvest/` (the directory does not exist — create it):
    1. `HarvestRunModels.cs` — `namespace DeckFlow.Web.Services.Harvest;` + the two enums + two sealed records exactly as shown in <interfaces>. Mark every `init` property with the appropriate nullability. `HarvestRunRow` is the wire-format used by GetActiveAsync/GetRecentAsync — no extra fields. Add `/// <summary>` blocks per type per CLAUDE.md.
    2. `IHarvestRunStore.cs` — `namespace DeckFlow.Web.Services.Harvest;` + the interface as shown. XML docs on every method explaining the role:
       - `EnsureSchemaAsync` — "Idempotent. On first call: creates harvest_runs table and indexes, then runs the D-02 startup reaper (UPDATE non-terminal rows to Failed)."
       - `InsertQueuedAsync` — "Inserts a new harvest_runs row with state='Queued' and returns the generated UUID. `url` is null for kind=Bulk, populated for kind=Url (D-10). Implementations MUST call _stats?.Invalidate() after the write succeeds (D-13)."
       - `UpdateStateAsync` — "Updates an existing run row to the new state. Pass startedUtc only when transitioning to Running; completedUtc only when transitioning to a terminal state. Implementations MUST call _stats?.Invalidate() after the write succeeds (D-13)."
       - `GetActiveAsync` — "Returns the most recent non-terminal row (state IN Queued/Running/Stopping) or null. Used by EnqueueAsync dedup check (D-01) and AJAX status poll (D-08)."
       - `GetRecentAsync` — "Returns the most recent N rows ordered by started_utc DESC NULLS LAST (D-16 #5)."
       - `GetLastSuccessUtcAsync` — "Returns MAX(completed_utc) for state='Succeeded' (D-16 #7). Single source of truth — both HarvestStatsAggregator and HarvestScheduleService MUST call this (W5)."
       - `GetTotalSucceededCountAsync` — "Returns COUNT(1) for state='Succeeded' — bulk-run lifetime total used by stats panel."
    3. `IHarvestScheduleStore.cs` — `namespace DeckFlow.Web.Services.Harvest;` + the interface as shown. XML docs:
       - `EnsureSchemaAsync` — "Idempotent. Creates harvest_schedule table and seeds row id=1 with interval_hours=NULL, paused=FALSE, updated_utc=now() via ON CONFLICT DO NOTHING (D-06, planner discretion #6)."
       - `GetAsync` — "Reads single-row snapshot. Always returns a snapshot — schema seed guarantees row exists."
       - `SaveAsync` — "UPSERT of id=1 row with new interval_hours/paused/updated_utc (D-07)."
    Do not add the implementations — those land in Task 2. Build must compile after this task because no consumers exist yet.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -20 && grep -c "interface IHarvestRunStore" DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs && grep -c "interface IHarvestScheduleStore" DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs && grep -c "sealed record HarvestRunRow" DeckFlow.Web/Services/Harvest/HarvestRunModels.cs</automated>
  </verify>
  <done>Build exits 0; the four required grep counts are each ≥ 1; namespace is `DeckFlow.Web.Services.Harvest`; every async method on both interfaces ends in `Async`.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Implement HarvestRunStore + HarvestScheduleStore with PG/SQLite branching, D-02 reaper, optional stats invalidator</name>
  <files>DeckFlow.Web/Services/Harvest/HarvestRunStore.cs, DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs</files>
  <behavior>
    - `HarvestRunStore` mirrors `FeatureFlagStore` shape: ctor variants (databasePath / RelationalDatabaseConnection / IWebHostEnvironment), `_schemaGate` SemaphoreSlim, `volatile bool _schemaReady`. Schema gate runs three commands inside the lock: CREATE TABLE, indexes, D-02 reaper UPDATE.
    - **NEW (B1):** Each ctor takes an optional trailing `IHarvestStatsAggregator? stats = null` parameter (forward declaration; the interface lives in Plan 06 namespace `DeckFlow.Web.Services.Harvest` so importing it is free). The reference is held in a `_stats` field; nullable so Plan 01 can compile in isolation before Plan 06 lands. Every state-write method (`InsertQueuedAsync`, `UpdateStateAsync`) calls `_stats?.Invalidate();` AFTER the SQL write succeeds. (D-13 explicit invalidation.)
    - `EnsureSchemaAsync` is idempotent — second call returns immediately on `_schemaReady` fast-path. Reaper SQL uses `now()` on PG, `datetime('now')` on SQLite.
    - `InsertQueuedAsync` uses `Guid.NewGuid()` for the id; for SQLite stores the UUID as TEXT, for PG stores as UUID parameter (`Npgsql` accepts `Guid` directly via `AddParameter`).
    - `UpdateStateAsync` writes only the columns it was given (NULL-coalesce on startedUtc/completedUtc/errorMessage); state is always written; uses parameterized SQL only.
    - `GetActiveAsync` SQL: `SELECT … FROM harvest_runs WHERE state IN ('Queued','Running','Stopping') ORDER BY requested_utc DESC LIMIT 1`. Returns null on empty.
    - `GetRecentAsync` SQL: `SELECT … FROM harvest_runs ORDER BY started_utc DESC NULLS LAST LIMIT @n` (PG) / `ORDER BY started_utc DESC NULLS LAST LIMIT @n` (SQLite — `NULLS LAST` works in modern SQLite ≥ 3.30 which ships with `Microsoft.Data.Sqlite` 10).
    - `GetLastSuccessUtcAsync` SQL: `SELECT MAX(completed_utc) FROM harvest_runs WHERE state='Succeeded'`. Reads with the `ReadTimestamp` helper (cross-provider). Returns null when no successful run exists. **W5: this single method is the sole source for both the stats panel and the scheduler.**
    - `GetTotalSucceededCountAsync` SQL: `SELECT COUNT(1) FROM harvest_runs WHERE state='Succeeded'`. Returns long.
    - `HarvestScheduleStore` mirrors single-row UPSERT shape from `FeatureFlagStore.cs:188-202`. Schema gate runs CREATE TABLE + ON CONFLICT DO NOTHING seed of id=1, NULL, FALSE. `GetAsync` always finds the row (by virtue of seed). `SaveAsync` is the EXCLUDED UPSERT.
    - All boolean reads use a private `ReadBool` helper (copy from `FeatureFlagStore.cs:131-143`); all timestamp reads use a private `ReadTimestamp` helper (copy from `AdminBruteForceTrackerStore.cs:124-134`).
    - `[InternalsVisibleTo("DeckFlow.Web.Tests")]` is already in `DeckFlow.Web/AssemblyInfo.cs` — internal test ctor accepting raw `RelationalDatabaseConnection` is sufficient.
  </behavior>
  <action>
    Create two sealed implementation files. Both store classes share the feedback DB per RESEARCH.md — call `DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection(environment)` from the IWebHostEnvironment ctor (NOT a new factory method; reuse the existing one — same DB file as feature_flags).

    **HarvestRunStore.cs:**
    - Three ctors (databasePath / RelationalDatabaseConnection / IWebHostEnvironment) verbatim shape from `FeatureFlagStore.cs:21-57`. **Each ctor takes an optional trailing `IHarvestStatsAggregator? stats = null` parameter** — the DI ctor passes it through to the connection-info ctor; tests can construct without one. Forward-declare the interface via `using DeckFlow.Web.Services.Harvest;` (same namespace, so the import is implicit).
    - Field declaration:
      ```csharp
      private readonly IHarvestStatsAggregator? _stats;
      ```
      Assigned in the connection-info ctor: `_stats = stats;` (no null-throw — null is a legal value when Plan 06 hasn't shipped yet).
    - Constants `PostgresCreateTableSql` / `SqliteCreateTableSql` containing the D-03 schema. Postgres form:
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
      SQLite form: `UUID` → `TEXT`, `TIMESTAMPTZ` → `TEXT`, `DEFAULT now()` → `DEFAULT (datetime('now'))`, otherwise identical with all CHECK constraints retained.
    - Constants `PostgresReaperSql` / `SqliteReaperSql`:
      ```sql
      -- D-02: any non-terminal row at startup is by definition orphaned (single-instance Render).
      UPDATE harvest_runs
         SET state='Failed',
             error_message='interrupted by redeploy',
             completed_utc = now()      -- SQLite uses datetime('now')
       WHERE state IN ('Queued','Running','Stopping');
      ```
    - `EnsureSchemaAsync` runs three commands inside the gate, in order: create table+indexes (multi-statement SQL is fine for both providers), reaper UPDATE. Set `_schemaReady = true` only after both succeed. Mirror the gate+double-check pattern from `FeatureFlagStore.cs:102-129`.
    - `InsertQueuedAsync`:
      ```sql
      INSERT INTO harvest_runs (id, kind, state, requested_utc, duration_seconds, url)
      VALUES (@id, @kind, 'Queued', @now, @duration, @url);
      ```
      Bind `@id` as `Guid.NewGuid()` (PG accepts Guid; SQLite stores as text via `id.ToString()` — handle in AddParameter). Bind `@kind` as the lowercase string `"bulk"` or `"url"` matching the CHECK constraint. Bind `@now` cross-provider (PG: `now.UtcDateTime`, SQLite: `now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)`). Return the new Guid. **After ExecuteNonQueryAsync succeeds, call `_stats?.Invalidate();` (D-13).**
    - `UpdateStateAsync`:
      ```sql
      UPDATE harvest_runs
         SET state = @state,
             started_utc = COALESCE(@startedUtc, started_utc),
             completed_utc = COALESCE(@completedUtc, completed_utc),
             decks_processed = @decksProcessed,
             additional_decks_found = @additionalDecksFound,
             error_message = @errorMessage
       WHERE id = @id;
      ```
      Bind `@state` as `state.ToString()`. Pass null timestamps as `DBNull.Value`. Same with `@errorMessage`. **After ExecuteNonQueryAsync succeeds, call `_stats?.Invalidate();` (D-13).**
    - `GetActiveAsync`, `GetRecentAsync`, `GetLastSuccessUtcAsync`, `GetTotalSucceededCountAsync` — single-statement SELECTs as described in <behavior>. Use private `ReadHarvestRunRow(DbDataReader reader)` helper to avoid duplicating column-read code across `GetActiveAsync` and `GetRecentAsync`. The helper uses `ReadTimestamp` for the timestamp columns, `ReadEnum<HarvestRunKind>` and `ReadEnum<HarvestRunState>` for enum strings, and direct `GetXxx` for ints/strings.
    - Private helpers at bottom of file: `ReadBool` (copy verbatim from FeatureFlagStore.cs:131-143), `ReadTimestamp` (copy from AdminBruteForceTrackerStore.cs:124-134), `ReadEnum<T>` where `T : struct, Enum`:
      ```csharp
      private static T ReadEnum<T>(DbDataReader reader, int ordinal) where T : struct, Enum
          => Enum.Parse<T>(reader.GetString(ordinal), ignoreCase: false);
      ```
    - `OpenConnectionAsync` private helper (copy from FeatureFlagStore.cs:145-150).

    **NOTE — IHarvestStatsAggregator forward declaration:** The interface is defined in Plan 06's `IHarvestStatsAggregator.cs`. To keep Plan 01 self-contained in build order, declare a tiny stub interface marker in `IHarvestRunStore.cs` ONLY IF Plan 06 cannot yet provide one. **Preferred path:** Plan 01 references the Plan 06 interface directly (same namespace `DeckFlow.Web.Services.Harvest`); since Plans 01 and 06 land within the same Wave-graph and the same csproj, the C# compiler resolves the reference once both files exist. If during execution the type is missing, define `public interface IHarvestStatsAggregator { void Invalidate(); /* GetAsync added by Plan 06 */ }` as a STUB in Plan 01's HarvestRunStore.cs (commented `// STUB — Plan 06 fleshes this out with GetAsync`) and Plan 06 amends it. The stub keeps Plan 01 buildable in isolation; Plan 06 is responsible for adding the `GetAsync` member without breaking the existing `Invalidate` surface.

    **HarvestScheduleStore.cs:**
    - Same three-ctor shape (no stats hook needed — schedule writes don't need to invalidate the harvest stats cache; the schedule snapshot is its own cache layer in Plan 03).
    - `PostgresCreateTableSql`:
      ```sql
      CREATE TABLE IF NOT EXISTS harvest_schedule (
        id              INT PRIMARY KEY CHECK (id = 1),
        interval_hours  INT NULL CHECK (interval_hours IS NULL OR interval_hours IN (2,4,8,24)),
        paused          BOOLEAN NOT NULL DEFAULT FALSE,
        updated_utc     TIMESTAMPTZ NOT NULL DEFAULT now()
      );
      ```
      SQLite form: `BOOLEAN` → `INTEGER`, `TIMESTAMPTZ` → `TEXT`, default `now()` → `(datetime('now'))`. Keep the CHECK constraints intact on both.
    - `PostgresSeedSql`:
      ```sql
      INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
      VALUES (1, NULL, FALSE, now())
      ON CONFLICT (id) DO NOTHING;
      ```
      SQLite form: replace `FALSE` with `0`, `now()` with `datetime('now')`, and `ON CONFLICT (id) DO NOTHING` works in modern SQLite verbatim.
    - `EnsureSchemaAsync` runs CREATE then SEED inside gate (mirror `FeatureFlagStore.cs:102-129`).
    - `GetAsync` — single SELECT `SELECT id, interval_hours, paused, updated_utc FROM harvest_schedule WHERE id = 1`. Read interval_hours via `reader.IsDBNull(1) ? null : reader.GetInt32(1)`, paused via `ReadBool`, updated_utc via `ReadTimestamp`. Always returns a snapshot (seed guarantees row exists; if reader has no rows, throw `InvalidOperationException("harvest_schedule seed missing")` — defensive).
    - `SaveAsync` — UPSERT EXCLUDED form mirroring `FeatureFlagStore.cs:188-202`:
      ```sql
      INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
      VALUES (1, @interval, @paused, @now)
      ON CONFLICT (id) DO UPDATE SET
        interval_hours = EXCLUDED.interval_hours,
        paused         = EXCLUDED.paused,
        updated_utc    = EXCLUDED.updated_utc;
      ```
      Bind `@interval` as `(object?)intervalHours ?? DBNull.Value`. Bind `@paused` cross-provider (`(object)paused` for PG, `paused ? 1 : 0` for SQLite). `@now` as in HarvestRunStore.

    Both files: `ArgumentNullException.ThrowIfNull` in every ctor for non-nullable args (skip the optional `stats` arg), structured Serilog logging only when something exceptional happens (no info-level logs on the happy path — these are hot stores).
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "CREATE TABLE IF NOT EXISTS harvest_runs" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && grep -q "CREATE TABLE IF NOT EXISTS harvest_schedule" DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs && grep -q "interrupted by redeploy" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && grep -q "ON CONFLICT (id) DO NOTHING" DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs && grep -q "_connectionInfo.IsPostgres" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && grep -q "ArgumentNullException.ThrowIfNull" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && grep -q "IHarvestStatsAggregator? stats = null" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && grep -c "_stats?.Invalidate" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs</automated>
  </verify>
  <done>Build exits 0; reaper SQL string is present in HarvestRunStore.cs; both schema CREATE strings present; both stores have IsPostgres-branched parameter binding; ON CONFLICT seed present in HarvestScheduleStore.cs; nullable `IHarvestStatsAggregator? stats = null` ctor param present; `_stats?.Invalidate()` count ≥ 2 (one per write method).</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Add commander_name column to deck_queue (D-17 additive migration)</name>
  <files>DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs</files>
  <behavior>
    - `EnsureDeckQueueColumnsAsync` (lines 86-97) gains a parallel branch: if `commander_name` not in columns, `ALTER TABLE deck_queue ADD COLUMN commander_name TEXT NULL;`.
    - The migration is additive only — existing rows stay NULL. No backfill (top-N query already filters `commander_name IS NOT NULL` per D-15).
    - Existing `skipped` migration logic untouched — both branches coexist inside the same method.
    - Migration is idempotent — second call hits the `hasCommander` short-circuit and no-ops.
    - SQL works on both providers (`ALTER TABLE … ADD COLUMN <name> TEXT NULL` is identical on PG and SQLite).
  </behavior>
  <action>
    In `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`, modify `EnsureDeckQueueColumnsAsync` (currently lines 86-97). Replace the body so it reads:
    ```csharp
    private async Task EnsureDeckQueueColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
        var hasSkipped = columns.Contains("skipped");
        var hasCommanderName = columns.Contains("commander_name");

        if (!hasSkipped)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN skipped INTEGER NOT NULL DEFAULT 0;";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!hasCommanderName)
        {
            // D-17: capture commander identity per processed deck so the harvest stats panel
            // can group top-N commanders by deck_count without joining card_category_observations.
            // Existing rows stay NULL; only newly-imported decks populate this column.
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN commander_name TEXT NULL;";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
    ```
    No XML doc comment changes (existing summary already says "verifies the deck queue table includes the latest needed columns" which still applies). Do not touch any other method in the file.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "ALTER TABLE deck_queue ADD COLUMN commander_name TEXT" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs && grep -q "hasCommanderName" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs && grep -v '^[[:space:]]*//' DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs | grep -c "ALTER TABLE deck_queue ADD COLUMN"</automated>
  </verify>
  <done>Build exits 0; the new ALTER TABLE statement for `commander_name` is present; both ALTER statements (`skipped`, `commander_name`) appear in non-comment lines; existing `skipped` migration untouched.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 4: Seed `harvest.cron.enabled` flag default-on in FeatureFlagStore (B3)</name>
  <files>DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs</files>
  <behavior>
    - Phase 6 D-09 seed list (lines 172-184) gains a third row `('harvest.cron.enabled', TRUE)` on PG and `('harvest.cron.enabled', 1)` on SQLite.
    - ON CONFLICT (key) DO NOTHING semantics are preserved verbatim — once an operator has flipped the flag (Phase 6 FLAG-01 contract), re-bootstrapping the seed never overwrites their choice.
    - Without this seed: ROADMAP SC #4 ("Operator selects an interval; the next scheduler tick fires a bulk harvest after that interval elapses") is unprovable on a fresh DB because Plan 03's HarvestScheduleService gates the entire tick on `IFeatureFlagCache.IsEnabled("harvest.cron.enabled")` — a missing key reads as `false`.
    - The seed amendment is purely additive; the existing two rows (`scryfall.tagger.enabled`, `page.help.enabled`) are unchanged.
  </behavior>
  <action>
    Open `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`. Modify the two seed constants in place (current lines 172-184).

    **`PostgresSeedSql`** — change from:
    ```csharp
    private const string PostgresSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', TRUE),
          ('page.help.enabled', TRUE)
        ON CONFLICT (key) DO NOTHING;
        """;
    ```
    to:
    ```csharp
    private const string PostgresSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', TRUE),
          ('page.help.enabled', TRUE),
          ('harvest.cron.enabled', TRUE)
        ON CONFLICT (key) DO NOTHING;
        """;
    ```

    **`SqliteSeedSql`** — change from:
    ```csharp
    private const string SqliteSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', 1),
          ('page.help.enabled', 1)
        ON CONFLICT (key) DO NOTHING;
        """;
    ```
    to:
    ```csharp
    private const string SqliteSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', 1),
          ('page.help.enabled', 1),
          ('harvest.cron.enabled', 1)
        ON CONFLICT (key) DO NOTHING;
        """;
    ```

    Update the class XML doc summary (lines 7-13) so the "Seed list (D-09) inserts default-on rows" comment lists three flag keys instead of two:
    ```csharp
    /// ... Seed list (Phase 6 D-09 + Phase 7 B3) inserts default-on rows for
    /// 'scryfall.tagger.enabled', 'page.help.enabled', and 'harvest.cron.enabled'
    /// using ON CONFLICT (key) DO NOTHING ...
    ```

    Do NOT change any other code in this file. The seed runs inside the existing `EnsureSchemaAsync` gate; idempotency is preserved.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "harvest.cron.enabled" DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs && grep -c "harvest.cron.enabled" DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs</automated>
  </verify>
  <done>Build exits 0; the literal `harvest.cron.enabled` appears at least twice in FeatureFlagStore.cs (once in PostgresSeedSql, once in SqliteSeedSql); both existing seed entries (`scryfall.tagger.enabled`, `page.help.enabled`) remain intact; ON CONFLICT (key) DO NOTHING tail is preserved.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Process → Postgres | Schema-bootstrap runs at startup; SQL is constant strings, parameters bound; no untrusted input crosses here. |
| Process → SQLite | Same as PG path; SQLite file under MTG_DATA_DIR. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-01 | Tampering | HarvestRunStore SQL | mitigate | Parameterized SQL only — `RelationalDatabaseConnection.AddParameter` for every value; no string concat. |
| T-07-02 | Tampering | HarvestScheduleStore.SaveAsync interval_hours | mitigate | DB-level CHECK `interval_hours IS NULL OR interval_hours IN (2,4,8,24)` is the second line of defense behind controller-side whitelist (Plan 04). |
| T-07-03 | Denial of service | EnsureSchemaAsync gate | accept | `_schemaGate` is a 1-permit SemaphoreSlim; concurrent first-callers serialize but only the first does real work. Steady-state path is the lock-free `_schemaReady` fast-return. |
| T-07-04 | Information disclosure | error_message column | accept | Operator-only surface (BasicAuth via /Admin); error messages may include upstream stack fragments which is acceptable for an admin console. |
| T-07-05 | Tampering | D-02 reaper UPDATE | mitigate | Reaper runs only inside the schema gate on first call per process; idempotent (zero rows on fresh DB or already-terminal state). |
| T-07-06 | Repudiation | commander_name capture | accept | No audit trail; single-operator BasicAuth context per CLAUDE.md and REQUIREMENTS.md (POLISH-02 deferred). |
| T-07-34 | Tampering | harvest.cron.enabled seed | accept | Constant SQL value `TRUE`/`1`; ON CONFLICT (key) DO NOTHING preserves any operator-set value on subsequent boots. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "CREATE TABLE IF NOT EXISTS harvest_runs" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` ≥ 1.
- `grep -c "CREATE TABLE IF NOT EXISTS harvest_schedule" DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` ≥ 1.
- `grep -c "interrupted by redeploy" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` ≥ 1.
- `grep -v '^[[:space:]]*//' DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs | grep -c "ALTER TABLE deck_queue ADD COLUMN"` = 2.
- `grep -c "ON CONFLICT (id) DO NOTHING" DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` ≥ 1.
- `grep -c "harvest.cron.enabled" DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` ≥ 2 (one PG, one SQLite seed row).
- `grep -c "IHarvestStatsAggregator? stats = null" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` ≥ 1.
- `grep -c "_stats?.Invalidate" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` ≥ 2 (one per write site).
- No `new HttpClient(` or `Microsoft.Extensions.Http.Resilience` introduced (no HTTP in this plan).
</verification>

<success_criteria>
- Both store interfaces and impls compile and follow the FeatureFlagStore shape.
- Schema is idempotent and includes the D-02 reaper baked in.
- harvest_schedule is seeded with a single id=1 row on first EnsureSchemaAsync call.
- deck_queue carries the commander_name column on every database (PG and SQLite) after a process boot.
- FeatureFlagStore seed table now contains `harvest.cron.enabled = TRUE/1` on a fresh DB so SC #4 is provable end-to-end (B3).
- HarvestRunStore is ready to receive an `IHarvestStatsAggregator` (nullable) so Plan 06 can wire D-13 explicit invalidation without circular dependencies.
- No public-facing surface change yet — DI wiring and consumers land in later plans.
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-01-SUMMARY.md` summarizing: files added, exact reaper SQL strings, the `commander_name` migration verification, the FeatureFlagStore seed amendment (B3), the nullable IHarvestStatsAggregator ctor param shape, and any deviation from the plan's <action> blocks.
</output>
</content>
</invoke>