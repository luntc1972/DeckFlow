using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

[Collection("CategoryKnowledgeStoreTests")]
public sealed class CategoryKnowledgeStoreMaintenanceTests
{
    [Fact]
    public async Task Drain_WithMaxDecksPerRun_StopsAtCap_NoOvershoot()
    {
        var store = await CreatePilotStoreAsync(5);

        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain);

        Assert.Equal(5, outcome.DecksAffected);
        Assert.Equal("PerRunCapReached", outcome.StopReason.ToString());
        Assert.Equal("per-run-cap-reached", HarvestMaintenanceStatusText.Describe(new HarvestMaintenanceStatus { State = HarvestMaintenanceRunState.Finished, StopReason = outcome.StopReason }, DateTimeOffset.UtcNow).State);
        var status = HarvestMaintenanceStatusText.Describe(new HarvestMaintenanceStatus { State = HarvestMaintenanceRunState.Finished, Operation = HarvestMaintenanceOperation.Drain, StopReason = outcome.StopReason }, DateTimeOffset.UtcNow);
        Assert.Equal("warning", status.Tone);
        Assert.Contains("Stopped at the per-run deck cap", status.Headline);
        var remaining = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain);
        Assert.Equal(3, remaining.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, remaining.StopReason);
    }

    [Fact]
    public async Task MaxDecksPerRun_Zero_IsUnbounded()
    {
        var store = await CreatePilotStoreAsync(0);

        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain);

        Assert.Equal(8, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
    }

    private static async Task<CategoryKnowledgeStore> CreatePilotStoreAsync(int cap)
    {
        var options = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MaintenanceMaxDecksPerRun"] = cap.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build().Get<HarvestRetentionOptions>()!;
        options.PerCommanderCap = 2;
        options.BatchSize = 2;
        options.MaintenanceChunkDecks = 3;
        options.PauseMilliseconds = 0;
        options.MinFreeDiskBytes = 0;
        var store = CreateStore(options);
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDecksAsync(repository, 10, "Alpha", 0);
        return store;
    }

    [Fact]
    public async Task RunMaintenanceAsync_Drain_RemovesOverCapDecksAcrossChunks()
    {
        var store = CreateStore();
        await SeedTracerAsync(store);
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, null);

        Assert.Equal(HarvestMaintenanceOperation.Drain, outcome.Operation);
        Assert.Equal(5, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        Assert.True(outcome.Batches >= 2);
    }

    [Fact]
    public async Task RunMaintenanceAsync_Purge_RemovesIllegalDecksAcrossChunks()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "illegal-format-one", "Alpha", 7);
        await SeedStoredDeckAsync(repository, "illegal-format-two", "Alpha", 7);
        await SeedStoredDeckAsync(repository, "missing-one", null, 3);
        await SeedStoredDeckAsync(repository, "missing-two", null, 3);

        var progress = new List<DeckPruningProgress>();
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Purge, new InlineProgress(progress.Add));

        Assert.Equal(4, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        AssertBatchReports(progress, outcome);
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
    }

    [Fact]
    public async Task RunMaintenanceAsync_Refetch_MarksMetadataLessDecks()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "refresh-oldest", "Gamma", 3, metadataLess: true, insertedUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        await SeedStoredDeckAsync(repository, "refresh-middle", "Gamma", 3, metadataLess: true, insertedUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await SeedStoredDeckAsync(repository, "refresh-newest", "Gamma", 3, metadataLess: true, insertedUtc: DateTimeOffset.Parse("2026-01-03T00:00:00Z"));

        var drain = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain);
        Assert.Equal(1, drain.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, drain.StopReason);
        Assert.Equal(new DeckState(1, 1, "retention", null), await ReadDeckStateAsync(store, "refresh-oldest"));

        var progress = new List<DeckPruningProgress>();
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch, new InlineProgress(progress.Add));

        Assert.Equal(2, outcome.DecksAffected);
        Assert.Equal(1, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        Assert.Equal([new DeckPruningProgress(2, 1)], progress);
        var middle = await ReadDeckStateAsync(store, "refresh-middle");
        var newest = await ReadDeckStateAsync(store, "refresh-newest");
        var oldest = await ReadDeckStateAsync(store, "refresh-oldest");
        Assert.Equal(0, middle.Processed);
        Assert.NotNull(middle.RefreshRequestedUtc);
        Assert.Equal(0, newest.Processed);
        Assert.NotNull(newest.RefreshRequestedUtc);
        Assert.Equal(new DeckState(1, 1, "retention", null), oldest);
    }

    [Fact]
    public async Task RunMaintenanceAsync_Refetch_MarksInBatchesAcrossChunks()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        for (var index = 1; index <= 5; index++)
        {
            await SeedStoredDeckAsync(repository, $"refetch-batch-{index}", "Gamma", 3, metadataLess: true);
        }

        var progress = new List<DeckPruningProgress>();
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch, new InlineProgress(progress.Add));

        Assert.Equal(5, outcome.DecksAffected);
        Assert.Equal(2, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        Assert.Equal([new DeckPruningProgress(3, 1), new DeckPruningProgress(5, 2)], progress);
        for (var index = 1; index <= 5; index++)
        {
            Assert.NotNull((await ReadDeckStateAsync(store, $"refetch-batch-{index}")).RefreshRequestedUtc);
        }
    }

    [Fact]
    public async Task RunMaintenanceAsync_RefetchFailsInSecondChunk_KeepsFirstChunk()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        for (var index = 1; index <= 3; index++)
        {
            await SeedStoredDeckAsync(repository, $"refetch-first-{index}", "Alpha", 3, metadataLess: true);
        }
        await SeedStoredDeckAsync(repository, "refetch-alpha-processed", "Alpha", 3);
        await SeedStoredDeckAsync(repository, "refetch-second-4", "Beta", 3, metadataLess: true);
        await SeedStoredDeckAsync(repository, "refetch-second-5", "Beta", 3, metadataLess: true);
        await using (var connection = new SqliteConnection($"Data Source={Assert.IsType<string>(store.DatabasePath)}"))
        {
            await connection.OpenAsync();
            await using var trigger = connection.CreateCommand();
            trigger.CommandText = "CREATE TRIGGER reject_refetch_second_chunk BEFORE UPDATE OF refresh_requested_utc ON deck_queue WHEN NEW.deck_id = 'refetch-second-5' BEGIN SELECT RAISE(ABORT, 'second chunk failure'); END;";
            await trigger.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<SqliteException>(() => store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch));

        for (var index = 1; index <= 3; index++)
        {
            Assert.NotNull((await ReadDeckStateAsync(store, $"refetch-first-{index}")).RefreshRequestedUtc);
        }
        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("Alpha"));
        Assert.Null((await ReadDeckStateAsync(store, "refetch-second-4")).RefreshRequestedUtc);
        Assert.Null((await ReadDeckStateAsync(store, "refetch-second-5")).RefreshRequestedUtc);
        Assert.Equal(2, await repository.GetCommanderDeckCountAsync("Beta"));
    }

    [Fact]
    public async Task RunMaintenanceAsync_Refetch_WaitsForSweepGate()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "gate-one", "Delta", 3, metadataLess: true);
        await SeedStoredDeckAsync(repository, "gate-two", "Delta", 3, metadataLess: true);
        Task<HarvestMaintenanceOutcome>? refetch = null;
        await store.SweepGate.WaitAsync();
        var gateHeld = true;
        try
        {
            refetch = Task.Run(() => store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch));
            var delay = Task.Delay(TimeSpan.FromSeconds(2));
            Assert.Same(delay, await Task.WhenAny(refetch, delay));
            Assert.Null((await ReadDeckStateAsync(store, "gate-one")).RefreshRequestedUtc);
            Assert.Null((await ReadDeckStateAsync(store, "gate-two")).RefreshRequestedUtc);
            store.SweepGate.Release();
            gateHeld = false;
            var outcome = await refetch.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(2, outcome.DecksAffected);
            Assert.NotNull((await ReadDeckStateAsync(store, "gate-one")).RefreshRequestedUtc);
            Assert.NotNull((await ReadDeckStateAsync(store, "gate-two")).RefreshRequestedUtc);
        }
        finally
        {
            if (gateHeld)
            {
                store.SweepGate.Release();
            }

            if (refetch is not null)
            {
                await refetch.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }
    }

    [Fact]
    public async Task GetMaintenancePreviewAsync_ReturnsAllThreeCounts()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "retention-one", "Alpha", 3, insertedUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        await SeedStoredDeckAsync(repository, "retention-two", "Alpha", 3, insertedUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await SeedStoredDeckAsync(repository, "retention-three", "Alpha", 3, insertedUtc: DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        await SeedStoredDeckAsync(repository, "purge-one", null, 7);
        await SeedStoredDeckAsync(repository, "purge-two", null, 3);
        await SeedStoredDeckAsync(repository, "metadata-less", "Beta", 3, metadataLess: true);

        var preview = await store.GetMaintenancePreviewAsync();

        Assert.NotNull(preview);
        Assert.Equal(1, preview.RetentionExcessDecks);
        Assert.Equal(2, preview.PurgeCandidates);
        Assert.Equal(1, preview.RefetchCandidates);

        var firstPurge = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Purge);
        Assert.Equal(2, firstPurge.DecksAffected);
        var afterPurge = await store.GetMaintenancePreviewAsync();
        Assert.NotNull(afterPurge);
        Assert.Equal(0, afterPurge.PurgeCandidates);

        var secondPurge = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Purge);
        Assert.Equal(0, secondPurge.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, secondPurge.StopReason);
    }

    [Fact]
    public async Task RunMaintenanceAsync_DrainWithPostHarvestDisabled_StillRemoves()
    {
        var store = CreateStore(new HarvestRetentionOptions { PerCommanderCap = 2, BatchSize = 2, MaintenanceChunkDecks = 3, PauseMilliseconds = 0, MinFreeDiskBytes = 0, PostHarvestMaxDecks = 0 });
        await SeedTracerAsync(store);

        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain);

        Assert.Equal(5, outcome.DecksAffected);
    }

    [Fact]
    public async Task RunMaintenanceAsync_DrainBelowFloor_StopsAfterOneChunk()
    {
        var store = CreateStore(new HarvestRetentionOptions { PerCommanderCap = 2, BatchSize = 2, MaintenanceChunkDecks = 3, PauseMilliseconds = 0, MinFreeDiskBytes = long.MaxValue });
        await SeedTracerAsync(store);
        var progress = new List<DeckPruningProgress>();

        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, new InlineProgress(progress.Add));

        Assert.Equal(0, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.DiskFloor, outcome.StopReason);
        Assert.Empty(progress);
    }

    [Fact]
    public async Task RunMaintenanceAsync_CancelledToken_Throws()
    {
        var store = CreateStore();
        await SeedTracerAsync(store);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task RunMaintenanceAsync_Drain_ReleasesSweepGateBetweenChunks()
    {
        var store = CreateStore();
        await SeedTracerAsync(store);
        var firstReport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new PausingProgress(firstReport, resume);

        var drain = Task.Run(() => store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, progress));
        await firstReport.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var postHarvestTask = store.RunPostHarvestRetentionAsync();
        Assert.False(postHarvestTask.IsCompleted);
        resume.TrySetResult();
        var postHarvest = await postHarvestTask.WaitAsync(TimeSpan.FromSeconds(10));
        var outcome = await drain.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, postHarvest!.DecksRemoved);
        Assert.Equal(3, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
    }

    [Fact]
    public async Task RunMaintenanceAsync_Refetch_ReleasesSweepGateBetweenChunks()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        for (var index = 1; index <= 5; index++)
        {
            await SeedStoredDeckAsync(repository, $"refetch-gate-{index}", "Gamma", 3, metadataLess: true);
        }

        var firstReport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new TwicePausingProgress(firstReport, secondReport, resumeFirst, resumeSecond);

        var refetch = Task.Run(() => store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch, progress));
        await firstReport.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var postHarvestTask = store.RunPostHarvestRetentionAsync();
        Assert.False(postHarvestTask.IsCompleted);
        resumeFirst.TrySetResult();
        await secondReport.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await postHarvestTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(refetch.IsCompleted);
        resumeSecond.TrySetResult();
        var outcome = await refetch.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(5, outcome.DecksAffected);
        Assert.Equal(2, outcome.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
    }

    [Fact]
    public async Task RunMaintenanceAsync_Backfill_CompletesAndOpensPreview()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "illegal", "Alpha", 7);
        await SeedStoredDeckAsync(repository, "metadata-less", "Alpha", null, metadataLess: true);
        await MakeMarkerPendingAsync(store);
        Assert.True((await store.GetMaintenancePreviewAsync())!.MarkerBackfillPending);

        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Backfill);

        Assert.Equal(HarvestMaintenanceOperation.Backfill, outcome.Operation);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        Assert.Equal(2, outcome.DecksAffected);
        var preview = Assert.IsType<HarvestMaintenancePreview>(await store.GetMaintenancePreviewAsync());
        Assert.False(preview.MarkerBackfillPending);
        Assert.Equal(1, preview.PurgeCandidates);
        Assert.Equal(1, preview.RefetchCandidates);
    }

    [Fact]
    public async Task GetMaintenancePreviewAsync_MarkerPending_ReturnsPendingWithoutCounts()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "illegal", "Alpha", 7);
        await MakeMarkerPendingAsync(store);
        var preview = Assert.IsType<HarvestMaintenancePreview>(await store.GetMaintenancePreviewAsync());
        Assert.True(preview.MarkerBackfillPending);
        Assert.Equal((0, 0, 0), (preview.RetentionExcessDecks, preview.PurgeCandidates, preview.RefetchCandidates));
    }

    [Fact]
    public async Task RunMaintenanceAsync_RefetchMarkerPending_MarksNothing()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "metadata-less", "Alpha", null, metadataLess: true);
        await MakeMarkerPendingAsync(store);
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch);
        Assert.Equal(DeckPruningStopReason.MarkerBackfillPending, outcome.StopReason);
        Assert.Equal(0, outcome.DecksAffected);
        Assert.Null((await ReadDeckStateAsync(store, "metadata-less")).RefreshRequestedUtc);
    }

    [Fact]
    public async Task RunMaintenanceAsync_PurgeMarkerPending_RemovesNothing()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "illegal", "Alpha", 7);
        await MakeMarkerPendingAsync(store);
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Purge);
        Assert.Equal(DeckPruningStopReason.MarkerBackfillPending, outcome.StopReason);
        Assert.Equal(0, outcome.DecksAffected);
        Assert.Equal(1, (await ReadDeckStateAsync(store, "illegal")).Processed);
        await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Backfill);
        Assert.Equal(1, (await store.GetMaintenancePreviewAsync())!.PurgeCandidates);
    }

    [Fact]
    public async Task GetMaintenancePreviewAsync_DeckEmptiedAfterStore_NotCounted()
    {
        var store = CreateStore();
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDeckAsync(repository, "illegal", "Alpha", 7);
        Assert.Equal(1, (await store.GetMaintenancePreviewAsync())!.PurgeCandidates);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:illegal", []);
        Assert.Equal(0, (await store.GetMaintenancePreviewAsync())!.PurgeCandidates);
    }

    private static async Task MakeMarkerPendingAsync(CategoryKnowledgeStore store)
    {
        await using var connection = new SqliteConnection($"Data Source={Assert.IsType<string>(store.DatabasePath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');";
        await command.ExecuteNonQueryAsync();
    }

    private static CategoryKnowledgeStore CreateStore(HarvestRetentionOptions? options = null)
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "deckflow-maintenance-" + Guid.NewGuid().ToString("N"));
        var previousDataDirectory = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", dataDirectory);
        try
        {
            return new CategoryKnowledgeStore(
                new FakeWebHostEnvironment(dataDirectory),
                new MemoryCache(new MemoryCacheOptions()),
                retentionOptions: Options.Create(options ?? new HarvestRetentionOptions
                {
                    PerCommanderCap = 2,
                    BatchSize = 2,
                    MaintenanceChunkDecks = 3,
                    PauseMilliseconds = 0,
                    MinFreeDiskBytes = 0
                }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", previousDataDirectory);
        }
    }

    private static async Task SeedTracerAsync(CategoryKnowledgeStore store)
    {
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        await SeedStoredDecksAsync(repository, 6, "Alpha", 0);
        await SeedStoredDecksAsync(repository, 3, "Beta", 100);
    }

    private static async Task SeedStoredDecksAsync(CategoryKnowledgeRepository repository, int count, string commander, int offset)
    {
        for (var index = 0; index < count; index++)
        {
            var deckId = $"{commander}-{index:D3}";
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
            await repository.MarkDeckProcessedAsync(deckId, commander, skip: false, new ArchidektDeckMetadata(null, 3, false, null, DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddDays(offset + index), DateTimeOffset.UtcNow));
        }
    }

    private static async Task<DeckState> ReadDeckStateAsync(CategoryKnowledgeStore store, string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={Assert.IsType<string>(store.DatabasePath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT processed, skipped, skip_reason, refresh_requested_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DeckState(reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private static async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string? commander, int? deckFormat, bool metadataLess = false, DateTimeOffset? insertedUtc = null)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, false, metadataLess ? null : new ArchidektDeckMetadata(null, deckFormat, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        if (insertedUtc is not null)
        {
            await using var connection = new SqliteConnection($"Data Source={Assert.IsType<string>(repository.DatabasePath)}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE deck_queue SET inserted_utc = $insertedUtc WHERE deck_id = $deckId;";
            command.Parameters.AddWithValue("$insertedUtc", insertedUtc.Value.ToString("O"));
            command.Parameters.AddWithValue("$deckId", deckId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task RunMaintenanceAsync_Drain_ReportsCumulativeBatchProgress()
    {
        var store = CreateStore();
        await SeedTracerAsync(store);
        var reports = new List<DeckPruningProgress>();
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, new InlineProgress(reports.Add));
        AssertBatchReports(reports, outcome);
    }

    private static void AssertBatchReports(List<DeckPruningProgress> reports, HarvestMaintenanceOutcome outcome)
    {
        Assert.NotEmpty(reports);
        var previous = new DeckPruningProgress(0, 0);
        foreach (var report in reports)
        {
            Assert.True(report.DecksAffected > previous.DecksAffected);
            Assert.Equal(previous.Batches + 1, report.Batches);
            previous = report;
        }
        Assert.Equal(new DeckPruningProgress(outcome.DecksAffected, outcome.Batches), reports[^1]);
    }

    private sealed class InlineProgress(Action<DeckPruningProgress> report) : IProgress<DeckPruningProgress>
    {
        public void Report(DeckPruningProgress value) => report(value);
    }

    private sealed record DeckState(int Processed, int Skipped, string? SkipReason, string? RefreshRequestedUtc);

    private sealed class PausingProgress(TaskCompletionSource firstReport, TaskCompletionSource resume) : IProgress<DeckPruningProgress>
    {
        public void Report(DeckPruningProgress value)
        {
            if (!firstReport.TrySetResult()) return;
            resume.Task.Wait(TimeSpan.FromSeconds(30));
        }
    }

    private sealed class TwicePausingProgress(TaskCompletionSource firstReport, TaskCompletionSource secondReport, TaskCompletionSource resumeFirst, TaskCompletionSource resumeSecond) : IProgress<DeckPruningProgress>
    {
        private int _reports;

        public void Report(DeckPruningProgress value)
        {
            if (Interlocked.Increment(ref _reports) == 1)
            {
                firstReport.TrySetResult();
                resumeFirst.Task.Wait(TimeSpan.FromSeconds(30));
            }
            else if (_reports == 2)
            {
                secondReport.TrySetResult();
                resumeSecond.Task.Wait(TimeSpan.FromSeconds(30));
            }
        }
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
