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

        Assert.Equal(5, outcome.DecksAffected);
        Assert.Equal(DeckPruningStopReason.Completed, outcome.StopReason);
        Assert.True(outcome.Batches >= 2);
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
        => new(
            new FakeWebHostEnvironment(Path.Combine(Path.GetTempPath(), "deckflow-maintenance-" + Guid.NewGuid().ToString("N"))),
            new MemoryCache(new MemoryCacheOptions()),
            retentionOptions: Options.Create(options ?? new HarvestRetentionOptions
            {
                PerCommanderCap = 2,
                BatchSize = 2,
                MaintenanceChunkDecks = 3,
                PauseMilliseconds = 0,
                MinFreeDiskBytes = 0
            }));

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
