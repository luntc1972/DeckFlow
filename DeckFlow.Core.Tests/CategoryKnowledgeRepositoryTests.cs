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
    public async Task GetPagedProcessedDeckRowsAsync_ReturnsStablePageWithTupleFields()
    {
        var repository = CreateRepository();
        var oldestInsertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var newestInsertedUtc = DateTimeOffset.Parse("2026-01-03T00:00:00Z");
        var middleInsertedUtc = DateTimeOffset.Parse("2026-01-02T00:00:00Z");
        var newestLastCheckedUtc = DateTimeOffset.Parse("2026-01-04T00:00:00Z");
        var middleLastCheckedUtc = DateTimeOffset.Parse("2026-01-05T00:00:00Z");
        var oldestLastCheckedUtc = DateTimeOffset.Parse("2026-01-02T00:00:00Z");

        await repository.AddDeckIdsAsync(new[] { "deck-old", "deck-new", "deck-mid" });
        await repository.MarkDeckProcessedAsync("deck-old", "Old Commander");
        await repository.MarkDeckProcessedAsync("deck-new", "New Commander");
        await repository.MarkDeckProcessedAsync("deck-mid", "Mid Commander");
        await SetDeckQueueFieldsAsync("deck-old", oldestInsertedUtc, "Old Commander", oldestLastCheckedUtc);
        await SetDeckQueueFieldsAsync("deck-new", newestInsertedUtc, "New Commander", newestLastCheckedUtc);
        await SetDeckQueueFieldsAsync("deck-mid", middleInsertedUtc, "Mid Commander", middleLastCheckedUtc);

        var pageOne = await repository.GetPagedProcessedDeckRowsAsync(page: 1, pageSize: 2);
        var pageTwo = await repository.GetPagedProcessedDeckRowsAsync(page: 2, pageSize: 2);

        Assert.Equal(2, pageOne.Count);
        Assert.Equal("deck-new", pageOne[0].DeckId);
        Assert.Equal("New Commander", pageOne[0].CommanderName);
        Assert.Equal(newestInsertedUtc.ToString("O"), pageOne[0].InsertedUtc);
        Assert.Equal(newestLastCheckedUtc.ToString("O"), pageOne[0].LastCheckedUtc);
        Assert.Equal("deck-mid", pageOne[1].DeckId);
        Assert.Equal("Mid Commander", pageOne[1].CommanderName);
        Assert.Equal(middleInsertedUtc.ToString("O"), pageOne[1].InsertedUtc);
        Assert.Equal(middleLastCheckedUtc.ToString("O"), pageOne[1].LastCheckedUtc);

        var onlyPageTwoRow = Assert.Single(pageTwo);
        Assert.Equal("deck-old", onlyPageTwoRow.DeckId);
        Assert.Equal("Old Commander", onlyPageTwoRow.CommanderName);
        Assert.Equal(oldestInsertedUtc.ToString("O"), onlyPageTwoRow.InsertedUtc);
        Assert.Equal(oldestLastCheckedUtc.ToString("O"), onlyPageTwoRow.LastCheckedUtc);
    }

    [Fact]
    public async Task GetPagedProcessedDeckRowsAsync_UsesDeckIdTiebreakerForSharedInsertedUtc()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await repository.AddDeckIdsAsync(new[] { "deck-001", "deck-002", "deck-003", "deck-004" });
        await repository.MarkDecksProcessedAsync(new[] { "deck-001", "deck-002", "deck-003", "deck-004" });
        foreach (var deckId in new[] { "deck-001", "deck-002", "deck-003", "deck-004" })
        {
            await SetDeckQueueFieldsAsync(deckId, insertedUtc, commanderName: deckId, lastCheckedUtc: insertedUtc);
        }

        var pageOne = await repository.GetPagedProcessedDeckRowsAsync(page: 1, pageSize: 2);
        var pageTwo = await repository.GetPagedProcessedDeckRowsAsync(page: 2, pageSize: 2);

        Assert.Equal(new[] { "deck-004", "deck-003" }, pageOne.Select(row => row.DeckId));
        Assert.Equal(new[] { "deck-002", "deck-001" }, pageTwo.Select(row => row.DeckId));
        Assert.Equal(
            new[] { "deck-001", "deck-002", "deck-003", "deck-004" },
            pageOne.Concat(pageTwo).Select(row => row.DeckId).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetPagedProcessedDeckRowsAsync_ReturnsEmptyListForEmptyQueue()
    {
        var repository = CreateRepository();

        var rows = await repository.GetPagedProcessedDeckRowsAsync(page: 1, pageSize: 2);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetPagedProcessedDeckRowsAsync_ExcludesUnprocessedDecksAndPreservesNullFields()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await repository.AddDeckIdsAsync(new[] { "processed-null", "unprocessed" });
        await repository.MarkDeckProcessedAsync("processed-null", commanderName: null);
        await SetDeckQueueFieldsAsync("processed-null", insertedUtc, commanderName: null, lastCheckedUtc: null);

        var rows = await repository.GetPagedProcessedDeckRowsAsync(page: 1, pageSize: 10);

        var row = Assert.Single(rows);
        Assert.Equal("processed-null", row.DeckId);
        Assert.Null(row.CommanderName);
        Assert.Equal(insertedUtc.ToString("O"), row.InsertedUtc);
        Assert.Null(row.LastCheckedUtc);
    }

    [Fact]
    public async Task GetPagedProcessedDeckRowsAsync_ClampsInvalidPagingInputs()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await repository.AddDeckIdsAsync(new[] { "deck-001", "deck-002" });
        await repository.MarkDecksProcessedAsync(new[] { "deck-001", "deck-002" });
        await SetDeckQueueFieldsAsync("deck-001", insertedUtc, commanderName: "One", lastCheckedUtc: insertedUtc);
        await SetDeckQueueFieldsAsync("deck-002", insertedUtc.AddDays(1), commanderName: "Two", lastCheckedUtc: insertedUtc);

        var pageZero = await repository.GetPagedProcessedDeckRowsAsync(page: 0, pageSize: 2);
        var pageOne = await repository.GetPagedProcessedDeckRowsAsync(page: 1, pageSize: 2);
        var zeroPageSize = await repository.GetPagedProcessedDeckRowsAsync(page: 1, pageSize: 0);

        Assert.Equal(pageOne.Select(row => row.DeckId), pageZero.Select(row => row.DeckId));
        var row = Assert.Single(zeroPageSize);
        Assert.Equal("deck-002", row.DeckId);
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

    private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);

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
