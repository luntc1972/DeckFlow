using Microsoft.Data.Sqlite;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="CategoryKnowledgeRepository"/> covering read, write,
/// and deduplication of card-category knowledge rows against a temporary SQLite database.
/// </summary>
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

    [Fact]
    public async Task AddDeckIdsAsync_DoesNotRequeueRecentlyProcessedDeck()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "123" });
        await repository.MarkDecksProcessedAsync(new[] { "123" });
        await repository.AddDeckIdsAsync(new[] { "123" });

        var queuedIds = await repository.GetNextUnprocessedDeckIdsAsync(10);

        Assert.Empty(queuedIds);
    }

    [Fact]
    public async Task AddDeckIdsAsync_RequeuesDeckAfterCooldownExpires()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "123" });
        await repository.MarkDecksProcessedAsync(new[] { "123" });
        await SetLastCheckedUtcAsync("123", DateTimeOffset.UtcNow.AddDays(-2));

        await repository.AddDeckIdsAsync(new[] { "123" });
        var queuedIds = await repository.GetNextUnprocessedDeckIdsAsync(10);

        Assert.Single(queuedIds);
        Assert.Equal("123", queuedIds[0]);
    }

    [Fact]
    public async Task GetRecentDeckCrawlPageAsync_DefaultsToSecondPage()
    {
        var repository = CreateRepository();

        var page = await repository.GetRecentDeckCrawlPageAsync();

        Assert.Equal(2, page);
    }

    [Fact]
    public async Task SetRecentDeckCrawlPageAsync_PersistsPage()
    {
        var repository = CreateRepository();

        await repository.SetRecentDeckCrawlPageAsync(7);

        var page = await repository.GetRecentDeckCrawlPageAsync();
        Assert.Equal(7, page);
    }

    [Fact]
    public async Task HasSourceDataAsync_ReturnsTrue_WhenSourceRowsExist()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:123", "Sol Ring", new[] { "Ramp" });

        var exists = await repository.HasSourceDataAsync("archidekt_live:123");

        Assert.True(exists);
    }

    [Fact]
    public async Task DeleteSourceDataAsync_RemovesExistingSourceRows()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:123", "Sol Ring", new[] { "Ramp" });
        await repository.DeleteSourceDataAsync("archidekt_live:123");

        var exists = await repository.HasSourceDataAsync("archidekt_live:123");

        Assert.False(exists);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ReturnsAggregatesOrderedByCountLastProcessedAndName()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var atraxaMax = DateTimeOffset.Parse("2026-01-04T00:00:00Z");
        var bragoChulaneMax = DateTimeOffset.Parse("2026-01-07T00:00:00Z");
        var muldrothaMax = DateTimeOffset.Parse("2026-01-05T00:00:00Z");
        var dinaMax = DateTimeOffset.Parse("2026-01-09T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "atraxa-1", "Atraxa", insertedUtc, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "atraxa-2", "Atraxa", insertedUtc, atraxaMax);
        await SeedProcessedDeckAsync(repository, "atraxa-3", "Atraxa", insertedUtc, DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "brago-1", "Brago", insertedUtc, bragoChulaneMax);
        await SeedProcessedDeckAsync(repository, "brago-2", "Brago", insertedUtc, DateTimeOffset.Parse("2026-01-06T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "chulane-1", "Chulane", insertedUtc, bragoChulaneMax);
        await SeedProcessedDeckAsync(repository, "chulane-2", "Chulane", insertedUtc, DateTimeOffset.Parse("2026-01-06T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "muldrotha-1", "Muldrotha", insertedUtc, muldrothaMax);
        await SeedProcessedDeckAsync(repository, "muldrotha-2", "Muldrotha", insertedUtc, DateTimeOffset.Parse("2026-01-04T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "dina-1", "Dina", insertedUtc, dinaMax);

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 10);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("Atraxa", row.CommanderName);
                Assert.Equal(3, row.DeckCount);
                Assert.Equal(atraxaMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Brago", row.CommanderName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(bragoChulaneMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Chulane", row.CommanderName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(bragoChulaneMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Muldrotha", row.CommanderName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(muldrothaMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Dina", row.CommanderName);
                Assert.Equal(1, row.DeckCount);
                Assert.Equal(dinaMax.ToString("O"), row.LastProcessedUtc);
            });
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_AppliesPageSizeSlicing()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "one-1", "One", insertedUtc, insertedUtc.AddDays(4));
        await SeedProcessedDeckAsync(repository, "one-2", "One", insertedUtc, insertedUtc.AddDays(4));
        await SeedProcessedDeckAsync(repository, "one-3", "One", insertedUtc, insertedUtc.AddDays(4));
        await SeedProcessedDeckAsync(repository, "two-1", "Two", insertedUtc, insertedUtc.AddDays(3));
        await SeedProcessedDeckAsync(repository, "two-2", "Two", insertedUtc, insertedUtc.AddDays(3));
        await SeedProcessedDeckAsync(repository, "three-1", "Three", insertedUtc, insertedUtc.AddDays(2));
        await SeedProcessedDeckAsync(repository, "four-1", "Four", insertedUtc, insertedUtc.AddDays(1));

        var pageTwo = await repository.GetPagedProcessedCommanderRowsAsync(page: 2, pageSize: 2);

        Assert.Equal(new[] { "Three", "Four" }, pageTwo.Select(row => row.CommanderName));
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_GroupsCommanderNamesCaseInsensitively()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var lastProcessedUtc = DateTimeOffset.Parse("2026-01-04T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "atraxa-1", "Atraxa", insertedUtc, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "atraxa-2", "atraxa", insertedUtc, lastProcessedUtc);

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 10);
        var count = await repository.GetDistinctProcessedCommanderCountAsync();

        var row = Assert.Single(rows);
        Assert.Equal("atraxa", row.CommanderName);
        Assert.Equal(2, row.DeckCount);
        Assert.Equal(lastProcessedUtc.ToString("O"), row.LastProcessedUtc);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ReturnsEmptyListForEmptyQueue()
    {
        var repository = CreateRepository();

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 2);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ExcludesUnprocessedAndNullCommanderRows()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "processed", "Known Commander", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "processed-null", commanderName: null, insertedUtc, lastCheckedUtc: null);
        await repository.AddDeckIdsAsync(new[] { "unprocessed" });
        await SetDeckQueueFieldsAsync("unprocessed", insertedUtc, commanderName: "Unprocessed Commander", lastCheckedUtc: insertedUtc);

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 10);

        var row = Assert.Single(rows);
        Assert.Equal("Known Commander", row.CommanderName);
        Assert.Equal(1, row.DeckCount);
        Assert.Equal(insertedUtc.ToString("O"), row.LastProcessedUtc);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ClampsInvalidPagingInputs()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "deck-001", "One", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "deck-002", "Two", insertedUtc, insertedUtc.AddDays(1));

        var pageZero = await repository.GetPagedProcessedCommanderRowsAsync(page: 0, pageSize: 2);
        var pageOne = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 2);
        var zeroPageSize = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 0);

        Assert.Equal(pageOne.Select(row => row.CommanderName), pageZero.Select(row => row.CommanderName));
        var row = Assert.Single(zeroPageSize);
        Assert.Equal("Two", row.CommanderName);
    }

    [Fact]
    public async Task GetDistinctProcessedCommanderCountAsync_ReturnsDistinctCommanderCount()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "atraxa-1", "Atraxa", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "atraxa-2", "Atraxa", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "brago-1", "Brago", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "processed-null", commanderName: null, insertedUtc, lastCheckedUtc: insertedUtc);
        await repository.AddDeckIdsAsync(new[] { "unprocessed" });
        await SetDeckQueueFieldsAsync("unprocessed", insertedUtc, commanderName: "Chulane", lastCheckedUtc: insertedUtc);

        var count = await repository.GetDistinctProcessedCommanderCountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetCategoryRowsForCommanderAsync_ReturnsCardWithOnlyCardTypeCategory()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "TESTDECK", "Krenko, Mob Boss", insertedUtc, insertedUtc);
        await repository.PersistObservedCategoriesAsync("archidekt_live:TESTDECK", "Sol Ring", new[] { "Artifact" });

        var rows = await repository.GetCategoryRowsForCommanderAsync("Krenko, Mob Boss");

        var row = Assert.Single(rows);
        Assert.Equal("Artifact", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
    }

    [Fact]
    public async Task EnsureSchemaAsync_CreatesDeckQueueIndexes()
    {
        var repository = CreateRepository();

        await repository.EnsureSchemaAsync();

        var indexNames = await GetDeckQueueIndexNamesAsync();
        Assert.Contains("ix_deck_queue_processed", indexNames);
        Assert.Contains("ix_deck_queue_processed_inserted_deck", indexNames);
        Assert.Contains("ix_deck_queue_processed_commander", indexNames);
    }

    [Fact]
    public async Task EnsureSchemaAsync_CreatesCardLookupIndexes()
    {
        var repository = CreateRepository();

        await repository.EnsureSchemaAsync();

        var indexNames = await GetCardLookupIndexNamesAsync();
        Assert.Contains("ix_card_deck_totals_normalized", indexNames);
        Assert.Contains("ix_card_category_observations_normalized", indexNames);
    }

    [Fact]
    public async Task EnsureSchemaAsync_DoesNotThrow_WhenIndexCreationFails()
    {
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE card_deck_totals (
                    source TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = CreateRepository();

        var exception = await Record.ExceptionAsync(() => repository.EnsureSchemaAsync());

        Assert.Null(exception);
        var tableNames = await GetTableNamesAsync();
        Assert.Contains("deck_queue", tableNames);
        Assert.Contains("card_category_observations", tableNames);
        Assert.Contains("card_deck_totals", tableNames);
    }

    private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);

    private async Task SeedProcessedDeckAsync(
        CategoryKnowledgeRepository repository,
        string deckId,
        string? commanderName,
        DateTimeOffset insertedUtc,
        DateTimeOffset? lastCheckedUtc)
    {
        await repository.AddDeckIdsAsync(new[] { deckId });
        await repository.MarkDeckProcessedAsync(deckId, commanderName);
        await SetDeckQueueFieldsAsync(deckId, insertedUtc, commanderName, lastCheckedUtc);
    }

    private async Task SetLastCheckedUtcAsync(string deckId, DateTimeOffset timestamp)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE deck_queue
            SET last_checked_utc = $timestamp
            WHERE deck_id = $deckId;
            """;
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

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
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$insertedUtc", insertedUtc.ToString("O"));
        command.Parameters.AddWithValue("$commanderName", (object?)commanderName ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastCheckedUtc", lastCheckedUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<IReadOnlyList<string>> GetDeckQueueIndexNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'index'
              AND name IN (
                'ix_deck_queue_processed',
                'ix_deck_queue_processed_inserted_deck',
                'ix_deck_queue_processed_commander')
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<IReadOnlyList<string>> GetCardLookupIndexNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'index'
              AND name IN (
                'ix_card_deck_totals_normalized',
                'ix_card_category_observations_normalized')
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<IReadOnlyList<string>> GetTableNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                'deck_queue',
                'card_category_observations',
                'card_deck_totals')
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
