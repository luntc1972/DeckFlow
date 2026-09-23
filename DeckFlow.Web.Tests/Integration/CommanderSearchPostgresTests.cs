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
