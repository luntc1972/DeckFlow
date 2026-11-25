using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class DeckQueuePendingIndexPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DeckQueuePendingIndexPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task PendingDeckQueries_Postgres_UsePendingIndex()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped)
            SELECT 'skipped-' || n, '2026-01-01', 0, 1 FROM generate_series(1, 20000) AS n;
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped)
            VALUES ('pending-1', '2026-01-02', 0, 0), ('pending-2', '2026-01-03', 0, 0);
            ANALYZE deck_queue;
            """, connection))
        {
            await seed.ExecuteNonQueryAsync();
        }

        await using (var setting = new NpgsqlCommand("SET enable_seqscan = off;", connection))
        {
            await setting.ExecuteNonQueryAsync();
        }

        var dequeuePlan = await ExplainAsync(connection, DeckQueueRepository.NextUnprocessedDeckIdsSql, "count", 2);
        var countPlan = await ExplainAsync(connection, DeckQueueRepository.UnprocessedCountSql);

        Assert.Contains(dequeuePlan, line => line.Contains("ix_deck_queue_pending", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(countPlan, line => line.Contains("ix_deck_queue_pending", StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task QueuedForRefetchCount_UsesIndex()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        await repository.EnsureSchemaAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, has_stored_live_rows)
            SELECT 'queued-refetch-skipped-' || n, '2026-01-01', 0, 1, 0 FROM generate_series(1, 20000) AS n;
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, has_stored_live_rows)
            VALUES ('queued-refetch-pending-1', '2026-01-02', 0, 0, 1), ('queued-refetch-pending-2', '2026-01-03', 0, 0, 1);
            ANALYZE deck_queue;
            """, connection))
        {
            await seed.ExecuteNonQueryAsync();
        }

        await using (var setting = new NpgsqlCommand("SET enable_seqscan = off;", connection))
        {
            await setting.ExecuteNonQueryAsync();
        }

        var plan = await ExplainAsync(connection, $"SELECT COUNT(1) FROM deck_queue q WHERE {DeckQueueRepository.QueuedForRefetchSql};");
        Assert.Contains(plan, line => line.Contains("ix_deck_queue_pending", StringComparison.OrdinalIgnoreCase)
            || line.Contains("ix_deck_queue_stored_live", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IReadOnlyList<string>> ExplainAsync(
        NpgsqlConnection connection,
        string sql,
        string? parameterName = null,
        int parameterValue = 0)
    {
        await using var command = new NpgsqlCommand($"EXPLAIN {sql}", connection);
        if (parameterName is not null)
        {
            command.Parameters.AddWithValue(parameterName, parameterValue);
        }

        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            plan.Add(reader.GetString(0));
        }

        return plan;
    }
}
