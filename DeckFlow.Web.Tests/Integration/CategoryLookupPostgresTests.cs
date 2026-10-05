using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

public sealed class CategoryLookupPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public CategoryLookupPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task GetCategoriesForNamesAsync_Postgres_MatchesGroupByReference()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        var suffix = Guid.NewGuid().ToString("N");
        var first = $"pg-summary-first-{suffix}";
        var second = $"pg-summary-second-{suffix}";
        var noObservations = $"pg-summary-empty-{suffix}";
        var absent = $"pg-summary-absent-{suffix}";
        var minObservationRows = GetCardCategoryRepository(repository).MinObservationRows;
        var shareDenominator = GetCardCategoryRepository(repository).ObservationShareDenominator;
        Assert.Equal(5, minObservationRows);
        Assert.Equal(2000, shareDenominator);
        var sources = await SeedThresholdObservationsAsync(repository, first, new[] { "Ramp", "ramp", "Card Draw" }, suffix);

        await repository.PersistObservedCategoriesAsync($"summary-rare-{suffix}", first, new[] { "Tutor" });
        await SeedThresholdObservationsAsync(repository, second, new[] { "Removal", "Interaction" }, suffix, count: minObservationRows - 1);
        await repository.PersistCardDeckTotalsAsync(sources[0], noObservations);

        var names = new[] { first, second, noObservations, absent };
        var actual = await repository.GetCategoriesForNamesAsync(names);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT normalized_card_name, category FROM (SELECT counts.normalized_card_name, counts.category, counts.observation_rows, MAX(counts.observation_rows) OVER (PARTITION BY counts.card_id) AS top_rows FROM (SELECT o.card_id, c.normalized_card_name, o.category, COUNT(*) AS observation_rows FROM card_category_observations o JOIN cards c ON c.id = o.card_id WHERE c.normalized_card_name = ANY(@n) GROUP BY o.card_id, c.normalized_card_name, o.category) counts) ranked WHERE observation_rows >= @minObservationRows AND CAST(observation_rows AS BIGINT) * @shareDenominator >= top_rows ORDER BY normalized_card_name, LOWER(category), category",
            connection);
        command.Parameters.AddWithValue("n", names.Select(CardNormalizer.Normalize).ToArray());
        command.Parameters.AddWithValue("minObservationRows", GetCardCategoryRepository(repository).MinObservationRows);
        command.Parameters.AddWithValue("shareDenominator", shareDenominator);
        var reference = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            reference.TryAdd(name, new List<string>());
            reference[name].Add(reader.GetString(1));
        }

        foreach (var name in names)
        {
            var expected = CategoryFilter.IncludedOrFallback(
                reference.TryGetValue(CardNormalizer.Normalize(name), out var categories)
                    ? categories
                    : Array.Empty<string>());
            Assert.Equal(expected, actual[name]);
        }
        Assert.Equal(names.Length, actual.Count);
        Assert.Contains("Ramp", actual[first]);
        Assert.DoesNotContain("Tutor", actual[first]);
        Assert.Equal(CategoryFilter.IncludedOrFallback(Array.Empty<string>()), actual[second]);
    }

    private static CardCategoryRepository GetCardCategoryRepository(CategoryKnowledgeRepository repository)
    {
        var field = typeof(CategoryKnowledgeRepository).GetField(
            "_cardCategory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<CardCategoryRepository>(field?.GetValue(repository));
    }

    private static async Task<IReadOnlyList<string>> SeedThresholdObservationsAsync(
        CategoryKnowledgeRepository repository,
        string cardName,
        IReadOnlyList<string> categories,
        string suffix,
        int? count = null)
    {
        var sources = Enumerable.Range(0, count ?? GetCardCategoryRepository(repository).MinObservationRows)
            .Select(i => $"summary-{i}-{suffix}").ToArray();
        foreach (var source in sources)
        {
            await repository.PersistObservedCategoriesAsync(source, cardName, categories);
        }

        return sources;
    }

    [PostgresFact]
    public async Task BatchCategoryLookup_Postgres_UsesQualifiedPrimaryKeyIndex()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        var suffix = Guid.NewGuid().ToString("N");
        var cardName = $"pg-summary-index-{suffix}";
        await repository.PersistObservedCategoriesAsync($"summary-index-{suffix}", cardName, new[] { "Ramp" });

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var setting = new NpgsqlCommand("SET enable_seqscan = off;", connection))
        {
            await setting.ExecuteNonQueryAsync();
        }

        await using var planCommand = new NpgsqlCommand(
            $"EXPLAIN {CardCategoryRepository.BuildCategoryLookupSql("= ANY(@normalized)")}",
            connection);
        planCommand.Parameters.AddWithValue(
            "normalized",
            new[] { CardNormalizer.Normalize(cardName) });
        planCommand.Parameters.AddWithValue("minObservationRows", GetCardCategoryRepository(repository).MinObservationRows);
        planCommand.Parameters.AddWithValue("shareDenominator", GetCardCategoryRepository(repository).ObservationShareDenominator);
        await using var reader = await planCommand.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync())
        {
            plan.Add(reader.GetString(0));
        }

        Assert.Contains(plan, line => line.Contains("card_category_qualified_pkey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(plan, line => line.Contains("card_category_summary", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(plan, line => line.Contains("card_category_observations", StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_BackfillWaitsForSummaryWriterAndUsesCommittedValue()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var cardName = $"pg-qualified-backfill-{suffix}";
        var seedTarget = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = $"qualified-backfill-seed-{suffix}"
        }.ConnectionString;
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, seedTarget));
        await SeedThresholdObservationsAsync(repository, cardName, new[] { "Ramp" }, suffix);

        await using var writer = new NpgsqlConnection(connectionString);
        await writer.OpenAsync();
        await using (var clearQualified = new NpgsqlCommand(
            // Why: the backfill only runs on an empty side table, and this class shares one database across tests.
            "DELETE FROM card_category_qualified;",
            writer))
        {
            await clearQualified.ExecuteNonQueryAsync();
        }

        await using var transaction = await writer.BeginTransactionAsync();
        await using (var updateSummary = new NpgsqlCommand(
            "UPDATE card_category_summary SET observation_rows = observation_rows + 1 WHERE card_id = (SELECT id FROM cards WHERE normalized_card_name = @cardName) AND category = 'Ramp';",
            writer,
            transaction))
        {
            updateSummary.Parameters.AddWithValue("cardName", CardNormalizer.Normalize(cardName));
            Assert.Equal(1, await updateSummary.ExecuteNonQueryAsync());
        }

        var backfillTarget = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = $"qualified-backfill-{suffix}"
        }.ConnectionString;
        var ensureTask = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, backfillTarget)).EnsureCardCategoryQualifiedBackfilledAsync();
        var completedTask = await Task.WhenAny(ensureTask, Task.Delay(TimeSpan.FromMilliseconds(250)));
        Assert.NotSame(ensureTask, completedTask);

        await transaction.CommitAsync();
        await ensureTask;

        await using var verifyConnection = new NpgsqlConnection(connectionString);
        await verifyConnection.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand(
            "SELECT observation_rows FROM card_category_qualified WHERE card_id = (SELECT id FROM cards WHERE normalized_card_name = @cardName) AND category = 'Ramp';",
            verifyConnection);
        verifyCommand.Parameters.AddWithValue("cardName", CardNormalizer.Normalize(cardName));
        Assert.Equal(6, Convert.ToInt32(await verifyCommand.ExecuteScalarAsync()));
    }
}
