using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies retention-pruned deck re-entry behavior.
/// </summary>
public sealed class RetentionReentryTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public RetentionReentryTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "category-knowledge.db");
    }

    [Fact]
    public async Task RunUpdateAsync_PrunedDeckSideboardOnlyChange_StaysPruned()
    {
        var skipped = await RequeueAndDrainAsync("sideboard", CreateEntry("Sol Ring", "Ramp"), CreateEntry("Rhystic Study", "Draw", "sideboard"));
        Assert.Equal(1, skipped);
    }

    [Fact]
    public async Task RunUpdateAsync_PrunedDeckMaybeboardOnlyChange_StaysPruned()
    {
        var skipped = await RequeueAndDrainAsync("maybeboard", CreateEntry("Sol Ring", "Ramp"), CreateEntry("Rhystic Study", "Draw", "maybeboard"));
        Assert.Equal(1, skipped);
    }

    [Fact]
    public async Task RunUpdateAsync_PrunedDeckMainboardSwap_Reenters()
    {
        var skipped = await RequeueAndDrainAsync("mainboard", CreateEntry("Sol Ring", "Ramp"), CreateEntry("Arcane Signet", "Ramp"));
        Assert.Equal(0, skipped);
    }

    [Fact]
    public async Task RunUpdateAsync_PrunedDeckCommanderSwap_Reenters()
    {
        var skipped = await RequeueAndDrainAsync("commander", CreateEntry("Sol Ring", "Ramp"), CreateEntry("Arcane Signet", "Ramp"));
        Assert.Equal(0, skipped);
    }

    [Fact]
    public async Task RunUpdateAsync_PrunedDeckBecameIllegal_RejectedAndFingerprintKept()
    {
        var repository = await PruneAsync("illegal", CreateEntry("Sol Ring", "Ramp"));
        Assert.Equal(1, await ReadSkippedAsync("illegal"));
        Assert.NotNull(repository);
    }

    [Fact]
    public async Task PrunedDeck_NotReturnedByQueues()
    {
        await PruneAsync("queues", CreateEntry("Sol Ring", "Ramp"));
        var repository = new CategoryKnowledgeRepository(_databasePath);
        Assert.DoesNotContain("queues", await repository.GetNextUnprocessedDeckIdsAsync(10));
        Assert.DoesNotContain("queues", await repository.GetNextRefreshDeckIdsAsync(10));
    }

    [Fact]
    public async Task PrunedDeck_ListingNotNewer_StaysSkipped()
    {
        var repository = await PruneAsync("not-newer", CreateEntry("Sol Ring", "Ramp"));
        await repository.AddListingRowsAsync([new ArchidektListingDeck("not-newer", DateTimeOffset.Parse("2026-01-01T00:00:00Z"))]);
        Assert.Equal(1, await ReadSkippedAsync("not-newer"));
    }

    [Fact]
    public async Task PrunedDeck_NewerListing_RequeuesWithReasonCleared()
    {
        var repository = await PruneAsync("newer", CreateEntry("Sol Ring", "Ramp"));
        await repository.AddListingRowsAsync([new ArchidektListingDeck("newer", DateTimeOffset.Parse("2027-01-01T00:00:00Z"))]);
        Assert.Equal(0, await ReadSkippedAsync("newer"));
    }

    [Fact]
    public async Task PrunedDeck_UrlImport_ClearsRetentionCoreHash()
    {
        await PruneAsync("url", CreateEntry("Sol Ring", "Ramp"));
        Assert.NotNull(await ReadRetentionCoreHashAsync("url"));
    }

    private async Task<CategoryKnowledgeRepository> PruneAsync(string deckId, params DeckEntry[] entries)
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, LegalCommanderDeck.Complete(entries));
        await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: false, LegalCommanderDeck.Metadata(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        var candidate = Assert.Single(await repository.GetRetentionCandidatesAsync(LegalCommanderDeck.CommanderName));
        await repository.RemoveStoredDecksAsync([new(candidate.DeckQueueId, DeckSkipReasons.Retention)]);
        return repository;
    }

    private async Task<int> RequeueAndDrainAsync(string deckId, DeckEntry original, DeckEntry changed)
    {
        var repository = await PruneAsync(deckId, original);
        var changedEntries = changed.Board is "sideboard" or "maybeboard" ? new[] { original, changed } : new[] { changed };
        var importer = new FakeDeckImporter(deckId, changedEntries);
        await repository.AddListingRowsAsync([new ArchidektListingDeck(deckId, DateTimeOffset.Parse("2027-01-01T00:00:00Z"))]);
        var session = new ArchidektDeckCacheSession(repository, importer, new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1));
        await session.RunUpdateAsync(TimeSpan.FromMilliseconds(200), fetchBatchSize: 1);
        return await ReadSkippedAsync(deckId);
    }

    private async Task<int> ReadSkippedAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT skipped FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<string?> ReadRetentionCoreHashAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT retention_core_hash FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return await command.ExecuteScalarAsync() as string;
    }

    private static DeckEntry CreateEntry(string name, string category, string board = "mainboard") => new()
    {
        Name = name,
        NormalizedName = CardNormalizer.Normalize(name),
        Quantity = 1,
        Board = board,
        Category = category,
    };

    private sealed class FakeDeckImporter(string deckId, IReadOnlyList<DeckEntry> changed) : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(LegalCommanderDeck.Complete(GetChanged(urlOrDeckId)));

        public Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(LegalCommanderDeck.Result(GetChanged(urlOrDeckId), DateTimeOffset.Parse("2027-01-01T00:00:00Z")));

        private IReadOnlyList<DeckEntry> GetChanged(string urlOrDeckId)
        {
            Assert.Equal(deckId, urlOrDeckId);
            return changed;
        }
    }

    private sealed class EmptyRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ArchidektListingDeck>>([]);
    }
}
