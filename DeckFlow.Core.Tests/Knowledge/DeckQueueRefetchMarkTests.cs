using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies requeueing stored decks whose Archidekt metadata was not captured.
/// </summary>
public sealed class DeckQueueRefetchMarkTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public DeckQueueRefetchMarkTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task MarkMetadataLessDecksForRefreshAsync_StoredMetadataLessDeck_Queued()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "missing-metadata", "Delta", metadata: null);
        var insertedUtc = await GetValueAsync<string>("SELECT inserted_utc FROM deck_queue WHERE deck_id = 'missing-metadata';");

        var marked = await repository.MarkMetadataLessDecksForRefreshAsync();

        Assert.Equal(1, marked);
        await AssertQueuedAsync("missing-metadata", insertedUtc);
        Assert.Equal(["missing-metadata"], await repository.GetNextRefreshDeckIdsAsync(10));
        Assert.Equal(1, await CountLiveRowsAsync("missing-metadata"));
    }

    [Fact]
    public async Task MarkMetadataLessDecksForRefreshAsync_NoStoredRowsOrPending_Untouched()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["no-source"]);
        await SeedStoredDeckAsync(repository, "pruned", "Delta", metadata: null, skipped: true);
        await SeedStoredDeckAsync(repository, "pending", "Delta", metadata: null, processed: false);
        await ExecuteAsync("UPDATE deck_queue SET skip_reason = 'retention', retention_core_hash = 'core' WHERE deck_id = 'pruned';");
        await DeleteLiveRowsAsync("pruned");

        var marked = await repository.MarkMetadataLessDecksForRefreshAsync();

        Assert.Equal(0, marked);
        await AssertQueueStateAsync("no-source", 0, 0, null, null);
        await AssertQueueStateAsync("pruned", 1, 1, "retention", null);
        Assert.Equal("core", await GetValueAsync<string>("SELECT retention_core_hash FROM deck_queue WHERE deck_id = 'pruned';"));
        await AssertQueueStateAsync("pending", 0, 0, null, null);
    }

    [Fact]
    public async Task MarkMetadataLessDecksForRefreshAsync_SkippedStoredDeck_Requeued()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "skipped", "Delta", metadata: null, skipped: true);

        var marked = await repository.MarkMetadataLessDecksForRefreshAsync();

        Assert.Equal(1, marked);
        await AssertQueuedAsync("skipped", await GetValueAsync<string>("SELECT inserted_utc FROM deck_queue WHERE deck_id = 'skipped';"));
        Assert.Equal(["skipped"], await repository.GetNextRefreshDeckIdsAsync(10));
        Assert.Equal(1, await CountLiveRowsAsync("skipped"));
    }

    [Fact]
    public async Task MarkMetadataLessDecksForRefreshAsync_DeckWithMetadata_Untouched()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "captured", "Delta", new ArchidektDeckMetadata(null, 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        Assert.Equal(0, await repository.MarkMetadataLessDecksForRefreshAsync());
        await AssertQueueStateAsync("captured", 1, 0, null, null);
    }

    [Fact]
    public async Task MarkMetadataLessDecksForRefreshAsync_RefreshesCommanderSummary()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "missing", "Delta", metadata: null);
        await SeedStoredDeckAsync(repository, "captured", "Delta", new ArchidektDeckMetadata(null, 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        await repository.MarkMetadataLessDecksForRefreshAsync();

        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("Delta"));
        Assert.Equal(1L, await GetValueAsync<long>("SELECT deck_count FROM processed_commander_summary WHERE LOWER(commander_name) = 'delta';"));
    }

    [Fact]
    public async Task MarkMetadataLessDecksForRefreshAsync_SecondCall_ReturnsZero()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "missing", "Delta", metadata: null);

        Assert.Equal(1, await repository.MarkMetadataLessDecksForRefreshAsync());
        Assert.Equal(0, await repository.MarkMetadataLessDecksForRefreshAsync());
    }

    [Fact]
    public async Task CountRefetchCandidatesAsync_MatchesMarkSelector()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "one", "Delta", metadata: null);
        await SeedStoredDeckAsync(repository, "two", "Delta", metadata: null, skipped: true);

        Assert.Equal(2, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(2, await repository.MarkMetadataLessDecksForRefreshAsync());
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, ArchidektDeckMetadata? metadata, bool processed = true, bool skipped = false)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, skipped, metadata);
        if (!processed)
        {
            await ExecuteAsync($"UPDATE deck_queue SET processed = 0, skipped = 0, skip_reason = NULL WHERE deck_id = '{deckId}';");
        }
    }

    private async Task AssertQueuedAsync(string deckId, string insertedUtc)
    {
        await AssertQueueStateAsync(deckId, 0, 0, null, expectedRefreshRequested: true);
        Assert.Equal(insertedUtc, await GetValueAsync<string>($"SELECT inserted_utc FROM deck_queue WHERE deck_id = '{deckId}';"));
        Assert.Equal(0, await GetValueAsync<int>($"SELECT transient_failure_count FROM deck_queue WHERE deck_id = '{deckId}';"));
    }

    private async Task AssertQueueStateAsync(string deckId, int processed, int skipped, string? skipReason, bool? expectedRefreshRequested)
    {
        Assert.Equal(processed, await GetValueAsync<int>($"SELECT processed FROM deck_queue WHERE deck_id = '{deckId}';"));
        Assert.Equal(skipped, await GetValueAsync<int>($"SELECT skipped FROM deck_queue WHERE deck_id = '{deckId}';"));
        Assert.Equal(skipReason, await GetValueAsync<string?>($"SELECT skip_reason FROM deck_queue WHERE deck_id = '{deckId}';"));
        if (expectedRefreshRequested.HasValue)
        {
            Assert.Equal(expectedRefreshRequested.Value, await GetValueAsync<string?>($"SELECT refresh_requested_utc FROM deck_queue WHERE deck_id = '{deckId}';") is not null);
        }
    }

    private async Task DeleteLiveRowsAsync(string deckId)
    {
        await ExecuteAsync($"DELETE FROM card_category_observations WHERE source_id IN (SELECT id FROM sources WHERE deck_queue_id = (SELECT id FROM deck_queue WHERE deck_id = '{deckId}'));");
        await ExecuteAsync($"DELETE FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE deck_queue_id = (SELECT id FROM deck_queue WHERE deck_id = '{deckId}'));");
    }

    private async Task<int> CountLiveRowsAsync(string deckId)
        => await GetValueAsync<int>($"SELECT COUNT(1) FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE deck_queue_id = (SELECT id FROM deck_queue WHERE deck_id = '{deckId}')); ");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> GetValueAsync<T>(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull
            ? default!
            : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }
}
