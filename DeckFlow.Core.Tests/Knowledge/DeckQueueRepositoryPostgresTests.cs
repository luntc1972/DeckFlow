using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// PostgreSQL integration coverage for persisted transient deck-failure handling.
/// </summary>
public sealed class DeckQueueRepositoryPostgresTests
{
    [CreatorSuppressionPostgresFact]
    public async Task RecordTransientDeckFailureAsync_ThirdFailureSkipsDeck()
    {
        var schema = $"deck_queue_test_{Guid.NewGuid():N}";
        var connectionString = Environment.GetEnvironmentVariable("DECKFLOW_TEST_POSTGRES")!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA {schema};", connection))
        {
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            var testConnectionString = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            var repository = new CategoryKnowledgeRepository(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, testConnectionString));
            await repository.EnsureSchemaAsync();
            await repository.AddDeckIdsAsync(["transient"]);

            Assert.False(await repository.RecordTransientDeckFailureAsync("transient"));
            Assert.False(await repository.RecordTransientDeckFailureAsync("transient"));
            Assert.True(await repository.RecordTransientDeckFailureAsync("transient"));
            Assert.Empty(await repository.GetNextUnprocessedDeckIdsAsync(1));
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE;", connection);
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
