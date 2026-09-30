using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class CategoryWriterLockOrderPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public CategoryWriterLockOrderPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_SecondRepositoryCall_DoesNotIssueDdl()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var target = new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = $"catsum-schema-{Guid.NewGuid():N}" }.ConnectionString;
        await Repository(target).EnsureSchemaAsync();

        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        await using var transaction = await holder.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("LOCK TABLE cards IN ACCESS EXCLUSIVE MODE;", holder, transaction))
        {
            await command.ExecuteNonQueryAsync();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await Repository(target).EnsureSchemaAsync(timeout.Token);
            Assert.False(timeout.IsCancellationRequested, "Second ensure waited for blocked DDL.");
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [PostgresFact]
    public async Task ReplaceSourceRowsAsync_CardLockContention_DoesNotLockHigherCardFirst()
    {
        await AssertOrderedLockAcquisitionAsync(holdSummary: false);
    }

    [PostgresFact]
    public async Task ReplaceSourceRowsAsync_SummaryLockContention_DoesNotLockHigherSummaryFirst()
    {
        await AssertOrderedLockAcquisitionAsync(holdSummary: true);
    }

    [PostgresFact]
    public async Task PersistDeckCategoryBatchAsync_CardLockContention_DoesNotLockHigherCardFirst()
    {
        await AssertOrderedLockAcquisitionAsync(holdSummary: false, persistBatch: true);
    }

    [PostgresFact]
    public async Task ReplaceSourceRowsAsync_OpposingReplacements_CompleteWithExactSummary()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = Repository(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var first = $"catsum-a-{suffix}";
        var second = $"catsum-b-{suffix}";
        var sourceA = $"catsum-source-a-{suffix}";
        var sourceB = $"catsum-source-b-{suffix}";
        await repository.ReplaceSourceRowsAsync(sourceA, new[] { Row("Ramp", first) });
        await repository.ReplaceSourceRowsAsync(sourceB, new[] { Row("Tutor", second) });

        await Task.WhenAll(
            repository.ReplaceSourceRowsAsync(sourceA, new[] { Row("Tutor", second) }),
            repository.ReplaceSourceRowsAsync(sourceB, new[] { Row("Ramp", first) })).WaitAsync(TimeSpan.FromSeconds(15));

        await AssertSummaryExactAsync(connectionString, first, second);
    }

    [PostgresFact]
    public async Task DeleteSourceDataAsync_ObservationLockContention_DoesNotLockHigherObservationFirst()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var applicationName = $"catsum-delete-{Guid.NewGuid():N}";
        var writerConnectionString = new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = applicationName }.ConnectionString;
        var repository = Repository(writerConnectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var lowName = $"catsum-low-{suffix}";
        var highName = $"catsum-high-{suffix}";
        var source = $"catsum-delete-source-{suffix}";
        await repository.PersistObservedCategoriesAsync($"{source}-seed", lowName, new[] { "Ramp" });
        await repository.PersistObservedCategoriesAsync(source, highName, new[] { "Ramp" });
        await repository.PersistObservedCategoriesAsync(source, lowName, new[] { "Ramp" });

        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        var lowId = await CardIdAsync(holder, lowName);
        var highId = await CardIdAsync(holder, highName);
        Assert.True(lowId < highId);
        await using var heldTransaction = await holder.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "SELECT card_id FROM card_category_observations WHERE source_id = (SELECT id FROM sources WHERE source = @source) AND card_id = @id FOR UPDATE;",
            holder, heldTransaction))
        {
            hold.Parameters.AddWithValue("source", source);
            hold.Parameters.AddWithValue("id", lowId);
            Assert.Equal(lowId, Convert.ToInt64(await hold.ExecuteScalarAsync()));
        }

        var writer = repository.DeleteSourceDataAsync(source);
        try
        {
            await WaitForWriterLockAsync(connectionString, applicationName);
            await using var probe = new NpgsqlConnection(connectionString);
            await probe.OpenAsync();
            await using var probeTransaction = await probe.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(
                "SELECT card_id FROM card_category_observations WHERE source_id = (SELECT id FROM sources WHERE source = @source) AND card_id = @id FOR UPDATE NOWAIT;",
                probe, probeTransaction);
            command.Parameters.AddWithValue("source", source);
            command.Parameters.AddWithValue("id", highId);
            Assert.Equal(highId, Convert.ToInt64(await command.ExecuteScalarAsync()));
            await probeTransaction.RollbackAsync();
        }
        finally
        {
            await heldTransaction.RollbackAsync();
        }

        await writer.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [PostgresFact]
    public async Task ConcurrentWriters_WithSchemaEnsure_CompleteWithExactSummary()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var seed = Repository(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var first = $"catsum-stress-z-{suffix}";
        var second = $"catsum-stress-a-{suffix}";
        var sources = Enumerable.Range(0, 8).Select(index => $"catsum-stress-{index}-{suffix}").ToArray();
        foreach (var source in sources)
        {
            await seed.ReplaceSourceRowsAsync(source, new[] { Row("Ramp", first), Row("Ramp", second) });
        }

        var target = new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = $"catsum-stress-{suffix}" }.ConnectionString;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = sources.Select(async (source, index) =>
        {
            await start.Task;
            var repository = Repository(target);
            for (var round = 0; round < 3; round++)
            {
                switch ((index + round) % 4)
                {
                    case 0:
                        await repository.ReplaceSourceRowsAsync(source, new[] { Row("Ramp", second), Row("Ramp", first) });
                        break;
                    case 1:
                        await repository.PersistDeckCategoryBatchAsync(source,
                            new[] { (second, "Ramp", "mainboard", 1, 1), (first, "Ramp", "mainboard", 1, 1) },
                            Array.Empty<(string CardName, string Board)>());
                        break;
                    case 2:
                        await repository.PersistObservedCategoriesAsync(source, first, new[] { "Ramp" });
                        break;
                    default:
                        await repository.DeleteSourceDataAsync(source);
                        break;
                }
            }
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(45));
        await AssertSummaryExactAsync(connectionString, first, second);
    }

    private async Task AssertOrderedLockAcquisitionAsync(bool holdSummary, bool persistBatch = false)
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var applicationName = $"catsum-order-{Guid.NewGuid():N}";
        var writerConnectionString = new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = applicationName }.ConnectionString;
        var repository = Repository(writerConnectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var first = $"catsum-z-{suffix}";
        var second = $"catsum-a-{suffix}";
        // Every probed key exists in a committed transaction before the writer starts.
        await repository.PersistObservedCategoriesAsync($"catsum-seed-{suffix}", first, new[] { "Ramp" });
        await repository.PersistObservedCategoriesAsync($"catsum-seed-{suffix}", second, new[] { "Ramp" });
        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        var firstId = await CardIdAsync(holder, first);
        var secondId = await CardIdAsync(holder, second);
        var lowId = Math.Min(firstId, secondId);
        var highId = Math.Max(firstId, secondId);
        var lowName = firstId == lowId ? first : second;
        var highName = firstId == highId ? first : second;
        Assert.True(StringComparer.Ordinal.Compare(CardNormalizer.Normalize(lowName), CardNormalizer.Normalize(highName)) > 0);
        var heldId = holdSummary ? lowId : highId;
        var probeId = holdSummary ? highId : lowId;
        await using var heldTransaction = await holder.BeginTransactionAsync();
        var table = holdSummary ? "card_category_summary" : "cards";
        await using (var hold = new NpgsqlCommand($"SELECT card_id FROM {table} WHERE card_id = @id AND category = 'Ramp' FOR UPDATE;", holder, heldTransaction))
        {
            if (!holdSummary)
            {
                hold.CommandText = "SELECT id FROM cards WHERE id = @id FOR UPDATE;";
            }
            hold.Parameters.AddWithValue("id", heldId);
            Assert.Equal(heldId, Convert.ToInt64(await hold.ExecuteScalarAsync()));
        }

        var writerSource = $"catsum-writer-{suffix}";
        if (holdSummary)
        {
            await repository.ReplaceSourceRowsAsync(writerSource, new[] { Row("Ramp", highName) });
        }

        var writer = persistBatch
            ? repository.PersistDeckCategoryBatchAsync(writerSource,
                new[] { (lowName, "Ramp", "mainboard", 1, 1), (highName, "Ramp", "mainboard", 1, 1) },
                Array.Empty<(string CardName, string Board)>())
            : repository.ReplaceSourceRowsAsync(writerSource, holdSummary
                ? new[] { Row("Ramp", lowName) }
                : new[] { Row("Ramp", lowName), Row("Ramp", highName) });
        try
        {
            await WaitForWriterLockAsync(connectionString, applicationName);
            await using var probe = new NpgsqlConnection(connectionString);
            await probe.OpenAsync();
            await using var probeTransaction = await probe.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(
                holdSummary
                    ? "SELECT card_id FROM card_category_summary WHERE card_id = @id AND category = 'Ramp' FOR UPDATE NOWAIT;"
                    : "SELECT id FROM cards WHERE id = @id FOR UPDATE NOWAIT;", probe, probeTransaction);
            command.Parameters.AddWithValue("id", probeId);
            Assert.Equal(probeId, Convert.ToInt64(await command.ExecuteScalarAsync()));
            await probeTransaction.RollbackAsync();
        }
        finally
        {
            await heldTransaction.RollbackAsync();
        }

        await writer.WaitAsync(TimeSpan.FromSeconds(15));
        await AssertSummaryExactAsync(connectionString, first, second);
    }

    private static async Task WaitForWriterLockAsync(string connectionString, string applicationName)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var command = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_stat_activity WHERE application_name = @name AND wait_event_type = 'Lock';", observer);
            command.Parameters.AddWithValue("name", applicationName);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0)
            {
                return;
            }

            await Task.Delay(20, timeout.Token);
        }
    }

    private static async Task<long> CardIdAsync(NpgsqlConnection connection, string name)
    {
        await using var command = new NpgsqlCommand("SELECT id FROM cards WHERE normalized_card_name = @name;", connection);
        command.Parameters.AddWithValue("name", CardNormalizer.Normalize(name));
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task AssertSummaryExactAsync(string connectionString, string first, string second)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            WITH selected AS (SELECT id FROM cards WHERE normalized_card_name IN (@first, @second)),
            expected AS (SELECT card_id, category, COUNT(*) AS observation_rows FROM card_category_observations
                         WHERE card_id IN (SELECT id FROM selected) GROUP BY card_id, category),
            actual AS (SELECT card_id, category, observation_rows FROM card_category_summary
                       WHERE card_id IN (SELECT id FROM selected))
            SELECT COUNT(*) FROM ((SELECT * FROM expected EXCEPT SELECT * FROM actual)
                                 UNION ALL (SELECT * FROM actual EXCEPT SELECT * FROM expected)) differences;
            """, connection);
        command.Parameters.AddWithValue("first", CardNormalizer.Normalize(first));
        command.Parameters.AddWithValue("second", CardNormalizer.Normalize(second));
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private static CategoryKnowledgeRepository Repository(string connectionString) =>
        new(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));

    private static CategoryKnowledgeRow Row(string category, string cardName) => new(category, cardName, 1, 1);
}
