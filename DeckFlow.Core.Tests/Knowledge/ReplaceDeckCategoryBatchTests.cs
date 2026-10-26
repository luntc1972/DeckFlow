using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>Verifies atomic replacement of a harvested deck's category rows.</summary>
public sealed class ReplaceDeckCategoryBatchTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public ReplaceDeckCategoryBatchTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "replace.db");
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_ChangedRows_LeavesExactlyNewRowsAndExactSummaries()
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { "changed" });
        await repository.PersistDeckCategoryBatchAsync("changed", Rows(("A", "Ramp"), ("B", "Draw")), Totals("A", "B"));

        await repository.ReplaceDeckCategoryBatchAsync("changed", Rows(("B", "Draw"), ("C", "Ramp")), Totals("B", "C"), new DeckContentHashWrite("changed", "h2"));

        Assert.Equal(2L, await CountAsync("card_category_observations", "source_id IN (SELECT id FROM sources WHERE source = 'changed')"));
        Assert.Equal(2L, await CountAsync("card_deck_totals", "source_id IN (SELECT id FROM sources WHERE source = 'changed')"));
        Assert.Equal("h2", await ScalarAsync("SELECT content_hash FROM deck_queue WHERE deck_id = 'changed'"));
        await AssertSummaryExactAsync();
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_EmptyBatchOnStoredSource_RemovesRowsAndClearsHash()
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { "stored" });
        await repository.PersistDeckCategoryBatchAsync("stored", Rows(("A", "Ramp"), ("B", "Draw")), Totals("A", "B"));
        await repository.PersistDeckCategoryBatchAsync("other", Rows(("A", "Ramp")), Totals("A"));

        await repository.ReplaceDeckCategoryBatchAsync("stored", Array.Empty<(string, string, string, int, int)>(), Array.Empty<(string, string)>(), new DeckContentHashWrite("stored", null));

        Assert.Equal(0L, await CountAsync("card_category_observations", "source_id IN (SELECT id FROM sources WHERE source = 'stored')"));
        Assert.Equal(1L, await CountAsync("card_category_observations", "source_id IN (SELECT id FROM sources WHERE source = 'other')"));
        Assert.Null(await ScalarAsync("SELECT content_hash FROM deck_queue WHERE deck_id = 'stored'"));
        await AssertSummaryExactAsync();
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_EmptyBatchOnUnknownSource_CreatesNoSourceRow()
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { "unknown" });
        await ExecuteAsync("UPDATE deck_queue SET content_hash = 'h1' WHERE deck_id = 'unknown';");
        var before = await CountAsync("sources", "1 = 1");

        await repository.ReplaceDeckCategoryBatchAsync("unknown", Array.Empty<(string, string, string, int, int)>(), Array.Empty<(string, string)>(), new DeckContentHashWrite("unknown", "h2"));

        Assert.Equal(before, await CountAsync("sources", "1 = 1"));
        Assert.Equal("h2", await ScalarAsync("SELECT content_hash FROM deck_queue WHERE deck_id = 'unknown'"));
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_NullHashWrite_LeavesDeckQueueUntouched()
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { "null-hash" });
        await ExecuteAsync("UPDATE deck_queue SET content_hash = 'h1' WHERE deck_id = 'null-hash';");

        await repository.ReplaceDeckCategoryBatchAsync("null-hash", Rows(("A", "Ramp")), Totals("A"), contentHashWrite: null);

        Assert.Equal("h1", await ScalarAsync("SELECT content_hash FROM deck_queue WHERE deck_id = 'null-hash'"));
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_HashUpdateFails_RollsBackEverything()
    {
        var repository = await SeedQualifiedAsync("hash-failure");
        var snapshot = await StateAsync();
        await ExecuteAsync("""
            CREATE TRIGGER fail_hash BEFORE UPDATE OF content_hash ON deck_queue
            WHEN EXISTS (SELECT 1 FROM card_category_observations WHERE card_name = 'Zz Replacement Marker')
            BEGIN SELECT RAISE(ABORT, 'hash boom'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => repository.ReplaceDeckCategoryBatchAsync("hash-failure", Rows(("Zz Replacement Marker", "Ramp")), Totals("Zz Replacement Marker"), new DeckContentHashWrite("hash-failure", "h2")));

        Assert.Equal(snapshot, await StateAsync());
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_TotalsInsertFails_RollsBackEverything()
    {
        var repository = await SeedQualifiedAsync("totals-failure");
        var snapshot = await StateAsync();
        await ExecuteAsync("CREATE TRIGGER fail_total BEFORE INSERT ON card_deck_totals BEGIN SELECT RAISE(ABORT, 'total boom'); END;");

        await Assert.ThrowsAsync<SqliteException>(() => repository.ReplaceDeckCategoryBatchAsync("totals-failure", Rows(("Boom", "Ramp")), Totals("Boom"), new DeckContentHashWrite("totals-failure", "h2")));

        Assert.Equal(snapshot, await StateAsync());
    }

    [Fact]
    public async Task ReplaceDeckCategoryBatchAsync_QualifiedSeedWithoutFailure_DeletesQualifiedRow()
    {
        var repository = await SeedQualifiedAsync("qualified-control");
        Assert.Equal(1L, await CountAsync("card_category_qualified", "1 = 1"));

        await repository.ReplaceDeckCategoryBatchAsync("qualified-control", Array.Empty<(string, string, string, int, int)>(), Array.Empty<(string, string)>(), new DeckContentHashWrite("qualified-control", "h2"));

        Assert.Equal(0L, await CountAsync("card_category_qualified", "1 = 1"));
        await AssertSummaryExactAsync();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
    }

    private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);

    private async Task<CategoryKnowledgeRepository> SeedQualifiedAsync(string source)
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { source });
        for (var index = 0; index < 5; index++)
        {
            await repository.PersistDeckCategoryBatchAsync(index == 0 ? source : $"seed-{index}", Rows(("A", "Ramp")), Totals("A"));
        }

        await repository.ReplaceDeckCategoryBatchAsync(source, Rows(("A", "Ramp")), Totals("A"), new DeckContentHashWrite(source, "h1"));
        return repository;
    }

    private static (string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)[] Rows(params (string CardName, string Category)[] rows) => rows.Select(row => (row.CardName, row.Category, "mainboard", 1, 1)).ToArray();
    private static (string CardName, string Board)[] Totals(params string[] cards) => cards.Select(card => (card, "mainboard")).ToArray();

    private async Task<long> CountAsync(string table, string where) => Convert.ToInt64(await ScalarObjectAsync($"SELECT COUNT(1) FROM {table} WHERE {where};"));
    private async Task<string?> ScalarAsync(string sql)
    {
        var value = await ScalarObjectAsync(sql);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private async Task<object?> ScalarObjectAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> StateAsync() => string.Join("|", await ScalarAsync("SELECT COUNT(1) FROM card_category_observations;"), await ScalarAsync("SELECT COUNT(1) FROM card_deck_totals;"), await ScalarAsync("SELECT COUNT(1) FROM card_category_summary;"), await ScalarAsync("SELECT COUNT(1) FROM card_category_qualified;"), await ScalarAsync("SELECT content_hash FROM deck_queue WHERE deck_id LIKE '%failure';"));

    private async Task AssertSummaryExactAsync()
    {
        const string expected = "SELECT card_id, category, COUNT(1) AS observation_rows FROM card_category_observations GROUP BY card_id, category";
        const string actual = "SELECT card_id, category, observation_rows FROM card_category_summary";
        Assert.Equal(0L, Convert.ToInt64(await ScalarObjectAsync($"SELECT COUNT(1) FROM ({expected} EXCEPT {actual});")));
        Assert.Equal(0L, Convert.ToInt64(await ScalarObjectAsync($"SELECT COUNT(1) FROM ({actual} EXCEPT {expected});")));
    }
}
