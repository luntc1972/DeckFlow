using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Specifies the Postgres schema and query contract for stored live rows.
/// </summary>
public sealed class StoredLiveRowsMarkerPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public StoredLiveRowsMarkerPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EmptiedDeck_Postgres_KeepsSourceClearsMarkerAndIsNotMatched()
    {
        var repository = await CreateRepositoryAsync();
        var deckId = "marker-empty-" + Guid.NewGuid().ToString("N");
        await StoreAsync(repository, deckId);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, []);

        Assert.Equal(1L, await ScalarAsync<long>("SELECT COUNT(1) FROM sources WHERE source = @source;", ("source", "archidekt_live:" + deckId)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(1) FROM card_deck_totals t JOIN sources s ON s.id = t.source_id WHERE s.source = @source;", ("source", "archidekt_live:" + deckId)));
        await AssertMarkerAsync(deckId, 0L);
        Assert.DoesNotContain((await repository.GetRetentionCandidatesAsync("Atraxa")).Select(candidate => candidate.DeckId), id => id == deckId);
        var queueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId;", ("deckId", deckId));
        Assert.DoesNotContain((await repository.GetPurgeCandidatesAsync()).Select(candidate => candidate.DeckQueueId), id => id == queueId);
    }

    [PostgresFact]
    public async Task WritePaths_Postgres_SetAndClearMarker()
    {
        var repository = await CreateRepositoryAsync();
        var prefix = "marker-write-" + Guid.NewGuid().ToString("N");
        var deleteId = prefix + "-delete";
        await StoreAsync(repository, deleteId);
        await AssertMarkerAsync(deleteId, 1L);
        await repository.DeleteSourceDataAsync("archidekt_live:" + deleteId);
        await AssertMarkerAsync(deleteId, 0L);

        var rejectId = prefix + "-reject";
        await StoreAsync(repository, rejectId);
        await repository.RejectUrlDeckAsync("archidekt_url:https://archidekt.com/decks/" + rejectId, rejectId, "Atraxa", null, DeckSkipReasons.NotCommanderFormat);
        await AssertMarkerAsync(rejectId, 0L);

        var removeId = prefix + "-remove";
        await StoreAsync(repository, removeId);
        var queueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId;", ("deckId", removeId));
        await repository.RemoveStoredDecksAsync([(queueId, DeckSkipReasons.Retention)]);
        await AssertMarkerAsync(removeId, 0L);
    }

    [PostgresFact]
    public async Task StoredLiveRowsSql_Postgres_PlanReadsNeitherTotalsNorSources()
    {
        await CreateRepositoryAsync();
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var sql = ("SELECT COUNT(1) FROM deck_queue q WHERE " + DeckQueueRepository.StoredLiveRowsSql)
            .Replace("@livePrefixLength", "15", StringComparison.Ordinal)
            .Replace("@livePrefix", "'archidekt_live:'", StringComparison.Ordinal);
        var plan = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("EXPLAIN " + sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            plan.Add(reader.GetString(0));
        }

        Assert.DoesNotContain(plan, line => line.Contains("card_deck_totals", StringComparison.OrdinalIgnoreCase)
            || line.Contains("sources", StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task StoredLiveIndex_Postgres_IsValidAndUsedForMarkerPredicate()
    {
        await CreateRepositoryAsync();
        var valid = await ScalarAsync<bool>("SELECT indisvalid FROM pg_index WHERE indexrelid = 'ix_deck_queue_stored_live'::regclass;");
        Assert.True(valid);

        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setting = new NpgsqlCommand("SET LOCAL enable_seqscan = off;", connection, transaction))
        {
            await setting.ExecuteNonQueryAsync();
        }

        var plan = new List<string>();
        await using (var explain = new NpgsqlCommand("EXPLAIN SELECT COUNT(1) FROM deck_queue q WHERE q.has_stored_live_rows = 1;", connection, transaction))
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }
        }

        Assert.Contains(plan, line => line.Contains("ix_deck_queue_stored_live", StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task Backfill_Postgres_LegacyPendingCompletesExactAndRerunChangesNothing()
    {
        var repository = await CreateRepositoryAsync();
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var saved = new Dictionary<string, string>();
        await using (var read = new NpgsqlCommand("SELECT key, value FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');", connection))
        await using (var reader = await read.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) saved.Add(reader.GetString(0), reader.GetString(1));
        }

        try
        {
            var deckId = "marker-backfill-" + Guid.NewGuid().ToString("N");
            await StoreAsync(repository, deckId);
            await StoreAsync(repository, deckId + "-empty");
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId + "-empty", []);
            await repository.AddDeckIdsAsync([deckId + "-never"]);
            await using (var reset = new NpgsqlCommand("UPDATE deck_queue SET has_stored_live_rows = 0; DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');", connection))
            {
                await reset.ExecuteNonQueryAsync();
            }

            var service = new StoredLiveRowsBackfillService(repository, new DeckPruningOptions(), _ => Task.FromResult<long?>(long.MaxValue));
            var result = await service.RunAsync();
            Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
            Assert.True(await repository.IsStoredLiveRowsMarkerReadyAsync());
            Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(*) FROM deck_queue q WHERE has_stored_live_rows <> CASE WHEN " + OracleExistsSql + " THEN 1 ELSE 0 END;"));
            await using (var resetCursor = new NpgsqlCommand("DELETE FROM crawl_state WHERE key = 'stored_live_rows_backfill_cursor';", connection))
            {
                await resetCursor.ExecuteNonQueryAsync();
            }

            var rerun = await repository.RunStoredLiveRowsBackfillBatchAsync(int.MaxValue);
            Assert.True(rerun.DecksRechecked > 0);
            Assert.Equal(0, rerun.MarkersChanged);
            Assert.True(rerun.Completed);
        }
        finally
        {
            await using var clear = new NpgsqlCommand("DELETE FROM crawl_state WHERE key IN ('stored_live_rows_backfill_complete', 'stored_live_rows_backfill_cursor');", connection);
            await clear.ExecuteNonQueryAsync();
            foreach (var (key, value) in saved)
            {
                await using var restore = new NpgsqlCommand("INSERT INTO crawl_state (key, value) VALUES (@key, @value);", connection);
                restore.Parameters.AddWithValue("key", key);
                restore.Parameters.AddWithValue("value", value);
                await restore.ExecuteNonQueryAsync();
            }
        }
    }

    [PostgresFact]
    public async Task RetentionAndPurge_Postgres_WaitForMarkerBackfill()
    {
        var repository = await CreateRepositoryAsync();
        var deckId = "marker-gate-" + Guid.NewGuid().ToString("N");
        await StoreAsync(repository, deckId);
        await StoreAsync(repository, deckId + "-second");
        await repository.MarkDeckProcessedAsync(deckId, "Atraxa", false,
            new ArchidektDeckMetadata(null, 2, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var read = new NpgsqlCommand("SELECT value FROM crawl_state WHERE key = 'stored_live_rows_backfill_complete';", connection);
        var saved = await read.ExecuteScalarAsync();
        try
        {
            await using var reset = new NpgsqlCommand("DELETE FROM crawl_state WHERE key = 'stored_live_rows_backfill_complete';", connection);
            await reset.ExecuteNonQueryAsync();
            var probes = 0;
            var service = new DeckPruningService(repository,
                new DeckPruningOptions { PerCommanderCap = 1, Pause = TimeSpan.Zero },
                _ => { probes++; return Task.FromResult<long?>(long.MaxValue); });

            Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.MarkerBackfillPending), await service.RunRetentionAsync());
            Assert.Equal(new DeckPruningResult(0, 0, DeckPruningStopReason.MarkerBackfillPending), await service.RunPurgeAsync());
            Assert.Equal(0, probes);
            Assert.Equal(2L, await ScalarAsync<long>("SELECT COUNT(*) FROM card_deck_totals t INNER JOIN sources s ON s.id = t.source_id INNER JOIN deck_queue q ON q.id = s.deck_queue_id WHERE q.deck_id IN (@deckId, @second);", ("deckId", deckId), ("second", deckId + "-second")));
        }
        finally
        {
            await using var restore = new NpgsqlCommand("DELETE FROM crawl_state WHERE key = 'stored_live_rows_backfill_complete';", connection);
            await restore.ExecuteNonQueryAsync();
            if (saved is not null and not DBNull)
            {
                await using var insert = new NpgsqlCommand("INSERT INTO crawl_state (key, value) VALUES ('stored_live_rows_backfill_complete', @value);", connection);
                insert.Parameters.AddWithValue("value", saved);
                await insert.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<CategoryKnowledgeRepository> CreateRepositoryAsync()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();
        return repository;
    }

    private static async Task StoreAsync(CategoryKnowledgeRepository repository, string deckId)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, Entries());
        await repository.MarkDeckProcessedAsync(deckId, "Atraxa", false, null);
    }

    private async Task AssertMarkerAsync(string deckId, long expected)
    {
        Assert.Equal(expected, await ScalarAsync<long>("SELECT has_stored_live_rows FROM deck_queue WHERE deck_id = @deckId;", ("deckId", deckId)));
        Assert.Equal(expected, await ScalarAsync<long>("SELECT CASE WHEN " + OracleExistsSql + " THEN 1 ELSE 0 END FROM deck_queue q WHERE q.deck_id = @deckId;", ("deckId", deckId)));
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static IReadOnlyList<DeckEntry> Entries() => [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }];

    private const string OracleExistsSql = "EXISTS (SELECT 1 FROM sources s INNER JOIN card_deck_totals t ON t.source_id = s.id WHERE s.deck_queue_id = q.id AND SUBSTR(s.source, 1, 15) = 'archidekt_live:')";
}
