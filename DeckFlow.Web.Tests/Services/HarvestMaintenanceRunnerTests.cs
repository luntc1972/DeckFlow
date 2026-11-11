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
        var runner = CreateRunner(store, utcNow: () => started);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain));
            var running = runner.GetStatus();
            Assert.True(running.IsRunning);
            Assert.Equal(HarvestMaintenanceOperation.Drain, running.Operation);
            Assert.Equal(started, running.StartedUtc);

            var status = await WaitForCompletionAsync(runner);
            Assert.Equal(5, status.DecksAffected);
            Assert.False(status.IsRunning);
            Assert.Equal(DeckPruningStopReason.Completed, status.StopReason);
            Assert.Equal(started, status.EndedUtc);
            Assert.Equal(HarvestMaintenanceRunState.Finished, status.State);
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
        var runner = CreateRunner(new FakeCategoryKnowledgeStore());

        Assert.Equal(HarvestMaintenanceStatus.Idle, runner.GetStatus());
    }

    [Fact]
    public async Task TryStart_WhileQueuedOrRunning_ReturnsFalse()
    {
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceBlocksUntilCancelled = true };
        var runner = CreateRunner(store);
        Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain));
        Assert.Equal(HarvestMaintenanceStartResult.AlreadyActive, await runner.TryStartAsync(HarvestMaintenanceOperation.Purge));
        await runner.StartAsync(CancellationToken.None);
        try
        {
            await WaitForMaintenanceCallAsync(store);
            Assert.Equal(HarvestMaintenanceStartResult.AlreadyActive, await runner.TryStartAsync(HarvestMaintenanceOperation.Refetch));
            Assert.Equal(1, store.RunMaintenanceCalls);
            Assert.Equal(HarvestMaintenanceOperation.Drain, store.LastMaintenanceOperation);
        }
        finally { await runner.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task RequestStop_WhileRunning_EndsStoppedWithReportedProgress()
    {
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceProgress = 7, RunMaintenanceBlocksUntilCancelled = true };
        var logger = new ExceptionRecordingLogger();
        var runner = CreateRunner(store, logger);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain));
            await WaitForMaintenanceCallAsync(store);
            Assert.True(runner.RequestStop());
            var status = await WaitForCompletionAsync(runner);
            Assert.False(status.IsRunning); Assert.True(status.StopRequested); Assert.Equal(HarvestMaintenanceRunState.Stopped, status.State); Assert.Equal(7, status.DecksAffected);
            Assert.Single(logger.Fields, field => field.Key == "{OriginalFormat}" && Equals(field.Value, "Harvest.Maintenance.Stopped operation={Operation} decksAffected={DecksAffected}"));
            Assert.Contains(logger.Fields, field => field.Key == "Operation" && Equals(field.Value, HarvestMaintenanceOperation.Drain));
            Assert.Contains(logger.Fields, field => field.Key == "DecksAffected" && Equals(field.Value, 7));
        }
        finally { await runner.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public void RequestStop_WhenIdle_ReturnsFalse()
    {
        Assert.False(CreateRunner(new FakeCategoryKnowledgeStore()).RequestStop());
    }

    [Fact]
    public async Task StoreThrows_EndsFailed_AndNextStartRuns()
    {
        var exception = new InvalidOperationException("secret-detail");
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceException = exception };
        var logger = new ExceptionRecordingLogger();
        var runner = CreateRunner(store, logger);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain));
            var failed = await WaitForCompletionAsync(runner);
            var errorEntries = await WaitForErrorEntriesAsync(logger);
            Assert.Equal(HarvestMaintenanceRunState.Failed, failed.State); Assert.Single(errorEntries); Assert.Same(exception, errorEntries[0].Exception);
            Assert.DoesNotContain("secret-detail", string.Join(' ', failed.ToString()));
            store.RunMaintenanceException = null;
            Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain));
            Assert.Equal(HarvestMaintenanceRunState.Finished, (await WaitForCompletionAsync(runner)).State);
        }
        finally { await runner.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task StopAsync_WhileRunning_EndsInterrupted()
    {
        var store = new FakeCategoryKnowledgeStore { RunMaintenanceBlocksUntilCancelled = true };
        var logger = new ExceptionRecordingLogger();
        var runner = CreateRunner(store, logger);
        await runner.StartAsync(CancellationToken.None);
        Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain));
        await WaitForMaintenanceCallAsync(store);
        await runner.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(runner.GetStatus().StopRequested);
        Assert.Equal(HarvestMaintenanceRunState.Interrupted, runner.GetStatus().State);
        Assert.Single(logger.Fields, field => field.Key == "{OriginalFormat}" && Equals(field.Value, "Harvest.Maintenance.Interrupted operation={Operation} decksAffected={DecksAffected}"));
        Assert.Contains(logger.Fields, field => field.Key == "Operation" && Equals(field.Value, HarvestMaintenanceOperation.Drain));
        Assert.Contains(logger.Fields, field => field.Key == "DecksAffected" && Equals(field.Value, 0));
    }

    private static async Task WaitForMaintenanceCallAsync(FakeCategoryKnowledgeStore store)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.RunMaintenanceCalls == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.Equal(1, store.RunMaintenanceCalls);
    }

    [Fact]
    public async Task Backfill_Finished_PersistsInsertRunningAndFinished()
    {
        var runs = new FakeHarvestMaintenanceRunStore(); var store = new FakeCategoryKnowledgeStore(); using var runner = CreateRunner(store, runStore: runs);
        Assert.Equal(HarvestMaintenanceStartResult.Started, await runner.TryStartAsync(HarvestMaintenanceOperation.Backfill));
        Assert.Equal(HarvestMaintenanceRunState.Starting, Assert.Single(runs.Inserted).State);
        await runner.StartAsync(default); await WaitForCompletionAsync(runner); await runner.StopAsync(default);
        Assert.Equal(new[] { HarvestMaintenanceRunState.Running, HarvestMaintenanceRunState.Finished }, runs.Saved.Select(x => x.State!.Value));
        Assert.All(runs.Saved, x => Assert.Equal(runs.Inserted[0].RunId, x.RunId));
    }

    [Fact]
    public async Task StartAccepted_ShutdownBeforeDequeue_RestartShowsInterrupted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"maintenance-{Guid.NewGuid():N}.db");
        try
        {
            var firstStore = new HarvestMaintenanceRunStore(path, NullLogger<HarvestMaintenanceRunStore>.Instance); Guid? id;
            using (var first = CreateRunner(new FakeCategoryKnowledgeStore(), runStore: firstStore)) { Assert.Equal(HarvestMaintenanceStartResult.Started, await first.TryStartAsync(HarvestMaintenanceOperation.Drain)); id = first.GetStatus().RunId; }
            var now = HarvestMaintenanceRunStoreTests.Now;
            var secondStore = new HarvestMaintenanceRunStore(path, NullLogger<HarvestMaintenanceRunStore>.Instance, () => now); await secondStore.EnsureSchemaAsync();
            using var second = CreateRunner(new FakeCategoryKnowledgeStore(), runStore: secondStore); var status = await second.GetStatusAsync();
            Assert.Equal(HarvestMaintenanceRunState.Interrupted, status.State); Assert.Equal(id, status.RunId); Assert.Equal(0, status.DecksAffected); Assert.Equal(now, status.EndedUtc);
            Assert.Equal("Retention drain interrupted by restart at 2026-11-11 08:00:00 UTC, 0 decks done — press Retention drain to resume.", HarvestMaintenanceStatusText.Describe(status, now).Headline);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Fact]
    public async Task TryStartAsync_InsertFails_ReturnsNotRecordedAndQueuesNothing()
    {
        var runs = new FakeHarvestMaintenanceRunStore { ThrowOnInsert = true }; var store = new FakeCategoryKnowledgeStore(); var logger = new ExceptionRecordingLogger(); using var runner = CreateRunner(store, logger, runStore: runs); var before = runner.GetStatus();
        Assert.Equal(HarvestMaintenanceStartResult.NotRecorded, await runner.TryStartAsync(HarvestMaintenanceOperation.Drain)); Assert.Equal(before, runner.GetStatus());
        Assert.Contains(logger.Fields, x => x.Key == "State" && Equals(x.Value, HarvestMaintenanceRunState.Starting));
        runs.ThrowOnInsert = false; await runner.StartAsync(default); await runner.TryStartAsync(HarvestMaintenanceOperation.Backfill); await WaitForCompletionAsync(runner); await runner.StopAsync(default); Assert.Equal(1, store.RunMaintenanceCalls); Assert.Equal(HarvestMaintenanceOperation.Backfill, store.LastMaintenanceOperation);
    }

    [Fact]
    public async Task TryStartAsync_InsertInFlight_SecondStartReturnsAlreadyActive()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var runs = new FakeHarvestMaintenanceRunStore { InsertGate = gate.Task }; using var runner = CreateRunner(new FakeCategoryKnowledgeStore(), runStore: runs);
        var first = runner.TryStartAsync(HarvestMaintenanceOperation.Drain); Assert.Equal(HarvestMaintenanceRunState.Starting, runner.GetStatus().State); Assert.Equal(HarvestMaintenanceStartResult.AlreadyActive, await runner.TryStartAsync(HarvestMaintenanceOperation.Purge)); gate.SetResult(); Assert.Equal(HarvestMaintenanceStartResult.Started, await first); Assert.Equal(HarvestMaintenanceOperation.Drain, Assert.Single(runs.Inserted).Operation);
    }

    [Fact]
    public async Task GetStatusAsync_NoRunThisBoot_ReturnsLatestPersisted()
    {
        var row = HarvestMaintenanceRunStoreTests.Row(HarvestMaintenanceRunState.Finished); using var runner = CreateRunner(new FakeCategoryKnowledgeStore(), runStore: new FakeHarvestMaintenanceRunStore { Latest = row }); Assert.Equal(row, await runner.GetStatusAsync());
    }

    [Fact]
    public async Task GetStatusAsync_StoreThrows_ReturnsIdleAndLogsStatusUnavailable()
    {
        var logger = new ExceptionRecordingLogger(); using var runner = CreateRunner(new FakeCategoryKnowledgeStore(), logger, runStore: new FakeHarvestMaintenanceRunStore { ThrowOnGetLatest = true }); Assert.Equal(HarvestMaintenanceStatus.Idle, await runner.GetStatusAsync()); Assert.Contains(logger.Fields, x => Equals(x.Value, "Harvest.Maintenance.StatusUnavailable"));
    }

    [Fact]
    public async Task PersistFailure_LogsPersistFailedAndRunFinishes()
    {
        var logger = new ExceptionRecordingLogger(); using var runner = CreateRunner(new FakeCategoryKnowledgeStore(), logger, runStore: new FakeHarvestMaintenanceRunStore { ThrowOnSave = true }); await runner.StartAsync(default); await runner.TryStartAsync(HarvestMaintenanceOperation.Backfill); Assert.Equal(HarvestMaintenanceRunState.Finished, (await WaitForCompletionAsync(runner)).State); await runner.StopAsync(default); Assert.Contains(logger.Fields, x => Equals(x.Value, "Harvest.Maintenance.PersistFailed operation={Operation} state={State}"));
    }

    [Fact]
    public async Task PersistInterval_FlushesProgressWhileRunning()
    {
        var runs = new FakeHarvestMaintenanceRunStore(); var store = new FakeCategoryKnowledgeStore { RunMaintenanceProgress = 7, RunMaintenanceBlocksUntilCancelled = true }; using var runner = CreateRunner(store, runStore: runs, persistInterval: TimeSpan.FromMilliseconds(50)); await runner.StartAsync(default);
        try { await runner.TryStartAsync(HarvestMaintenanceOperation.Drain); await WaitForMaintenanceCallAsync(store); await runs.ProgressSaved.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Contains(runs.Saved.ToArray(), x => x.State == HarvestMaintenanceRunState.Running && x.DecksAffected == 7); } finally { await runner.StopAsync(default); }
    }

    [Fact]
    public async Task RequestStop_BeforeDequeue_EndsStoppedWithoutCallingStore()
    {
        var runs = new FakeHarvestMaintenanceRunStore(); var store = new FakeCategoryKnowledgeStore(); using var runner = CreateRunner(store, runStore: runs); await runner.TryStartAsync(HarvestMaintenanceOperation.Drain); Assert.True(runner.RequestStop()); await runner.StartAsync(default); Assert.Equal(HarvestMaintenanceRunState.Stopped, (await WaitForCompletionAsync(runner)).State); await runner.StopAsync(default); Assert.Single(runs.Inserted); Assert.Equal(HarvestMaintenanceRunState.Stopped, runs.Saved[^1].State); Assert.Equal(0, store.RunMaintenanceCalls);
    }

    [Fact]
    public async Task Restart_RealStore_ShowsInterruptedText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"maintenance-{Guid.NewGuid():N}.db");
        using var first = CreateRunner(new FakeCategoryKnowledgeStore { RunMaintenanceBlocksUntilCancelled = true }, runStore: new HarvestMaintenanceRunStore(path, NullLogger<HarvestMaintenanceRunStore>.Instance));
        try
        {
            await first.StartAsync(default); await first.TryStartAsync(HarvestMaintenanceOperation.Drain);
            await Task.Delay(150);
            using var second = CreateRunner(new FakeCategoryKnowledgeStore(), runStore: new HarvestMaintenanceRunStore(path, NullLogger<HarvestMaintenanceRunStore>.Instance)); var status = await second.GetStatusAsync();
            Assert.Equal(HarvestMaintenanceRunState.Interrupted, status.State);
            Assert.StartsWith("Retention drain interrupted by restart at", HarvestMaintenanceStatusText.Describe(status, DateTimeOffset.UtcNow).Headline);
        }
        finally { await first.StopAsync(default); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Fact]
    public async Task Tracer_BackfillThroughRealStore_ShowsDone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"maintenance-{Guid.NewGuid():N}.db");
        using var first = CreateRunner(new FakeCategoryKnowledgeStore { RunMaintenanceBlocksUntilCancelled = false }, runStore: new HarvestMaintenanceRunStore(path, NullLogger<HarvestMaintenanceRunStore>.Instance));
        try
        {
            await first.StartAsync(default); await first.TryStartAsync(HarvestMaintenanceOperation.Backfill);
            await WaitForCompletionAsync(first); await first.StopAsync(default);
            using var second = CreateRunner(new FakeCategoryKnowledgeStore(), runStore: new HarvestMaintenanceRunStore(path, NullLogger<HarvestMaintenanceRunStore>.Instance)); var status = await second.GetStatusAsync();
            Assert.Equal(HarvestMaintenanceRunState.Finished, status.State);
            Assert.Equal("Marker backfill: Done.", HarvestMaintenanceStatusText.Describe(status, DateTimeOffset.UtcNow).Headline);
        }
        finally { await first.StopAsync(default); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Fact]
    public async Task Drain_PurgePending_EndsRefusedWithoutRunning()
    {
        var now = DateTimeOffset.Parse("2026-11-11T07:00:00Z");
        var store = new FakeCategoryKnowledgeStore { MaintenancePreview = new(0, 5, 0) };
        var runs = new FakeHarvestMaintenanceRunStore();
        var logger = new ExceptionRecordingLogger();
        using var runner = CreateRunner(store, logger, () => now, runs);
        await runner.StartAsync(default);
        try
        {
            await runner.TryStartAsync(HarvestMaintenanceOperation.Drain);
            var status = await WaitForCompletionAsync(runner);
            await runner.StopAsync(default);
            Assert.Equal(HarvestMaintenanceRunState.Refused, status.State); Assert.Equal("purge_pending", status.RefusalReason); Assert.Equal(store.MaintenancePreview, status.PreviewAtStart); Assert.Equal(0, store.RunMaintenanceCalls); Assert.Single(logger.Fields, x => Equals(x.Value, "Harvest.Maintenance.Refused operation={Operation} reason={Reason}")); Assert.Contains(logger.Fields, x => x.Key == "Operation" && Equals(x.Value, HarvestMaintenanceOperation.Drain)); Assert.Contains(logger.Fields, x => x.Key == "Reason" && Equals(x.Value, "purge_pending"));
            Assert.Equal(status.State, runs.Saved[^1].State);
        }
        finally { await runner.StopAsync(default); }
    }

    [Fact]
    public async Task Drain_PreviewThrows_EndsRefusedPreviewUnavailable()
    {
        var now = DateTimeOffset.Parse("2026-11-11T07:00:00Z");
        var store = new FakeCategoryKnowledgeStore { MaintenancePreviewException = new InvalidOperationException("unavailable") };
        var runs = new FakeHarvestMaintenanceRunStore();
        var logger = new ExceptionRecordingLogger();
        using var runner = CreateRunner(store, logger, () => now, runs);
        await runner.StartAsync(default);
        try
        {
            await runner.TryStartAsync(HarvestMaintenanceOperation.Drain);
            var status = await WaitForCompletionAsync(runner);
            await runner.StopAsync(default);
            Assert.Equal(HarvestMaintenanceRunState.Refused, status.State); Assert.Equal("preview_unavailable", status.RefusalReason); Assert.Single(logger.Fields, x => Equals(x.Value, "Harvest.Maintenance.PreviewUnavailable operation={Operation}")); Assert.Contains(logger.Fields, x => x.Key == "Operation" && Equals(x.Value, HarvestMaintenanceOperation.Drain));
            Assert.Equal(status.State, runs.Saved[^1].State);
        }
        finally { await runner.StopAsync(default); }
    }

    [Fact]
    public async Task Purge_PreviewUnavailable_RunsWithoutEstimate()
    {
        var now = DateTimeOffset.Parse("2026-11-11T07:00:00Z");
        var store = new FakeCategoryKnowledgeStore { MaintenancePreviewException = new InvalidOperationException("unavailable") };
        var runs = new FakeHarvestMaintenanceRunStore();
        var logger = new ExceptionRecordingLogger();
        using var runner = CreateRunner(store, logger, () => now, runs);
        await runner.StartAsync(default);
        try
        {
            await runner.TryStartAsync(HarvestMaintenanceOperation.Purge);
            var status = await WaitForCompletionAsync(runner);
            await runner.StopAsync(default);
            Assert.Equal(HarvestMaintenanceRunState.Finished, status.State); Assert.Null(status.PreviewAtStart); Assert.Equal(1, store.RunMaintenanceCalls);
            Assert.Equal(status.State, runs.Saved[^1].State);
        }
        finally { await runner.StopAsync(default); }
    }

    [Fact]
    public async Task Backfill_SkipsPreview()
    {
        var now = DateTimeOffset.Parse("2026-11-11T07:00:00Z");
        var store = new FakeCategoryKnowledgeStore { MaintenancePreviewException = new InvalidOperationException("must not read") };
        var runs = new FakeHarvestMaintenanceRunStore();
        var logger = new ExceptionRecordingLogger();
        using var runner = CreateRunner(store, logger, () => now, runs);
        await runner.StartAsync(default);
        try
        {
            await runner.TryStartAsync(HarvestMaintenanceOperation.Backfill);
            var status = await WaitForCompletionAsync(runner);
            await runner.StopAsync(default);
            Assert.Equal(HarvestMaintenanceRunState.Finished, status.State); Assert.Equal(0, store.MaintenancePreviewCalls);
            Assert.Equal(status.State, runs.Saved[^1].State);
        }
        finally { await runner.StopAsync(default); }
    }

    [Fact]
    public async Task Drain_GatePasses_RecordsPreviewAtStartAndRuns()
    {
        var now = DateTimeOffset.Parse("2026-11-11T07:00:00Z");
        var store = new FakeCategoryKnowledgeStore { MaintenancePreview = new(2000, 0, 0) };
        var runs = new FakeHarvestMaintenanceRunStore();
        var logger = new ExceptionRecordingLogger();
        using var runner = CreateRunner(store, logger, () => now, runs);
        await runner.StartAsync(default);
        try
        {
            await runner.TryStartAsync(HarvestMaintenanceOperation.Drain);
            var status = await WaitForCompletionAsync(runner);
            await runner.StopAsync(default);
            Assert.Equal(store.MaintenancePreview, status.PreviewAtStart); Assert.Equal(now, status.PreviewAsOfUtc); Assert.Equal(1, store.RunMaintenanceCalls);
            Assert.Equal(status.State, runs.Saved[^1].State);
        }
        finally { await runner.StopAsync(default); }
    }

    [Fact]
    public async Task Drain_PreviewInFlight_StatusStartingAndDuplicateStartRefused()
    {
        var store = new FakeCategoryKnowledgeStore { MaintenancePreviewBlocksUntilCancelled = true };
        using var runner = CreateRunner(store);
        await runner.StartAsync(default);
        try
        {
            await runner.TryStartAsync(HarvestMaintenanceOperation.Drain);
            await Task.Delay(100);
            Assert.Equal(1, store.MaintenancePreviewCalls);
            Assert.Equal(HarvestMaintenanceRunState.Starting, runner.GetStatus().State);
            Assert.Equal(HarvestMaintenanceStartResult.AlreadyActive, await runner.TryStartAsync(HarvestMaintenanceOperation.Purge));
        }
        finally { runner.RequestStop(); await runner.StopAsync(default); }
    }

    private static HarvestMaintenanceRunner CreateRunner(ICategoryKnowledgeStore store, ILogger<HarvestMaintenanceRunner>? logger = null, Func<DateTimeOffset>? utcNow = null, IHarvestMaintenanceRunStore? runStore = null, TimeSpan? persistInterval = null)
        => new(WithCleanPreview(store), runStore ?? new FakeHarvestMaintenanceRunStore(), logger ?? NullLogger<HarvestMaintenanceRunner>.Instance, utcNow ?? (() => DateTimeOffset.UtcNow), persistInterval);

    private static ICategoryKnowledgeStore WithCleanPreview(ICategoryKnowledgeStore store)
    {
        if (store is FakeCategoryKnowledgeStore fake && fake.MaintenancePreview is null && fake.MaintenancePreviewException is null) fake.MaintenancePreview = new(0, 0, 0);
        return store;
    }

    private sealed class ExceptionRecordingLogger : ILogger<HarvestMaintenanceRunner>
    {
        public List<KeyValuePair<string, object?>> Fields { get; } = [];
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
            lock (_sync)
            {
                _entries.Add((logLevel, exception));
                if (state is IEnumerable<KeyValuePair<string, object?>> fields) Fields.AddRange(fields);
            }
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
