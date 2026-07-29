# Phase 05: Archidekt Bracket Capture - Pattern Map

**Mapped:** 2026-07-29
**Files analyzed:** 15
**Analogs found:** 15 / 15

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` | service contract/model | request-response, transform | `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` | exact |
| `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` | service | request-response, transform | `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` | exact |
| `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` | service/orchestrator | batch, request-response | `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` | exact |
| `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` | schema/migration | CRUD | `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` | exact |
| `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` | repository | CRUD | `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` | exact |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | repository facade | CRUD | `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | exact |
| `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` | service contract | CRUD | `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` | exact |
| `DeckFlow.Web/Services/Persistence/CategoryKnowledgeStore.cs` | service adapter | CRUD, batch | `DeckFlow.Web/Services/Persistence/CategoryKnowledgeStore.cs` | exact |
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` | controller | request-response, CRUD | `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` | exact |
| `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs` | test | request-response fixture, transform | `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs` | exact |
| `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs` | test | batch orchestration | `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs` | exact |
| `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` | test | CRUD, schema verification | `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` | exact |
| `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` | test | CRUD, schema parity | `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` | exact |
| `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` | test | request-response controller | `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` | role-match |
| `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` | test | CRUD, integration | `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` | role-match |

## Pattern Assignments

### `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` (service contract/model, request-response + transform)

**Analog:** `DeckFlow.Core/Integration/DeckImporterInterfaces.cs`

**Rich result record pattern** (lines 16-23):
```csharp
/// <summary>
/// Wraps the imported deck entries with metadata about the import source and any user-visible notice.
/// </summary>
public sealed record MoxfieldImportResult(
    List<DeckEntry> Entries,
    MoxfieldImportSource Source,
    string? FallbackNotice = null,
    string? DetectedCompanionName = null);
```

**Compatibility method pattern** (lines 38-48):
```csharp
async Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
{
    var entries = await ImportAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
    return new MoxfieldImportResult(entries, MoxfieldImportSource.Direct);
}
```

Apply this shape by adding `ArchidektDeckMetadata` and `ArchidektDeckImportResult` near the importer contracts, then add `ImportWithMetadataAsync(...)` while preserving `Task<List<DeckEntry>> ImportAsync(...)` for existing callers (lines 51-62).

---

### `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` (service, request-response + transform)

**Analog:** `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs`

**Imports pattern** (lines 1-7):
```csharp
using System.Net;
using System.Text.Json;
using Polly;
using Polly.Retry;
using RestSharp;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
```

**HTTP and error handling pattern** (lines 44-56):
```csharp
if (!ArchidektApiUrl.TryGetDeckId(urlOrDeckId, out var deckId))
{
    throw new InvalidOperationException($"Unable to determine Archidekt deck id from: {urlOrDeckId}");
}

var response = await RetryPolicy.ExecuteAsync(ct => _restClient.ExecuteAsync(CreateDeckRequest(deckId), ct), cancellationToken);
var body = response.Content ?? string.Empty;
if (!response.IsSuccessStatusCode)
{
    throw new InvalidOperationException($"Archidekt API deck {deckId} returned {(int)response.StatusCode} {response.StatusDescription}: {body[..Math.Min(body.Length, 500)]}");
}

using var document = JsonDocument.Parse(body);
```

**Payload traversal pattern** (lines 56-75):
```csharp
using var document = JsonDocument.Parse(body);
var root = document.RootElement;
var entries = new List<DeckEntry>();

if (!root.TryGetProperty("cards", out var cardsElement) || cardsElement.ValueKind != JsonValueKind.Array)
{
    return entries;
}

foreach (var item in cardsElement.EnumerateArray())
{
    var quantity = item.GetProperty("quantity").GetInt32();
    if (quantity == 0)
    {
        continue;
    }
```

Add metadata parsing from the same `root` before returning the rich result. Use `TryGetProperty` plus `ValueKind` guards, and make malformed/missing `edhBracket`, `deckFormat`, `createdAt`, `updatedAt`, or `theorycrafted` nullable while still setting `CapturedUtc` after successful JSON parse.

---

### `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` (service/orchestrator, batch + request-response)

**Analog:** `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs`

**Imports/dependency pattern** (lines 1-18):
```csharp
using System.Diagnostics;
using System.Net;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Knowledge;
```

**Bulk success/failure boundary** (lines 105-138):
```csharp
foreach (var deckId in deckIds)
{
    try
    {
        var (cacheResult, commanderName) = await PersistDeckAsync(deckId, cancellationToken);
        ...
        await _repository.MarkDeckProcessedAsync(deckId, commanderName, skip: false, cancellationToken: cancellationToken);
        progress?.Report(added + updated);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
    {
        skipped++;
        _logger?.LogWarning(exception, "Skipping deck {DeckId} while caching categories.", deckId);
        await _repository.MarkDeckProcessedAsync(deckId, commanderName: null, skip: true, cancellationToken: cancellationToken);
        progress?.Report(added + updated);
    }
}
```

**Hash boundary pattern** (lines 177-200):
```csharp
private async Task<(DeckCacheWriteResult Result, string? CommanderName)> PersistDeckAsync(string deckId, CancellationToken cancellationToken)
{
    var source = $"archidekt_live:{deckId}";
    var alreadyCached = await _repository.HasSourceDataAsync(source, cancellationToken);
    var entries = await _deckImporter.ImportAsync(deckId, cancellationToken);
    ...
    var newHash = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);
    var storedHash = await _repository.GetContentHashAsync(deckId, cancellationToken);
    if (storedHash is not null && string.Equals(storedHash, newHash, StringComparison.Ordinal))
    {
        return (DeckCacheWriteResult.Unchanged, commanderName);
    }
```

Change `PersistDeckAsync` to call the metadata-bearing import and return metadata alongside the existing cache result and commander. Keep `ComputeCanonicalHash(entries)` card-list only; write metadata through `MarkDeckProcessedAsync` even when `DeckCacheWriteResult.Unchanged`.

---

### `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` (schema/migration, CRUD)

**Analog:** `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs`

**Initial table DDL pattern** (lines 46-59):
```csharp
var command = connection.CreateCommand();
command.CommandText = $"""
    CREATE TABLE IF NOT EXISTS deck_queue (
        id {_connectionInfo.Dialect.SurrogateIdColumnType},
        deck_id TEXT NOT NULL,
        inserted_utc TEXT NOT NULL,
        processed INTEGER NOT NULL DEFAULT 0,
        skipped INTEGER NOT NULL DEFAULT 0,
        last_checked_utc TEXT,
        commander_name TEXT NULL,
        content_hash TEXT NULL
    );
    """;
await command.ExecuteNonQueryAsync(cancellationToken);
```

**Additive migration pattern** (lines 61-67):
```csharp
var deckQueueColumns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
if (!deckQueueColumns.Contains("content_hash"))
{
    var addContentHashCommand = connection.CreateCommand();
    addContentHashCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;";
    await addContentHashCommand.ExecuteNonQueryAsync(cancellationToken);
}
```

**Dialect-neutral column discovery pattern** (lines 181-220):
```csharp
if (_connectionInfo.IsSqlite)
{
    var rows = await connection.QueryAsync<SqliteTableInfoRow>(new CommandDefinition(
        $"PRAGMA table_info({tableName});",
        cancellationToken: cancellationToken)).ConfigureAwait(false);
    ...
}

var pgColumns = await connection.QueryAsync<string>(new CommandDefinition(
    """
    SELECT column_name
    FROM information_schema.columns
    WHERE table_schema = current_schema()
      AND table_name = @tableName
    ORDER BY ordinal_position;
    """,
    new { tableName },
    cancellationToken: cancellationToken)).ConfigureAwait(false);
```

Add nullable columns to both initial DDL and idempotent `ALTER TABLE` blocks: `archidekt_edh_bracket INTEGER NULL`, `archidekt_deck_format INTEGER NULL`, `archidekt_theorycrafted INTEGER NULL`, `archidekt_created_utc TEXT NULL`, `archidekt_updated_utc TEXT NULL`, and `archidekt_metadata_captured_utc TEXT NULL`.

---

### `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` (repository, CRUD)

**Analog:** `DeckFlow.Core/Knowledge/DeckQueueRepository.cs`

**Imports and collaborators** (lines 1-16):
```csharp
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;
```

Add `using DeckFlow.Core.Integration;` when the metadata record lives in the integration namespace.

**Processed update pattern** (lines 281-313):
```csharp
internal async Task MarkDeckProcessedAsync(
    string deckId,
    string? commanderName,
    bool skip = false,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

    await _schema.EnsureSchemaAsync(cancellationToken);
    await using var connection = CreateConnection();
    await connection.OpenAsync(cancellationToken);

    await connection.ExecuteAsync(new CommandDefinition(
        """
        UPDATE deck_queue
           SET processed = 1,
               skipped = @skipped,
               last_checked_utc = @now,
               commander_name = @commanderName
         WHERE deck_id = @deckId;
        """,
        new
        {
            deckId,
            now = DateTime.UtcNow,
            skipped = skip ? 1 : 0,
            commanderName
        },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
}
```

Extend this method with `ArchidektDeckMetadata? metadata = null`. Only assign metadata columns when metadata is non-null; skip/failure calls pass null and leave metadata columns null. Store timestamps with existing UTC text semantics (`DateTime.UtcNow`/`ToString("O")` where direct SQL helpers are used).

**URL upsert pattern** (lines 393-416):
```csharp
internal async Task MarkUrlDeckProcessedAsync(
    string deckId,
    string? commanderName,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

    await _schema.EnsureSchemaAsync(cancellationToken);
    await using var connection = CreateConnection();
    await connection.OpenAsync(cancellationToken);

    var now = DateTime.UtcNow;
    await connection.ExecuteAsync(new CommandDefinition(
        """
        INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
        VALUES (@deckId, @now, 1, 0, @now, @commanderName)
        ON CONFLICT(deck_id) DO UPDATE
        SET processed = 1,
            skipped = 0,
            last_checked_utc = excluded.last_checked_utc,
            commander_name = COALESCE(excluded.commander_name, deck_queue.commander_name);
        """,
        new { deckId, now, commanderName },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
}
```

Extend both `INSERT` and `DO UPDATE SET` with metadata columns so URL import uses the same row semantics as bulk harvest.

---

### `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (repository facade, CRUD)

**Analog:** `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`

**Facade collaborator pattern** (lines 10-18, 43-45):
```csharp
public sealed class CategoryKnowledgeRepository
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly ILogger? _logger;
    private readonly string? _databasePath;
    private readonly string _directoryPath;
    private readonly CategoryCacheSchema _schema;
    private readonly DeckQueueRepository _deckQueue;
    private readonly CardCategoryRepository _cardCategory;
...
    _schema = new CategoryCacheSchema(connectionInfo, _directoryPath, logger);
    _deckQueue = new DeckQueueRepository(connectionInfo, _schema);
    _cardCategory = new CardCategoryRepository(connectionInfo, _schema);
```

**Forwarding pattern** (lines 252-257, 296-300):
```csharp
public Task MarkDeckProcessedAsync(
    string deckId,
    string? commanderName,
    bool skip = false,
    CancellationToken cancellationToken = default)
    => _deckQueue.MarkDeckProcessedAsync(deckId, commanderName, skip, cancellationToken);

public Task MarkUrlDeckProcessedAsync(
    string deckId,
    string? commanderName,
    CancellationToken cancellationToken = default)
    => _deckQueue.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken);
```

Add the same optional metadata parameter to both public facade methods and forward it unchanged to `DeckQueueRepository`.

---

### `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` (service contract, CRUD)

**Analog:** `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs`

**Interface documentation pattern** (lines 65-71):
```csharp
/// <summary>
/// Marks an imported deck as processed for cache accounting.
/// </summary>
/// <param name="deckId">External deck identifier to mark processed.</param>
/// <param name="commanderName">Commander name associated with the processed deck, when known.</param>
/// <param name="cancellationToken">Token used to cancel the update.</param>
Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);
```

Extend this signature with `ArchidektDeckMetadata? metadata = null` and document that null means no metadata was captured.

---

### `DeckFlow.Web/Services/Persistence/CategoryKnowledgeStore.cs` (service adapter, CRUD + batch)

**Analog:** `DeckFlow.Web/Services/Persistence/CategoryKnowledgeStore.cs`

**Imports pattern** (lines 1-13):
```csharp
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Threading;
using Dapper;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
```

**Adapter forwarding pattern** (lines 109-110):
```csharp
/// <inheritdoc/>
public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default) => _repository.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken);
```

Keep this as a thin pass-through and add the metadata parameter here rather than duplicating SQL in Web.

---

### `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` (controller, request-response + CRUD)

**Analog:** `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`

**Dependency injection pattern** (lines 23-31, 36-45):
```csharp
private readonly IArchidektDeckImporter _deckImporter;
private readonly ICategoryKnowledgeStore _categoryStore;
...
public AdminHarvestController(
    IArchidektCacheJobService jobService,
    IHarvestRunStore runStore,
    IHarvestScheduleStore scheduleStore,
    IHarvestScheduleCache scheduleCache,
    IHarvestStatsAggregator statsAggregator,
    IArchidektDeckImporter deckImporter,
    ICategoryKnowledgeStore categoryStore,
```

**URL validation and run bookkeeping pattern** (lines 231-261):
```csharp
public async Task<IActionResult> SubmitUrl(string url, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(url))
    {
        TempData[BannerKey] = "URL is required.";
        return RedirectToAction(nameof(Index));
    }

    if (!ArchidektApiUrl.TryGetDeckId(url, out var deckId))
    {
        TempData[BannerKey] = "URL must be an Archidekt deck URL.";
        return RedirectToAction(nameof(Index));
    }
```

**Successful import and processed-write pattern** (lines 263-287):
```csharp
try
{
    var entries = await _deckImporter.ImportAsync(url, cancellationToken).ConfigureAwait(false);
    await PersistImportedDeckEntriesAsync(url, entries, cancellationToken).ConfigureAwait(false);

    var commanderName = entries
        .Where(entry => string.Equals(entry.Category, "Commander", StringComparison.OrdinalIgnoreCase))
        .Select(entry => entry.Name)
        .FirstOrDefault();

    await _categoryStore.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken).ConfigureAwait(false);
```

Use the metadata-bearing import in this action, then pass `import.Metadata` into `MarkUrlDeckProcessedAsync`. While touching this block, prefer the bulk-harvest commander extraction pattern (`Board == "commander"`) from `ArchidektDeckCacheSession.cs` lines 183-188.

**Controller error boundary** (lines 289-309):
```csharp
catch (OperationCanceledException)
{
    throw;
}
catch (Exception exception)
{
    _logger.LogError(exception, "Harvest URL import failed for {Url}.", url);
    ...
    TempData[BannerKey] = $"Failed to harvest URL: {exception.Message}";
    return RedirectToAction(nameof(Index));
}
```

Do not write metadata in this catch path.

---

### `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs` (test, request-response fixture + transform)

**Analog:** `DeckFlow.Core.Tests/ArchidektApiDeckImporterTests.cs`

**Fixture HTTP pattern** (lines 36-45, 66-72):
```csharp
private static ArchidektApiDeckImporter CreateImporterReturningFixture(string fileName)
{
    var restClient = new RestClient(new RestClientOptions
    {
        BaseUrl = new Uri("https://archidekt.com"),
        ConfigureMessageHandler = _ => new FixtureMessageHandler(ReadFixture(fileName))
    });

    return new ArchidektApiDeckImporter(restClient);
}
...
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(content)
    });
```

**Assertion pattern** (lines 12-21, 24-33):
```csharp
var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

var backgroundEntry = Assert.Single(entries, entry => entry.Name == "Passionate Archaeologist");
Assert.Equal("commander", backgroundEntry.Board);
Assert.Null(backgroundEntry.Category);
```

Add metadata tests beside these using the fixture importer. Assert the old `ImportAsync` entry behavior still holds, and add `ImportWithMetadataAsync` assertions for `edhBracket`, nullable absent bracket, deck format, theorycrafted, and parsed UTC timestamps.

---

### `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs` (test, batch orchestration)

**Analog:** `DeckFlow.Core.Tests/ArchidektDeckCacheSessionTests.cs`

**Temporary SQLite setup** (lines 15-23):
```csharp
private readonly string _databasePath;
private readonly string _tempDirectory;

public ArchidektDeckCacheSessionTests()
{
    _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_tempDirectory);
    _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
}
```

**Fake importer pattern** (lines 118-136):
```csharp
private sealed class FakeDeckImporter : IArchidektDeckImporter
{
    public int ImportCalls { get; private set; }

    public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        ImportCalls++;
        return Task.FromResult(new List<DeckEntry>
        {
            new()
            {
                Name = $"Card {urlOrDeckId}",
                NormalizedName = CardNormalizer.Normalize($"Card {urlOrDeckId}"),
                Quantity = 1,
                Board = "mainboard",
                Category = "Ramp"
            }
        });
    }
}
```

Update fakes to support the metadata-bearing method. Add one test that runs a queued deck and queries `deck_queue` directly to assert metadata is populated on success, and one failure/skip path test to assert metadata columns remain null.

---

### `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` (test, CRUD + schema verification)

**Analog:** `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs`

**Repository temp-db pattern** (lines 10-20, 378):
```csharp
public sealed class CategoryKnowledgeRepositoryTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public CategoryKnowledgeRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }
...
private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);
```

**Direct deck_queue helper pattern** (lines 408-429):
```csharp
private async Task SetDeckQueueFieldsAsync(
    string deckId,
    DateTimeOffset insertedUtc,
    string? commanderName,
    DateTimeOffset? lastCheckedUtc)
{
    await using var connection = new SqliteConnection($"Data Source={_databasePath}");
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE deck_queue
        SET inserted_utc = $insertedUtc,
            commander_name = $commanderName,
            last_checked_utc = $lastCheckedUtc
        WHERE deck_id = $deckId;
        """;
```

Use direct SQLite queries/helpers for metadata verification instead of adding production read APIs. Add tests for `MarkDeckProcessedAsync(... metadata)` and `MarkUrlDeckProcessedAsync(... metadata)` and for `metadata: null` preserving null metadata columns.

---

### `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` (test, schema parity)

**Analog:** `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs`

**Fresh schema assertion pattern** (lines 22-63):
```csharp
[Fact]
public async Task EnsureSchema_OnFreshSqlite_AllowsSurrogateIdPlusUniqueGrain()
{
    var repository = CreateRepository();

    await repository.EnsureSchemaAsync();
    await using var connection = await OpenConnectionAsync();

    await ExecuteNonQueryAsync(
        connection,
        "INSERT INTO cards (normalized_card_name, display_name) VALUES ('sol ring', 'Sol Ring');");
```

**Low-level SQL helpers** (lines 507-541):
```csharp
private async Task<SqliteConnection> OpenConnectionAsync()
{
    var connection = new SqliteConnection($"Data Source={_databasePath}");
    await connection.OpenAsync();
    return connection;
}

private static async Task<long> QuerySingleInt64Async(SqliteConnection connection, string commandText)
{
    var command = connection.CreateCommand();
    command.CommandText = commandText;
    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt64(result);
}
```

Add schema tests that `PRAGMA table_info(deck_queue)` contains all new metadata columns on a fresh database and that an old-shape `deck_queue` gains columns after `EnsureSchemaAsync`.

---

### `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` (test, request-response controller)

**Analog:** `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs`

**Controller construction pattern** (lines 170-190):
```csharp
private static AdminHarvestController Build(ICategoryKnowledgeStore store, bool crossOrigin = false)
{
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Scheme = "https";
    httpContext.Request.Host = new HostString("deckflow.test");
    httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

    return new AdminHarvestController(
        new StubArchidektCacheJobService(),
        new StubHarvestRunStore(),
        new StubHarvestScheduleStore(),
        new StubHarvestScheduleCache(),
        new StubHarvestStatsAggregator(),
        new StubArchidektDeckImporter(),
        store,
```

**Stub importer/store pattern** (lines 334-337):
```csharp
private sealed class StubArchidektDeckImporter : IArchidektDeckImporter
{
    public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<DeckEntry>());
}
```

Extend the stub importer and fake store to record metadata passed from URL import. Add a controller test only if the implementation changes URL import behavior enough to need controller-level proof; repository tests can cover most persistence semantics.

---

### `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` (test, CRUD integration)

**Analog:** `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs`

**Postgres fixture pattern** (lines 14-30):
```csharp
public sealed class PostgresStorageTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresStorageTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static RelationalDatabaseConnection CreateConnection(string connectionString)
        => new(RelationalDatabaseProvider.Postgres, connectionString);

    private async Task<CategoryKnowledgeRepository> CreateRepositoryAsync()
        => new(CreateConnection(await _fixture.GetConnectionStringOrSkipAsync()));
```

**Gated deck_queue roundtrip pattern** (lines 173-194):
```csharp
[PostgresFact]
public async Task CategoryKnowledgeRepository_DeckQueue_AddClaimAndMarkProcessed_Roundtrips()
{
    var repo = await CreateRepositoryAsync();
    var deckIds = new[]
    {
        $"deck-{Guid.NewGuid():N}",
        $"deck-{Guid.NewGuid():N}",
    };

    await repo.AddDeckIdsAsync(deckIds);
    ...
    await repo.MarkDecksProcessedAsync(deckIds, skip: false);
```

Use this only for optional dialect-specific confidence. SQLite repository/schema tests are sufficient for the default phase plan; Postgres tests are gated by `PostgresFact`.

## Shared Patterns

### Archidekt Payload Parsing

**Source:** `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` lines 49-60 and 73-101
**Apply to:** `ArchidektApiDeckImporter.cs`, `ArchidektApiDeckImporterTests.cs`

```csharp
var response = await RetryPolicy.ExecuteAsync(ct => _restClient.ExecuteAsync(CreateDeckRequest(deckId), ct), cancellationToken);
var body = response.Content ?? string.Empty;
if (!response.IsSuccessStatusCode)
{
    throw new InvalidOperationException($"Archidekt API deck {deckId} returned {(int)response.StatusCode} {response.StatusDescription}: {body[..Math.Min(body.Length, 500)]}");
}

using var document = JsonDocument.Parse(body);
var root = document.RootElement;
```

Parse top-level metadata from `root`; never perform a second request.

### Rich Import Compatibility

**Source:** `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` lines 41-49 and `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` lines 38-48
**Apply to:** `DeckImporterInterfaces.cs`, `ArchidektApiDeckImporter.cs`, all fakes implementing `IArchidektDeckImporter`

```csharp
public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
{
    var result = await ImportWithSourceAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
    return result.Entries;
}
```

Use `ImportAsync` as the compatibility wrapper and route metadata consumers through a richer method.

### Additive Nullable Schema Migration

**Source:** `DeckFlow.Core/Knowledge/CategoryCacheSchema.cs` lines 46-67 and 181-220
**Apply to:** `CategoryCacheSchema.cs`, `CategoryCacheSchemaParityTests.cs`

```csharp
var deckQueueColumns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
if (!deckQueueColumns.Contains("content_hash"))
{
    var addContentHashCommand = connection.CreateCommand();
    addContentHashCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;";
    await addContentHashCommand.ExecuteNonQueryAsync(cancellationToken);
}
```

Every new column should be nullable and added idempotently after column discovery.

### Processed-Row Metadata Write

**Source:** `DeckFlow.Core/Knowledge/DeckQueueRepository.cs` lines 281-313 and 393-416
**Apply to:** `DeckQueueRepository.cs`, `CategoryKnowledgeRepository.cs`, `ICategoryKnowledgeStore.cs`, `CategoryKnowledgeStore.cs`, `ArchidektDeckCacheSession.cs`, `AdminHarvestController.cs`

```csharp
await connection.ExecuteAsync(new CommandDefinition(
    """
    UPDATE deck_queue
       SET processed = 1,
           skipped = @skipped,
           last_checked_utc = @now,
           commander_name = @commanderName
     WHERE deck_id = @deckId;
    """,
    new
    {
        deckId,
        now = DateTime.UtcNow,
        skipped = skip ? 1 : 0,
        commanderName
    },
    cancellationToken: cancellationToken)).ConfigureAwait(false);
```

Add metadata writes here, not in controllers or session-specific SQL. `metadata == null` means leave metadata null.

### Skip/Failure Error Boundaries

**Source:** `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` lines 128-138 and `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` lines 289-309
**Apply to:** Bulk and URL import paths

```csharp
catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
{
    skipped++;
    _logger?.LogWarning(exception, "Skipping deck {DeckId} while caching categories.", deckId);
    await _repository.MarkDeckProcessedAsync(deckId, commanderName: null, skip: true, cancellationToken: cancellationToken);
    progress?.Report(added + updated);
}
```

Failure paths must not fabricate an `ArchidektDeckMetadata` instance or captured timestamp.

### Direct SQL Test Verification

**Source:** `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` lines 392-429 and `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` lines 507-541
**Apply to:** repository/schema metadata tests

```csharp
await using var connection = new SqliteConnection($"Data Source={_databasePath}");
await connection.OpenAsync();

var command = connection.CreateCommand();
command.CommandText = """
    UPDATE deck_queue
    SET inserted_utc = $insertedUtc,
        commander_name = $commanderName,
        last_checked_utc = $lastCheckedUtc
    WHERE deck_id = $deckId;
    """;
```

Prefer direct test SQL over adding production read APIs solely for Phase 5 verification.

## No Analog Found

None. All planned files have exact or role-match analogs in the current codebase.

## Metadata

**Analog search scope:** `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`
**Files scanned:** C# files under the search scope, with focused reads of 13 primary analog files plus phase context/research.
**Pattern extraction date:** 2026-07-29
