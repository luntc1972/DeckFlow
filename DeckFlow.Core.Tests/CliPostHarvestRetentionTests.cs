using DeckFlow.CLI;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Specifies post-harvest retention behavior invoked by the Archidekt cache CLI command.
/// </summary>
public sealed class CliPostHarvestRetentionTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public CliPostHarvestRetentionTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_CommanderWith101Decks_RemovesOldest()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var commander = "Atraxa, Praetors' Voice";
        var baseUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index <= 100; index++)
        {
            await SeedStoredDeckAsync(repository, $"deck-{index:D3}", commander, baseUtc.AddDays(index));
        }

        var result = await DeckCommandRunners.RunPostHarvestRetentionAsync(
            repository,
            _databasePath,
            options: new DeckPruningOptions { PerCommanderCap = 100, BatchSize = 25, Pause = TimeSpan.Zero, MinFreeDiskBytes = 0 });

        Assert.NotNull(result);
        Assert.Equal(1, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
        Assert.Equal(100, await repository.GetCommanderDeckCountAsync(commander));
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_CorruptDatabase_ReturnsNull()
    {
        await File.WriteAllBytesAsync(_databasePath, [0, 1, 2, 3]);
        var repository = new CategoryKnowledgeRepository(_databasePath);

        var result = await DeckCommandRunners.RunPostHarvestRetentionAsync(repository, _databasePath);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShouldRunPostHarvestRetention_OnlyForCleanSweep(bool rateLimited, bool endedEarly, bool expected)
    {
        var result = new ArchidektCacheRunResult(0, 0, 0, 0, 0, TimeSpan.Zero, rateLimited, endedEarly);

        Assert.Equal(expected, DeckCommandRunners.ShouldRunPostHarvestRetention(result));
    }

    public void Dispose()
    {
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_databasePath)}"));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Directory.Delete(_tempDirectory, recursive: true);
    }

    private static async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, DateTimeOffset updatedUtc)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, skip: false, new ArchidektDeckMetadata(null, 3, false, null, updatedUtc, DateTimeOffset.UtcNow));
    }
}
