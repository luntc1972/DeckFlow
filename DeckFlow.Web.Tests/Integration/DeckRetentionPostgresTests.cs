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

    public DeckRetentionPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_Postgres_RemovesBatchWithExactSummaries()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, "one", "A"); await SeedAsync(repository, "two", "A"); await SeedAsync(repository, "three", "A");
        var rows = await repository.GetRetentionCandidatesAsync("A");
        await repository.RemoveStoredDecksAsync(rows.Take(2).Select(row => (row.DeckQueueId, DeckSkipReasons.Retention)).ToArray());
        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("A"));
        await AssertRecomputedAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RunRetentionAsync_Postgres_OverCapCommander_RemovesOldest()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, "old", "A"); await SeedAsync(repository, "new-1", "A"); await SeedAsync(repository, "new-2", "A"); await SeedAsync(repository, "other", "B");
        var result = await new DeckPruningService(repository, new DeckPruningOptions { PerCommanderCap = 2 }, static _ => Task.FromResult<long?>(long.MaxValue)).RunRetentionAsync();
        Assert.Equal(1, result.DecksRemoved); Assert.Equal(2, await repository.GetCommanderDeckCountAsync("A")); Assert.Equal(1, await repository.GetCommanderDeckCountAsync("B"));
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_ConcurrentWithReharvest_BothComplete()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        for (var round = 0; round < 20; round++)
        {
            await SeedAsync(repository, $"remove-{round}", "A"); await SeedAsync(repository, $"rewrite-{round}", "B");
            var row = (await repository.GetRetentionCandidatesAsync("A"))[0];
            await Task.WhenAll(repository.RemoveStoredDecksAsync([(row.DeckQueueId, DeckSkipReasons.Retention)]), DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, $"archidekt_live:rewrite-{round}", [Entry(2)])).WaitAsync(TimeSpan.FromSeconds(30));
        }
        await AssertRecomputedAsync(database.ConnectionString);
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_Postgres_QueueMarkFails_RollsBackEverything()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, "first", "A"); await SeedAsync(repository, "second", "A");
        var before = await SnapshotAsync(database.ConnectionString);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        { await connection.OpenAsync(); await using var command = new NpgsqlCommand("CREATE FUNCTION retention_boom() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'boom'; END; $$; CREATE TRIGGER retention_boom BEFORE UPDATE ON deck_queue FOR EACH ROW EXECUTE FUNCTION retention_boom();", connection); await command.ExecuteNonQueryAsync(); }
        var rows = await repository.GetRetentionCandidatesAsync("A");
        await Assert.ThrowsAnyAsync<Exception>(() => repository.RemoveStoredDecksAsync(rows.Select(row => (row.DeckQueueId, DeckSkipReasons.Retention)).ToArray()));
        Assert.Equal(before, await SnapshotAsync(database.ConnectionString));
    }

    [PostgresFact]
    public async Task RemoveStoredDecksAsync_Postgres_RetentionRecordsCoreFingerprint()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = await RepositoryAsync(database.ConnectionString);
        await SeedAsync(repository, "fingerprint", "A");
        var row = (await repository.GetRetentionCandidatesAsync("A"))[0]; await repository.RemoveStoredDecksAsync([(row.DeckQueueId, DeckSkipReasons.Retention)]);
        Assert.False(string.IsNullOrWhiteSpace(await repository.GetRetentionCoreHashAsync("fingerprint")));
    }

    private static DeckEntry Entry(int quantity = 1) => new() { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = quantity, Board = "mainboard", Category = "Ramp" };
    private static async Task SeedAsync(CategoryKnowledgeRepository repository, string id, string commander) { await repository.AddDeckIdsAsync([id]); await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + id, [Entry()]); await repository.MarkDeckProcessedAsync(id, commander, false, null); }
    private static async Task<CategoryKnowledgeRepository> RepositoryAsync(string connectionString) { var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString)); await repository.EnsureSchemaAsync(); return repository; }
    private static async Task AssertRecomputedAsync(string connectionString) { await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); foreach (var table in new[] { "card_category_summary", "card_category_qualified" }) { await using var command = new NpgsqlCommand($"SELECT COUNT(1) FROM (SELECT card_id, category, COUNT(1) FROM card_category_observations GROUP BY card_id, category EXCEPT SELECT card_id, category, observation_rows FROM {table}) x;", connection); Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync())); } }
    private static async Task<string> SnapshotAsync(string connectionString) { await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await using var command = new NpgsqlCommand("SELECT (SELECT COUNT(1) FROM card_category_observations), (SELECT COUNT(1) FROM card_deck_totals), (SELECT COUNT(1) FROM card_category_summary), (SELECT COUNT(1) FROM card_category_qualified), (SELECT COUNT(1) FROM deck_queue WHERE skipped = 1);", connection); return Convert.ToString(await command.ExecuteScalarAsync())!; }
    private async Task<TemporaryDatabase> CreateDatabaseAsync() { var admin = await _fixture.GetConnectionStringOrSkipAsync(); var name = $"df_retention_{Guid.NewGuid():N}"; var quoted = $"\"{name}\""; await using (var connection = new NpgsqlConnection(admin)) { await connection.OpenAsync(); await using var command = new NpgsqlCommand($"CREATE DATABASE {quoted};", connection); await command.ExecuteNonQueryAsync(); } return new TemporaryDatabase(admin, new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString, quoted); }
    private sealed class TemporaryDatabase(string admin, string connectionString, string quoted) : IAsyncDisposable { public string ConnectionString { get; } = connectionString; public async ValueTask DisposeAsync() { NpgsqlConnection.ClearAllPools(); await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync(); await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quoted} WITH (FORCE);", connection); await command.ExecuteNonQueryAsync(); } }
}
