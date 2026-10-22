using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeckFlow.Web.Tests;

[Collection("CategoryKnowledgeStoreTests")]
public sealed class HarvestMaintenanceRunnerTests
{
    [Fact]
    public async Task Drain_StartedThroughRunner_RemovesOverCapDecks()
    {
        var store = CreateStore();
        await SeedTracerAsync(store);
        var started = DateTimeOffset.Parse("2026-10-22T00:00:00Z");
        var runner = new HarvestMaintenanceRunner(store, NullLogger<HarvestMaintenanceRunner>.Instance, () => started);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(runner.TryStart(HarvestMaintenanceOperation.Drain));
            var running = runner.GetStatus();
            Assert.True(running.IsRunning);
            Assert.Equal(HarvestMaintenanceOperation.Drain, running.Operation);
            Assert.Equal(started, running.StartedUtc);

            var status = await WaitForCompletionAsync(runner);
            Assert.Equal(5, status.DecksAffected);
            Assert.False(status.IsRunning);
            Assert.Equal(DeckPruningStopReason.Completed, status.StopReason);
            Assert.Equal(started, status.CompletedUtc);
            Assert.False(status.Failed);
            Assert.False(status.StopRequested);
            Assert.Equal(4, await CountProcessedUnskippedDecksAsync(store));
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void GetStatus_BeforeAnyRun_IsIdle()
    {
        var runner = new HarvestMaintenanceRunner(new FakeCategoryKnowledgeStore(), NullLogger<HarvestMaintenanceRunner>.Instance);

        Assert.Equal(HarvestMaintenanceStatus.Idle, runner.GetStatus());
    }

    [Fact]
    public async Task TryStart_WhileQueuedOrRunning_ReturnsFalse()
    {
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceBlocksUntilCancelled = true };
        var runner = new HarvestMaintenanceRunner(store, NullLogger<HarvestMaintenanceRunner>.Instance);
        Assert.True(runner.TryStart(HarvestMaintenanceOperation.Drain));
        Assert.False(runner.TryStart(HarvestMaintenanceOperation.Purge));
        await runner.StartAsync(CancellationToken.None);
        try
        {
            await WaitForMaintenanceCallAsync(store);
            Assert.False(runner.TryStart(HarvestMaintenanceOperation.Refetch));
            Assert.Equal(1, store.RunMaintenanceCalls);
            Assert.Equal(HarvestMaintenanceOperation.Drain, store.LastMaintenanceOperation);
        }
        finally { await runner.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task RequestStop_WhileRunning_EndsStoppedWithReportedProgress()
    {
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceProgress = 7, RunMaintenanceBlocksUntilCancelled = true };
        var runner = new HarvestMaintenanceRunner(store, NullLogger<HarvestMaintenanceRunner>.Instance);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(runner.TryStart(HarvestMaintenanceOperation.Drain));
            await WaitForMaintenanceCallAsync(store);
            Assert.True(runner.RequestStop());
            var status = await WaitForCompletionAsync(runner);
            Assert.False(status.IsRunning); Assert.True(status.StopRequested); Assert.False(status.Failed); Assert.Equal(7, status.DecksAffected);
        }
        finally { await runner.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public void RequestStop_WhenIdle_ReturnsFalse()
    {
        Assert.False(new HarvestMaintenanceRunner(new FakeCategoryKnowledgeStore(), NullLogger<HarvestMaintenanceRunner>.Instance).RequestStop());
    }

    [Fact]
    public async Task StoreThrows_EndsFailed_AndNextStartRuns()
    {
        var exception = new InvalidOperationException("secret-detail");
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceException = exception };
        var logger = new ExceptionRecordingLogger();
        var runner = new HarvestMaintenanceRunner(store, logger);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(runner.TryStart(HarvestMaintenanceOperation.Drain));
            var failed = await WaitForCompletionAsync(runner);
            var errorEntries = await WaitForErrorEntriesAsync(logger);
            Assert.True(failed.Failed); Assert.Single(errorEntries); Assert.Same(exception, errorEntries[0].Exception);
            Assert.DoesNotContain("secret-detail", string.Join(' ', failed.ToString()));
            store.RunMaintenanceException = null;
            Assert.True(runner.TryStart(HarvestMaintenanceOperation.Drain));
            Assert.False((await WaitForCompletionAsync(runner)).Failed);
        }
        finally { await runner.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task StopAsync_WhileRunning_CancelsOperation()
    {
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceBlocksUntilCancelled = true };
        var runner = new HarvestMaintenanceRunner(store, NullLogger<HarvestMaintenanceRunner>.Instance);
        await runner.StartAsync(CancellationToken.None);
        Assert.True(runner.TryStart(HarvestMaintenanceOperation.Drain));
        await WaitForMaintenanceCallAsync(store);
        await runner.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(runner.GetStatus().StopRequested);
    }

    private static async Task WaitForMaintenanceCallAsync(FakeCategoryKnowledgeStore store)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.RunMaintenanceCalls == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.Equal(1, store.RunMaintenanceCalls);
    }

    private sealed class ExceptionRecordingLogger : ILogger<HarvestMaintenanceRunner>
    {
        private readonly object _sync = new();
        private readonly List<(LogLevel Level, Exception? Exception)> _entries = [];

        public IReadOnlyList<(LogLevel Level, Exception? Exception)> Snapshot
        {
            get { lock (_sync) return [.. _entries]; }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_sync) _entries.Add((logLevel, exception));
        }
    }

    private static async Task<IReadOnlyList<(LogLevel Level, Exception? Exception)>> WaitForErrorEntriesAsync(ExceptionRecordingLogger logger)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var errorEntries = logger.Snapshot.Where(entry => entry.Level == LogLevel.Error).ToArray();
            if (errorEntries.Length > 0) return errorEntries;
            await Task.Delay(20);
        }

        throw new TimeoutException("Maintenance runner did not log an error within 10 seconds.");
    }

    private static CategoryKnowledgeStore CreateStore()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "deckflow-maintenance-runner-" + Guid.NewGuid().ToString("N"));
        var previousDataDirectory = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", dataDirectory);
        try
        {
            return new CategoryKnowledgeStore(
                new FakeWebHostEnvironment(dataDirectory),
                new MemoryCache(new MemoryCacheOptions()),
                retentionOptions: Options.Create(new HarvestRetentionOptions { PerCommanderCap = 2, BatchSize = 2, MaintenanceChunkDecks = 3, PauseMilliseconds = 0, MinFreeDiskBytes = 0 }));
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

    private static async Task<HarvestMaintenanceStatus> WaitForCompletionAsync(IHarvestMaintenanceRunner runner)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var status = runner.GetStatus();
            if (!status.IsRunning) return status;
            await Task.Delay(20);
        }

        throw new TimeoutException("Maintenance runner did not finish within 10 seconds.");
    }

    private static async Task<int> CountProcessedUnskippedDecksAsync(CategoryKnowledgeStore store)
    {
        await using var connection = new SqliteConnection($"Data Source={Assert.IsType<string>(store.DatabasePath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM deck_queue WHERE processed = 1 AND skipped = 0;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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
