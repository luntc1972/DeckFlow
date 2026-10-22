using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Caching.Memory;
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
    public async Task RunMaintenanceAsync_Drain_RemovesOverCapDecksAcrossChunks()
    {
        var store = CreateStore();
        await SeedTracerAsync(store);
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, new Progress<int>());

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

        var progress = new List<int>();
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Purge, new Progress<int>(progress.Add));

        Assert.Equal(4, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        Assert.Equal([3, 4], progress);
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

        var progress = new List<int>();
        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Refetch, new Progress<int>(progress.Add));

        Assert.Equal(2, outcome.DecksAffected);
        Assert.Equal(0, outcome.Batches);
        Assert.Null(outcome.StopReason);
        Assert.Equal([2], progress);
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
        var progress = new List<int>();

        var outcome = await store.RunMaintenanceAsync(HarvestMaintenanceOperation.Drain, new Progress<int>(progress.Add));

        Assert.Equal(0, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.DiskFloor, outcome.StopReason);
        Assert.Equal([0], progress);
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
        var postHarvest = await store.RunPostHarvestRetentionAsync().WaitAsync(TimeSpan.FromSeconds(10));
        resume.TrySetResult();
        var outcome = await drain.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, postHarvest!.DecksRemoved);
        Assert.Equal(3, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
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

    private sealed record DeckState(int Processed, int Skipped, string? SkipReason, string? RefreshRequestedUtc);

    private sealed class PausingProgress(TaskCompletionSource firstReport, TaskCompletionSource resume) : IProgress<int>
    {
        public void Report(int value)
        {
            firstReport.TrySetResult();
            resume.Task.Wait(TimeSpan.FromSeconds(30));
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
