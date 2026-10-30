using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class StoredLiveRowsBackfillServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "stored-live-backfill-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_directory, "knowledge.db");
    private const string OracleExistsSql = "EXISTS (SELECT 1 FROM sources s INNER JOIN card_deck_totals t ON t.source_id = s.id WHERE s.deck_queue_id = q.id AND SUBSTR(s.source, 1, 15) = 'archidekt_live:')";

    [Fact]
    public async Task RunAsync_LegacyMarkersPending_CompletesWithExactMarkersAndReady()
    {
        var repository = await SeedAsync(2);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:deck-1", []);
        await repository.AddDeckIdsAsync(["never-stored"]);
        await SetPendingAsync();
        var result = await Service(repository).RunAsync();
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
        Assert.Equal(2, result.DecksRemoved);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(*) FROM deck_queue q WHERE has_stored_live_rows <> CASE WHEN " + OracleExistsSql + " THEN 1 ELSE 0 END;"));
        Assert.True(await repository.IsStoredLiveRowsMarkerReadyAsync());
    }

    [Fact]
    public async Task RunStoredLiveRowsBackfillBatchAsync_Rerun_ChangesNoMarkers()
    {
        var repository = await SeedAsync(3);
        await Service(repository).RunAsync();
        await SqlAsync("DELETE FROM crawl_state WHERE key = 'stored_live_rows_backfill_cursor';");
        var batch = await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        Assert.True(batch.DecksRechecked > 0);
        Assert.Equal(0, batch.MarkersChanged);
        Assert.True(batch.Completed);
    }

    [Fact]
    public async Task RunAsync_MaxDecks_StopsAndNextRunResumesFromCursor()
    {
        var repository = await SeedAsync(5);
        var result = await Service(repository).RunAsync(2);
        Assert.Equal(DeckPruningStopReason.MaxDecksReached, result.StopReason);
        Assert.Equal(2, result.DecksRemoved);
        await AssertSecondDeckCursorAsync();
        Assert.False(await repository.IsStoredLiveRowsMarkerReadyAsync());
        var resumed = await Service(repository).RunAsync();
        Assert.Equal(3, resumed.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, resumed.StopReason);
        Assert.True(await repository.IsStoredLiveRowsMarkerReadyAsync());
    }

    [Fact]
    public async Task RunAsync_FreeDiskBelowFloor_StopsWithDiskFloor()
    {
        var repository = await SeedAsync(1);
        var result = await Service(repository, _ => Task.FromResult<long?>(99)).RunAsync();
        Assert.Equal(DeckPruningStopReason.DiskFloor, result.StopReason);
        Assert.Equal(0, result.DecksRemoved);
        Assert.False(await repository.IsStoredLiveRowsMarkerReadyAsync());
    }

    [Fact]
    public async Task RunAsync_DiskDropsBelowFloorAfterFirstBatch_StopsBeforeSecondBatch()
    {
        var repository = await SeedAsync(5);
        var probes = 0;
        var result = await Service(repository, _ => Task.FromResult<long?>(++probes == 1 ? long.MaxValue : 99)).RunAsync();
        Assert.Equal(DeckPruningStopReason.DiskFloor, result.StopReason);
        Assert.Equal(2, result.DecksRemoved);
        Assert.Equal(1, result.Batches);
        await AssertSecondDeckCursorAsync();
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(*) FROM crawl_state WHERE key = 'stored_live_rows_backfill_complete';"));
        Assert.Equal(2, probes);
    }

    [Fact]
    public void PublicConstructor_UsesDefaultBatchDecks()
    {
        Directory.CreateDirectory(_directory);
        var service = new StoredLiveRowsBackfillService(new CategoryKnowledgeRepository(DatabasePath), new DeckPruningOptions(), _ => Task.FromResult<long?>(long.MaxValue));
        Assert.Equal(500, StoredLiveRowsBackfillService.DefaultBatchDecks);
        Assert.Equal(StoredLiveRowsBackfillService.DefaultBatchDecks, service.BatchDecks);
    }

    [Fact]
    public async Task RunAsync_ProbeUnavailable_StopsWithDiskProbeUnavailable()
    {
        var repository = await SeedAsync(1);
        foreach (var probe in new Func<CancellationToken, Task<long?>>[] { _ => Task.FromResult<long?>(null), _ => throw new IOException("probe unavailable") })
        {
            var result = await Service(repository, probe).RunAsync();
            Assert.Equal(DeckPruningStopReason.DiskProbeUnavailable, result.StopReason);
            Assert.Equal(0, result.DecksRemoved);
            Assert.False(await repository.IsStoredLiveRowsMarkerReadyAsync());
        }
    }

    [Fact]
    public async Task RunAsync_AlreadyComplete_ReturnsCompletedWithoutProbing()
    {
        var repository = await CreateRepositoryAsync();
        var probes = 0;
        var result = await Service(repository, _ => { probes++; return Task.FromResult<long?>(null); }).RunAsync();
        Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.Completed), result);
        Assert.Equal(0, probes);
    }

    [Fact]
    public async Task EnsureSchema_FreshDatabase_IsReady()
    {
        var repository = await CreateRepositoryAsync();
        Assert.True(await repository.IsStoredLiveRowsMarkerReadyAsync());
    }

    [Fact]
    public async Task EnsureSchema_ExistingSourcesWithoutFlag_StaysPending()
    {
        await SeedAsync(1);
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, $"Data Source={DatabasePath};Pooling=False"));
        await repository.EnsureSchemaAsync();
        Assert.False(await repository.IsStoredLiveRowsMarkerReadyAsync());
    }

    private static StoredLiveRowsBackfillService Service(CategoryKnowledgeRepository repository, Func<CancellationToken, Task<long?>>? probe = null)
        => new(repository, new DeckPruningOptions { MinFreeDiskBytes = 100 }, probe ?? (_ => Task.FromResult<long?>(long.MaxValue)), (_, _) => Task.CompletedTask, 2);

    private async Task<CategoryKnowledgeRepository> CreateRepositoryAsync()
    {
        Directory.CreateDirectory(_directory);
        var repository = new CategoryKnowledgeRepository(DatabasePath);
        await repository.EnsureSchemaAsync();
        return repository;
    }

    private async Task<CategoryKnowledgeRepository> SeedAsync(int count)
    {
        var repository = await CreateRepositoryAsync();
        for (var i = 0; i < count; i++)
        {
            var deckId = $"deck-{i}";
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
                [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        }
        await SetPendingAsync();
        return repository;
    }

    private async Task SetPendingAsync()
        => await SqlAsync("UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');");

    private async Task AssertSecondDeckCursorAsync()
    {
        var secondId = await ScalarAsync<long>("SELECT id FROM deck_queue ORDER BY id LIMIT 1 OFFSET 1;");
        Assert.Equal(secondId.ToString(System.Globalization.CultureInfo.InvariantCulture), await ScalarAsync<string>("SELECT value FROM crawl_state WHERE key = 'stored_live_rows_backfill_cursor';"));
    }

    private async Task SqlAsync(string sql)
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
}
