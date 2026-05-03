# Phase 7: Harvest Controls + Stats - Pattern Map

**Mapped:** 2026-05-03
**Files analyzed:** 11 (8 new, 3 modified)
**Analogs found:** 11 / 11 (100% — every file has a strong shipped analog)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs` (NEW) | store-interface | CRUD | `Services/FeatureFlags/IFeatureFlagStore.cs` | exact |
| `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` (NEW) | store-impl | CRUD | `Services/FeatureFlags/FeatureFlagStore.cs` + `Services/AdminBruteForceTrackerStore.cs` | exact |
| `DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs` (NEW) | store-interface | CRUD | `Services/FeatureFlags/IFeatureFlagStore.cs` | exact |
| `DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` (NEW) | store-impl | CRUD | `Services/FeatureFlags/FeatureFlagStore.cs` | exact |
| `DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs` (NEW) | cache-interface | request-response | `Services/FeatureFlags/IFeatureFlagCache.cs` | exact |
| `DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs` (NEW) | cache + hosted | event-driven (poll) | `Services/FeatureFlags/FeatureFlagCache.cs` | exact |
| `DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs` (NEW) | hosted-bg | event-driven (tick) | `Services/FeatureFlags/FeatureFlagCache.cs` (PeriodicTimer body) + `Services/ArchidektCacheJobService.cs` (BackgroundService shape) | role-match |
| `DeckFlow.Web/Services/ArchidektCacheJobService.cs` (MODIFIED) | hosted-bg | event-driven (channel) | self — surgically modified | self |
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` (MODIFIED) | controller | request-response + AJAX | `Controllers/Admin/AdminFlagsController.cs` (Phase 6) + `Controllers/Api/ArchidektCacheJobsController.cs` (same-origin gate) | exact |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` (MODIFIED) | view | render | `Views/AdminFlags/Index.cshtml` (Phase 6) | exact |
| `DeckFlow.Web/wwwroot/ts/admin-harvest.ts` (NEW) | client-script | request-response (poll) | `wwwroot/ts/category-suggestions.ts` (fetch + JSON) | role-match |
| `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` (NEW) | DI extension | config | `Extensions/FeatureFlagsServiceCollectionExtensions.cs` | exact |
| `DeckFlow.Web/Program.cs` (MODIFIED) | composition root | config | self — surgical edit at lines 159, 280-283 | self |
| `DeckFlow.Web/Views/AdminHarvest/_ViewStart.cshtml` (EXISTS) | view-config | n/a | `Views/AdminFlags/_ViewStart.cshtml` | exact (already in place) |

---

## Pattern Assignments

### `Services/Harvest/IHarvestRunStore.cs` (store-interface, CRUD)

**Analog:** `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore.cs`

**Imports + namespace pattern** (lines 1-8):
```csharp
namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Persistence contract for the feature_flags table (Phase 6, FLAG-01..03).
/// Implementations bootstrap schema lazily on first call and seed default-on rows
/// for shipped features so a fresh DB never silently kills live behavior (FLAG-01).
/// </summary>
public interface IFeatureFlagStore
```

**Interface shape pattern** (lines 8-34) — copy verbatim, adapt method names:
- `Task<...> GetAllAsync(CancellationToken cancellationToken = default)` — bulk read for cache hydration
- `Task SetEnabledAsync(string key, ..., CancellationToken cancellationToken = default)` — single-row write
- `Task EnsureSchemaAsync(CancellationToken cancellationToken = default)` — explicit bootstrap

**For `IHarvestRunStore` add:**
- `Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken ct)` — D-03 INSERT
- `Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken ct)` — D-01 transition
- `Task<HarvestRunRow?> GetActiveAsync(CancellationToken ct)` — first non-terminal row (D-08 status poll)
- `Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken ct)` — D-16 #5
- `Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken ct)` — D-16 #7
- `Task ReapInterruptedRunsAsync(string errorMessage, CancellationToken ct)` — D-02 startup reaper

---

### `Services/Harvest/HarvestRunStore.cs` (store-impl, CRUD)

**Analog:** `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`

**Sealed class + connection-info field + schema-gate pattern** (lines 15-19):
```csharp
public sealed class FeatureFlagStore : IFeatureFlagStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;
```

**Three-ctor test-seam pattern** (lines 21-57) — copy verbatim, swap `CreateFeatureFlagConnection` for the appropriate connection factory call:
```csharp
public FeatureFlagStore(string databasePath)
    : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

public FeatureFlagStore(RelationalDatabaseConnection connectionInfo)
{
    ArgumentNullException.ThrowIfNull(connectionInfo);
    _connectionInfo = connectionInfo;
    if (_connectionInfo.IsSqlite)
    {
        var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }
}

public FeatureFlagStore(IWebHostEnvironment environment)
    : this(DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection(environment)) { }
```
**Note for planner:** `harvest_runs` and `harvest_schedule` share the feedback DB per RESEARCH.md — call `CreateFeatureFlagConnection(environment)` (or add `CreateHarvestConnection` if separation is wanted; planner picks).

**EnsureSchemaAsync pattern** (lines 102-129) — double-check + gate + create + seed:
```csharp
public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
{
    if (_schemaReady) return;
    await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        if (_schemaReady) return;
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var seed = connection.CreateCommand())
        {
            seed.CommandText = _connectionInfo.IsPostgres ? PostgresSeedSql : SqliteSeedSql;
            await seed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _schemaReady = true;
    }
    finally
    {
        _schemaGate.Release();
    }
}
```

**Critical for D-02:** Add a third command inside the gate that runs the reaper UPDATE:
```sql
-- D-02 reaper: any row left non-terminal at startup is by definition orphaned (single-instance Render)
UPDATE harvest_runs
   SET state='Failed',
       error_message='interrupted by redeploy',
       completed_utc=now()
 WHERE state IN ('Queued','Running','Stopping');
```
**Idempotent** — running on a fresh DB hits zero rows, no-op.

**IsPostgres dialect-branching SQL constant pattern** (lines 152-202) — copy structure verbatim, swap schema:
```csharp
private const string PostgresCreateTableSql = """
    CREATE TABLE IF NOT EXISTS feature_flags (
      key        TEXT PRIMARY KEY,
      enabled    BOOLEAN NOT NULL DEFAULT TRUE,
      updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
    );
    """;

private const string SqliteCreateTableSql = """
    CREATE TABLE IF NOT EXISTS feature_flags (
      key        TEXT PRIMARY KEY,
      enabled    INTEGER NOT NULL DEFAULT 1,
      updated_at TEXT NOT NULL DEFAULT (datetime('now'))
    );
    """;
```

**For `harvest_runs` D-03 schema (Postgres form):**
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
SQLite variant: `UUID` → `TEXT`, `TIMESTAMPTZ` → `TEXT`, `BOOLEAN`/`INT` unchanged. Lowercase `INTEGER` for Boolean if any, but harvest_runs has none.

**Parameter-binding dialect-branch pattern** (lines 86-98) — both stores use this verbatim:
```csharp
RelationalDatabaseConnection.AddParameter(command, "@key", key);
RelationalDatabaseConnection.AddParameter(
    command, "@enabled",
    _connectionInfo.IsPostgres ? (object)enabled : (enabled ? 1 : 0));
RelationalDatabaseConnection.AddParameter(
    command, "@now",
    _connectionInfo.IsPostgres
        ? (object)now.UtcDateTime
        : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
```

**`ReadBool` cross-provider helper** (lines 131-143):
```csharp
private static bool ReadBool(DbDataReader reader, int ordinal)
{
    var raw = reader.GetValue(ordinal);
    return raw switch
    {
        bool b => b,
        long l => l != 0,
        int i => i != 0,
        short s => s != 0,
        string str => str == "1" || string.Equals(str, "true", StringComparison.OrdinalIgnoreCase),
        _ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
    };
}
```

**`ReadTimestamp` cross-provider helper** — copy from `Services/AdminBruteForceTrackerStore.cs:124-134`:
```csharp
private static DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal)
{
    var raw = reader.GetValue(ordinal);
    return raw switch
    {
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
        DateTimeOffset dto => dto.ToUniversalTime(),
        string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
        _ => new DateTimeOffset(Convert.ToDateTime(raw, CultureInfo.InvariantCulture), TimeSpan.Zero)
    };
}
```
**Use this for every `started_utc / completed_utc / requested_utc` read in `HarvestRunStore`.**

**Connection-open pattern** (lines 145-150):
```csharp
private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
{
    var connection = _connectionInfo.CreateConnection();
    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    return connection;
}
```

**`pg_database_size()` dialect branch (HarvestStatsAggregator)** — anchor pattern from `Services/AdminBruteForceTrackerStore.cs:114-121` (IsPostgres branch on whole SQL string):
```csharp
command.CommandText = _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql;
```
For storage size:
```csharp
if (!_connectionInfo.IsPostgres) return null; // SQLite path: UI renders "N/A"
command.CommandText = "SELECT pg_database_size(current_database())";
// scalar Int64 read, return as long?
```

---

### `Services/Harvest/IHarvestScheduleStore.cs` + `HarvestScheduleStore.cs` (store, CRUD)

**Analog:** `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (single-row table variant)

Same structure as `HarvestRunStore` (above) with single-row schema:

**Schema constants (Postgres):**
```sql
CREATE TABLE IF NOT EXISTS harvest_schedule (
  id              INT PRIMARY KEY CHECK (id = 1),
  interval_hours  INT NULL CHECK (interval_hours IS NULL OR interval_hours IN (2,4,8,24)),
  paused          BOOLEAN NOT NULL DEFAULT FALSE,
  updated_utc     TIMESTAMPTZ NOT NULL DEFAULT now()
);
INSERT INTO harvest_schedule (id, interval_hours, paused) VALUES (1, NULL, FALSE)
ON CONFLICT (id) DO NOTHING;
```
**Per RESEARCH.md** (Open Question #6 resolution): seed the row on EnsureSchemaAsync — eliminates a null-row branch on every page render.

**Method shape:**
```csharp
Task<HarvestScheduleSnapshot> GetAsync(CancellationToken ct);
Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken ct);
Task EnsureSchemaAsync(CancellationToken ct);
```

**UPSERT SQL pattern** — mirror `FeatureFlagStore.cs:188-202` (PostgresUpsertSql / SqliteUpsertSql, EXCLUDED form):
```sql
INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
VALUES (1, @interval, @paused, @now)
ON CONFLICT (id) DO UPDATE SET
  interval_hours = EXCLUDED.interval_hours,
  paused         = EXCLUDED.paused,
  updated_utc    = EXCLUDED.updated_utc;
```

---

### `Services/Harvest/IHarvestScheduleCache.cs` + `HarvestScheduleCache.cs` (cache, event-driven)

**Analog:** `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs` (verbatim shape)

**Interface shape — copy from `IFeatureFlagCache.cs` lines 11-36, swap method names:**
```csharp
public interface IHarvestScheduleCache
{
    HarvestScheduleSnapshot Snapshot();
    Task ReloadAsync(CancellationToken cancellationToken = default);
}

public sealed record HarvestScheduleSnapshot(int? IntervalHours, bool Paused, DateTimeOffset UpdatedUtc);
```

**Class shape — copy from `FeatureFlagCache.cs` (lines 14-43), swap dependencies + snapshot type:**
```csharp
public sealed class FeatureFlagCache : BackgroundService, IFeatureFlagCache
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IFeatureFlagStore _store;
    private readonly ILogger<FeatureFlagCache> _logger;
    // ... volatile snapshot field, replaced atomically by ReloadAsync ...
    private volatile IReadOnlyDictionary<string, bool> _snapshot =
        new Dictionary<string, bool>(0, StringComparer.Ordinal);

    public FeatureFlagCache(IFeatureFlagStore store, ILogger<FeatureFlagCache> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Test seam — bypasses logging plumbing for unit tests that drive the cache directly.</summary>
    internal FeatureFlagCache(IFeatureFlagStore store)
        : this(store, NullLogger<FeatureFlagCache>.Instance) { }
```

**Sync StartAsync initial-load pattern (D-14 mirror)** (lines 87-91):
```csharp
public override async Task StartAsync(CancellationToken cancellationToken)
{
    await ReloadAsync(cancellationToken).ConfigureAwait(false);
    await base.StartAsync(cancellationToken).ConfigureAwait(false);
}
```

**PeriodicTimer poller body** (lines 95-109) — for `HarvestScheduleCache` keep the 30-60s backstop; **`HarvestScheduleService` is a separate class** with its own `ExecuteAsync` doing the schedule-tick logic:
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(PollInterval);
    try
    {
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await ReloadAsync(stoppingToken).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        // Normal shutdown path.
    }
}
```

**ReloadAsync atomic-replace + preserve-on-failure pattern** (lines 62-80):
```csharp
public async Task ReloadAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var fresh = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        _snapshot = fresh;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        // T-06-D1: never replace a good snapshot with an empty one on transient PG failure.
        _logger.LogError(exception,
            "FeatureFlag.ReloadFailure could not refresh feature_flags snapshot; existing snapshot preserved (count={Count}).",
            _snapshot.Count);
    }
}
```

---

### `Services/Harvest/HarvestScheduleService.cs` (hosted-bg, event-driven tick)

**Analog (BackgroundService body shape):** `Services/FeatureFlags/FeatureFlagCache.cs:95-109` (PeriodicTimer + try/while/OCE)
**Analog (job-enqueue mechanics):** `Services/ArchidektCacheJobService.cs:36-90` (channel + enqueue + dedup)

**Composition pattern:**
```csharp
public sealed class HarvestScheduleService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

    private readonly IFeatureFlagCache _flagCache;            // gate: harvest.cron.enabled
    private readonly IHarvestScheduleCache _scheduleCache;    // Snapshot()
    private readonly IHarvestRunStore _runStore;              // GetLastSuccessUtcAsync, GetActiveAsync
    private readonly IArchidektCacheJobService _jobService;   // EnqueueAsync(60min)
    private readonly ILogger<HarvestScheduleService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // 1. Phase 6 D-12 kill-switch — copy from ScryfallTaggerService.cs:94-98
        if (!_flagCache.IsEnabled("harvest.cron.enabled")) return;

        // 2. Snapshot from cache (no per-tick PG roundtrip — D-07)
        var s = _scheduleCache.Snapshot();
        if (s.Paused || s.IntervalHours is null) return;

        // 3. Read last_success from PG (1 query/tick is fine — RESEARCH.md confirms)
        var last = await _runStore.GetLastSuccessUtcAsync(ct).ConfigureAwait(false);
        if (last is null) return;

        // 4. Compute next_due, fire if elapsed
        var nextDue = last.Value + TimeSpan.FromHours(s.IntervalHours.Value);
        if (DateTimeOffset.UtcNow >= nextDue)
        {
            _logger.LogInformation("Harvest.Schedule.Tick firing scheduled run intervalHours={IntervalHours} lastSuccess={LastSuccess}.",
                s.IntervalHours, last);
            await _jobService.EnqueueAsync(TimeSpan.FromMinutes(60), ct).ConfigureAwait(false);
        }
    }
}
```

**Flag-gate pattern** — copy verbatim from `Services/ScryfallTaggerService.cs:94-98`:
```csharp
// FLAG-04, D-11: kill-switch gate. Off → return empty without any HTTP work.
if (!_flagCache.IsEnabled("scryfall.tagger.enabled"))
{
    return Array.Empty<string>();
}
```
For `HarvestScheduleService` the early return is `return;` (no payload).

---

### `Services/ArchidektCacheJobService.cs` (MODIFIED — drop dict, write through PG, hold cancel CTS)

**Self-analog** — surgical modifications to the existing file. Keep the `BackgroundService + Channel` shape (RESEARCH.md Open Question #3 resolution).

**Current dict-based dedup at `Services/ArchidektCacheJobService.cs:65-89` becomes:**
```csharp
public async Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default)
{
    if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");
    if (duration > TimeSpan.FromHours(1)) throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot exceed one hour.");

    // D-01: PG is source of truth — check for any active row before inserting a new one
    var active = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
    if (active is not null)
    {
        return new ArchidektCacheJobEnqueueResult(MapToStatus(active), StartedNewJob: false);
    }

    // D-03: insert Queued row, get the UUID
    var jobId = await _runStore.InsertQueuedAsync(
        HarvestRunKind.Bulk,
        (int)Math.Ceiling(duration.TotalSeconds),
        url: null,
        DateTimeOffset.UtcNow,
        cancellationToken).ConfigureAwait(false);

    var queuedSignal = new QueuedJobSignal(jobId, (int)Math.Ceiling(duration.TotalSeconds));
    _queue.Writer.TryWrite(queuedSignal);
    return new ArchidektCacheJobEnqueueResult(/* mapped status */, StartedNewJob: true);
}
```

**Per-job CTS pattern (D-05) — new field + Cancel method:**
```csharp
private CancellationTokenSource? _activeJobCts;
private readonly object _ctsLock = new();

public async Task<bool> CancelActiveAsync(CancellationToken cancellationToken)
{
    CancellationTokenSource? cts;
    lock (_ctsLock) { cts = _activeJobCts; }
    if (cts is null) return false;

    // D-05 + RESEARCH.md Open Question #1: write the Stopping row from the
    // controller side BEFORE returning (so AJAX poll sees it within 1s).
    // The cancel itself is fire-and-forget; OCE in ExecuteAsync flips to Cancelled.
    cts.Cancel();
    return true;
}
```

**ExecuteAsync linked-token pattern** — replace existing lines 105-150 with PG writes + linked CTS:
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var signal in _queue.Reader.ReadAllAsync(stoppingToken))
    {
        // D-05: link host stoppingToken with a per-job CTS for graceful operator cancel
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        lock (_ctsLock) { _activeJobCts = jobCts; }

        try
        {
            await _runStore.UpdateStateAsync(
                signal.JobId, HarvestRunState.Running,
                startedUtc: DateTimeOffset.UtcNow, completedUtc: null,
                decksProcessed: 0, additionalDecksFound: 0, errorMessage: null,
                jobCts.Token).ConfigureAwait(false);

            var initialDeckCount = await _knowledgeStore.GetProcessedDeckCountAsync(jobCts.Token);
            var decksProcessed = await _knowledgeStore.RunCacheSweepAsync(_logger, signal.DurationSeconds, jobCts.Token);
            var finalDeckCount = await _knowledgeStore.GetProcessedDeckCountAsync(jobCts.Token);

            await _runStore.UpdateStateAsync(
                signal.JobId, HarvestRunState.Succeeded,
                startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                decksProcessed, additionalDecksFound: Math.Max(finalDeckCount - initialDeckCount, 0),
                errorMessage: null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw; // host shutdown
        }
        catch (OperationCanceledException)
        {
            // D-05: operator cancel landed (jobCts.Cancel from controller)
            await _runStore.UpdateStateAsync(
                signal.JobId, HarvestRunState.Cancelled,
                startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                decksProcessed: 0, additionalDecksFound: 0,
                errorMessage: "cancelled by operator",
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Archidekt cache background job {JobId} failed.", signal.JobId);
            await _runStore.UpdateStateAsync(
                signal.JobId, HarvestRunState.Failed,
                startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                decksProcessed: 0, additionalDecksFound: 0,
                errorMessage: exception.Message, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lock (_ctsLock) { _activeJobCts = null; }
        }
    }
}
```

**Drop entirely:**
- `ConcurrentDictionary<Guid, ArchidektCacheJobStatus> _jobs` (line 39)
- `Guid? _activeJobId` (line 43)
- `ClearActiveJob` (lines 152-161)
- The `_jobs[...]` writes inside ExecuteAsync (lines 109, 114, 129, 146)

**Keep:**
- `Channel<...>` (line 38) — needed for graceful enqueue + ordered execution
- `BackgroundService` shape — needed for IHostedService lifecycle
- `EnqueueAsync` 60-min cap throw at lines 60-63 — already matches HARV-01

**Wire-compat note for `ArchidektCacheJobsController` (`Controllers/Api/ArchidektCacheJobsController.cs`):** the existing public API contract (freeform 1-3600s) must keep working. `GetJob(Guid)` and `GetActiveJob()` on the interface now read from PG via `_runStore` — return `ArchidektCacheJobStatus?` constructed from `HarvestRunRow`. Don't change controller signatures.

---

### `Controllers/Admin/AdminHarvestController.cs` (controller, request-response + AJAX)

**Analog 1 (admin form pattern):** `Controllers/Admin/AdminFlagsController.cs` (Phase 6, shipped)
**Analog 2 (same-origin gate):** `Controllers/Api/ArchidektCacheJobsController.cs:25-28`
**Analog 3 (admin layout binding):** existing `Views/AdminHarvest/_ViewStart.cshtml` already sets `Layout="_AdminLayout"`

**Class + route + ctor pattern** (lines 28-43 of AdminFlagsController):
```csharp
[Route("Admin/Flags")]
public sealed class AdminFlagsController : Controller
{
    private readonly IFeatureFlagStore _store;
    private readonly IFeatureFlagCache _cache;

    public AdminFlagsController(IFeatureFlagStore store, IFeatureFlagCache cache)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cache);
        _store = store;
        _cache = cache;
    }
```

For `AdminHarvestController` `[Route("Admin/Harvest")]` already exists at `Controllers/Admin/AdminHarvestController.cs:10`. Replace the placeholder body with the multi-action shape below; inject:
- `IHarvestRunStore _runStore`
- `IHarvestScheduleStore _scheduleStore`
- `IHarvestScheduleCache _scheduleCache`
- `IArchidektCacheJobService _jobService` (existing — for EnqueueAsync + Cancel)
- `IArchidektDeckImporter _deckImporter` (existing DI — for URL harvest, RESEARCH.md confirms `Program.cs:294`)
- `ICategoryKnowledgeStore _knowledgeStore` (existing — for single-deck PersistObservedCategoriesAsync)
- `IHarvestStatsAggregator _stats` (new)

**GET Index pattern** (lines 49-59 AdminFlagsController):
```csharp
[HttpGet("")]
public IActionResult Index()
{
    var snapshot = _cache.Snapshot();
    var rows = snapshot
        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => new FlagRow(kv.Key, kv.Value))
        .ToArray();
    var vm = new AdminFlagsListViewModel { Flags = rows };
    return View(vm);
}
```
For `AdminHarvest`, Index becomes `async` and assembles a single `AdminHarvestViewModel` containing: stats payload, schedule snapshot, active job (or null), recent-runs list. Single PG round-trip via the cached aggregator.

**POST + write + cache-reload + redirect pattern (D-07 mirrors Phase 6 D-10)** (lines 70-93):
```csharp
[HttpPost("{key}/toggle")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Toggle(string key, bool enabled, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(key)) return BadRequest();

    var snapshot = _cache.Snapshot();
    if (!snapshot.ContainsKey(key)) return BadRequest("Unknown flag key.");

    await _store.SetEnabledAsync(key, enabled, cancellationToken).ConfigureAwait(false);
    await _cache.ReloadAsync(cancellationToken).ConfigureAwait(false);

    TempData["AdminFlagsAction"] = $"Flag '{key}' is now {(enabled ? "enabled" : "disabled")}.";
    return RedirectToAction(nameof(Index));
}
```

**For AdminHarvest applies to:**
- `POST RunNow(int durationSeconds)` — validate against whitelist `{900, 1800, 3600}` (D-04), call `_jobService.EnqueueAsync(TimeSpan.FromSeconds(durationSeconds), ct)`, set `TempData["HarvestResult"]`, redirect.
- `POST Cancel()` — call `_jobService.CancelActiveAsync(ct)`. **Per RESEARCH.md Open Question #1**: write `Stopping` row directly from this controller action (before returning) so AJAX poll sees it within 1s. Set `TempData`, redirect.
- `POST SubmitUrl(string url)` — D-09 sync path: validate URL, call `_runStore.InsertQueuedAsync(kind: Url, ...)`, run inline import via `_deckImporter.ImportAsync(url, ct)` + `_knowledgeStore.PersistObservedCategoriesAsync(...)`, write Succeeded/Failed PG row, set TempData banner with `"Harvested {commander}: {n} new observations"`, redirect.
- `POST SaveSchedule(int? intervalHours, bool paused)` — validate `intervalHours` against `{2,4,8,24}` or null, call `_scheduleStore.SaveAsync(...)`, then **`await _scheduleCache.ReloadAsync(ct)` (D-07 sync invalidation)**, redirect.
- `POST PauseSchedule(bool paused)` — same write+reload+redirect.

**`[ValidateAntiForgeryToken]` is mandatory on every POST** — exact precedent: `AdminFlagsController.cs:71` and `AdminFeedbackController.cs:69`.

**AJAX status endpoint pattern** (D-08) — combine `AdminFlagsController` GET shape + same-origin gate from `Controllers/Api/ArchidektCacheJobsController.cs:25-28`:
```csharp
[HttpGet("status")]
public async Task<IActionResult> Status(CancellationToken cancellationToken)
{
    if (!SameOriginRequestValidator.IsValid(Request))
    {
        return StatusCode(StatusCodes.Status403Forbidden,
            new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
    }

    var active = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
    return Json(new
    {
        active = active is null ? null : new { active.Id, active.State, active.StartedUtc, /*...*/ }
    });
}
```
**Per RESEARCH.md Open Question #2**: 1-second `IMemoryCache` TTL on this endpoint to absorb tight 3s polling under multiple browser tabs. Optional polish.

**TempData banner pattern** (line 91):
```csharp
TempData["AdminFlagsAction"] = $"Flag '{key}' is now {(enabled ? "enabled" : "disabled")}.";
```
For AdminHarvest use distinct keys: `TempData["HarvestRunResult"]`, `TempData["HarvestUrlResult"]`, `TempData["HarvestScheduleResult"]`.

---

### `Views/AdminHarvest/Index.cshtml` (view, render)

**Analog:** `Views/AdminFlags/Index.cshtml` (Phase 6, shipped — lines 1-45)

**Top of view + TempData banner** (lines 1-11):
```razor
@model DeckFlow.Web.Controllers.Admin.AdminFlagsListViewModel
@{
    ViewData["Title"] = "Flags";
    var actionMessage = TempData["AdminFlagsAction"] as string;
}

<section class="admin-flags">
    @if (!string.IsNullOrEmpty(actionMessage))
    {
        <div class="admin-banner admin-banner--success">@actionMessage</div>
    }
```

For Harvest: read three TempData keys (`HarvestRunResult`, `HarvestUrlResult`, `HarvestScheduleResult`) and render banner per panel. Use `<section class="admin-harvest__panel">` per D-11 panel and class `admin-banner admin-banner--success` (already styled in `wwwroot/css/admin.css`).

**POST form + antiforgery pattern** (lines 34-38):
```razor
<form method="post" asp-action="Toggle" asp-route-key="@flag.Key" class="admin-action-form">
    @Html.AntiForgeryToken()
    <input type="hidden" name="enabled" value="@((!flag.Enabled).ToString().ToLowerInvariant())" />
    <button type="submit">@(flag.Enabled ? "Disable" : "Enable")</button>
</form>
```

For Harvest panels:
- **Run Now** form posts to `RunNow` with `<select name="durationSeconds">` (15/30/60 min); status block shows live status from server-rendered VM, then JS replaces it on poll.
- **Single URL** form posts to `SubmitUrl` with `<input name="url">`.
- **Schedule** form posts to `SaveSchedule` with `<select name="intervalHours">` (Off/2/4/8/24) + `<button>Pause</button>` posting to `PauseSchedule`.
- **Stats** is read-only display table (no form).

**`<noscript>` fallback (D-08):**
```razor
<noscript>
    <meta http-equiv="refresh" content="5" />
</noscript>
```

**Scripts section bind for TS module:**
```razor
@section Scripts {
    <script src="~/js/admin-harvest.js" asp-append-version="true"></script>
}
```
**Note:** the `Scripts` section is rendered by `_AdminLayout.cshtml` at line 36 (`@RenderSection("Scripts", required: false)`).

---

### `wwwroot/ts/admin-harvest.ts` (client-script, request-response poll)

**Analog:** `DeckFlow.Web/wwwroot/ts/category-suggestions.ts` (fetch + JSON pattern, lines 410-459)

**Fetch + JSON parse + error fallback pattern** (lines 419-458):
```typescript
try {
  const response = await fetch(endpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(readRequestData(form))
  });

  if (!response.ok) {
    let payload: { message?: string; Message?: string } | null = null;
    try { payload = await response.json() as { message?: string; Message?: string }; }
    catch { payload = null; }
    handleError(/* ... */, payload?.message ?? payload?.Message ?? 'Unable to fetch suggestions.');
    return;
  }

  const payload = await response.json() as CardSuggestionResponse;
  // ... render ...
} catch (error) {
  const message = error instanceof Error ? error.message : 'Unable to fetch suggestions.';
  handleError(/* ... */, message);
}
```

**For admin-harvest.ts:** GET (not POST), recursive `setTimeout` (3000) loop while state ∈ {Queued,Running,Stopping}, stop on terminal state. Native fetch only — no jQuery, no helper library (per CLAUDE.md anti-patterns + Don't-Hand-Roll table in RESEARCH.md):

```typescript
((): void => {
  'use strict';
  const POLL_INTERVAL_MS = 3000;
  const TERMINAL_STATES = new Set(['Succeeded', 'Failed', 'Cancelled']);

  type StatusResponse = { active: { id: string; state: string; startedUtc: string | null; decksProcessed: number; /* ... */ } | null };

  const renderStatus = (data: StatusResponse): void => {
    // update status block in DOM
  };

  const poll = async (): Promise<void> => {
    try {
      const res = await fetch('/Admin/Harvest/status', { method: 'GET' });
      if (!res.ok) return; // soft-fail; meta-refresh fallback covers it
      const data = await res.json() as StatusResponse;
      renderStatus(data);
      if (data.active && !TERMINAL_STATES.has(data.active.state)) {
        window.setTimeout(poll, POLL_INTERVAL_MS);
      }
    } catch { /* network glitch — let meta-refresh handle it */ }
  };

  // Kick off only if the server-rendered state is non-terminal
  document.addEventListener('DOMContentLoaded', () => {
    const root = document.querySelector<HTMLElement>('[data-harvest-status]');
    if (!root) return;
    const initialState = root.dataset.state;
    if (initialState && !TERMINAL_STATES.has(initialState)) {
      window.setTimeout(poll, POLL_INTERVAL_MS);
    }
  });
})();
```

**MSBuild compile pickup:** TypeScript files in `wwwroot/ts/*.ts` compile to `wwwroot/js/*.js` automatically via the `CompileTypeScriptAssets` MSBuild target in `DeckFlow.Web.csproj`. **No csproj edit needed** — just drop the `.ts` file in `wwwroot/ts/` and reference `~/js/admin-harvest.js` in the Razor view.

---

### `Extensions/HarvestServiceCollectionExtensions.cs` (DI extension)

**Analog:** `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` (verbatim shape)

**Full pattern** (lines 1-28 — copy verbatim, swap names):
```csharp
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

public static class FeatureFlagsServiceCollectionExtensions
{
    public static IServiceCollection AddDeckFlowFeatureFlags(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureFlagStore, FeatureFlagStore>();
        services.AddSingleton<FeatureFlagCache>();
        services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>());
        services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagCache>());
        return services;
    }
}
```

**For `AddDeckFlowHarvest()`:**
```csharp
public static IServiceCollection AddDeckFlowHarvest(this IServiceCollection services)
{
    services.AddSingleton<IHarvestRunStore, HarvestRunStore>();
    services.AddSingleton<IHarvestScheduleStore, HarvestScheduleStore>();

    services.AddSingleton<HarvestScheduleCache>();
    services.AddSingleton<IHarvestScheduleCache>(sp => sp.GetRequiredService<HarvestScheduleCache>());
    services.AddHostedService(sp => sp.GetRequiredService<HarvestScheduleCache>());

    services.AddHostedService<HarvestScheduleService>();

    services.AddSingleton<IHarvestStatsAggregator, HarvestStatsAggregator>();
    return services;
}
```

---

### `Program.cs` (MODIFIED — register stores + cache + hosted service; bootstrap schema)

**Self-analog** — surgical edits at three locations.

**Insertion point 1 — DI registration (line 159, immediately after `AddDeckFlowFeatureFlags()`):**
```csharp
builder.Services.AddDeckFlowFeatureFlags();
builder.Services.AddDeckFlowHarvest();   // NEW — Phase 7
```

**Insertion point 2 — startup schema bootstrap.** RESEARCH.md notes that `EnsureSchemaAsync` lazy-bootstraps on first call from the cache's `StartAsync`. The `HarvestScheduleCache.StartAsync` mirrors `FeatureFlagCache.StartAsync` (lines 87-91) — it calls `ReloadAsync` which calls `_store.GetAllAsync` which calls `EnsureSchemaAsync` (the D-02 reaper runs here, **before Kestrel binds**, because the hosted-service `StartAsync` blocks on the synchronous load).

**For `IHarvestRunStore`:** there's no cache layer, so the planner needs to call `_runStore.EnsureSchemaAsync()` explicitly at startup to run the D-02 reaper. Pattern: extend the existing `ValidateDatabaseConnectionsAsync` block referenced at the bottom of Program.cs (line ~373):
```csharp
// Phase 7 D-02: run startup reaper. Idempotent — fresh DB hits zero rows.
await app.Services.GetRequiredService<IHarvestRunStore>()
    .EnsureSchemaAsync(CancellationToken.None);
```
Place this BEFORE `app.RunAsync()` so the reaper has run before any request hits `/Admin/Harvest`.

**Insertion point 3 — existing ArchidektCacheJobService registration at lines 281-283 stays unchanged:**
```csharp
builder.Services.AddSingleton<ArchidektCacheJobService>();
builder.Services.AddSingleton<IArchidektCacheJobService>(sp => sp.GetRequiredService<ArchidektCacheJobService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ArchidektCacheJobService>());
```
The ctor signature for `ArchidektCacheJobService` gains `IHarvestRunStore` — DI resolves automatically because both are registered as Singleton.

**No changes to:**
- `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/Admin"), ...)` block at lines 332-334 — admin auth path already covers `/Admin/Harvest`.
- Forwarded-headers ordering (lines 305 `app.UseForwardedHeaders()` BEFORE security headers and HTTPS redirect — must remain).

---

## Shared Patterns

### Pattern S-1: Antiforgery on every admin POST (ADMIN-05 carry-forward)

**Source:** `Controllers/Admin/AdminFlagsController.cs:71` and `Controllers/Admin/AdminFeedbackController.cs:69`
**Apply to:** All five POST actions on `AdminHarvestController` (RunNow, Cancel, SubmitUrl, SaveSchedule, PauseSchedule)

```csharp
[HttpPost("{key}/toggle")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Toggle(string key, bool enabled, CancellationToken cancellationToken)
```

Razor side (already shown):
```razor
<form method="post" asp-action="Toggle" ...>
    @Html.AntiForgeryToken()
    ...
</form>
```

### Pattern S-2: Same-origin gate on AJAX endpoints (RESEARCH.md Pattern 5)

**Source:** `Controllers/Api/ArchidektCacheJobsController.cs:25-28`
**Apply to:** `GET /Admin/Harvest/status` (the only AJAX endpoint on this surface)

```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden,
        new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
}
```
Note: `SameOriginRequestValidator.IsValid` returns `true` when both Origin and Referer are absent (line 32) — non-browser callers unaffected, browsers always send one.

### Pattern S-3: IsPostgres dialect branching (RESEARCH.md Pattern 2 + memory `feedback_sqlite_postgres_sql_divergence.md`)

**Source:** `Services/FeatureFlags/FeatureFlagStore.cs:88,113,119` + `Services/AdminBruteForceTrackerStore.cs:114`
**Apply to:** `HarvestRunStore` (every SQL command), `HarvestScheduleStore`, `HarvestStatsAggregator` (especially `pg_database_size()` PG-only path)

```csharp
command.CommandText = _connectionInfo.IsPostgres ? PostgresSomeSql : SqliteSomeSql;
RelationalDatabaseConnection.AddParameter(command, "@now",
    _connectionInfo.IsPostgres
        ? (object)now.UtcDateTime
        : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
```

### Pattern S-4: Singleton + IHostedService dual registration (RESEARCH.md Pattern 3)

**Source:** `Extensions/FeatureFlagsServiceCollectionExtensions.cs:20-27`
**Apply to:** `HarvestScheduleCache` (singleton facade for callers + hosted-service for StartAsync sync load + 30s poller)

```csharp
services.AddSingleton<HarvestScheduleCache>();
services.AddSingleton<IHarvestScheduleCache>(sp => sp.GetRequiredService<HarvestScheduleCache>());
services.AddHostedService(sp => sp.GetRequiredService<HarvestScheduleCache>());
```

### Pattern S-5: Sync StartAsync initial load (D-14 mirror)

**Source:** `Services/FeatureFlags/FeatureFlagCache.cs:87-91`
**Apply to:** `HarvestScheduleCache.StartAsync`

```csharp
public override async Task StartAsync(CancellationToken cancellationToken)
{
    await ReloadAsync(cancellationToken).ConfigureAwait(false);
    await base.StartAsync(cancellationToken).ConfigureAwait(false);
}
```
Net effect: PG `harvest_schedule` row read → `EnsureSchemaAsync` runs → schedule snapshot populated → host reports ready.

### Pattern S-6: Public DI ctor + internal test ctor + ArgumentNullException.ThrowIfNull

**Source:** `Services/FeatureFlags/FeatureFlagCache.cs:32-43`
**Apply to:** All new sealed services in `Services/Harvest/`

```csharp
public FeatureFlagCache(IFeatureFlagStore store, ILogger<FeatureFlagCache> logger)
{
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(logger);
    _store = store;
    _logger = logger;
}

internal FeatureFlagCache(IFeatureFlagStore store)
    : this(store, NullLogger<FeatureFlagCache>.Instance) { }
```
`InternalsVisibleTo("DeckFlow.Web.Tests")` is already in `DeckFlow.Web/AssemblyInfo.cs`.

### Pattern S-7: Structured Serilog logging — named placeholders only

**Source:** `Services/FeatureFlags/FeatureFlagCache.cs:76-79, 116-118`; `Services/ScryfallTaggerService.cs:94-98`
**Apply to:** All new services

```csharp
_logger.LogError(exception,
    "FeatureFlag.ReloadFailure could not refresh feature_flags snapshot; existing snapshot preserved (count={Count}).",
    _snapshot.Count);
```
Use dotted-prefix message templates (`Harvest.Schedule.Tick`, `Harvest.Run.StateChange`, `Harvest.UrlImport.Success`, `Harvest.Run.Cancelled`). Never interpolate.

### Pattern S-8: Per-feature flag-gate at top of public method (FLAG-04 / D-12 carry-forward)

**Source:** `Services/ScryfallTaggerService.cs:94-98`
**Apply to:** `HarvestScheduleService.TickAsync` (gate by `harvest.cron.enabled`)

```csharp
// FLAG-04, D-11: kill-switch gate. Off → return empty without any HTTP work.
if (!_flagCache.IsEnabled("scryfall.tagger.enabled"))
{
    return Array.Empty<string>();
}
```

### Pattern S-9: TempData banner + RedirectToAction after admin POST (D-07)

**Source:** `Controllers/Admin/AdminFlagsController.cs:91-92`
**Apply to:** All five admin harvest POST actions

```csharp
TempData["AdminFlagsAction"] = $"Flag '{key}' is now {(enabled ? "enabled" : "disabled")}.";
return RedirectToAction(nameof(Index));
```

---

## No Analog Found

**None.** Every Phase 7 file has at least one strong shipped analog. This is consistent with the RESEARCH.md "key insight" that Phase 7 should be ~90% pattern reuse from Phases 5-6. Net-new pattern count = **0**.

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Services/FeatureFlags/` — Phase 6 cache + store (primary anchor)
- `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs` — Phase 5 dialect-branching anchor
- `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — existing job machinery
- `DeckFlow.Web/Controllers/Admin/` — admin controller placement + antiforgery precedent
- `DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs` — same-origin gate
- `DeckFlow.Web/Views/AdminFlags/Index.cshtml` + `_ViewStart.cshtml` — admin Razor panel + layout binding
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — admin chrome contract (renders Scripts section)
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — AJAX guard
- `DeckFlow.Web/wwwroot/ts/category-suggestions.ts` + `deck-sync.ts` — fetch + JSON pattern
- `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` — DI extension shape
- `DeckFlow.Web/Program.cs` — composition root insertion points

**Files scanned:** 14 (8 read in full, 4 read in targeted sections, 2 verified via grep)
**Pattern extraction date:** 2026-05-03
