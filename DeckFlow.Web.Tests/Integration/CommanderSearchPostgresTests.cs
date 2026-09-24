using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Dapper;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// PostgreSQL ordering coverage for the harvested-commanders grid.
/// </summary>
public sealed class CommanderSearchPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public CommanderSearchPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortDefault_ReturnsDeckCountDescendingWithNullLast()
    {
        var connectionInfo = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        var repository = new CategoryKnowledgeRepository(connectionInfo);
        await SeedRowsAsync(connectionInfo, ("Alpha", 2, "2026-01-01T00:00:00Z"), ("Beta", 2, null), ("Gamma", 2, "2026-01-03T00:00:00Z"));

        var rows = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.Default);

        Assert.Equal(new[] { "Gamma", "Alpha", "Beta" }, rows.Select(row => row.CommanderName));
    }

    [PostgresFact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortName_ReturnsExactAscendingAndDescendingSequences()
    {
        var connectionInfo = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        var repository = new CategoryKnowledgeRepository(connectionInfo);
        await repository.EnsureSchemaAsync();
        await SeedRowsAsync(connectionInfo, ("Atraxa", 1, null), ("Éowyn", 1, null), ("Krenko", 1, null), ("Ob Nixilis, Reignited", 1, null), ("Obeka, Splitter of Seconds", 1, null), ("Zada", 1, null), ("\u0301", 1, null));

        var ascending = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(null, "name", "asc"));
        var descending = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(null, "name", "desc"));

        Assert.Equal(new[] { "Atraxa", "Éowyn", "Krenko", "Ob Nixilis, Reignited", "Obeka, Splitter of Seconds", "Zada", "\u0301" }, ascending.Select(row => row.CommanderName));
        Assert.Equal(new[] { "Zada", "Obeka, Splitter of Seconds", "Ob Nixilis, Reignited", "Krenko", "Éowyn", "Atraxa", "\u0301" }, descending.Select(row => row.CommanderName));
    }

    private static async Task SeedRowsAsync(RelationalDatabaseConnection connectionInfo, params (string Name, int Count, string? LastProcessed)[] rows)
    {
        await using var connection = connectionInfo.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync("DELETE FROM processed_commander_summary;");
        foreach (var row in rows)
        {
            await connection.ExecuteAsync("INSERT INTO processed_commander_summary (commander_name, deck_count, last_processed_utc, commander_name_search_key) VALUES (@Name, @Count, @LastProcessed, @Key);", new { row.Name, row.Count, LastProcessed = row.LastProcessed, Key = CommanderSearchKey.Normalize(row.Name) });
        }
    }

    [PostgresTheory]
    [InlineData("deck_count", "desc")]
    [InlineData("deck_count", "asc")]
    [InlineData("name", "desc")]
    [InlineData("name", "asc")]
    [InlineData("last_processed", "desc")]
    [InlineData("last_processed", "asc")]
    public async Task GetFilteredProcessedCommanderRowsAsync_Sort_ReturnsAStableFilteredPage(string sortBy, string sortDir)
    {
        var connectionInfo = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        var repository = new CategoryKnowledgeRepository(connectionInfo);
        var prefix = $"Sort{Guid.NewGuid():N}";
        var names = new[] { $"{prefix}Alpha", $"{prefix}Beta", $"{prefix}Gamma" };

        await repository.GetFilteredProcessedCommanderCountAsync(CommanderGridQuery.Default);
        await using (var connection = connectionInfo.CreateConnection())
        {
            await connection.OpenAsync();
            foreach (var name in names)
            {
                await connection.ExecuteAsync("INSERT INTO processed_commander_summary (commander_name, deck_count, last_processed_utc, commander_name_search_key) VALUES (@name, 1, NOW(), @key) ON CONFLICT (commander_name) DO NOTHING;", new { name, key = CommanderSearchKey.Normalize(name) });
            }
        }

        var rows = await repository.GetFilteredProcessedCommanderRowsAsync(1, 3, CommanderGridQuery.FromRequest(prefix, sortBy, sortDir));

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows.Select(row => row.CommanderName).Distinct(StringComparer.Ordinal).Count());
    }

    [PostgresFact]
    public async Task MarkDeckProcessedAsync_ConcurrentCaseVariants_RefreshesOneCommanderSummary()
    {
        var connectionInfo = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        var repository = new CategoryKnowledgeRepository(connectionInfo);
        var commanderName = $"Kinnan-{Guid.NewGuid():N}";
        var lowerCommanderName = commanderName.ToLowerInvariant();
        var firstDeckId = $"deck-{Guid.NewGuid():N}";
        var secondDeckId = $"deck-{Guid.NewGuid():N}";

        await repository.AddDeckIdsAsync(new[] { firstDeckId, secondDeckId });
        await Task.WhenAll(
            repository.MarkDeckProcessedAsync(firstDeckId, commanderName),
            repository.MarkDeckProcessedAsync(secondDeckId, lowerCommanderName));

        await using var connection = connectionInfo.CreateConnection();
        await connection.OpenAsync();
        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT deck_count FROM processed_commander_summary WHERE LOWER(commander_name) = LOWER(@commanderName);",
            new { commanderName });
        Assert.Equal(2L, count);
    }
}

/// <summary>Marks a Theory that requires PostgreSQL integration testing.</summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    /// <summary>Initializes PostgreSQL test skip behavior.</summary>
    public PostgresTheoryAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 and ensure Docker is running to enable.";
        }
    }
}
