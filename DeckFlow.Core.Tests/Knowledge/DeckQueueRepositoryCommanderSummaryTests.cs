using Microsoft.Data.Sqlite;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class DeckQueueRepositoryCommanderSummaryTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AddDeckIdsAsync_ReaddingQueuedIdsCountsOnlyNewRows()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        var repository = new DeckQueueRepository(connectionInfo, schema);

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2" });

        var enqueued = await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2", "deck-3" });

        Assert.Equal(1, enqueued);
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_UpdatesCommanderSummaryWithNormalizedAggregate()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        var repository = new DeckQueueRepository(connectionInfo, schema);

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2", "deck-3" });
        await repository.MarkDeckProcessedAsync("deck-1", "Kinnan, Bonder Prodigy");
        await repository.MarkDeckProcessedAsync("deck-2", "kinnan, bonder prodigy");
        await repository.MarkDeckProcessedAsync("deck-3", "Tymna the Weaver");

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT deck_count FROM processed_commander_summary WHERE commander_name = 'kinnan, bonder prodigy';";

        Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        Assert.Equal(2, await repository.GetDistinctProcessedCommanderCountAsync());
    }

    [Theory]
    [InlineData("tef", 1)]
    [InlineData("TEF", 1)]
    [InlineData("", 3)]
    [InlineData("100%", 0)]
    [InlineData("a_b", 0)]
    [InlineData("back\\sl", 0)]
    [InlineData("back\\", 0)]
    [InlineData("éowyn", 0)]
    [InlineData("eowyn", 0)]
    public async Task GetFilteredProcessedCommanderRowsAsync_Prefix_ReturnsExpectedCount(string search, int expectedCount)
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        var repository = new DeckQueueRepository(connectionInfo, schema);
        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2", "deck-3" });
        await repository.MarkDeckProcessedAsync("deck-1", "Teferi, Temporal Archmage");
        await repository.MarkDeckProcessedAsync("deck-2", "Tergrid, God of Fright");
        await repository.MarkDeckProcessedAsync("deck-3", "Krenko, Mob Boss");

        var rows = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(search, null, null));

        Assert.Equal(expectedCount, rows.Count);
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_WritesSearchKey()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2" });
        await repository.MarkDeckProcessedAsync("deck-1", "Krenko, Mob Boss");
        await repository.MarkDeckProcessedAsync("deck-2", "Éowyn, Lady of Rohan");

        Assert.Equal("eowyn, lady of rohan", await ReadSearchKeyAsync(databasePath, "Éowyn, Lady of Rohan"));
    }

    [Fact]
    public async Task MarkUrlDeckProcessedAsync_WritesSearchKey()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await repository.AddDeckIdsAsync(new[] { "deck-1" });
        await repository.MarkUrlDeckProcessedAsync("deck-1", "Éowyn, Lady of Rohan");

        Assert.Equal(CommanderSearchKey.Normalize("Éowyn, Lady of Rohan"), await ReadSearchKeyAsync(databasePath, "Éowyn, Lady of Rohan"));
    }

    [Fact]
    public async Task MarkDecksProcessedAsync_WritesSearchKey()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await repository.AddDeckIdsAsync(new[] { "deck-1" });
        await repository.MarkDeckProcessedAsync("deck-1", "Éowyn, Lady of Rohan");

        await repository.MarkDecksProcessedAsync(new[] { "deck-1" });

        Assert.Equal(CommanderSearchKey.Normalize("Éowyn, Lady of Rohan"), await ReadSearchKeyAsync(databasePath, "Éowyn, Lady of Rohan"));
    }

    [Fact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortDefault_UsesTimestampThenNameTotalOrder()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await SeedCommanderRowsAsync(repository, databasePath, ("Alpha", 2, "2026-01-01T00:00:00Z"), ("Beta", 2, null), ("Gamma", 2, "2026-01-03T00:00:00Z"));

        var rows = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.Default);

        Assert.Equal(new[] { "Gamma", "Alpha", "Beta" }, rows.Select(row => row.CommanderName));
    }

    [Fact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortDeckCountAscending_OrdersLowestFirst()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await SeedCommanderRowsAsync(repository, databasePath, ("Three", 3, null), ("One", 1, null), ("Two", 2, null));

        var rows = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(null, "deck_count", "asc"));

        Assert.Equal(new[] { "One", "Two", "Three" }, rows.Select(row => row.CommanderName));
    }

    [Fact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortPaging_ProducesNoRepeatOrOmission()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await SeedCommanderRowsAsync(repository, databasePath, ("Alpha", 2, "2026-01-01T00:00:00Z"), ("Beta", 2, "2026-01-01T00:00:00Z"), ("Gamma", 2, "2026-01-01T00:00:00Z"));

        var names = new[] { 1, 2, 3 }.SelectMany(page => repository.GetFilteredProcessedCommanderRowsAsync(page, 1, CommanderGridQuery.Default).Result).Select(row => row.CommanderName).ToArray();

        Assert.Equal(3, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("desc", new[] { "Newest", "Oldest", "Missing" })]
    [InlineData("asc", new[] { "Oldest", "Newest", "Missing" })]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortLastProcessed_PlacesNullLast(string direction, string[] expected)
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await SeedCommanderRowsAsync(repository, databasePath, ("Newest", 1, "2026-01-03T00:00:00Z"), ("Oldest", 1, "2026-01-01T00:00:00Z"), ("Missing", 1, null));

        var rows = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(null, "last_processed", direction));

        Assert.Equal(expected, rows.Select(row => row.CommanderName));
    }

    [Fact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortNameAscending_ReturnsExactSequence()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await SeedCommanderRowsAsync(repository, databasePath, ("Atraxa", 1, null), ("Éowyn", 1, null), ("Krenko", 1, null), ("Zada", 1, null), ("\u0301", 1, null));

        var ascending = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(null, "name", "asc"));

        Assert.Equal(new[] { "Atraxa", "Éowyn", "Krenko", "Zada", "\u0301" }, ascending.Select(row => row.CommanderName));
    }

    [Fact]
    public async Task GetFilteredProcessedCommanderRowsAsync_SortNameDescending_ReturnsExactSequence()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        await SeedCommanderRowsAsync(repository, databasePath, ("Atraxa", 1, null), ("Éowyn", 1, null), ("Krenko", 1, null), ("Zada", 1, null), ("\u0301", 1, null));

        var descending = await repository.GetFilteredProcessedCommanderRowsAsync(1, 20, CommanderGridQuery.FromRequest(null, "name", "desc"));

        Assert.Equal(new[] { "Zada", "Krenko", "Éowyn", "Atraxa", "\u0301" }, descending.Select(row => row.CommanderName));
    }

    private static async Task SeedCommanderRowsAsync(DeckQueueRepository repository, string databasePath, params (string Name, int Count, string? LastProcessedUtc)[] rows)
    {
        await repository.AddDeckIdsAsync(rows.Select(row => $"deck-{row.Name}"));
        foreach (var row in rows)
        {
            await repository.MarkDeckProcessedAsync($"deck-{row.Name}", row.Name);
        }

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        foreach (var row in rows)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE processed_commander_summary SET deck_count = $count, last_processed_utc = $lastProcessedUtc WHERE commander_name = $name;";
            command.Parameters.AddWithValue("$count", row.Count);
            command.Parameters.AddWithValue("$lastProcessedUtc", (object?)row.LastProcessedUtc ?? DBNull.Value);
            command.Parameters.AddWithValue("$name", row.Name);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<(DeckQueueRepository Repository, string DatabasePath)> CreateRepositoryAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        await schema.EnsureSchemaAsync();
        return (new DeckQueueRepository(connectionInfo, schema), databasePath);
    }

    private static async Task<string?> ReadSearchKeyAsync(string databasePath, string commanderName)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT commander_name_search_key FROM processed_commander_summary WHERE commander_name = $name;";
        command.Parameters.AddWithValue("$name", commanderName);
        return await command.ExecuteScalarAsync() as string;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
