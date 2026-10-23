using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>Specifies transactional Postgres retention removal.</summary>
public sealed class DeckRetentionPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DeckRetentionPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_Postgres_RemovesBatchWithExactSummaries()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, database.ConnectionString, "one", "A", "2026-01-01T00:00:00Z");
        await SeedAsync(repository, database.ConnectionString, "two", "A", "2026-01-02T00:00:00Z");
        await SeedAsync(repository, database.ConnectionString, "three", "A", "2026-01-03T00:00:00Z");

        var rows = await repository.GetRetentionCandidatesAsync("A");
        await repository.RemoveStoredDecksAsync(rows.Take(2).Select(row => (row.DeckQueueId, DeckSkipReasons.Retention)).ToArray());

        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("A"));
        await AssertSkippedForRetentionAsync(database.ConnectionString, "one", "two");
        await AssertSourceRowsRemovedAsync(database.ConnectionString, "one", "two");
        await AssertRecomputedAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RunRetentionAsync_Postgres_OverCapCommander_RemovesOldest()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, database.ConnectionString, "old", "A", "2026-01-01T00:00:00Z");
        await SeedAsync(repository, database.ConnectionString, "new-1", "A", "2026-01-02T00:00:00Z");
        await SeedAsync(repository, database.ConnectionString, "new-2", "A", "2026-01-03T00:00:00Z");
        await SeedAsync(repository, database.ConnectionString, "other", "B", "2026-01-01T00:00:00Z");
        var service = new DeckPruningService(repository, new DeckPruningOptions { PerCommanderCap = 2 }, static _ => Task.FromResult<long?>(long.MaxValue));

        var result = await service.RunRetentionAsync();

        Assert.Equal(1, result.DecksRemoved);
        Assert.Equal(2, await repository.GetCommanderDeckCountAsync("A"));
        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("B"));
        await AssertSkippedForRetentionAsync(database.ConnectionString, "old");
        await AssertNotSkippedAsync(database.ConnectionString, "new-1", "new-2", "other");
        await AssertSourceRowsRemovedAsync(database.ConnectionString, "old");
        await AssertRecomputedAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_ConcurrentWithReharvest_BothComplete()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);

        for (var round = 0; round < 20; round++)
        {
            var removedId = $"remove-{round}";
            var rewrittenId = $"rewrite-{round}";
            await SeedAsync(repository, database.ConnectionString, removedId, "A", DateTimeOffset.UtcNow.AddMinutes(round).ToString("O"));
            await SeedAsync(repository, database.ConnectionString, rewrittenId, "B", DateTimeOffset.UtcNow.AddMinutes(round).ToString("O"));
            var row = (await repository.GetRetentionCandidatesAsync("A")).Single(candidate => candidate.DeckId == removedId);
            var removal = repository.RemoveStoredDecksAsync([(row.DeckQueueId, DeckSkipReasons.Retention)]);
            var reharvest = DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + rewrittenId, [Entry("Sol Ring", "Ramp", 2)]);

            await Task.WhenAll(removal, reharvest).WaitAsync(TimeSpan.FromSeconds(30));
        }

        await AssertRecomputedAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_Postgres_QueueMarkFails_RollsBackEverything()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, database.ConnectionString, "first", "A", "2026-01-01T00:00:00Z");
        await SeedAsync(repository, database.ConnectionString, "second", "A", "2026-01-02T00:00:00Z");
        var before = await SnapshotAsync(database.ConnectionString);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            const string sql = "CREATE FUNCTION retention_boom() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'boom'; END; $$; CREATE TRIGGER retention_boom BEFORE UPDATE ON deck_queue FOR EACH ROW EXECUTE FUNCTION retention_boom();";
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        var rows = await repository.GetRetentionCandidatesAsync("A");
        await Assert.ThrowsAnyAsync<Exception>(() => repository.RemoveStoredDecksAsync(rows.Select(row => (row.DeckQueueId, DeckSkipReasons.Retention)).ToArray()));
        Assert.Equal(before, await SnapshotAsync(database.ConnectionString));
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_Postgres_RetentionRecordsCoreFingerprint()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        var entries = new[]
        {
            Entry("Commander", null, 1, "commander"),
            Entry("Sol Ring", "Ramp", 1),
            Entry("Sol Ring", "Engine", 1),
            Entry("Untyped", null, 1),
            Entry("Side", "Ramp", 1, "sideboard"),
            Entry("Maybe", "Ramp", 1, "maybeboard")
        };
        await SeedAsync(repository, database.ConnectionString, "fingerprint", "A", "2026-01-01T00:00:00Z", entries);

        var row = (await repository.GetRetentionCandidatesAsync("A")).Single();
        await repository.RemoveStoredDecksAsync([(row.DeckQueueId, DeckSkipReasons.Retention)]);

        var retainedHash = await repository.GetRetentionCoreHashAsync("fingerprint");
        Assert.Equal(DeckCategoryCacheWriter.ComputeCoreFingerprint(entries), retainedHash);
        Assert.Equal(retainedHash, DeckCategoryCacheWriter.ComputeCoreFingerprint(entries.Select(entry => entry.Name == "Untyped" ? entry with { Quantity = 2 } : entry)));

        await SeedAsync(repository, database.ConnectionString, "no-commander", null, "2026-01-02T00:00:00Z", [Entry("Arcane Signet", "Ramp", 1)]);
        await repository.RemoveStoredDecksAsync([(await QueueIdAsync(database.ConnectionString, "no-commander"), DeckSkipReasons.NoCommander)]);
        Assert.Null(await repository.GetRetentionCoreHashAsync("no-commander"));
    }

    private static DeckEntry Entry(string name, string? category, int quantity, string board = "mainboard")
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = CardNormalizer.Normalize(name),
            Quantity = quantity,
            Board = board,
            Category = category
        };
    }

    private static async Task SeedAsync(CategoryKnowledgeRepository repository, string connectionString, string id, string? commander, string updatedUtc, IReadOnlyList<DeckEntry>? entries = null)
    {
        await repository.AddDeckIdsAsync([id]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + id, entries ?? [Entry("Sol Ring", "Ramp", 1)]);
        await repository.MarkDeckProcessedAsync(id, commander, false, null);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE deck_queue SET archidekt_updated_utc = @updatedUtc WHERE deck_id = @id;", connection);
        command.Parameters.AddWithValue("updatedUtc", DateTimeOffset.Parse(updatedUtc).UtcDateTime);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<CategoryKnowledgeRepository> RepositoryAsync(string connectionString)
    {
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();
        return repository;
    }

    private static async Task AssertRecomputedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        // Why: card_category_qualified only holds groups at or above the observation-row threshold.
        foreach (var (table, having) in new[] { ("card_category_summary", string.Empty), ("card_category_qualified", $" HAVING COUNT(1) >= {CategoryCacheSchema.DefaultMinObservationRows}") })
        {
            var sql = $"SELECT COUNT(1) FROM ((SELECT card_id, category, COUNT(1) FROM card_category_observations GROUP BY card_id, category{having} EXCEPT SELECT card_id, category, observation_rows FROM {table}) UNION ALL (SELECT card_id, category, observation_rows FROM {table} EXCEPT SELECT card_id, category, COUNT(1) FROM card_category_observations GROUP BY card_id, category{having})) x;";
            await using var command = new NpgsqlCommand(sql, connection);
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
    }

    private static async Task AssertSourceRowsRemovedAsync(string connectionString, params string[] deckIds)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var deckId in deckIds)
        {
            const string sql = "SELECT (SELECT COUNT(1) FROM card_category_observations o JOIN sources s ON s.id = o.source_id WHERE s.source = @sourceId), (SELECT COUNT(1) FROM card_deck_totals t JOIN sources s ON s.id = t.source_id WHERE s.source = @sourceId), (SELECT COUNT(1) FROM sources WHERE source = @sourceId);";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("sourceId", "archidekt_live:" + deckId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(0L, reader.GetInt64(0));
            Assert.Equal(0L, reader.GetInt64(1));
            Assert.Equal(1L, reader.GetInt64(2));
        }
    }

    private static async Task AssertSkippedForRetentionAsync(string connectionString, params string[] deckIds)
    {
        await AssertQueueStateAsync(connectionString, DeckSkipReasons.Retention, true, deckIds);
    }

    private static async Task AssertNotSkippedAsync(string connectionString, params string[] deckIds)
    {
        await AssertQueueStateAsync(connectionString, null, false, deckIds);
    }

    private static async Task AssertQueueStateAsync(string connectionString, string? reason, bool skipped, IEnumerable<string> deckIds)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var deckId in deckIds)
        {
            await using var command = new NpgsqlCommand("SELECT skipped, skip_reason FROM deck_queue WHERE deck_id = @id;", connection);
            command.Parameters.AddWithValue("id", deckId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(skipped ? 1 : 0, reader.GetInt32(0));
            Assert.Equal(reason, reader.IsDBNull(1) ? null : reader.GetString(1));
        }
    }

    private static async Task<long> QueueIdAsync(string connectionString, string deckId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM deck_queue WHERE deck_id = @id;", connection);
        command.Parameters.AddWithValue("id", deckId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> SnapshotAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = "SELECT jsonb_build_object('deck_queue', (SELECT COALESCE(jsonb_agg(to_jsonb(q) ORDER BY q.id), '[]'::jsonb) FROM deck_queue q), 'observations', (SELECT COALESCE(jsonb_agg(to_jsonb(o) ORDER BY o.source_id, o.card_id, o.category), '[]'::jsonb) FROM card_category_observations o), 'totals', (SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY t.source_id, t.card_id, t.board), '[]'::jsonb) FROM card_deck_totals t), 'summary', (SELECT COALESCE(jsonb_agg(to_jsonb(s) ORDER BY s.card_id, s.category), '[]'::jsonb) FROM card_category_summary s), 'qualified', (SELECT COALESCE(jsonb_agg(to_jsonb(q) ORDER BY q.card_id, q.category), '[]'::jsonb) FROM card_category_qualified q), 'commanders', (SELECT COALESCE(jsonb_agg(to_jsonb(p) ORDER BY p.commander_name), '[]'::jsonb) FROM processed_commander_summary p))::text;";
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync())!;
    }

    private async Task<TemporaryDatabase> CreateDatabaseAsync()
    {
        var admin = await _fixture.GetConnectionStringOrSkipAsync();
        var name = $"df_retention_{Guid.NewGuid():N}";
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
