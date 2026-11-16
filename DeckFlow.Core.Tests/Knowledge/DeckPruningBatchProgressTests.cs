using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class DeckPruningBatchProgressTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "batch-progress-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_directory, "knowledge.db");

    [Fact]
    public async Task RunRetentionAsync_ReportsAfterEachBatch()
    {
        var repository = await SeedAsync();
        var reports = new List<DeckPruningProgress>();
        var result = await Service(repository).RunRetentionAsync(null, new InlineProgress(reports.Add), null);
        AssertReports(reports, result);
    }

    [Fact]
    public async Task RunPurgeAsync_ReportsAfterEachBatch()
    {
        var repository = await SeedAsync(illegal: true);
        var reports = new List<DeckPruningProgress>();
        var result = await Service(repository).RunPurgeAsync(null, new InlineProgress(reports.Add), null);
        AssertReports(reports, result);
    }

    [Fact]
    public async Task BackfillRunAsync_ReportsAfterEachBatch()
    {
        var repository = await SeedAsync(count: 6);
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');";
            await command.ExecuteNonQueryAsync();
        }
        var reports = new List<DeckPruningProgress>();
        var service = new StoredLiveRowsBackfillService(repository, Options(), _ => Task.FromResult<long?>(long.MaxValue), (_, _) => Task.CompletedTask, 2);
        var result = await service.RunAsync(null, new InlineProgress(reports.Add), null);
        Assert.Equal(4, reports.Count);
        Assert.Equal([1, 2, 3, 4], reports.Select(report => report.Batches));
        Assert.Equal([2, 4, 6, 6], reports.Select(report => report.DecksAffected));
        Assert.Equal(new DeckPruningProgress(result.DecksRemoved, result.Batches), reports[^1]);
    }

    [Fact]
    public async Task RunRetentionAsync_ReportBeforePause()
    {
        var repository = await SeedAsync();
        var events = new List<string>();
        var service = Service(repository, delay: (_, _) => { events.Add("pause"); return Task.CompletedTask; });
        await service.RunRetentionAsync(null, new InlineProgress(_ => events.Add("report")), null);
        Assert.Equal(["report", "pause", "report", "pause", "report"], events);
    }

    [Fact]
    public async Task ExistingOverload_ReportsNothingAndMatchesResult()
    {
        var repository = await SeedAsync();
        var service = Service(repository);
        var existing = await service.RunRetentionAsync(2);
        var explicitNull = await service.RunRetentionAsync(2, null, null);
        Assert.Equal(existing, explicitNull);
        var existingPurge = await service.RunPurgeAsync();
        Assert.Equal(existingPurge, await service.RunPurgeAsync(null, null, null));
    }

    [Fact]
    public async Task RunRetentionAsync_StopBeforeBatchTrue_StartsNoFurtherBatch()
    {
        var repository = await SeedAsync();
        var events = new List<string>();
        var reports = new List<DeckPruningProgress>();
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = Service(repository, probe: _ => { events.Add("probe"); return Task.FromResult<long?>(long.MaxValue); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunRetentionAsync(null, new InlineProgress(reports.Add), () =>
        {
            events.Add("stop");
            if (++calls != 3) return false;
            cancellation.Cancel();
            return true;
        }, cancellation.Token));
        Assert.Equal(["stop", "probe", "stop", "probe", "stop"], events);
        Assert.Equal(2, reports.Count);
        Assert.Equal(new DeckPruningProgress(4, 2), reports[^1]);
        Assert.Equal(3, await repository.GetCommanderDeckCountAsync("Alpha"));
    }

    private static void AssertReports(List<DeckPruningProgress> reports, DeckPruningResult result)
    {
        Assert.Equal(3, reports.Count);
        Assert.Equal([1, 2, 3], reports.Select(report => report.Batches));
        Assert.Equal([2, 4, 6], reports.Select(report => report.DecksAffected));
        Assert.Equal(new DeckPruningProgress(result.DecksRemoved, result.Batches), reports[^1]);
    }

    private static DeckPruningOptions Options() => new() { PerCommanderCap = 1, BatchSize = 2, Pause = TimeSpan.Zero };

    private static DeckPruningService Service(CategoryKnowledgeRepository repository, Func<TimeSpan, CancellationToken, Task>? delay = null, Func<CancellationToken, Task<long?>>? probe = null)
        => new(repository, Options(), probe ?? (_ => Task.FromResult<long?>(long.MaxValue)), delay ?? ((_, _) => Task.CompletedTask), () => DateTimeOffset.Parse("2026-11-11T00:00:00Z"));

    private async Task<CategoryKnowledgeRepository> SeedAsync(bool illegal = false, int count = 7)
    {
        Directory.CreateDirectory(_directory);
        var repository = new CategoryKnowledgeRepository(DatabasePath);
        for (var i = 0; i < (illegal ? 6 : count); i++)
        {
            var id = $"deck-{i}";
            await repository.AddDeckIdsAsync([id]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + id,
                [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
            await repository.MarkDeckProcessedAsync(id, "Alpha", skip: false, new ArchidektDeckMetadata(null, illegal ? 7 : 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }
        return repository;
    }

    private sealed class InlineProgress(Action<DeckPruningProgress> report) : IProgress<DeckPruningProgress>
    {
        public void Report(DeckPruningProgress value) => report(value);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
