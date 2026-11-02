using DeckFlow.CLI;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Core;
using Serilog.Events;

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

    [Fact]
    public async Task RunPostHarvestRetentionAsync_MarkerPending_BackfillsThenRetains()
    {
        var repository = await SeedPendingAsync();

        var result = await DeckCommandRunners.RunPostHarvestRetentionAsync(repository, _databasePath,
            options: new DeckPruningOptions { PerCommanderCap = 1, Pause = TimeSpan.Zero, MinFreeDiskBytes = 0 });

        Assert.Equal(new DeckPruningResult(1, 1, DeckPruningStopReason.Completed), result);
        Assert.True(await repository.IsStoredLiveRowsMarkerReadyAsync());
        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("Atraxa"));
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_MarkerPendingBelowFloor_ReturnsDiskFloorWithoutRetention()
    {
        var repository = await SeedPendingAsync();
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        var result = await DeckCommandRunners.RunPostHarvestRetentionAsync(repository, _databasePath, logger,
            new DeckPruningOptions { PerCommanderCap = 1, Pause = TimeSpan.Zero, MinFreeDiskBytes = long.MaxValue });

        Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.DiskFloor), result);
        Assert.False(await repository.IsStoredLiveRowsMarkerReadyAsync());
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM card_deck_totals;";
        Assert.Equal(2L, (long)(await command.ExecuteScalarAsync())!);
        var stopped = Assert.Single(sink.Events, entry => entry.MessageTemplate.Text ==
            "Post-harvest marker backfill stopped with {StopReason} after {DecksRechecked} decks; retention skipped.");
        Assert.Equal("DiskFloor", Assert.IsType<ScalarValue>(stopped.Properties["StopReason"]).Value?.ToString());
        Assert.Equal(0, Assert.IsType<ScalarValue>(stopped.Properties["DecksRechecked"]).Value);
    }

    private async Task<CategoryKnowledgeRepository> SeedPendingAsync()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "pending-1", "Atraxa", DateTimeOffset.UtcNow.AddDays(-1));
        await SeedStoredDeckAsync(repository, "pending-2", "Atraxa", DateTimeOffset.UtcNow);
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');";
        await command.ExecuteNonQueryAsync();
        return repository;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
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
