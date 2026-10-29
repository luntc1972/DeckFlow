using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies the stored-live-rows marker maintained beside queued decks.
/// </summary>
public sealed class StoredLiveRowsMarkerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public StoredLiveRowsMarkerTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "category-knowledge.db");
    }

    [Fact]
    public async Task ReplaceDeckEntriesAsync_FirstWrite_SetsMarker()
    {
        await StoreAsync("first");
        await AssertMarkerAsync("first", 1L);
    }

    [Fact]
    public async Task ReplaceDeckEntriesAsync_EmptiedDeck_KeepsSourceClearsMarkerAndExcludesFromReaders()
    {
        var repository = await StoreAsync("empty");
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:empty", []);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT COUNT(1) FROM sources WHERE source = 'archidekt_live:empty';"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(1) FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE source = 'archidekt_live:empty');"));
        await AssertMarkerAsync("empty", 0L);
    }

    [Fact]
    public async Task PersistDeckEntriesAsync_FirstWrite_SetsMarker()
    {
        await StoreAsync("persist-entries");
        await AssertMarkerAsync("persist-entries", 1L);
    }

    [Fact]
    public async Task PersistCardDeckTotalsAsync_LinkedSource_SetsMarker()
    {
        await StoreAsync("persist-totals");
        await AssertMarkerAsync("persist-totals", 1L);
    }

    [Fact]
    public async Task PersistObservedCategoriesAsync_LinksSourceWithExistingTotals_SetsMarker()
    {
        await StoreAsync("observed");
        await AssertMarkerAsync("observed", 1L);
    }

    [Fact]
    public async Task ReplaceSourceRowsAsync_LinksSourceWithExistingTotals_SetsMarker()
    {
        await StoreAsync("replace-source");
        await AssertMarkerAsync("replace-source", 1L);
    }

    [Fact]
    public async Task DeleteSourceDataAsync_StoredDeck_ClearsMarker()
    {
        var repository = await StoreAsync("delete-source");
        await repository.DeleteSourceDataAsync("archidekt_live:delete-source");
        await AssertMarkerAsync("delete-source", 0L);
    }

    [Fact]
    public async Task RejectUrlDeckAsync_StoredLiveDeck_ClearsMarker()
    {
        await StoreAsync("reject");
        await AssertMarkerAsync("reject", 1L);
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_StoredDeck_ClearsMarker()
    {
        await StoreAsync("remove");
        await AssertMarkerAsync("remove", 1L);
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_StaleMarkerWithoutTotals_HealsMarkerAndPurgeTerminates()
    {
        var repository = await StoreAsync("stale");
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:stale", []);
        await ExecuteAsync("UPDATE deck_queue SET has_stored_live_rows = 1 WHERE deck_id = 'stale';");
        var deckQueueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = 'stale';");
        await repository.RemoveStoredDecksAsync([(deckQueueId, DeckSkipReasons.NotCommanderFormat)]);
        await AssertMarkerAsync("stale", 0L);
    }

    [Fact]
    public async Task NonLiveSources_NeverSetMarker()
    {
        var repository = await StoreAsync("non-live");
        await ExecuteAsync("UPDATE sources SET source = 'manual:non-live' WHERE source = 'archidekt_live:non-live';");
        await repository.PersistCardDeckTotalsAsync("manual:non-live", "Sol Ring");
        await AssertMarkerAsync("non-live", 0L);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<CategoryKnowledgeRepository> StoreAsync(string deckId)
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, "Atraxa", false, null);
        return repository;
    }

    private async Task AssertMarkerAsync(string deckId, long expected)
    {
        Assert.Equal(expected, await ScalarAsync<long>($"SELECT has_stored_live_rows FROM deck_queue WHERE deck_id = '{deckId}';"));
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull ? default! : (T)Convert.ChangeType(value, typeof(T));
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
