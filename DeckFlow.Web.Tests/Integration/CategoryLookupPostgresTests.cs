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
        var first = $"pg-loose-first-{suffix}";
        var second = $"pg-loose-second-{suffix}";
        var noObservations = $"pg-loose-empty-{suffix}";
        var absent = $"pg-loose-absent-{suffix}";
        var sources = new[] { $"loose-a-{suffix}", $"loose-b-{suffix}", $"loose-c-{suffix}" };

        foreach (var source in sources)
        {
            await repository.PersistObservedCategoriesAsync(
                source,
                first,
                new[] { "Ramp", "ramp", "Tutor", "Card Draw" });
        }

        await repository.PersistObservedCategoriesAsync(sources[0], second, new[] { "Removal", "Interaction" });
        await repository.PersistCardDeckTotalsAsync(sources[0], noObservations);

        var names = new[] { first, second, noObservations, absent };
        var actual = await repository.GetCategoriesForNamesAsync(names);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT c.normalized_card_name, o.category FROM card_category_observations o JOIN cards c ON c.id = o.card_id WHERE c.normalized_card_name = ANY(@n) GROUP BY c.normalized_card_name, o.category ORDER BY c.normalized_card_name, LOWER(o.category), o.category",
            connection);
        command.Parameters.AddWithValue("n", names.Select(CardNormalizer.Normalize).ToArray());
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
    }

    [PostgresFact]
    public async Task BatchCategoryLookup_Postgres_UsesCardCategoryIndex()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var repository = new CategoryKnowledgeRepository(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
        var suffix = Guid.NewGuid().ToString("N");
        var cardName = $"pg-loose-index-{suffix}";
        await repository.PersistObservedCategoriesAsync($"loose-index-{suffix}", cardName, new[] { "Ramp" });

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
        await using var reader = await planCommand.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync())
        {
            plan.Add(reader.GetString(0));
        }

        Assert.Contains(plan, line => line.Contains("ix_obs_card_category", StringComparison.OrdinalIgnoreCase));
    }
}
