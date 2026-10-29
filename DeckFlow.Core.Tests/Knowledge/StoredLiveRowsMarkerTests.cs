using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
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
        await StoreAsync("empty-second");
        await repository.MarkDeckProcessedAsync("empty-second", "Atraxa", false, new ArchidektDeckMetadata(null, null, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        await repository.MarkDeckProcessedAsync("empty", "Atraxa", false, new ArchidektDeckMetadata(null, null, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:empty", []);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT COUNT(1) FROM sources WHERE source = 'archidekt_live:empty';"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT COUNT(1) FROM card_deck_totals WHERE source_id IN (SELECT id FROM sources WHERE source = 'archidekt_live:empty');"));
        await AssertMarkerAsync("empty", 0L);
        var emptyQueueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = 'empty';");
        Assert.DoesNotContain((await repository.GetRetentionCandidatesAsync("Atraxa")).Select(candidate => candidate.DeckQueueId), id => id == emptyQueueId);
        Assert.DoesNotContain((await repository.GetPurgeCandidatesAsync()).Select(candidate => candidate.DeckQueueId), id => id == emptyQueueId);
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    [Fact]
    public async Task PersistDeckEntriesAsync_FirstWrite_SetsMarker()
    {
        var repository = await EnqueueAsync("persist-entries");
        await DeckCategoryCacheWriter.PersistDeckEntriesAsync(repository, "archidekt_live:persist-entries", Entries());
        await repository.MarkDeckProcessedAsync("persist-entries", "Atraxa", false, null);
        await AssertMarkerAsync("persist-entries", 1L);
    }

    [Fact]
    public async Task PersistCardDeckTotalsAsync_LinkedSource_SetsMarker()
    {
        var repository = await EnqueueAsync("persist-totals");
        await repository.PersistCardDeckTotalsAsync("archidekt_live:persist-totals", "Sol Ring");
        await repository.MarkDeckProcessedAsync("persist-totals", "Atraxa", false, null);
        await AssertMarkerAsync("persist-totals", 1L);
    }

    [Fact]
    public async Task PersistObservedCategoriesAsync_LinksSourceWithExistingTotals_SetsMarker()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:observed", "Sol Ring");
        await repository.AddDeckIdsAsync(["observed"]);
        await repository.PersistObservedCategoriesAsync("archidekt_live:observed", "Sol Ring", ["Ramp"], deckCountIncrement: 1);
        await repository.MarkDeckProcessedAsync("observed", "Atraxa", false, null);
        await AssertMarkerAsync("observed", 1L);
    }

    [Fact]
    public async Task ReplaceSourceRowsAsync_LinksSourceWithExistingTotals_SetsMarker()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:replace-source", "Sol Ring");
        await repository.AddDeckIdsAsync(["replace-source"]);
        await repository.ReplaceSourceRowsAsync("archidekt_live:replace-source", [new CategoryKnowledgeRow("Ramp", "Sol Ring", 1, 1)]);
        await repository.MarkDeckProcessedAsync("replace-source", "Atraxa", false, null);
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
        var repository = await StoreAsync("777777");
        await repository.RejectUrlDeckAsync("archidekt_url:https://archidekt.com/decks/777777/test", "777777", "Atraxa", null, DeckSkipReasons.NotCommanderFormat);
        await AssertMarkerAsync("777777", 0L);
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_StoredDeck_ClearsMarker()
    {
        var repository = await StoreAsync("remove");
        var deckQueueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = 'remove';");
        await repository.RemoveStoredDecksAsync([(deckQueueId, DeckSkipReasons.Retention)]);
        await AssertMarkerAsync("remove", 0L);
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_StaleMarkerWithoutTotals_HealsMarkerAndPurgeTerminates()
    {
        var repository = await StoreAsync("stale");
        await repository.MarkDeckProcessedAsync("stale", "Atraxa", false, new ArchidektDeckMetadata(null, null, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:stale", []);
        await ExecuteAsync("UPDATE deck_queue SET has_stored_live_rows = 1 WHERE deck_id = 'stale';");
        var before = await ScalarAsync<long>("SELECT processed FROM deck_queue WHERE deck_id = 'stale';");
        var deckQueueId = await ScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = 'stale';");
        await repository.RemoveStoredDecksAsync([(deckQueueId, DeckSkipReasons.Retention)]);
        Assert.Equal(before, await ScalarAsync<long>("SELECT processed FROM deck_queue WHERE deck_id = 'stale';"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT skipped FROM deck_queue WHERE deck_id = 'stale';"));
        Assert.Null(await ScalarAsync<string?>("SELECT skip_reason FROM deck_queue WHERE deck_id = 'stale';"));
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
        var repository = await EnqueueAsync(deckId);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, Entries());
        await repository.MarkDeckProcessedAsync(deckId, "Atraxa", false, null);
        return repository;
    }

    private async Task<CategoryKnowledgeRepository> EnqueueAsync(string deckId)
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync([deckId]);
        return repository;
    }

    private static IReadOnlyList<DeckEntry> Entries() => [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }];

    private async Task AssertMarkerAsync(string deckId, long expected)
    {
        Assert.Equal(expected, await ScalarAsync<long>($"SELECT has_stored_live_rows FROM deck_queue WHERE deck_id = '{deckId}';"));
        Assert.Equal(expected, await ScalarAsync<long>($"SELECT CASE WHEN EXISTS (SELECT 1 FROM sources s INNER JOIN card_deck_totals t ON t.source_id = s.id WHERE s.deck_queue_id = (SELECT id FROM deck_queue WHERE deck_id = '{deckId}') AND SUBSTR(s.source, 1, 15) = 'archidekt_live:') THEN 1 ELSE 0 END;"));
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
