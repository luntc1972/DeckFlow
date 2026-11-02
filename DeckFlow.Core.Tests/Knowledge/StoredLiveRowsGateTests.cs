using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>Specifies maintenance behavior while legacy marker rows are pending.</summary>
public sealed class StoredLiveRowsGateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "stored-live-gate-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_directory, "knowledge.db");
    private static DeckPruningOptions Options => new() { PerCommanderCap = 1, Pause = TimeSpan.Zero, MinFreeDiskBytes = 0 };

    [Fact]
    public async Task RunRetentionAsync_MarkerPending_ReturnsMarkerBackfillPendingAndRemovesNothing()
    {
        var repository = await SeedPendingAsync();
        var probes = 0;
        var service = new DeckPruningService(repository, Options, _ => { probes++; return Task.FromResult<long?>(long.MaxValue); });

        var result = await service.RunRetentionAsync();

        Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.MarkerBackfillPending), result);
        Assert.Equal(0, probes);
        await AssertTotalsAsync(2);
    }

    [Fact]
    public async Task RunPurgeAsync_MarkerPending_ReturnsMarkerBackfillPendingAndRemovesNothing()
    {
        var repository = await SeedPendingAsync(purgeCandidate: true);
        var probes = 0;
        var service = new DeckPruningService(repository, Options, _ => { probes++; return Task.FromResult<long?>(long.MaxValue); });

        var result = await service.RunPurgeAsync();

        Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.MarkerBackfillPending), result);
        Assert.Equal(0, probes);
        await AssertTotalsAsync(2);
    }

    [Fact]
    public async Task RunRetentionAsync_AfterBackfill_RemovesOverCapDecks()
    {
        var repository = await SeedPendingAsync();
        var backfill = new StoredLiveRowsBackfillService(repository, Options, _ => Task.FromResult<long?>(long.MaxValue));
        Assert.Equal(DeckPruningStopReason.Completed, (await backfill.RunAsync()).StopReason);

        var result = await new DeckPruningService(repository, Options, _ => Task.FromResult<long?>(long.MaxValue)).RunRetentionAsync();

        Assert.Equal(1, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
        await AssertTotalsAsync(1);
    }

    private async Task<CategoryKnowledgeRepository> SeedPendingAsync(bool purgeCandidate = false)
    {
        Directory.CreateDirectory(_directory);
        var repository = new CategoryKnowledgeRepository(DatabasePath);
        await repository.EnsureSchemaAsync();
        for (var i = 0; i < 2; i++)
        {
            var deckId = $"deck-{i}";
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
                [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
            await repository.MarkDeckProcessedAsync(deckId, "Atraxa", false,
                new ArchidektDeckMetadata(null, purgeCandidate ? 2 : 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');";
        await command.ExecuteNonQueryAsync();
        return repository;
    }

    private async Task AssertTotalsAsync(int expected)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM card_deck_totals;";
        Assert.Equal((long)expected, (long)(await command.ExecuteScalarAsync())!);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
