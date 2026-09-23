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
