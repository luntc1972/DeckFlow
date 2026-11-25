using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Specifies Postgres purge and metadata-less refresh operations.
/// </summary>
public sealed class DeckPurgeRefetchPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DeckPurgeRefetchPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RunPurgeAsync_Postgres_RemovesIllegalWithExactSummaries()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "format", "Alpha", 7);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "commander", null, 3);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "legal", "Gamma", 3);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "skipped", "Alpha", 7, skipped: true);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "pruned", "Alpha", 3, skipped: true);
        await repository.DeleteSourceDataAsync("archidekt_live:pruned");
        var service = new DeckPruningService(repository, new DeckPruningOptions { Pause = TimeSpan.Zero }, static _ => Task.FromResult<long?>(long.MaxValue));

        var result = await service.RunPurgeAsync();

        Assert.Equal(3, result.DecksRemoved);
        await AssertNoLiveRowsAsync(database.ConnectionString, "format", "commander", "skipped");
        Assert.Equal(1L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM card_deck_totals;"));
        Assert.Equal(1L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM card_category_summary;"));
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
    }

    [PostgresFact]
    public async Task RunRefetchMarkBatchAsync_Postgres_MarksOnlyStoredDecks()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "stored", "Delta", metadata: null);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "skipped", "Delta", metadata: null, skipped: true);
        await repository.AddDeckIdsAsync(["no-source"]);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "pruned", "Delta", metadata: null, skipped: true);
        await DeleteLiveRowsAsync(database.ConnectionString, "pruned");
        await SeedStoredDeckAsync(repository, database.ConnectionString, "pending", "Delta", metadata: null, processed: false);

        Assert.Equal(1L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM processed_commander_summary WHERE LOWER(commander_name) = 'delta';"));

        var marked = await MarkAllRefetchCandidatesAsync(repository);

        Assert.Equal(2, marked);
        Assert.Equal(["skipped", "stored"], (await repository.GetNextRefreshDeckIdsAsync(10)).Order());
        Assert.Equal(0L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM processed_commander_summary WHERE LOWER(commander_name) = 'delta';"));
        Assert.Equal(0, await repository.GetCommanderDeckCountAsync("Delta"));
        Assert.Equal(0L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM deck_queue WHERE deck_id IN ('no-source', 'pruned', 'pending') AND refresh_requested_utc IS NOT NULL;"));
    }

    [PostgresFact]
    public async Task RunRefetchMarkBatchAsync_Postgres_RechecksCandidateAfterConcurrentMetadataCapture()
    {
        await using var database = await CreateDatabaseAsync();
        const string deckId = "candidate";
        var applicationName = "refetch-recheck-" + Guid.NewGuid().ToString("N");
        var repositoryConnectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString) { ApplicationName = applicationName }.ConnectionString;
        var repository = await RepositoryAsync(repositoryConnectionString);
        await SeedStoredDeckAsync(repository, database.ConnectionString, deckId, "Delta", metadata: null);

        await using var holder = new NpgsqlConnection(database.ConnectionString);
        await holder.OpenAsync();
        await using var heldTransaction = await holder.BeginTransactionAsync();
        await using (var captureMetadata = new NpgsqlCommand("UPDATE deck_queue SET archidekt_metadata_captured_utc = now() WHERE deck_id = @deckId;", holder, heldTransaction))
        {
            captureMetadata.Parameters.AddWithValue("deckId", deckId);
            Assert.Equal(1, await captureMetadata.ExecuteNonQueryAsync());
        }

        var batchTask = repository.RunRefetchMarkBatchAsync(1);
        try
        {
            await WaitForRefetchUpdateLockAsync(database.ConnectionString, applicationName);
            await heldTransaction.CommitAsync();
        }
        catch
        {
            await heldTransaction.RollbackAsync();
            throw;
        }

        var batch = await batchTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, batch.DecksMarked);
        Assert.Equal(1L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM deck_queue WHERE deck_id = 'candidate' AND processed = 1 AND refresh_requested_utc IS NULL;"));
    }

    [PostgresFact]
    public async Task CountsAsync_Postgres_MatchOperations()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "illegal", "Alpha", 7);
        await SeedStoredDeckAsync(repository, database.ConnectionString, "missing", "Delta", metadata: null);
        var service = new DeckPruningService(repository, new DeckPruningOptions { Pause = TimeSpan.Zero }, static _ => Task.FromResult<long?>(long.MaxValue));

        Assert.Equal(1, await repository.CountPurgeCandidatesAsync());
        Assert.Equal(1, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(1, (await service.RunPurgeAsync()).DecksRemoved);
        Assert.Equal(1, await MarkAllRefetchCandidatesAsync(repository));
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [PostgresFact]
    public async Task CountQueuedForRefetchAsync_Postgres_MatchesSqliteSeed()
    {
        await using var database = await CreateDatabaseAsync();
        var connectionString = database.ConnectionString;
        var repository = await RepositoryAsync(connectionString);
        await SeedStoredDeckAsync(repository, connectionString, "marked-one", "Alpha", metadata: null);
        await SeedStoredDeckAsync(repository, connectionString, "marked-two", "Alpha", metadata: null);
        Assert.Equal(2, await MarkAllRefetchCandidatesAsync(repository));

        await SeedStoredDeckAsync(repository, connectionString, "processed-metadata-less", "Alpha", metadata: null);
        await SeedStoredDeckAsync(repository, connectionString, "pending-with-metadata", "Alpha", format: 3);
        await ExecuteAsync(connectionString, "UPDATE deck_queue SET processed = 0 WHERE deck_id = @id;", "pending-with-metadata");
        await repository.AddDeckIdsAsync(["freshly-queued"]);
        await SeedStoredDeckAsync(repository, connectionString, "skipped-metadata-less", "Alpha", metadata: null);
        await ExecuteAsync(connectionString, "UPDATE deck_queue SET processed = 0, skipped = 1, skip_reason = 'fetch_failure' WHERE deck_id = @id;", "skipped-metadata-less");

        var queued = await repository.CountQueuedForRefetchAsync();
        Assert.Equal(2, queued);
        Assert.Equal(queued, await ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM deck_queue WHERE processed = 0 AND skipped = 0 AND archidekt_metadata_captured_utc IS NULL AND has_stored_live_rows = 1;"));
        Assert.Equal(2, await repository.CountRefetchCandidatesAsync());
        var skippedDeckQueuedSql = $"SELECT COUNT(*) FROM deck_queue q WHERE q.deck_id = @id AND {DeckQueueRepository.QueuedForRefetchSql};";
        Assert.Equal(0, await ScalarAsync<int>(connectionString, skippedDeckQueuedSql, "id", "skipped-metadata-less"));
        Assert.Equal(2, await MarkAllRefetchCandidatesAsync(repository));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, skippedDeckQueuedSql, "id", "skipped-metadata-less"));
    }

    [PostgresFact]
    public async Task RefetchMarkService_Postgres_BatchesCommitIndependently()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        for (var index = 0; index < 5; index++)
        {
            await SeedStoredDeckAsync(repository, database.ConnectionString, $"deck-{index}", "Alpha", metadata: null);
        }

        var result = await RefetchService(repository, batchDecks: 2).RunAsync(null, null, null);

        Assert.Equal(new DeckPruningResult(5, 3, DeckPruningStopReason.Completed), result);
        Assert.Equal(0, await repository.GetCommanderDeckCountAsync("Alpha"));
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [PostgresFact]
    public async Task RunRefetchMarkBatchAsync_Postgres_RenameDuringUpdate_RefreshesSelectedCommander()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await SeedRenameScenarioAsync(database.ConnectionString, summariesAlreadyRenamed: false);

        await ExecuteSqlAsync(database.ConnectionString, "CREATE FUNCTION rename_refetch() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.deck_id = 'candidate' AND NEW.refresh_requested_utc IS NOT NULL THEN NEW.commander_name := 'B'; END IF; RETURN NEW; END; $$; CREATE TRIGGER rename_refetch BEFORE UPDATE ON deck_queue FOR EACH ROW EXECUTE FUNCTION rename_refetch();");
        var batch = await repository.RunRefetchMarkBatchAsync(1);

        Assert.Equal(new RefetchMarkBatch(1, 1, false), batch);
        await AssertRenameSummariesAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RunRefetchMarkBatchAsync_Postgres_RenameCommittedBeforeUpdate_RefreshesReturnedCommander()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await SeedRenameScenarioAsync(database.ConnectionString, summariesAlreadyRenamed: true);

        await ExecuteSqlAsync(database.ConnectionString, "CREATE FUNCTION rename_refetch() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.deck_id = 'candidate' AND NEW.refresh_requested_utc IS NOT NULL THEN NEW.commander_name := 'B'; END IF; RETURN NEW; END; $$; CREATE TRIGGER rename_refetch BEFORE UPDATE ON deck_queue FOR EACH ROW EXECUTE FUNCTION rename_refetch();");
        var batch = await repository.RunRefetchMarkBatchAsync(1);

        Assert.Equal(new RefetchMarkBatch(1, 1, false), batch);
        await AssertRenameSummariesAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RefetchMarkService_Postgres_FailureMidRun_KeepsEarlierBatches()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        for (var index = 0; index < 5; index++)
        {
            await SeedStoredDeckAsync(repository, database.ConnectionString, $"deck-{index}", "Alpha", metadata: null);
        }

        await ExecuteSqlAsync(database.ConnectionString, "CREATE FUNCTION poison_refetch() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.deck_id = 'deck-2' AND NEW.refresh_requested_utc IS NOT NULL THEN RAISE EXCEPTION 'poison'; END IF; RETURN NEW; END; $$; CREATE TRIGGER poison_refetch BEFORE UPDATE ON deck_queue FOR EACH ROW EXECUTE FUNCTION poison_refetch();");

        await Assert.ThrowsAsync<PostgresException>(() => RefetchService(repository, batchDecks: 2).RunAsync(null, null, null));
        Assert.Equal(3, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(3, await repository.GetCommanderDeckCountAsync("Alpha"));

        await ExecuteSqlAsync(database.ConnectionString, "DROP TRIGGER poison_refetch ON deck_queue; DROP FUNCTION poison_refetch();");
        Assert.Equal(3, (await RefetchService(repository, batchDecks: 2).RunAsync(null, null, null)).DecksRemoved);
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [PostgresFact]
    public async Task RefetchMarkService_Postgres_StopBetweenBatches()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        for (var index = 0; index < 5; index++)
        {
            await SeedStoredDeckAsync(repository, database.ConnectionString, $"deck-{index}", "Alpha", metadata: null);
        }

        var calls = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(() => RefetchService(repository, batchDecks: 2).RunAsync(null, null, () => ++calls > 1));

        Assert.Equal(3, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(3, await repository.GetCommanderDeckCountAsync("Alpha"));
    }

    private static RefetchMarkService RefetchService(CategoryKnowledgeRepository repository, int batchDecks)
        => new(repository, new DeckPruningOptions { Pause = TimeSpan.Zero }, static _ => Task.FromResult<long?>(long.MaxValue), static (_, _) => Task.CompletedTask, batchDecks);

    private static async Task<int> MarkAllRefetchCandidatesAsync(CategoryKnowledgeRepository repository)
    {
        var marked = 0;
        while (true)
        {
            var batch = await repository.RunRefetchMarkBatchAsync(500);
            marked += batch.DecksMarked;
            if (batch.Completed) return marked;
        }
    }

    private static async Task WaitForRefetchUpdateLockAsync(string connectionString, string applicationName)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM pg_stat_activity WHERE application_name = @applicationName AND wait_event_type = 'Lock' AND query LIKE 'UPDATE deck_queue AS q SET processed = 0%';", observer);
            command.Parameters.AddWithValue("applicationName", applicationName);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0)
            {
                return;
            }

            await Task.Delay(20, timeout.Token);
        }
    }

    private static async Task<CategoryKnowledgeRepository> SeedRenameScenarioAsync(string connectionString, bool summariesAlreadyRenamed)
    {
        var repository = await RepositoryAsync(connectionString);
        await SeedStoredDeckAsync(repository, connectionString, "candidate", "A", metadata: null);
        await SeedStoredDeckAsync(repository, connectionString, "a-live", "A", metadata: Metadata());
        await SeedStoredDeckAsync(repository, connectionString, "b-live", "B", metadata: Metadata());
        if (summariesAlreadyRenamed)
        {
            await ExecuteSqlAsync(connectionString, "UPDATE processed_commander_summary SET deck_count = CASE LOWER(commander_name) WHEN 'a' THEN 1 WHEN 'b' THEN 2 END WHERE LOWER(commander_name) IN ('a', 'b');");
        }

        return repository;
    }

    private static async Task AssertRenameSummariesAsync(string connectionString)
    {
        Assert.Equal(1L, await ScalarAsync<long>(connectionString, "SELECT deck_count FROM processed_commander_summary WHERE LOWER(commander_name) = 'a';"));
        Assert.Equal(1L, await ScalarAsync<long>(connectionString, "SELECT deck_count FROM processed_commander_summary WHERE LOWER(commander_name) = 'b';"));
    }

    private static ArchidektDeckMetadata Metadata()
        => new(null, 3, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string connectionString, string deckId, string? commander, int? format = null, ArchidektDeckMetadata? metadata = null, bool processed = true, bool skipped = false)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, skipped, metadata ?? (format.HasValue ? new ArchidektDeckMetadata(null, format, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) : null));
        if (!processed)
        {
            await ExecuteAsync(connectionString, "UPDATE deck_queue SET processed = 0, skipped = 0, skip_reason = NULL WHERE deck_id = @id;", deckId);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql, string deckId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", deckId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<CategoryKnowledgeRepository> RepositoryAsync(string connectionString)
    {
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();
        return repository;
    }

    private static async Task DeleteLiveRowsAsync(string connectionString, string deckId)
    {
        // Why: raw deletes bypass the writers that maintain the marker.
        const string sql = "DELETE FROM card_category_observations WHERE source_id IN (SELECT id FROM sources WHERE source = @source); DELETE FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE source = @source); UPDATE deck_queue SET has_stored_live_rows = 0 WHERE deck_id = @deckId;";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("source", "archidekt_live:" + deckId);
        command.Parameters.AddWithValue("deckId", deckId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertNoLiveRowsAsync(string connectionString, params string[] deckIds)
    {
        foreach (var deckId in deckIds)
        {
            Assert.Equal(0L, await ScalarAsync<long>(connectionString, "SELECT COUNT(1) FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE source = @source);", "source", "archidekt_live:" + deckId));
        }
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, string? parameterName = null, object? parameterValue = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (parameterName is not null)
        {
            command.Parameters.AddWithValue(parameterName, parameterValue!);
        }

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null || scalar is DBNull)
        {
            throw new InvalidOperationException("Expected a scalar database value.");
        }

        return (T)Convert.ChangeType(scalar, typeof(T))!;
    }

    private async Task<TemporaryDatabase> CreateDatabaseAsync()
    {
        var admin = await _fixture.GetConnectionStringOrSkipAsync();
        var name = $"df_purge_{Guid.NewGuid():N}";
        var quoted = $"\"{name}\"";
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {quoted};", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new TemporaryDatabase(admin, new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString, quoted);
    }

    private sealed class TemporaryDatabase(string admin, string connectionString, string quoted) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quoted} WITH (FORCE);", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
