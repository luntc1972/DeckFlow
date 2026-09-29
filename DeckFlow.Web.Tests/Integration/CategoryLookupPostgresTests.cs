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
        Assert.Equal(5, minObservationRows);
        var sources = await SeedThresholdObservationsAsync(repository, first, new[] { "Ramp", "ramp", "Card Draw" }, suffix);

        await repository.PersistObservedCategoriesAsync($"summary-rare-{suffix}", first, new[] { "Tutor" });
        await SeedThresholdObservationsAsync(repository, second, new[] { "Removal", "Interaction" }, suffix, count: minObservationRows - 1);
        await repository.PersistCardDeckTotalsAsync(sources[0], noObservations);

        var names = new[] { first, second, noObservations, absent };
        var actual = await repository.GetCategoriesForNamesAsync(names);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT c.normalized_card_name, o.category FROM card_category_observations o JOIN cards c ON c.id = o.card_id WHERE c.normalized_card_name = ANY(@n) GROUP BY c.normalized_card_name, o.category HAVING COUNT(*) >= @minObservationRows ORDER BY c.normalized_card_name, LOWER(o.category), o.category",
            connection);
        command.Parameters.AddWithValue("n", names.Select(CardNormalizer.Normalize).ToArray());
        command.Parameters.AddWithValue("minObservationRows", GetCardCategoryRepository(repository).MinObservationRows);
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
    public async Task BatchCategoryLookup_Postgres_UsesSummaryPrimaryKeyIndex()
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
        await using var reader = await planCommand.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync())
        {
            plan.Add(reader.GetString(0));
        }

        Assert.Contains(plan, line => line.Contains("card_category_summary_pkey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(plan, line => line.Contains("card_category_observations", StringComparison.OrdinalIgnoreCase));
    }
}
