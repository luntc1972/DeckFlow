# Phase 49: Dapper Data-Access Adoption - Pattern Map

**Mapped:** 2026-06-14
**Files analyzed:** 16 (2 new + 1 modified registration hook + 13 modified stores)
**Analogs found:** 16 / 16

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Storage/DapperTypeHandlers.cs` | utility | transform | `DeckFlow.Core/Content/ContentHarvestRunStore.cs` (`ReadDecimal`/`FormatTimestamp`) | role-match (coercion logic is here) |
| `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs` | test | CRUD | `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` | exact |
| `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` *(add static ctor)* | utility | — | self | exact |
| `DeckFlow.Web/Services/FeedbackStore.cs` *(spike)* | service | CRUD | self (before/after) | exact |
| `DeckFlow.Core/Content/ContentHarvestRunStore.cs` | service | CRUD | self (before/after) | exact |
| `DeckFlow.Core/Content/ContentSourceStore.cs` | service | CRUD | self (before/after) | exact |
| `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` | service | CRUD | self (before/after) | exact |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | repository | CRUD + transaction | self (before/after) | exact |
| All other 9 eligible stores (Wave 1–3) | service | CRUD | `FeedbackStore.cs` post-conversion | role-match |

---

## Pattern Assignments

### `DeckFlow.Core/Storage/DapperTypeHandlers.cs` (utility, transform) — NEW FILE

**Analog for coercion logic:** `DeckFlow.Core/Content/ContentHarvestRunStore.cs` (lines 164–209) and `DeckFlow.Core/Content/ContentSourceStore.cs` (lines 176–198)

**Namespace / file convention:** One public type (`DapperTypeHandlers`) per file, `sealed` internal handler classes, file-scoped namespace. The public registration class is `public static class DapperTypeHandlers`; the four handlers are `internal sealed class *TypeHandler`.

**Imports pattern** — mirror `ContentHarvestRunStore.cs` lines 1–4 plus Dapper:
```csharp
using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Storage;
```

**Registration guard pattern** (D-04) — `Interlocked.Exchange` as the once-only gate:
```csharp
public static class DapperTypeHandlers
{
    private static int _registered;

    /// <summary>
    /// Registers all Dapper type handlers and enables underscore-to-PascalCase column mapping.
    /// Thread-safe; idempotent — safe to call from static ctors, DI setup, and tests.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1) return;

        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
        SqlMapper.AddTypeHandler(new DecimalTypeHandler());
        SqlMapper.AddTypeHandler(new BoolTypeHandler());
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        // Why: D-01 — global flag maps snake_case DB columns to PascalCase C# properties;
        // safe because the project owns 100% of all queries.
        SqlMapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
```

**DateTimeTypeHandler** — canonical read comes from `FeedbackStore.cs:226-231`; write from `FeedbackStore.cs:63` and `ContentHarvestRunStore.cs:167-170`:
```csharp
internal sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value) => value switch
    {
        DateTime dt        => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        DateTimeOffset dto => dto.UtcDateTime,
        string text        => DateTime.Parse(text, null, DateTimeStyles.RoundtripKind),
        _                  => Convert.ToDateTime(value)
    };

    public override void SetValue(IDbDataParameter p, DateTime value)
    {
        if (p is SqliteParameter)
            p.Value = value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        else
            p.Value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
```

**DecimalTypeHandler** — canonical read from `ContentHarvestRunStore.cs:198-209`; write from `ContentHarvestRunStore.cs:164-165`:
```csharp
internal sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
{
    public override decimal Parse(object value) => value switch
    {
        decimal d => d,
        double d  => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
        float f   => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
        string t  => decimal.Parse(t, CultureInfo.InvariantCulture),
        _         => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
    };

    public override void SetValue(IDbDataParameter p, decimal value)
    {
        if (p is SqliteParameter)
            p.Value = value.ToString(CultureInfo.InvariantCulture);
        else
            p.Value = value;
    }
}
```

**BoolTypeHandler** — canonical read from `ContentSourceStore.cs:176-188`; write from `ContentSourceStore.cs:125-128`:
```csharp
internal sealed class BoolTypeHandler : SqlMapper.TypeHandler<bool>
{
    public override bool Parse(object value) => value switch
    {
        bool b   => b,
        long l   => l != 0,
        int i    => i != 0,
        short s  => s != 0,
        string s => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
        _        => Convert.ToBoolean(value)
    };

    public override void SetValue(IDbDataParameter p, bool value)
    {
        if (p is SqliteParameter)
            p.Value = value ? 1 : 0;
        else
            p.Value = value;
    }
}
```

**GuidTypeHandler** — canonical read from `HarvestRunStore.cs:383-389`; write from `HarvestRunStore.cs:118-121`:
```csharp
internal sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value) => value switch
    {
        Guid g   => g,
        string s => Guid.Parse(s),
        _        => throw new InvalidCastException($"Cannot convert {value?.GetType()} to Guid")
    };

    public override void SetValue(IDbDataParameter p, Guid value)
    {
        if (p is SqliteParameter)
            p.Value = value.ToString();
        else
            p.Value = value;
    }
}
```

**Open question for planner:** Stores that use `DateTimeOffset` result-record properties (all Content stores, `SpendLedgerBase`, `HarvestRunStore`) need mapping from the `DateTime` the handler returns to `DateTimeOffset`. Two options: (a) add a `TypeHandler<DateTimeOffset>` as the 4th handler (replacing Guid if no store uses Guid in result records — unlikely given `HarvestRunStore` does), or (b) keep result record properties typed as `DateTime` where Dapper maps timestamps. The planner must verify per-store record types before implementation. If `DateTimeOffset` properties exist on any mapped record, add a 5th handler or replace Guid with DateTimeOffset as the 4th. The research recommends verifying per store; flag in the plan if a 5th handler is needed.

---

### `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` *(add static constructor only)*

**Analog:** self (lines 1–120 already read)

**Only change:** add a static constructor before the existing `CreateConnection()` method. Touch no other lines (LF + formatting rules):

```csharp
// Insert after line 17 (after the closing brace of RelationalDatabaseProvider enum
// and before the sealed record declaration), OR as a static ctor inside the record:

static RelationalDatabaseConnection()
{
    // Why: registers Dapper type handlers once before any store opens a connection.
    // Must be the earliest possible registration point — static ctors run before
    // any instance method, including OpenConnectionAsync. See Phase 49 DapperTypeHandlers.
    DapperTypeHandlers.EnsureRegistered();
}
```

Note: `RelationalDatabaseConnection` is a `sealed record` (line 22). C# records support static constructors. The static ctor goes inside the record body.

---

### `DeckFlow.Web/Services/FeedbackStore.cs` — SPIKE CONVERSION (Wave 0)

**Analog:** self (lines 1–306 already read) — this is the before/after template

**Imports to add** (after existing `using` block, lines 1–4):
```csharp
using Dapper;
```

**Connection pattern preserved** — `FeedbackStore` uses `_connectionInfo.CreateConnection()` + `OpenAsync()` directly (NOT `OpenConnectionAsync()`), intentionally omitting FK PRAGMA. The private `OpenConnectionAsync` method (lines 247–252) stays unchanged. Pass its result to Dapper calls.

**BEFORE — `AddAsync` write path** (lines 59–74): `DbCommand` + `AddParameter` + `ExecuteScalarAsync` + `Convert.ToInt64`
```csharp
// BEFORE (lines 59-74):
await using var connection = await OpenConnectionAsync(cancellationToken);
await using var command = connection.CreateCommand();
command.CommandText = _connectionInfo.Dialect.FeedbackInsertReturningIdSql;
RelationalDatabaseConnection.AddParameter(command, "@created",
    _connectionInfo.IsPostgres ? DateTime.UtcNow : DateTime.UtcNow.ToString("O"));
// ... 7 more AddParameter calls ...
var idObj = await command.ExecuteScalarAsync(cancellationToken);
return Convert.ToInt64(idObj);
```

**AFTER — `AddAsync` write path** (Dapper):
```csharp
// AFTER: SQL text verbatim from _connectionInfo.Dialect.FeedbackInsertReturningIdSql
await using var connection = await OpenConnectionAsync(cancellationToken);
return await connection.ExecuteScalarAsync<long>(
    _connectionInfo.Dialect.FeedbackInsertReturningIdSql,
    new
    {
        created = DateTime.UtcNow,   // DateTimeTypeHandler.SetValue handles encoding
        type = submission.Type.ToString(),
        message = submission.Message,
        email = submission.Email,
        pageUrl = Truncate(context.PageUrl, 500),
        userAgent = Truncate(context.UserAgent, 500),
        ipHash = HashIpInternal(context.Ip),
        appVersion = context.AppVersion,
        status = FeedbackStatus.New.ToString()
    }).ConfigureAwait(false);
```

Note: Dapper anonymous-object property names must match `@paramName` in the SQL. The SQL uses `@created`, `@type`, etc. — verify dialect SQL param names before naming the anonymous object properties.

**BEFORE — `GetAsync` reader loop** (lines 82–93):
```csharp
// BEFORE (lines 82-93):
await using var reader = await command.ExecuteReaderAsync(cancellationToken);
if (!await reader.ReadAsync(cancellationToken)) return null;
return ReadItem(reader);
```

**AFTER — `GetAsync` with Dapper QuerySingleOrDefaultAsync**:
```csharp
// AFTER: FeedbackItem must be a class or record with { get; init; } properties
// matching column names via MatchNamesWithUnderscores (id, created_utc→CreatedUtc, etc.)
await using var connection = await OpenConnectionAsync(cancellationToken);
return await connection.QuerySingleOrDefaultAsync<FeedbackItem>(
    "SELECT id, created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status FROM feedback WHERE id = @id",
    new { id }).ConfigureAwait(false);
```

**BEFORE — `ListAsync` reader loop** (lines 104–125):
```csharp
// BEFORE (lines 118-125):
var results = new List<FeedbackItem>();
await using var reader = await command.ExecuteReaderAsync(cancellationToken);
while (await reader.ReadAsync(cancellationToken))
{
    results.Add(ReadItem(reader));
}
return results;
```

**AFTER — `ListAsync` with Dapper QueryAsync**:
```csharp
// AFTER: returns IEnumerable<FeedbackItem> which callers convert to IReadOnlyList<T>
await using var connection = await OpenConnectionAsync(cancellationToken);
var results = await connection.QueryAsync<FeedbackItem>(sql, param).ConfigureAwait(false);
return results.ToList();
```

**Spike gate check:** After conversion, `ReadItem(DbDataReader)` (lines 224–245) is deleted entirely. Zero occurrences of `ExecuteReaderAsync`, `GetValue`, `GetString`, `GetInt64`, `DateTime.SpecifyKind`, `DateTime.Parse` remain in non-DDL methods. The spike PASSes only when these are fully absent.

**`FeedbackItem` mapping caveat:** `FeedbackItem` is a record (check `DeckFlow.Web/Models/`). Dapper can map to records if they have a constructor with matching parameter names OR `{ get; init; }` properties. Per CLAUDE.md: never convert `{ get; init; }` to `{ get; }`. Verify FeedbackItem's record structure before conversion — if it uses a positional constructor, Dapper cannot map to it automatically and the record may need `{ get; init; }` properties added.

---

### `DeckFlow.Core/Content/ContentHarvestRunStore.cs` — WAVE 2 CONVERSION

**Analog:** self (lines 1–238 already read) — representative before/after

**Helpers deleted on conversion:** `FormatDecimal()` (line 164), `FormatTimestamp()` (line 167), `ReadDateTimeOffset()` (line 186), `ReadDecimal()` (line 198) — all four replaced by global type handlers.

**BEFORE — `StartRunAsync` write + RETURNING** (lines 64–80):
```csharp
// BEFORE (lines 64-80):
await using var command = connection.CreateCommand();
command.CommandText = "INSERT INTO content_harvest_runs (...) VALUES (@startedUtc, @spendUsd) RETURNING id;";
RelationalDatabaseConnection.AddParameter(command, "@startedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
RelationalDatabaseConnection.AddParameter(command, "@spendUsd", FormatDecimal(0m));
var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
return ContentStoreGeneratedId.Read(id);
```

**AFTER — `StartRunAsync` with Dapper ExecuteScalarAsync**:
```csharp
// AFTER: SQL verbatim; type handlers encode DateTimeOffset and decimal automatically
await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
var id = await connection.ExecuteScalarAsync<long>(
    "INSERT INTO content_harvest_runs (started_utc, spend_usd) VALUES (@startedUtc, @spendUsd) RETURNING id;",
    new { startedUtc = DateTimeOffset.UtcNow, spendUsd = 0m }).ConfigureAwait(false);
return id;
```

Note: `ContentStoreGeneratedId.Read(id)` is a helper that calls `Convert.ToInt64`. `ExecuteScalarAsync<long>` makes it unnecessary.

**BEFORE — `GetRunAsync` reader loop** (lines 130–158): `ExecuteReaderAsync` + `ReadRun(reader)` private method

**AFTER — `GetRunAsync` with Dapper**:
```csharp
// AFTER: result record ContentHarvestRun needs { get; init; } properties with names
// matching: id, started_utc→StartedUtc, completed_utc→CompletedUtc, etc.
await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
return await connection.QuerySingleOrDefaultAsync<ContentHarvestRun>(sql, new { runId })
    .ConfigureAwait(false);
```

---

### `DeckFlow.Core/Content/ContentSourceStore.cs` — WAVE 1 CONVERSION

**Analog:** self (lines 1–200 read) — representative before/after

**Helpers deleted on conversion:** `ReadBool()` (line 176), `ReadDateTimeOffset()` (line 190).

**BEFORE — bool write coercion** (lines 125–129):
```csharp
// BEFORE: provider-branch on every write
RelationalDatabaseConnection.AddParameter(
    command, "@isEnabled",
    _connectionInfo.IsPostgres ? (object)isEnabled : isEnabled ? 1 : 0);
```

**AFTER — bool write via Dapper** (BoolTypeHandler.SetValue handles encoding):
```csharp
// AFTER: pass bool directly; BoolTypeHandler.SetValue encodes to 1/0 for SQLite
await connection.ExecuteAsync(
    "UPDATE content_sources SET is_enabled = @isEnabled WHERE id = @id",
    new { isEnabled, id }).ConfigureAwait(false);
```

**BEFORE — `ListEnabledSourcesAsync` reader loop** (lines 134–159):
```csharp
// BEFORE (lines 152-158):
await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
{
    sources.Add(ReadSource(reader));
}
return sources;
```

**AFTER — `ListEnabledSourcesAsync` with Dapper**:
```csharp
// AFTER: bool param encoded by BoolTypeHandler; ContentSource needs { get; init; } props
await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
var sources = await connection.QueryAsync<ContentSource>(sql, new { isEnabled = true })
    .ConfigureAwait(false);
return sources.ToList();
```

---

### `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` — WAVE 2 CONVERSION

**Analog:** self (lines 1–404 read) — Guid pattern is the distinctive concern here

**BEFORE — Guid write** (lines 118–121):
```csharp
// BEFORE: explicit provider branch per write
RelationalDatabaseConnection.AddParameter(
    command, "@id",
    _connectionInfo.IsPostgres ? (object)id : id.ToString());
```

**AFTER — Guid write via Dapper** (GuidTypeHandler.SetValue handles encoding):
```csharp
// AFTER: pass Guid directly; GuidTypeHandler encodes to string for SQLite
await connection.ExecuteAsync(sql, new { id, kind = ..., now = ..., ... }).ConfigureAwait(false);
```

**BEFORE — Guid read** (lines 383–389):
```csharp
// BEFORE: manual switch in ReadHarvestRunRow
var idRaw = reader.GetValue(0);
var id = idRaw switch
{
    Guid g   => g,
    string s => Guid.Parse(s),
    _        => Guid.Parse(reader.GetString(0))
};
```

**AFTER — Guid read via Dapper**: GuidTypeHandler.Parse is called automatically; result record property `Id` of type `Guid` is populated directly.

**`BindNullableTimestamp` helper** (used for nullable DateTime params): this is a write-path formatter. After conversion, pass `DateTimeOffset?` directly in the anonymous object — `DateTimeTypeHandler.SetValue` handles encoding, and Dapper passes `null` as `DBNull.Value` for nullable types automatically. The helper is deleted.

---

### `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — WAVE 3 CONVERSION (LAST)

**Analog:** self — transaction pattern is the distinctive concern

**BEFORE — transaction + manual command wiring** (lines 430–471):
```csharp
// BEFORE (lines 431-448): connection opened manually; commands use .Transaction property
await using var connection = CreateConnection();
await connection.OpenAsync(cancellationToken);
await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

var deleteCommand = connection.CreateCommand();
deleteCommand.Transaction = transaction;
deleteCommand.CommandText = "DELETE FROM card_category_observations WHERE source_id = @sourceId;";
RelationalDatabaseConnection.AddParameter(deleteCommand, "@sourceId", sourceId.Value);
await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
```

**AFTER — transaction passed to Dapper calls**:
```csharp
// AFTER: same connection open pattern (note: this store uses CreateConnection() + OpenAsync()
// directly, NOT _connectionInfo.OpenConnectionAsync() — preserve this)
await using var connection = CreateConnection();
await connection.OpenAsync(cancellationToken);
await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

await connection.ExecuteAsync(
    "DELETE FROM card_category_observations WHERE source_id = @sourceId;",
    new { sourceId = sourceId.Value },
    transaction: transaction).ConfigureAwait(false);
```

**All Dapper calls inside a transaction scope MUST pass `transaction:` parameter.** Missing `transaction:` runs outside the transaction — a silent correctness bug.

**Card-id cache loop pattern**: `ResolveCardIdAsync` passes `connection` and `transaction` as parameters. After conversion, both are forwarded to Dapper `ExecuteAsync` and `ExecuteScalarAsync<long>` calls. The in-memory `Dictionary<string, long>` cache and the helper signature stay unchanged; only the inner ADO.NET execution calls change.

---

### All Other 9 Eligible Stores (Waves 1–3)

**Analog:** `FeedbackStore.cs` post-conversion + the store-specific `ContentHarvestRunStore` / `ContentSourceStore` patterns above.

**Common transformation template for every store:**

1. Add `using Dapper;` to the file's using block.
2. For each non-DDL method:
   - Replace `await using var command = connection.CreateCommand();` + `command.CommandText = sql;` + N × `AddParameter(...)` + `ExecuteReaderAsync` loop → `connection.QueryAsync<TResult>(sql, new { param1, param2 })`.
   - Replace `ExecuteNonQueryAsync` pattern → `connection.ExecuteAsync(sql, new { ... })`.
   - Replace `ExecuteScalarAsync` + `Convert.ToInt64` / `Convert.ToInt32` pattern → `connection.ExecuteScalarAsync<long>(sql, new { ... })`.
3. Delete the store's private `ReadBool`, `ReadDecimal`, `ReadDateTimeOffset`, `ReadTimestamp`, `FormatTimestamp`, `FormatDecimal`, `FormatVisibility`, `BindNullableTimestamp` helpers.
4. Replace provider-branch write expressions (`_connectionInfo.IsPostgres ? x : y`) in param values with the direct value (type handler handles encoding).
5. Add `// Why: stays raw ADO.NET — DDL/schema-init; Dapper does not improve schema management. See Phase 49 SPEC §Boundaries.` comment to each DDL method.

---

## Shared Patterns

### Registration Chokepoint
**Source:** `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` static ctor (to be added)
**Apply to:** Called automatically before any store opens a connection. No store calls `EnsureRegistered()` directly.
```csharp
static RelationalDatabaseConnection()
{
    DapperTypeHandlers.EnsureRegistered();
}
```

### `OpenConnectionAsync` vs `CreateConnection` + `OpenAsync` — Preserve Per Store
**Source:** `FeedbackStore.cs:247-252` (CreateConnection + OpenAsync — no FK PRAGMA) vs `ContentHarvestRunStore.cs:161-162` (OpenConnectionAsync — FK PRAGMA applied)

Each store's converted Dapper calls are made on the connection returned by the SAME connection-open call the store used before. Do not change the connection-open pattern when converting.

| Store | Connection Open Method | FK PRAGMA Applied |
|-------|----------------------|-------------------|
| `FeedbackStore` | `_connectionInfo.CreateConnection()` + `OpenAsync()` | No (intentional) |
| `CategoryKnowledgeRepository` | `CreateConnection()` + `OpenAsync()` | No |
| All Content stores, `HarvestRunStore`, etc. | `_connectionInfo.OpenConnectionAsync()` | Yes |

### `EnsureSchemaAsync` DDL Methods — Stay Raw
**Source:** All stores, e.g. `ContentHarvestRunStore.cs:43-61`
**Apply to:** Every DDL method in every store.
```csharp
// Why: DDL / schema-init / ALTER TABLE / migration — Dapper does not improve schema
// management and the project's immutable-migration rule protects these methods.
// See Phase 49 SPEC §Boundaries.
await using var create = connection.CreateCommand();
create.CommandText = ...;
await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
```

### `ContentStoreGeneratedId.Read(id)` Replacement
**Source:** `ContentHarvestRunStore.cs:79` — used in multiple content stores for RETURNING id
After conversion: `ExecuteScalarAsync<long>` returns `long` directly; `ContentStoreGeneratedId.Read(id)` is no longer needed at converted call sites (but do not delete the helper if other non-converted sites still use it until all are converted).

---

## Round-Trip Test File

### `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs` — NEW FILE

**Analog:** `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` (lines 1–77)

**Imports pattern** (copy from `PostgresStorageTests.cs` lines 1–8):
```csharp
using DeckFlow.Core.Storage;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;
```

**Class structure** (copy from `PostgresStorageTests.cs:14-20`):
```csharp
/// <summary>
/// Verifies that all four Dapper type handlers round-trip correctly through
/// a real SQLite database (always) and a Postgres container (when DECKFLOW_POSTGRES_TESTS=1).
/// </summary>
public sealed class DapperTypeHandlerRoundTripTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DapperTypeHandlerRoundTripTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }
```

**SQLite test helper** (copy from `CategoryKnowledgeRepositoryTests.cs:15-19`):
```csharp
private static RelationalDatabaseConnection CreateSqliteConnection()
{
    var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests",
        Guid.NewGuid().ToString("N"), "handler-roundtrip.db");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    return RelationalDatabaseConnection.FromSqlitePath(path);
}
```

**Postgres test helper** (copy from `PostgresStorageTests.cs:23-24`):
```csharp
private async Task<RelationalDatabaseConnection> CreatePostgresConnectionAsync()
    => new(RelationalDatabaseProvider.Postgres,
        await _fixture.GetConnectionStringOrSkipAsync());
```

**[Fact] SQLite test shape** (copy structure from `CategoryKnowledgeRepositoryTests.cs:[Fact]` methods):
```csharp
[Fact]
public async Task TypeHandlers_DateTime_RoundTrips_OnSqlite()
{
    var conn = CreateSqliteConnection();
    // create a scratch table, insert via Dapper, read back via Dapper, assert equality
    Assert.Equal(DateTimeKind.Utc, result.Kind);
}
```

**[PostgresFact] Postgres test shape** (copy from `PostgresStorageTests.cs:32`):
```csharp
[PostgresFact]
public async Task TypeHandlers_DateTime_RoundTrips_OnPostgres()
{
    var conn = await CreatePostgresConnectionAsync();
    // same assertions, different provider
    Assert.Equal(DateTimeKind.Utc, result.Kind);
}
```

**Result record mapping caveat**: the scratch table used in the round-trip test needs columns that map to a simple `sealed record` with `{ get; init; }` properties. Use `MatchNamesWithUnderscores` (already set by `EnsureRegistered()`).

---

## No Analog Found

None — all files have close analogs or are self-referential before/after conversions.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Storage/`, `DeckFlow.Core/Content/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Services/Harvest/`, `DeckFlow.Web/Services/FeatureFlags/`, `DeckFlow.Web.Tests/Integration/`, `DeckFlow.Core.Tests/`
**Files scanned:** 11 source files read directly; 4 via targeted grep
**Pattern extraction date:** 2026-06-14
