using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies removal of stored decks which cannot be retained as Commander decks.
/// </summary>
public sealed class DeckPruningPurgeTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public DeckPruningPurgeTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task RunPurgeAsync_IllegalStoredDecks_RemovedWithReasons()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "illegal-format", "Alpha", 7);
        await SeedStoredDeckAsync(repository, "no-commander", null, 3);
        await SeedStoredDeckAsync(repository, "legal", "Gamma", 3);
        var service = CreateService(repository);

        var result = await service.RunPurgeAsync();

        Assert.Equal(2, result.DecksRemoved);
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
    }

    [Fact]
    public async Task RunPurgeAsync_CapturedMetadataWithNullFormat_PurgedAsNotCommanderFormat()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "null-format", "Alpha", null);
        var service = CreateService(repository);

        var result = await service.RunPurgeAsync();

        Assert.Equal(1, result.DecksRemoved);
    }

    [Fact]
    public async Task RunPurgeAsync_PendingIllegalDeck_Untouched()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "pending", "Alpha", 7, processed: false);
        var service = CreateService(repository);

        var result = await service.RunPurgeAsync();

        Assert.Equal(0, result.DecksRemoved);
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
    }

    [Fact]
    public async Task RunPurgeAsync_SkippedIllegalStoredDeck_Removed()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "skipped", "Alpha", 7, skipped: true);
        var service = CreateService(repository);

        var result = await service.RunPurgeAsync();

        Assert.Equal(1, result.DecksRemoved);
    }

    [Fact]
    public async Task RunPurgeAsync_MoreCandidatesThanBatch_PagesThroughAll()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        for (var index = 0; index < 5; index++)
        {
            await SeedStoredDeckAsync(repository, $"illegal-{index}", "Alpha", 7);
        }
        var service = CreateService(repository, batchSize: 2);

        var result = await service.RunPurgeAsync();

        Assert.Equal(5, result.DecksRemoved);
        Assert.Equal(3, result.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunPurgeAsync_ProbeBelowFloor_RemovesNothing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "illegal", "Alpha", 7);
        var service = CreateService(repository, freeBytes: 0);

        var result = await service.RunPurgeAsync();

        Assert.Equal(0, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.DiskFloor, result.StopReason);
    }

    [Fact]
    public async Task CountPurgeCandidatesAsync_MatchesPurgeSelector()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "illegal-format", "Alpha", 7);
        await SeedStoredDeckAsync(repository, "no-commander", null, 3);

        Assert.Equal(2, await repository.CountPurgeCandidatesAsync());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDirectory, recursive: true); }
        catch (IOException) { }
    }

    private static DeckPruningService CreateService(CategoryKnowledgeRepository repository, int batchSize = 25, long freeBytes = 10L * 1024 * 1024 * 1024)
        => new(repository, new DeckPruningOptions { BatchSize = batchSize, Pause = TimeSpan.Zero }, _ => Task.FromResult<long?>(freeBytes));

    private async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string? commander, int? deckFormat, bool processed = true, bool skipped = false)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, skipped, new ArchidektDeckMetadata(null, deckFormat, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        if (!processed)
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE deck_queue SET processed = 0, skipped = @skipped WHERE deck_id = @deckId";
            command.Parameters.AddWithValue("@skipped", skipped);
            command.Parameters.AddWithValue("@deckId", deckId);
            await command.ExecuteNonQueryAsync();
        }
    }
}
