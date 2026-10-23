using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class RetentionExcessCountPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public RetentionExcessCountPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task CountRetentionExcessAsync_Postgres_EqualsRetentionRemovals()
    {
        var admin = await _fixture.GetConnectionStringOrSkipAsync();
        var name = $"df_excess_{Guid.NewGuid():N}";
        var quoted = $"\"{name}\"";
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {quoted};", connection);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString;
            var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
            await repository.EnsureSchemaAsync();
            var count = await repository.CountRetentionExcessAsync(2);
            var service = new DeckPruningService(repository, new DeckPruningOptions { Pause = TimeSpan.Zero }, _ => Task.FromResult<long?>(long.MaxValue));

            Assert.Equal(count, (await service.RunRetentionAsync()).DecksRemoved);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quoted} WITH (FORCE);", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
