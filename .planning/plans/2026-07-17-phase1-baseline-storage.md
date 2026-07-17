# Phase 1 — Manabase Baseline Storage (`manabase_baseline` table + store) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or subagent-driven-development) to implement task-by-task. Steps use checkbox (`- [ ]`) tracking.

**Goal:** Persist the confidence-weighting inputs. Add the `manabase_baseline` table (SQLite + Postgres, dialect-guarded), a `ManabaseBaselineStore` that upserts and reads rows, a Core row DTO, and DI wiring. Foundation only — **no consumer yet** (Phase 3 job writes it, Phase 4 analyzer reads it). Additive, no behavior change to any existing surface.

**Architecture:** Mirrors the established `FeedbackStore` pattern (`DeckFlow.Web/Services/Persistence/FeedbackStore.cs`): a store holding a `RelationalDatabaseConnection` + a tiny per-dialect helper, lazy `EnsureSchemaAsync` via raw ADO.NET DDL, Dapper for CRUD. The `manabase_baseline` table **co-locates in the category-knowledge database** (`category-knowledge.db` / its Postgres logical DB) because the baseline is derived from that same crawl corpus and Phase 3's aggregation job will read the corpus and write the baseline in one place.

**Tech Stack:** C# 12 / .NET 10, Dapper (already used by every store), xUnit (`DeckFlow.Web.Tests`). No new dependencies. LF endings; changed lines pass the format gate.

**Build/test (Windows dotnet from WSL):**
- Build web: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`
- Store tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "ManabaseBaselineStoreTests"`
- Full Web suite: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
- Full Core suite (regression): `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`

---

## Conventions (confirmed against the repo)

- **Store pattern:** `DeckFlow.Web/Services/Persistence/FeedbackStore.cs` — `RelationalDatabaseConnection _connectionInfo`, `SemaphoreSlim _schemaGate`, `volatile bool _schemaReady`, lazy `EnsureSchemaAsync` with placeholder-token DDL (`__..._COLUMN_TYPE__` swapped per dialect), Dapper `CommandDefinition` for reads/writes.
- **Per-dialect helper:** `DeckFlow.Web/Services/Persistence/FeedbackDialect.cs` — Web-layer class, `For(connection)` returns a SQLite/Postgres instance exposing the timestamp column type. Mirror this shape but smaller.
- **Connection factory:** `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` — the delegating-method idiom (`CreateAdminThrottleConnection => CreateFeedbackConnection`). Add `CreateManabaseBaselineConnection => CreateCategoryKnowledgeConnection`.
- **Dialect column type:** `_connectionInfo.Dialect.SurrogateIdColumnType` for ids (not needed here — no surrogate id); timestamp type comes from our helper (`"TEXT"` SQLite / `"TIMESTAMPTZ"` Postgres), exactly as `FeedbackDialect.FeedbackCreatedUtcColumnType`.
- **DateTime round-trip:** Dapper type handlers are registered in `RelationalDatabaseConnection`'s static ctor (`DapperTypeHandlers.EnsureRegistered()`); `FeedbackStore` stores `DateTime.UtcNow` into the TEXT/TIMESTAMPTZ column and reads it back — do the same for `computed_utc`. `PostgresStorageTests` asserts `DateTimeKind.Utc` on read; mirror that.
- **SQLite store test:** `DeckFlow.Web.Tests/FeedbackStoreTests.cs` — temp file `Path.Combine(Path.GetTempPath(), $"...-{Guid.NewGuid():N}.db")`, `IDisposable` cleanup calling `SqliteConnection.ClearAllPools()`. Mirror exactly.
- **Postgres integration test:** `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` — `[PostgresFact]` (gated by env `DECKFLOW_POSTGRES_TESTS`, skips otherwise), `IClassFixture<PostgresContainerFixture>`, `await _fixture.GetConnectionStringOrSkipAsync()`. These are the "14 skipped" tests in normal CI.
- **Upsert portability:** both SQLite and Postgres support `INSERT ... ON CONFLICT (cols) DO UPDATE SET c = excluded.c`. One SQL string serves both — do NOT fork it per dialect.
- Allman braces, file-scoped namespaces, 4-space indent, nullable enabled, `sealed`, XML docs on public types/members.

---

## File Structure

**Create:**
- `DeckFlow.Core/Manabase/ManabaseBaselineRow.cs` — `ManabaseBaselineRow` record + `ManabaseBaselineSources` string constants.
- `DeckFlow.Web/Services/Persistence/ManabaseBaselineDialect.cs` — timestamp column type per dialect.
- `DeckFlow.Web/Services/Persistence/IManabaseBaselineStore.cs` — store interface.
- `DeckFlow.Web/Services/Persistence/ManabaseBaselineStore.cs` — store implementation.
- `DeckFlow.Web.Tests/ManabaseBaselineStoreTests.cs` — SQLite integration tests.
- `DeckFlow.Web.Tests/Integration/ManabaseBaselineStorePostgresTests.cs` — `[PostgresFact]` round-trip.

**Modify:**
- `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` — add `CreateManabaseBaselineConnection`.
- `DeckFlow.Web/Program.cs` — register `IManabaseBaselineStore` (mirror the `FeedbackStore` registration lifetime — foundation only, no consumer yet).

---

## Task 1: Core row DTO + source constants

**Files:** Create `DeckFlow.Core/Manabase/ManabaseBaselineRow.cs`

- [ ] **Step 1:** Create the file:

```csharp
namespace DeckFlow.Core.Manabase;

/// <summary>
/// Canonical string values for the <c>source</c> column of the manabase baseline table.
/// Stored verbatim (lowercase) so the column reads the same across dialects and future EDHREC rows.
/// </summary>
public static class ManabaseBaselineSources
{
    /// <summary>Rows aggregated from DeckFlow's own classified crawl corpus.</summary>
    public const string Corpus = "corpus";

    /// <summary>Rows backfilled from EDHREC (optional, permission-gated — not written in this milestone).</summary>
    public const string Edhrec = "edhrec";

    /// <summary>The commander_slug sentinel identifying the global-per-bracket fallback row.</summary>
    public const string GlobalCommanderSlug = "*";
}

/// <summary>
/// One persisted baseline cell: the average lands/ramp/draw a set of decks ran for a given
/// (commander, bracket, source), with the sample size behind the average. A row where
/// <see cref="CommanderSlug"/> equals <see cref="ManabaseBaselineSources.GlobalCommanderSlug"/>
/// is the global-per-bracket fallback. Averages are always present (computed over the sample).
/// </summary>
public sealed record ManabaseBaselineRow
{
    /// <summary>Canonical commander key, or <c>*</c> for the global-per-bracket fallback row.</summary>
    public required string CommanderSlug { get; init; }

    /// <summary>Power bracket 1-5 (Exhibition..cEDH).</summary>
    public required int Bracket { get; init; }

    /// <summary>Data source: <see cref="ManabaseBaselineSources.Corpus"/> or <see cref="ManabaseBaselineSources.Edhrec"/>.</summary>
    public required string Source { get; init; }

    /// <summary>Average land count across the sample.</summary>
    public required double AvgLands { get; init; }

    /// <summary>Average ramp count across the sample (classified as the analyzer's ramp budget).</summary>
    public required double AvgRamp { get; init; }

    /// <summary>Average card-draw count across the sample (classified as the analyzer's draw budget).</summary>
    public required double AvgDraw { get; init; }

    /// <summary>Number of decks behind the averages (weighting + display).</summary>
    public required int DeckCount { get; init; }

    /// <summary>UTC time the cell was computed.</summary>
    public required DateTime ComputedUtc { get; init; }
}
```

- [ ] **Step 2:** Build Core: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj` → 0/0.
- [ ] **Step 3:** Commit: `git commit -m "feat(manabase): add baseline row DTO + source constants (Core)"`

---

## Task 2: Web dialect helper

**Files:** Create `DeckFlow.Web/Services/Persistence/ManabaseBaselineDialect.cs`

- [ ] **Step 1:** Create (mirror `FeedbackDialect`, but only the timestamp column type differs — the upsert/select SQL is dialect-identical, so it lives in the store, not here):

```csharp
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;

/// <summary>
/// Provides the one manabase-baseline SQL fragment that differs by provider: the
/// <c>computed_utc</c> column type. Upsert and select SQL are portable (both engines support
/// <c>ON CONFLICT ... DO UPDATE SET c = excluded.c</c>) and live in the store.
/// </summary>
public sealed class ManabaseBaselineDialect
{
    /// <summary>Gets the SQL column type for the <c>computed_utc</c> timestamp.</summary>
    public string ComputedUtcColumnType { get; }

    private ManabaseBaselineDialect(string computedUtcColumnType) => ComputedUtcColumnType = computedUtcColumnType;

    private static readonly ManabaseBaselineDialect SqliteInstance = new("TEXT");
    private static readonly ManabaseBaselineDialect PostgresInstance = new("TIMESTAMPTZ");

    /// <summary>Returns the dialect helper for the connection's provider.</summary>
    /// <param name="connection">Connection whose provider selects the dialect.</param>
    public static ManabaseBaselineDialect For(RelationalDatabaseConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.Provider switch
        {
            RelationalDatabaseProvider.Sqlite => SqliteInstance,
            RelationalDatabaseProvider.Postgres => PostgresInstance,
            _ => throw new NotSupportedException($"Unsupported database provider '{connection.Provider}'.")
        };
    }
}
```

- [ ] **Step 2:** (compiles as part of Task 3 build)

---

## Task 3: Store interface + implementation + factory

**Files:**
- Create `DeckFlow.Web/Services/Persistence/IManabaseBaselineStore.cs`
- Create `DeckFlow.Web/Services/Persistence/ManabaseBaselineStore.cs`
- Modify `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs`

- [ ] **Step 1:** Interface:

```csharp
using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Services;

/// <summary>
/// Persists and reads confidence-weighting baseline cells for the manabase feature.
/// </summary>
public interface IManabaseBaselineStore
{
    /// <summary>Inserts or updates one baseline cell (PK = commander_slug + bracket + source).</summary>
    /// <param name="row">The cell to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(ManabaseBaselineRow row, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates many baseline cells in a single transaction.</summary>
    /// <param name="rows">The cells to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRangeAsync(IReadOnlyCollection<ManabaseBaselineRow> rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every stored source row (e.g. corpus and/or edhrec) for the given commander at the
    /// given bracket. Pass <see cref="ManabaseBaselineSources.GlobalCommanderSlug"/> for the global row.
    /// </summary>
    /// <param name="commanderSlug">Canonical commander key, or <c>*</c> for the global row.</param>
    /// <param name="bracket">Power bracket 1-5.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ManabaseBaselineRow>> GetAsync(string commanderSlug, int bracket, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2:** Implementation. Mirror `FeedbackStore` structure exactly (three ctors: SQLite path, `RelationalDatabaseConnection`, `IWebHostEnvironment`; `_schemaGate` + `_schemaReady`; `OpenConnectionAsync`; `EnsureSchemaAsync`). Use Dapper for CRUD.

```csharp
using System.Data.Common;
using Dapper;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;

/// <inheritdoc/>
public sealed class ManabaseBaselineStore : IManabaseBaselineStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly ManabaseBaselineDialect _dialect;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    // Portable across SQLite + Postgres: both support ON CONFLICT ... DO UPDATE SET c = excluded.c.
    private const string UpsertSql = """
        INSERT INTO manabase_baseline
          (commander_slug, bracket, source, avg_lands, avg_ramp, avg_draw, deck_count, computed_utc)
        VALUES
          (@commanderSlug, @bracket, @source, @avgLands, @avgRamp, @avgDraw, @deckCount, @computedUtc)
        ON CONFLICT (commander_slug, bracket, source) DO UPDATE SET
          avg_lands   = excluded.avg_lands,
          avg_ramp    = excluded.avg_ramp,
          avg_draw    = excluded.avg_draw,
          deck_count  = excluded.deck_count,
          computed_utc = excluded.computed_utc;
        """;

    private const string SelectSql = """
        SELECT commander_slug, bracket, source, avg_lands, avg_ramp, avg_draw, deck_count, computed_utc
        FROM manabase_baseline
        WHERE commander_slug = @commanderSlug AND bracket = @bracket;
        """;

    /// <summary>Initializes the store from a SQLite database path.</summary>
    /// <param name="databasePath">Path to the SQLite database file.</param>
    public ManabaseBaselineStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath))
    {
    }

    /// <summary>Initializes the store from a resolved relational connection.</summary>
    /// <param name="connectionInfo">Database provider and connection details.</param>
    public ManabaseBaselineStore(RelationalDatabaseConnection connectionInfo)
    {
        _connectionInfo = connectionInfo;
        _dialect = ManabaseBaselineDialect.For(_connectionInfo);
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <summary>Initializes the store from the web host environment configuration.</summary>
    /// <param name="environment">Web host environment used to resolve the database.</param>
    public ManabaseBaselineStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateManabaseBaselineConnection(environment))
    {
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(ManabaseBaselineRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, ToParameters(row), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpsertRangeAsync(IReadOnlyCollection<ManabaseBaselineRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(UpsertSql, ToParameters(row), transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ManabaseBaselineRow>> GetAsync(string commanderSlug, int bracket, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commanderSlug);
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ManabaseBaselineRow>(new CommandDefinition(
            SelectSql,
            new { commanderSlug, bracket },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static object ToParameters(ManabaseBaselineRow row) => new
    {
        commanderSlug = row.CommanderSlug,
        bracket = row.Bracket,
        source = row.Source,
        avgLands = row.AvgLands,
        avgRamp = row.AvgRamp,
        avgDraw = row.AvgDraw,
        deckCount = row.DeckCount,
        computedUtc = row.ComputedUtc,
    };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            // Why: schema management is an intentional raw ADO.NET carve-out, matching FeedbackStore.
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS manabase_baseline (
                      commander_slug TEXT    NOT NULL,
                      bracket        INTEGER NOT NULL,
                      source         TEXT    NOT NULL,
                      avg_lands      REAL    NOT NULL,
                      avg_ramp       REAL    NOT NULL,
                      avg_draw       REAL    NOT NULL,
                      deck_count     INTEGER NOT NULL,
                      computed_utc   __COMPUTED_UTC_COLUMN_TYPE__ NOT NULL,
                      PRIMARY KEY (commander_slug, bracket, source)
                    );
                    """;
                create.CommandText = create.CommandText
                    .Replace("__COMPUTED_UTC_COLUMN_TYPE__", _dialect.ComputedUtcColumnType, StringComparison.Ordinal);
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }
}
```

- [ ] **Step 3:** Add the factory method to `DeckFlowDatabaseConnectionFactory` (place next to `CreateCategoryKnowledgeConnection`, matching the delegating-method idiom + XML doc style):

```csharp
    /// <summary>
    /// Returns the relational connection used by the manabase baseline store. Co-locates with the
    /// category-knowledge database because the baseline is derived from that crawl corpus and the
    /// Phase 3 aggregation job reads the corpus and writes the baseline together.
    /// </summary>
    /// <param name="environment">Web host environment used to resolve local artifact paths.</param>
    public static RelationalDatabaseConnection CreateManabaseBaselineConnection(IWebHostEnvironment environment)
        => CreateCategoryKnowledgeConnection(environment);
```

- [ ] **Step 4:** Build web: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj` → 0/0.
- [ ] **Step 5:** Commit: `git commit -m "feat(manabase): add manabase_baseline table + store (SQLite/Postgres)"`

---

## Task 4: DI registration

**Files:** Modify `DeckFlow.Web/Program.cs`

- [ ] **Step 1:** Register the store mirroring the existing `FeedbackStore`/`IFeedbackStore` registration — **same lifetime** as `FeedbackStore` (find that line; the store is designed as a process-lifetime singleton: schema init once behind `_schemaReady`). Resolve via the `IWebHostEnvironment` ctor. Place the registration next to the other persistence-store registrations. Add a `// Why:` note that it is foundation-only (no consumer until Phase 3/4).
  - Change only the added line(s); do not reflow neighbors (changed-lines format gate).
- [ ] **Step 2:** Build web → 0/0. Confirm the app still composes (registration resolves): `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`.
- [ ] **Step 3:** Commit: `git commit -m "chore(manabase): register IManabaseBaselineStore (foundation, no consumer yet)"`

---

## Task 5: Tests

**Files:**
- Create `DeckFlow.Web.Tests/ManabaseBaselineStoreTests.cs`
- Create `DeckFlow.Web.Tests/Integration/ManabaseBaselineStorePostgresTests.cs`

- [ ] **Step 1: SQLite store tests.** Mirror `FeedbackStoreTests` setup exactly: temp-file SQLite DB in the ctor (`Path.Combine(Path.GetTempPath(), $"manabase-baseline-test-{Guid.NewGuid():N}.db")`), `new ManabaseBaselineStore(_dbPath)`, `IDisposable` cleanup with `SqliteConnection.ClearAllPools()` + delete. Namespace `DeckFlow.Web.Tests`. Cover:
  1. **Upsert_then_Get_returns_row** — upsert a corpus row (commander "smeagol-helpful-guide", bracket 3), `GetAsync` returns exactly one row with every field equal (avgLands/avgRamp/avgDraw/deckCount/source/commanderSlug/bracket).
  2. **Upsert_same_key_updates_in_place** — upsert then upsert again with the same (slug,bracket,source) but new averages/deckCount; `GetAsync` returns a **single** row bearing the updated values (no duplicate).
  3. **Get_returns_all_sources_for_cell** — upsert a `corpus` and an `edhrec` row for the same (slug,bracket); `GetAsync` returns both (assert count 2 and both sources present).
  4. **Get_unknown_returns_empty** — `GetAsync("nobody", 1)` returns an empty list (not null).
  5. **Global_row_roundtrips** — upsert a row with `CommanderSlug = ManabaseBaselineSources.GlobalCommanderSlug` ("*"), bracket 2; `GetAsync("*", 2)` returns it.
  6. **ComputedUtc_roundtrips_utc** — upsert with a known `DateTime.UtcNow`; on read assert the value is within ~1s and (where the provider preserves it) `Kind`/value round-trips. (SQLite stores TEXT; assert the round-tripped instant matches to the second.)
  7. **UpsertRange_persists_all** — `UpsertRangeAsync` with 3 rows across 2 brackets; `GetAsync` per (slug,bracket) returns the expected rows; empty collection is a no-op.
  8. **Get_scopes_by_bracket** — same slug+source at bracket 3 and bracket 4; `GetAsync(slug,3)` returns only the bracket-3 row.

- [ ] **Step 2: Postgres integration test.** Create `ManabaseBaselineStorePostgresTests` in `DeckFlow.Web.Tests.Integration`, `IClassFixture<PostgresContainerFixture>`, one or two `[PostgresFact]` methods (skips unless `DECKFLOW_POSTGRES_TESTS`). Mirror `PostgresStorageTests`: build the store from `new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync())`. Cover: upsert → get round-trip (all fields), conflict-update-in-place, and `ComputedUtc` returns with `DateTimeKind.Utc` (mirrors the feedback assertion `Assert.Equal(DateTimeKind.Utc, fetched.CreatedUtc.Kind)`) — this is the real dialect proof (TIMESTAMPTZ path). Use a unique slug per run (`Guid`) so the shared container stays clean.

- [ ] **Step 3:** Run store tests: `--filter "ManabaseBaselineStoreTests"` → all pass. Then full Web suite → green (the new PG test **skipped**, matching the existing skipped-PG count going up by 1-2). Then full Core suite → green (Task 1 DTO is additive).
- [ ] **Step 4:** Commit: `git commit -m "test(manabase): baseline store tests (SQLite + Postgres round-trip)"`

---

## Task 6: Review for simplification

- [ ] **Step 1:** Review the diff for reduction without losing behavior (e.g. the `ToParameters` helper already dedupes the param object; confirm no other duplication). If your harness has `/simplify`, run it; else review by hand.
- [ ] **Step 2:** Re-run `--filter "ManabaseBaselineStoreTests"` → PASS.
- [ ] **Step 3:** Commit if anything changed: `git add -A && git commit -m "chore(manabase): simplify baseline store" || echo "nothing to simplify"`

---

## Self-Review notes (author)

- **Spec coverage:** implements spec Component 1 (`manabase_baseline` table, dialect-guarded, PK `(commander_slug,bracket,source)`, columns per the spec table) + the store to write/read it. Components 2-6 are later phases; the store exposes exactly what Phase 3 (write via `UpsertRangeAsync`) and Phase 4 (read via `GetAsync(slug,bracket)` + `GetAsync("*",bracket)`) need.
- **Co-location decision:** baseline lives in `category-knowledge.db` (via `CreateManabaseBaselineConnection => CreateCategoryKnowledgeConnection`) — same DB as the corpus it is derived from, so Phase 3 reads+writes in one place. Matches the factory's established "small operational stores share a DB" idiom.
- **Dialect surface is minimal:** only `computed_utc`'s column type forks (TEXT/TIMESTAMPTZ). Upsert + select SQL are portable (`ON CONFLICT ... excluded.*` works on both engines), so they are `const` in the store, not per-dialect. This is deliberately smaller than `FeedbackDialect` (which also forks order-by + insert-returning).
- **`source` as string constants, not an enum:** avoids Dapper enum⇄text mapping on a greenfield table and keeps the DB value identical to the spec (`corpus`/`edhrec`). Distinct from Phase 2's `ManabaseBaselineSource` enum (weighting provenance: commander/blended/global/none) — different concept, avoided a name clash.
- **No timestamp comparison anywhere:** `computed_utc` is write+read only (no WHERE/ORDER BY on it), so the prior Postgres `::timestamptz`-cast bug (F-51-PG-01, which bit a timestamp *comparison*) cannot arise here. If a freshness filter is added later, revisit.
- **DateTime round-trip risk:** relies on the same `DapperTypeHandlers` path `FeedbackStore` uses. The `ComputedUtc_roundtrips_utc` SQLite test and the `[PostgresFact]` `DateTimeKind.Utc` assertion are the guards; if SQLite TEXT loses `Kind`, assert on the instant (to the second) rather than `Kind`.
- **Additive / no behavior change:** new table + new store + one factory method + one DI line; nothing existing is modified in behavior. Full Core + Web suites stay green; only the skipped-PG count rises.
- **Constraints:** no new deps, LF, changed-lines format gate. New files LF. Do not touch compiled JS, lockfiles, or unrelated code.
- **Open item for Phase 3 (not this phase):** the corpus currently stores no per-deck decklists, land/ramp/draw counts, or bracket signal (see scout findings) — so *populating* `corpus` rows is a separate design problem. Phase 1 only builds the container.
