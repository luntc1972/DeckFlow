using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class RefetchMarkServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "refetch-mark-service-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_directory, "knowledge.db");

    [Fact]
    public async Task RunAsync_FiveCandidates_ThreeBatchesCommitAndComplete()
    {
        var repository = await SeedAsync(5);
        var progress = new InlineProgress<DeckPruningProgress>();

        var result = await Service(repository, batchDecks: 2).RunAsync(null, progress, null);

        Assert.Equal(new DeckPruningResult(5, 3, DeckPruningStopReason.Completed), result);
        Assert.Equal([(2, 1), (4, 2), (5, 3)], progress.Values.Select(value => (value.DecksAffected, value.Batches)).ToArray());
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [Fact]
    public async Task RunAsync_FailureInSecondBatch_KeepsFirstBatchAndSummary()
    {
        var repository = await SeedAsync(5, ["A", "A", "A", "B", "B"]);
        await SeedDeckAsync(repository, "processed-a", "A", metadata: CapturedMetadata());
        await ExecuteAsync("CREATE TRIGGER abort_refetch BEFORE UPDATE ON deck_queue WHEN NEW.deck_id = 'deck-3' AND NEW.refresh_requested_utc IS NOT NULL BEGIN SELECT RAISE(ABORT, 'poison'); END;");

        await Assert.ThrowsAsync<SqliteException>(() => Service(repository, batchDecks: 3).RunAsync(null, null, null));

        Assert.Equal(0, await CandidatesAsync("deck-0", "deck-1", "deck-2"));
        Assert.Equal(2, await CandidatesAsync("deck-3", "deck-4"));
        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("A"));
        Assert.Equal(2, await repository.GetCommanderDeckCountAsync("B"));
        await ExecuteAsync("DROP TRIGGER abort_refetch;");
        Assert.Equal(2, (await Service(repository, batchDecks: 3).RunAsync(null, null, null)).DecksRemoved);
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [Fact]
    public async Task RunAsync_StopBeforeSecondBatch_ThrowsAndKeepsFirstBatch()
    {
        var repository = await SeedAsync(5);
        var calls = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() => Service(repository, batchDecks: 2).RunAsync(null, null, () => ++calls > 1));

        Assert.Equal(0, await CandidatesAsync("deck-0", "deck-1"));
        Assert.Equal(3, await repository.CountRefetchCandidatesAsync());
    }

    [Fact]
    public async Task RunAsync_DiskDropsBelowFloorAfterFirstBatch_StopsWithDiskFloor()
    {
        var repository = await SeedAsync(5);
        var probes = 0;
        var service = Service(repository, new DeckPruningOptions { MinFreeDiskBytes = 10 }, _ => Task.FromResult<long?>(++probes == 1 ? 10 : 9), 2);

        var result = await service.RunAsync(null, null, null);

        Assert.Equal(new DeckPruningResult(2, 1, DeckPruningStopReason.DiskFloor), result);
        Assert.Equal(3, await repository.CountRefetchCandidatesAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_ProbeUnavailable_MarksNothing(bool throws)
    {
        var repository = await SeedAsync(2);
        Func<CancellationToken, Task<long?>> probe = throws
            ? _ => throw new InvalidOperationException("probe unavailable")
            : _ => Task.FromResult<long?>(null);

        var result = await Service(repository, new DeckPruningOptions(), probe, 2).RunAsync(null, null, null);

        Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.DiskProbeUnavailable), result);
        Assert.Equal(2, await repository.CountRefetchCandidatesAsync());
    }

    [Fact]
    public async Task RunAsync_MarkerPending_MarksNothingWithoutProbing()
    {
        var repository = await SeedAsync(2);
        await ExecuteAsync("UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');");
        var probed = false;

        var result = await Service(repository, new DeckPruningOptions(), _ => { probed = true; return Task.FromResult<long?>(long.MaxValue); }, 2).RunAsync(null, null, null);

        Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.MarkerBackfillPending), result);
        Assert.False(probed);
    }

    [Fact]
    public async Task RunAsync_MaxDecks_StopsAtBudgetAndNextRunContinues()
    {
        var repository = await SeedAsync(5);
        var service = Service(repository, batchDecks: 2);

        var capped = await service.RunAsync(3, null, null);
        var continued = await service.RunAsync(null, null, null);

        Assert.Equal(new DeckPruningResult(3, 2, DeckPruningStopReason.MaxDecksReached), capped);
        Assert.Equal(2, continued.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, continued.StopReason);
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [Fact]
    public async Task RunAsync_SummaryRefresh_TouchesOnlyBatchCommanders()
    {
        var repository = await SeedAsync(2, ["A", "A"]);
        await SeedDeckAsync(repository, "unrelated", "B", metadata: CapturedMetadata());
        await ExecuteAsync("UPDATE processed_commander_summary SET deck_count = 99 WHERE LOWER(commander_name) = 'b';");

        await Service(repository, batchDecks: 2).RunAsync(null, null, null);

        Assert.Equal(99, await ScalarAsync<int>("SELECT deck_count FROM processed_commander_summary WHERE LOWER(commander_name) = 'b';"));
    }

    private RefetchMarkService Service(CategoryKnowledgeRepository repository, int batchDecks)
        => Service(repository, new DeckPruningOptions(), _ => Task.FromResult<long?>(long.MaxValue), batchDecks);

    private static RefetchMarkService Service(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> probe, int batchDecks)
        => new(repository, options, probe, (_, _) => Task.CompletedTask, batchDecks);

    private async Task<CategoryKnowledgeRepository> SeedAsync(int count, string[]? commanders = null)
    {
        Directory.CreateDirectory(_directory);
        var repository = new CategoryKnowledgeRepository(DatabasePath);
        for (var index = 0; index < count; index++)
        {
            await SeedDeckAsync(repository, "deck-" + index, commanders?[index] ?? "A", metadata: null);
        }
        return repository;
    }

    private async Task SeedDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, ArchidektDeckMetadata? metadata)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, false, metadata);
    }

    private async Task<int> CandidatesAsync(params string[] deckIds)
        => await ScalarAsync<int>("SELECT COUNT(*) FROM deck_queue WHERE deck_id IN (" + string.Join(",", deckIds.Select(id => "'" + id + "'")) + ") AND refresh_requested_utc IS NULL;");

    private static ArchidektDeckMetadata CapturedMetadata()
        => new(null, 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default! : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];
        public void Report(T value) => Values.Add(value);
    }
}
