using System.Text.RegularExpressions;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>Proves a failed Postgres replacement cannot expose a partially harvested deck.</summary>
public sealed class AtomicReharvestPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AtomicReharvestPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task ReplaceDeckCategoryBatchAsync_ConstraintViolationMidway_LeavesPriorStateUnchanged()
    {
        var adminConnectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await using var database = await CreateDatabaseAsync(adminConnectionString);
        var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, database.ConnectionString));
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "atomic" });
        await repository.ReplaceDeckCategoryBatchAsync("atomic", Rows(("A", "Ramp")), Totals("A"), new DeckContentHashWrite("atomic", "h1"));
        var before = await SnapshotAsync(database.ConnectionString);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("ALTER TABLE card_category_observations ADD CONSTRAINT ck_atomic_boom CHECK (card_name <> 'Boom');", connection);
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<PostgresException>(() => repository.ReplaceDeckCategoryBatchAsync("atomic", Rows(("Boom", "Ramp")), Totals("Boom"), new DeckContentHashWrite("atomic", "h2")));

        Assert.Equal(before, await SnapshotAsync(database.ConnectionString));
    }

    private static (string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)[] Rows(params (string CardName, string Category)[] rows) => rows.Select(row => (row.CardName, row.Category, "mainboard", 1, 1)).ToArray();
    private static (string CardName, string Board)[] Totals(params string[] cards) => cards.Select(card => (card, "mainboard")).ToArray();

    private static async Task<string> SnapshotAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = "SELECT (SELECT COUNT(1) FROM card_category_observations), (SELECT COUNT(1) FROM card_deck_totals), (SELECT COUNT(1) FROM card_category_summary), (SELECT COUNT(1) FROM card_category_qualified), (SELECT content_hash FROM deck_queue WHERE deck_id = 'atomic');";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return string.Join("|", reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4));
    }

    private static async Task<TemporaryDatabase> CreateDatabaseAsync(string adminConnectionString)
    {
        var name = $"df_atomic_{Guid.NewGuid():N}";
        Assert.Matches("^[a-z0-9_]{1,63}$", name);
        var quotedName = $"\"{name}\"";
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {quotedName};", admin);
            await command.ExecuteNonQueryAsync();
        }

        var target = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name, Pooling = false };
        return new TemporaryDatabase(adminConnectionString, target.ConnectionString, quotedName);
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _quotedName;

        public TemporaryDatabase(string adminConnectionString, string connectionString, string quotedName)
        {
            _adminConnectionString = adminConnectionString;
            ConnectionString = connectionString;
            _quotedName = quotedName;
        }

        public string ConnectionString { get; }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_quotedName} WITH (FORCE);", admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}
