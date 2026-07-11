# Phase 94: Style-Profile Foundation - Pattern Map

**Mapped:** 2026-07-11
**Files analyzed:** 3 (record set, store+interface, round-trip tests)
**Analogs found:** 3 / 3

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` | model | CRUD (JSON-blob payload) | `DeckFlow.Core/Knowledge/ContentModels.cs` (records) + `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (JSON serialize/deserialize helpers) | role-match (records) / exact (JSON section helper) |
| `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs` + `CreatorStyleProfileStore.cs` | service (store) | CRUD (dialect-guarded upsert/read) | `DeckFlow.Core/Content/ContentSiteIndexStore.cs` + `IContentSiteIndexStore.cs` | exact |
| New xUnit round-trip tests in `DeckFlow.Core.Tests` | test | CRUD round-trip (SQLite + Postgres) | `DeckFlow.Core.Tests/ContentSiteIndexStoreTests.cs` (SQLite) — **no exact Postgres analog exists in `DeckFlow.Core.Tests`**; nearest Postgres pattern is `DeckFlow.Web.Tests/Integration/PostgresFactAttribute.cs` + `PostgresContainerFixture.cs` (different test project) | role-match (SQLite) / **no analog / package gap** (Postgres) |

## Pattern Assignments

### `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (model, CRUD)

**Analog:** `DeckFlow.Core/Knowledge/ContentModels.cs` (record shape) + `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (JSON section serialize/deserialize)

**Record shape pattern** (`ContentModels.cs` lines 6-21):
```csharp
public sealed record ContentSource
{
    /// <summary>Surrogate identifier for the content source row.</summary>
    public required long Id { get; init; }

    /// <summary>URL-safe slug used when constructing content artifact paths.</summary>
    public required string SourceSlug { get; init; }

    /// <summary>Human-readable source name shown in content KB surfaces.</summary>
    public required string DisplayName { get; init; }
    ...
```
Apply this shape to `CreatorStyleProfile`, `StatedRule`, `MeasuredMetric`, `FusedTarget` (D-08 field names) — `sealed record`, `{ get; init; }` / `required` only. **Never** `{ get; }` (CLAUDE.md carve-out; `CarveOutGuardTests.cs` fixture at `DeckFlow.Core.Tests/CarveOutGuardTests.cs:16-28` proves `{ get; init; }` survives `dotnet format` byte-identical — this is the regression tripwire, not something this phase edits, but the record style must match it).

**Min-deck floor const placement** — D-05 says `CreatorStyleProfile.MinDeckFloor = 5` lives as a named const directly on the record type (no existing analog constant-on-record in this codebase; closest precedent for a magic-number-as-named-const is `ContentSiteIndexBatchUpsertException`'s pattern of centralizing shared rules as static members — but simplest is a `public const int MinDeckFloor = 5;` directly inside the `CreatorStyleProfile` record body).

**JSON section serialize/deserialize pattern** (`ContentArtifactSpec.cs` lines 43-68 — this is the exact pattern for D-01's `stated_rules_json`/`measured_metrics_json`/`fused_targets_json` columns):
```csharp
public static string SerializeTags(IReadOnlyList<string> tags)
{
    ArgumentNullException.ThrowIfNull(tags);
    return JsonSerializer.Serialize(tags);
}

public static IReadOnlyList<string> DeserializeTags(string? serializedTags)
{
    if (string.IsNullOrWhiteSpace(serializedTags))
    {
        return Array.Empty<string>();
    }

    return JsonSerializer.Deserialize<string[]>(serializedTags) ?? Array.Empty<string>();
}
```
Write an analogous pair (e.g. `SerializeSection<T>`/`DeserializeSection<T>` or three typed helpers) for `IReadOnlyList<StatedRule>`, `IReadOnlyList<MeasuredMetric>`, `IReadOnlyList<FusedTarget>` — null/whitespace column reads back as `Array.Empty<T>()` (never null), matching D-07 exactly. `using System.Text.Json;` is the only import needed (`ContentArtifactSpec.cs:1`).

---

### `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs` + `CreatorStyleProfileStore.cs` (service/store, CRUD)

**Analog:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` + `IContentSiteIndexStore.cs` — **copy this shape wholesale**, as CONTEXT.md explicitly instructs (Claude's Discretion section). A simpler secondary analog, `DeckFlow.Core/Content/CreatorSourceStore.cs`, shows the same schema-gate pattern WITHOUT the `ensureSchemaEnabled` prod no-op flag — useful only as a "minimal version" cross-check, but `ContentSiteIndexStore` is the correct template since D-Discretion calls for the `ensureSchemaEnabled` flag too.

**Imports pattern** (`ContentSiteIndexStore.cs` lines 1-7):
```csharp
using System.Data.Common;
using System.Globalization;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;
```
(`System.Globalization` is only needed if formatting; drop if unused — CreatorStyleProfileStore likely doesn't need it.)

**Field + ctor pattern (schema-gate + ensureSchemaEnabled + test-seam ctor)** (`ContentSiteIndexStore.cs` lines 13-68):
```csharp
public sealed class ContentSiteIndexStore : IContentSiteIndexStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    public ContentSiteIndexStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    public ContentSiteIndexStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
        : this(connectionInfo, ensureSchemaEnabled, connectionFactoryOverride: null) { }

    internal ContentSiteIndexStore(
        RelationalDatabaseConnection connectionInfo,
        bool ensureSchemaEnabled,
        Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        _ensureSchemaEnabled = ensureSchemaEnabled;
        _connectionFactoryOverride = connectionFactoryOverride;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
```
This is THE test-seam ctor signature (`internal ... Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride`) referenced in `94-CONTEXT.md` Claude's Discretion section — copy verbatim. `[InternalsVisibleTo("DeckFlow.Core.Tests")]` must already cover `DeckFlow.Core` (check `DeckFlow.Core/AssemblyInfo.cs` or `.csproj` — do not re-add if present).

**Schema-gate wiring (EnsureSchemaAsync)** — the `_ensureSchemaEnabled` fast-exit MUST precede the `_schemaReady` fast-path (`ContentSiteIndexStore.cs` lines 70-85):
```csharp
public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
{
    // Why: prod-pointed stores (D-09) disable schema-ensure entirely — no CREATE/ALTER/DROP is
    // issued against prod. Placed before the _schemaReady fast-path so the ~20 call sites are untouched.
    if (!_ensureSchemaEnabled) return;
    if (_schemaReady) return;
    await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        if (_schemaReady) return;
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var create = connection.CreateCommand();
        create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        // ... (ALTER-backfill introspection loop omitted — P94 is a brand-new table, no legacy
        // columns to backfill, so the GetTableColumnsAsync/ALTER dance is unnecessary this phase)
        _schemaReady = true;
    }
    finally
    {
        _schemaGate.Release();
    }
}
```
Because `creator_style_profile` is a brand-new table (not an existing one gaining columns), the new store can skip the `GetTableColumnsAsync` + per-column `ALTER TABLE ... ADD COLUMN` introspection loop that `ContentSiteIndexStore` needs (that loop exists there only because it backfilled columns onto a pre-existing table across several phases). Just `CREATE TABLE IF NOT EXISTS` for both dialects.

**Dialect-guarded DDL (the exact trap to copy correctly — Postgres `timestamptz` vs SQLite `TEXT`)** (`ContentSiteIndexStore.cs` lines 1146-1196, condensed to the relevant columns):
```csharp
private const string PostgresCreateTableSql = """
    CREATE TABLE IF NOT EXISTS content_site_index (
      id                 BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
      ...
      published_utc      TIMESTAMPTZ NULL,
      indexed_utc        TIMESTAMPTZ NOT NULL DEFAULT now(),
      archetype_tags     TEXT NOT NULL DEFAULT '[]',
      ...
      is_visible         BOOLEAN NOT NULL DEFAULT FALSE,
      ...
    );
    """;

private const string SqliteCreateTableSql = """
    CREATE TABLE IF NOT EXISTS content_site_index (
      id                 INTEGER PRIMARY KEY AUTOINCREMENT,
      ...
      published_utc      TEXT NULL,
      indexed_utc        TEXT NOT NULL DEFAULT (datetime('now')),
      archetype_tags     TEXT NOT NULL DEFAULT '[]',
      ...
      is_visible         INTEGER NOT NULL DEFAULT 0,
      ...
    );
    """;
```
For `creator_style_profile` (D-02 column shape), the equivalent DDL is:
```
Postgres: slug TEXT PRIMARY KEY, platform TEXT NOT NULL, min_decks INT NOT NULL,
          insufficient_sample BOOLEAN NOT NULL DEFAULT FALSE,
          stated_rules_json TEXT NULL, measured_metrics_json TEXT NULL, fused_targets_json TEXT NULL,
          updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
SQLite:   slug TEXT PRIMARY KEY, platform TEXT NOT NULL, min_decks INTEGER NOT NULL,
          insufficient_sample INTEGER NOT NULL DEFAULT 0,
          stated_rules_json TEXT NULL, measured_metrics_json TEXT NULL, fused_targets_json TEXT NULL,
          updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
```
**Note D-01's column type note in CONTEXT.md says `jsonb` for Postgres** — but `ContentSiteIndexStore`'s established convention for JSON-blob columns is plain `TEXT NOT NULL DEFAULT '[]'` on BOTH dialects (see `archetype_tags`/`bracket_tags`/`card_category_tags` above — no `jsonb` anywhere in that table, even on Postgres). Flag this discrepancy to the planner: either (a) follow CONTEXT.md's `jsonb` literally (diverges from the copied pattern, gains Postgres-side JSON validation, but Dapper's string round-trip to `jsonb` needs verification — no existing `jsonb` column exists anywhere in this codebase to copy from), or (b) follow the `ContentSiteIndexStore` precedent of plain `TEXT` on both dialects (zero new risk, exact copy). Recommend (b) for planning unless the user re-confirms `jsonb` is required.

**Upsert pattern (Dapper `ON CONFLICT ... DO UPDATE`, single-row UPSERT matching D-04 overwrite semantics)** (`ContentSiteIndexStore.cs` lines 992-1036, `UpsertSql`):
```csharp
private const string UpsertSql = """
    INSERT INTO content_site_index (
      source, title, video_url, artifact_path, published_utc, indexed_utc,
      archetype_tags, bracket_tags, card_category_tags,
      natural_key_type, natural_key_value, is_hidden, is_evergreen, body_sha256)
    VALUES (
      @source, @title, @videoUrl, @artifactPath, @publishedUtc, @indexedUtc,
      @archetypeTags, @bracketTags, @cardCategoryTags,
      @naturalKeyType, @naturalKeyValue, @isHidden, @isEvergreen, @bodySha256)
    ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
      source             = EXCLUDED.source,
      ...
      body_sha256        = EXCLUDED.body_sha256;
    """;
```
For `creator_style_profile`, PK is `slug` directly (D-02: `slug TEXT` PK), so `ON CONFLICT (slug) DO UPDATE SET ...` — simpler than `ContentSiteIndexStore`'s two-column natural key, and no `RelationalDatabaseConnection.AddParameter` raw-ADO path is needed since Dapper's `DynamicParameters` (see below) handles it.

**Parameter-binding helper pattern** (`ContentSiteIndexStore.cs` lines 866-892, `BuildUpsertParameters` — adapt for the profile's flat scalar + 3 JSON-blob shape):
```csharp
private static DynamicParameters BuildUpsertParameters(ContentSiteIndexRow row, (string Type, string Value) naturalKey)
{
    var parameters = new DynamicParameters();
    parameters.Add("source", row.Source);
    ...
    parameters.Add("archetypeTags", ContentArtifactSpec.SerializeTags(row.ArchetypeTags));
    ...
    return parameters;
}
```

**Read pattern (Dapper `QuerySingleOrDefaultAsync` on a read-model class, then map to record)** (`ContentSiteIndexStore.cs` lines 243-263):
```csharp
public async Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
    string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
    ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    var row = await connection.QuerySingleOrDefaultAsync<ContentSiteIndexReadModel>(new CommandDefinition(
        $"""
        SELECT {ContentSiteIndexReadColumns.SelectList}
          FROM content_site_index
         WHERE natural_key_type = @naturalKeyType
           AND natural_key_value = @naturalKeyValue;
        """,
        new { naturalKeyType, naturalKeyValue },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
    return row is null ? null : ContentSiteIndexRowMapper.ToRow(row);
}
```
For `GetBySlugAsync(string slug, ...)`, the query is `WHERE slug = @slug` (single-column PK, simpler than the two-column natural key here). The read-model → record mapper (`ContentSiteIndexRowMapper.ToRow`, not shown but referenced) is where `DeserializeSection<T>` gets called for each JSON column — mirrors `ContentArtifactSpec.DeserializeTags` shown above.

**Read-model class shape** (`DeckFlow.Core/Content/ContentSiteIndexReadModel.cs` lines 1-32, Dapper materialization target — plain class, `{ get; init; }`, `required` for non-null columns):
```csharp
public sealed class ContentSiteIndexReadModel
{
    public long Id { get; init; }
    public required string Source { get; init; }
    ...
    public DateTimeOffset? PublishedUtc { get; init; }
    ...
    public required string ArchetypeTags { get; init; }  // still-serialized JSON text
}
```
A `CreatorStyleProfileReadModel` needs: `slug` (string), `platform` (string), `min_decks` (int), `insufficient_sample` (bool), `stated_rules_json`/`measured_metrics_json`/`fused_targets_json` (nullable string), `updated_utc` (DateTimeOffset).

**Connection-open helper (test-seam-aware)** (`ContentSiteIndexStore.cs` lines 906-909):
```csharp
private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    => _connectionFactoryOverride is not null
        ? await _connectionFactoryOverride(cancellationToken).ConfigureAwait(false)
        : await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
```

**Interface surface (async, `CancellationToken` last param)** — `IContentSiteIndexStore.cs` lines 8-21 show the minimum shape (`EnsureSchemaAsync` + `Upsert*Async` + `GetBy*Async`); `ICreatorStyleProfileStore` per CONTEXT.md Claude's Discretion needs at minimum:
```csharp
public interface ICreatorStyleProfileStore
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default);
    Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
```

---

### Round-trip tests in `DeckFlow.Core.Tests` (test, CRUD round-trip)

**SQLite analog (exact match):** `DeckFlow.Core.Tests/ContentSiteIndexStoreTests.cs` lines 1-33 — temp-file SQLite store per test class, `IDisposable` cleanup with `SqliteConnection.ClearAllPools()` + `GC.Collect()`/`GC.WaitForPendingFinalizers()` before `File.Delete`:
```csharp
using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ContentSiteIndexStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-test-{Guid.NewGuid():N}.db");
        _store = new ContentSiteIndexStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();
    }

    [Fact]
    public async Task UpsertRowAsync_ThenGetByNaturalKey_RoundTripsRowsAndTags()
    {
        await _store.UpsertRowAsync(CreateYoutubeRow("yt-round-trip"));
        ...
        var youtube = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-round-trip");
        Assert.NotNull(youtube);
        ...
    }
```
Copy this shape for `CreatorStyleProfileStoreTests`: temp SQLite file path, `new CreatorStyleProfileStore(dbPath)`, `[Fact] UpsertAsync_ThenGetBySlug_RoundTrips...` covering D-06 (insufficient-sample flag survives), D-07 (measured-only / stated-only / fully-fused partial-profile round-trips with empty-array-not-null reads).

**⚠️ CRITICAL GAP — no Postgres analog exists inside `DeckFlow.Core.Tests`.** Verified via `grep` across the repo:
- `DeckFlow.Core.Tests.csproj` has **no** `Testcontainers.PostgreSql` package reference.
- Only `DeckFlow.Web.Tests.csproj` references `Testcontainers.PostgreSql` (`Version="3.10.0"`), and only `DeckFlow.Web.Tests/Integration/PostgresFactAttribute.cs` + `PostgresContainerFixture.cs` + `RoundTrip/RoundTripSmokeTests.cs` exercise a real Postgres container.
- `DeckFlow.Core.Tests/PostgresConnectionStringNormalizerTests.cs` only unit-tests connection-string parsing (`NpgsqlConnectionStringBuilder`) — it does **not** open a real Postgres connection.

`PostgresFactAttribute` pattern (`DeckFlow.Web.Tests/Integration/PostgresFactAttribute.cs`, full file, 21 lines):
```csharp
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS");
        if (!string.Equals(enabled, "1", StringComparison.Ordinal))
        {
            Skip = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 and ensure Docker is running to enable.";
        }
    }
}
```
`PostgresContainerFixture` pattern (`DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs`, `IAsyncLifetime` fixture, lazy container start, `GetConnectionStringOrSkipAsync()` throws `SkipException` when Docker/flag unavailable) — full 95-line file already read; reuse its `EnsureStartedAsync` semaphore-gated lazy-start pattern verbatim if a Postgres fixture is added to `DeckFlow.Core.Tests`.

**Planner decision required:** CONTEXT.md's file list says "New xUnit round-trip tests in `DeckFlow.Core.Tests` — both SQLite and Postgres dialects," but the `Testcontainers.PostgreSql` package is not currently a `DeckFlow.Core.Tests` dependency. Per CLAUDE.md ("No new packages... without asking the user first" and "in-solution packages OK" feedback item — reusing an in-solution NuGet package needs no ask, only genuinely-new ones do), adding `Testcontainers.PostgreSql` to `DeckFlow.Core.Tests` is **already in-solution** (used by `DeckFlow.Web.Tests`) so it should NOT require a fresh ask — but the planner must explicitly add the `<PackageReference>` to `DeckFlow.Core.Tests.csproj` as a task, copy `PostgresFactAttribute`/`PostgresContainerFixture` (or reference them if cross-project sharing is viable — it is not, since `DeckFlow.Web.Tests` doesn't reference `DeckFlow.Core.Tests` and internal classes aren't visible cross-project), and gate on `DECKFLOW_POSTGRES_TESTS=1` exactly as done in `DeckFlow.Web.Tests`.

---

## Shared Patterns

### Dialect-guarded schema/DDL
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:70-184` (`EnsureSchemaAsync`), `:1146-1196` (DDL constants)
**Apply to:** `CreatorStyleProfileStore.EnsureSchemaAsync` + DDL constants — `_connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql` branch, `BOOLEAN`/`INTEGER` split for the `insufficient_sample` flag, `TIMESTAMPTZ`/`TEXT` split for `updated_utc` (the F-51-PG-01 lesson: **never** filter a WHERE clause on a raw-text-vs-timestamptz column across dialects without a cast — this phase's `GetBySlugAsync` only filters on `slug` (TEXT both dialects), so the trap doesn't apply to reads, but if any future SET/WHERE touches `updated_utc` it must follow the `awaiting_confirm_utc` dialect-guarded precedent at `ContentSiteIndexStore.cs:140-150`).

### Schema-gate double-checked locking
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:18-19, 70-85` (`SemaphoreSlim _schemaGate`, `volatile bool _schemaReady`)
**Apply to:** `CreatorStyleProfileStore` — identical field declarations and `EnsureSchemaAsync` double-checked-lock body (minus the ALTER-backfill loop, since this is a new table).

### Test-seam constructor + InternalsVisibleTo
**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs:51-68` (internal ctor), verify `DeckFlow.Core`'s `[InternalsVisibleTo("DeckFlow.Core.Tests")]` grant (check `DeckFlow.Core/AssemblyInfo.cs` or csproj `<InternalsVisibleTo>` item — not re-read here, but the same-project convention `DeckFlow.Web`'s `[InternalsVisibleTo("DeckFlow.Web.Tests")]` in `AssemblyInfo.cs:3` is the sibling precedent).
**Apply to:** `CreatorStyleProfileStore`'s internal ctor for connection-factory-override test seam (only needed if a recording-double test is planned; the round-trip tests described in CONTEXT.md may not need it — public ctors alone suffice for SQLite temp-file + Postgres-container round trips).

### JSON section serialize/deserialize with empty-not-null
**Source:** `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:43-68` (`SerializeTags`/`DeserializeTags`)
**Apply to:** `CreatorStyleProfile`'s three JSON-blob section columns — exact null-guard + `Array.Empty<T>()` fallback pattern satisfies D-07 verbatim.

### Record shape: `sealed record`, `{ get; init; }`/`required`, `IReadOnlyList<T>`
**Source:** `DeckFlow.Core/Knowledge/ContentModels.cs:6-21` (`ContentSource`) — house-wide convention, also enforced by `CarveOutGuardTests.InitAccessor_SurvivesFormatting_ByteIdentical` (`DeckFlow.Core.Tests/CarveOutGuardTests.cs:15-28`).
**Apply to:** All CS-01 records (`CreatorStyleProfile`, `StatedRule`, `MeasuredMetric`, `FusedTarget`, nested `distribution`/`conflict?`).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| Postgres round-trip test infra inside `DeckFlow.Core.Tests` | test | streaming/container-backed | No existing `DeckFlow.Core.Tests` file spins up a real Postgres container — that capability lives only in `DeckFlow.Web.Tests` (`PostgresFactAttribute`, `PostgresContainerFixture`, both non-shared/project-local classes). Planner must add the `Testcontainers.PostgreSql` PackageReference to `DeckFlow.Core.Tests.csproj` and re-create (not reuse cross-project) the `PostgresFactAttribute`/`PostgresContainerFixture` pair inside `DeckFlow.Core.Tests`, following the `DeckFlow.Web.Tests` files as the template (both fully excerpted above). |

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Core/Storage/`, `DeckFlow.Core.Tests/`, `DeckFlow.Web.Tests/Integration/`
**Files scanned:** `ContentSiteIndexStore.cs`, `IContentSiteIndexStore.cs`, `ContentSiteIndexReadModel.cs`, `SlugifySourceName.cs`, `CreatorSourceStore.cs`, `RelationalDatabaseConnection.cs`, `IRelationalDialect.cs`, `ContentModels.cs`, `ContentArtifactSpec.cs`, `ContentSiteIndexStoreTests.cs`, `PostgresConnectionStringNormalizerTests.cs`, `PostgresFactAttribute.cs`, `PostgresContainerFixture.cs`, `RoundTripSmokeTests.cs`, `CarveOutGuardTests.cs`
**Pattern extraction date:** 2026-07-11
