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
        await DeleteLiveRowsAsync(database.ConnectionString, "pruned");
        var service = new DeckPruningService(repository, new DeckPruningOptions { Pause = TimeSpan.Zero }, static _ => Task.FromResult<long?>(long.MaxValue));

        var result = await service.RunPurgeAsync();

        Assert.Equal(3, result.DecksRemoved);
        await AssertNoLiveRowsAsync(database.ConnectionString, "format", "commander", "skipped");
        Assert.Equal(1L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM card_deck_totals;"));
        Assert.Equal(1L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM card_category_summary;"));
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
    }

    [PostgresFact]
    public async Task MarkMetadataLessDecksForRefreshAsync_Postgres_MarksOnlyStoredDecks()
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

        var marked = await repository.MarkMetadataLessDecksForRefreshAsync();

        Assert.Equal(2, marked);
        Assert.Equal(["skipped", "stored"], (await repository.GetNextRefreshDeckIdsAsync(10)).Order());
        Assert.Equal(0L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM processed_commander_summary WHERE LOWER(commander_name) = 'delta';"));
        Assert.Equal(0, await repository.GetCommanderDeckCountAsync("Delta"));
        Assert.Equal(0L, await ScalarAsync<long>(database.ConnectionString, "SELECT COUNT(1) FROM deck_queue WHERE deck_id IN ('no-source', 'pruned', 'pending') AND refresh_requested_utc IS NOT NULL;"));
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
        Assert.Equal(1, await repository.MarkMetadataLessDecksForRefreshAsync());
        Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

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

    private static async Task<CategoryKnowledgeRepository> RepositoryAsync(string connectionString)
    {
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();
        return repository;
    }

    private static async Task DeleteLiveRowsAsync(string connectionString, string deckId)
    {
        const string sql = "DELETE FROM card_category_observations WHERE source_id IN (SELECT id FROM sources WHERE source = @source); DELETE FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE source = @source);";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("source", "archidekt_live:" + deckId);
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
