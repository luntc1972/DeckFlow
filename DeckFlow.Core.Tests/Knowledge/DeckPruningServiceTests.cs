using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies batch retention pruning of stored decks.
/// </summary>
public sealed class DeckPruningServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public DeckPruningServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task RunRetentionAsync_CommanderWith101Decks_RemovesExactlyOldest()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var commander = "Atraxa, Praetors' Voice";
        var baseUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index <= 100; index++)
        {
            await SeedStoredDeckAsync(repository, $"deck-{index:D3}", commander, baseUtc.AddDays(index));
        }

        var service = new DeckPruningService(
            repository,
            new DeckPruningOptions { PerCommanderCap = 100, BatchSize = 25, Pause = TimeSpan.Zero },
            static _ => Task.FromResult<long?>(10L * 1024 * 1024 * 1024));

        var result = await service.RunRetentionAsync();

        Assert.Equal(1, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
        Assert.Equal(1, result.Batches);
        Assert.Equal(DeckSkipReasons.Retention, await SkipReasonAsync("deck-000"));
        Assert.Equal(100, await repository.GetCommanderDeckCountAsync(commander));
        Assert.Equal(0L, await CountAsync("card_category_observations", "source_id = 'archidekt_live:deck-000'"));
        Assert.Equal(0L, await CountAsync("card_deck_totals", "source_id = 'archidekt_live:deck-000'"));
        await AssertSummaryMatchesRecomputeAsync("card_category_summary", 0);
        await AssertSummaryMatchesRecomputeAsync("card_category_qualified", 5);
    }

    [Fact]
    public void DeckPruningOptions_Defaults_MatchOq6()
    {
        var options = new DeckPruningOptions();

        Assert.Equal(100, options.PerCommanderCap);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.Pause);
        Assert.Equal(1_073_741_824L, options.MinFreeDiskBytes);
        Assert.Null(options.MaxAge);
    }

    private async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, DateTimeOffset updatedUtc)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, skip: false, new ArchidektDeckMetadata(null, 3, false, null, updatedUtc, DateTimeOffset.UtcNow));
    }

    private async Task<string?> SkipReasonAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId";
        command.Parameters.AddWithValue("@deckId", deckId);
        return (string?)await command.ExecuteScalarAsync();
    }

    private async Task<long> CountAsync(string table, string predicate)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table} WHERE {predicate}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task AssertSummaryMatchesRecomputeAsync(string table, int minimumRows)
    {
        var expected = $"SELECT card_id, category, COUNT(1) AS observation_rows FROM card_category_observations GROUP BY card_id, category {(minimumRows == 0 ? string.Empty : $"HAVING COUNT(1) >= {minimumRows}")}";
        var actual = $"SELECT card_id, category, observation_rows FROM {table}";
        Assert.Equal(0L, await ScalarAsync($"SELECT COUNT(1) FROM ({expected} EXCEPT {actual})"));
        Assert.Equal(0L, await ScalarAsync($"SELECT COUNT(1) FROM ({actual} EXCEPT {expected})"));
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A pooled SQLite handle can outlive the completed test on Windows.
        }
    }
}
