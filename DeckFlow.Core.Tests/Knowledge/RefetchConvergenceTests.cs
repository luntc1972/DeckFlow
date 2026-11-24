using System.Data.Common;
using Dapper;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class RefetchConvergenceTests
{
    [Fact] public Task Refetch_ThenNotFound_ArchivesAndLeavesNoCandidate() => WithSqliteAsync("not-found");
    [Fact] public Task Refetch_ThenSuccess_CapturesMetadataAndLeavesNoCandidate() => WithSqliteAsync("success");
    [Fact] public Task Refetch_ThenTransientThreeTimes_StaysRetryableAndNotArchived() => WithSqliteAsync("transient");
    [Fact] public Task Refetch_ThenOtherFailure_FetchFailureNotArchived() => WithSqliteAsync("other-failure");
    [Fact] public Task NotFound_UnstoredDeck_MarkedWithoutManifest() => WithSqliteAsync("unstored");
    [Fact] public Task RetentionArchived_RequeuedThenFails_KeepsRetentionReason() => WithSqliteAsync("retention");
    [Fact] public Task NotFoundArchived_Reappears_UnarchivesOnReentry() => WithSqliteAsync("reappear");
    [Fact] public Task NotFound_RetireFails_FallsBackToFetchFailureAndContinues() => WithSqliteAsync("fallback");

    internal static async Task VerifyAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        if (scenario == "unstored")
        {
            await VerifyUnstoredAsync(connection, repository);
            return;
        }
        if (scenario == "retention")
        {
            await VerifyRetentionAsync(connection, info, repository);
            return;
        }
        if (scenario == "fallback")
        {
            await VerifyFallbackAsync(connection, info, repository);
            return;
        }
        const string deckId = "refetch-convergence";
        const string otherDeckId = "refetch-convergence-other";
        var entries = LegalCommanderDeck.Complete([new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.AddDeckIdsAsync([deckId, otherDeckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + otherDeckId, entries);
        await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: true, metadata: null, skipReason: DeckSkipReasons.FetchFailure);
        await repository.MarkDeckProcessedAsync(otherDeckId, LegalCommanderDeck.CommanderName, skip: false, LegalCommanderDeck.Metadata());
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);

        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring");
        var categories = await repository.GetCategoriesAsync("Sol Ring");
        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        Assert.Single(await repository.GetCategoryRowsForCommanderAsync(LegalCommanderDeck.CommanderName));
        Assert.Single(await repository.GetCategoryDeckMembershipForCommanderAsync(LegalCommanderDeck.CommanderName));
        string? retentionHash = null;
        if (scenario == "retention")
        {
            var queueId = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId", new { deckId });
            await repository.RemoveStoredDecksAsync([(queueId, DeckSkipReasons.Retention)]);
            retentionHash = await connection.QuerySingleAsync<string>("SELECT retention_core_hash FROM deck_queue WHERE deck_id = @deckId", new { deckId });
            await repository.AddListingRowsAsync([new ArchidektListingDeck(deckId, DateTimeOffset.Parse("2027-01-01T00:00:00Z"))]);
            Assert.Equal((string?)null, await connection.QuerySingleAsync<string?>("SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId }));
            Assert.Equal(retentionHash, await connection.QuerySingleAsync<string>("SELECT retention_core_hash FROM deck_queue WHERE deck_id = @deckId", new { deckId }));
        }
        Assert.Equal(scenario == "retention" ? 0 : 1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        var importerScenario = scenario is "reappear" or "retention" ? "not-found" : scenario;
        var session = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckId, importerScenario, entries), new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(100));
        if (scenario == "transient")
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                await session.RunAsync(TimeSpan.FromMilliseconds(1));
                var state = await connection.QuerySingleAsync<(long Processed, long Skipped, long Strikes)>("SELECT processed, skipped, transient_failure_count FROM deck_queue WHERE deck_id = @deckId", new { deckId });
                if (attempt < 3) Assert.Equal((0L, 0L, (long)attempt), state);
            }
        }
        else
        {
            var result = await session.RunAsync(TimeSpan.FromMilliseconds(1));
            if (scenario == "not-found") Assert.Equal(1, result.DecksSkipped);
            if (scenario == "reappear")
            {
                await repository.AddListingRowsAsync([new ArchidektListingDeck(deckId, DateTimeOffset.Parse("2028-01-01T00:00:00Z"))]);
                var reentry = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckId, "success", entries), new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(100));
                await reentry.RunAsync(TimeSpan.FromMilliseconds(1));
            }
        }

        var row = await connection.QuerySingleAsync<(long Processed, long Skipped, string Reason, long Live, long Metadata)>("SELECT processed, skipped, skip_reason, has_stored_live_rows, CASE WHEN archidekt_metadata_captured_utc IS NULL THEN 0 ELSE 1 END FROM deck_queue WHERE deck_id = @deckId", new { deckId });
        var manifests = await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest WHERE deck_queue_id = (SELECT id FROM deck_queue WHERE deck_id = @deckId)", new { deckId });
        var liveRows = await connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM sources s JOIN card_deck_totals t ON t.source_id = s.id WHERE s.source = @source", new { source = "archidekt_live:" + deckId });
        if (scenario == "not-found")
        {
            Assert.Equal((1L, 1L, DeckSkipReasons.NotFound, 0L, 0L), row); Assert.Equal(1, manifests); Assert.Equal(0, liveRows); Assert.Equal(0, await repository.CountRefetchCandidatesAsync()); Assert.Equal(0, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
            var afterTotals = await repository.GetCardDeckTotalsAsync("Sol Ring"); Assert.Equal(totals.TotalDeckCount, afterTotals.TotalDeckCount); Assert.Equal(totals.BoardDeckCounts.Count, afterTotals.BoardDeckCounts.Count); Assert.Equal(categories, await repository.GetCategoriesAsync("Sol Ring")); Assert.Equal(counts.Count, (await repository.GetCategoryDeckCountsAsync("Sol Ring")).Count);
            Assert.Single(await repository.GetCategoryRowsForCommanderAsync(LegalCommanderDeck.CommanderName)); Assert.Single(await repository.GetCategoryDeckMembershipForCommanderAsync(LegalCommanderDeck.CommanderName));
            Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT deck_count FROM processed_commander_summary WHERE commander_name = @name", new { name = LegalCommanderDeck.CommanderName }));
        }
        else if (scenario is "success" or "reappear")
        {
            Assert.Equal(1L, row.Processed); Assert.Equal(0L, row.Skipped); Assert.Equal(1L, row.Live); Assert.Equal(1L, row.Metadata); Assert.Equal(0, manifests); Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
        }
        else
        {
            if (scenario == "retention") Assert.Equal(retentionHash, await connection.QuerySingleAsync<string>("SELECT retention_core_hash FROM deck_queue WHERE deck_id = @deckId", new { deckId }));
            Assert.Equal(scenario == "transient" ? 0L : 1L, row.Processed); Assert.Equal(1L, row.Skipped); Assert.Equal(scenario == "retention" ? DeckSkipReasons.Retention : DeckSkipReasons.FetchFailure, row.Reason); Assert.Equal(scenario == "retention" ? 0L : 1L, row.Live); Assert.Equal(scenario == "retention" ? 1 : 0, manifests); Assert.Equal(scenario == "retention" ? 0 : 3, liveRows); Assert.Equal(scenario == "retention" ? 0 : 1, await repository.CountRefetchCandidatesAsync());
            if (scenario == "transient") Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        }
    }

    private static async Task VerifyRetentionAsync(DbConnection connection, RelationalDatabaseConnection info, CategoryKnowledgeRepository repository)
    {
        var entries = LegalCommanderDeck.Complete([new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        var deckIds = new[] { "retention-a", "retention-b", "retention-c", "retention-d" };
        await repository.AddDeckIdsAsync(deckIds);
        foreach (var deckId in deckIds)
        {
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
            await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: deckId == "retention-d", metadata: deckId == "retention-d" ? null : LegalCommanderDeck.Metadata(), skipReason: deckId == "retention-d" ? DeckSkipReasons.FetchFailure : null);
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);

        var queueIds = await connection.QueryAsync<(long Id, string DeckId)>("SELECT id AS Id, deck_id AS DeckId FROM deck_queue WHERE deck_id LIKE 'retention-%' AND deck_id <> 'retention-d' ORDER BY id");
        Assert.Equal(3, await repository.RemoveStoredDecksAsync(queueIds.Select(item => (item.Id, DeckSkipReasons.Retention)).ToArray()));
        var hashes = await connection.QueryAsync<(string DeckId, string? Hash)>("SELECT deck_id AS DeckId, retention_core_hash AS Hash FROM deck_queue WHERE deck_id LIKE 'retention-%' AND deck_id <> 'retention-d' ORDER BY id");
        Assert.All(hashes, item => Assert.NotNull(item.Hash));
        await connection.ExecuteAsync("UPDATE deck_queue SET retention_core_hash = 'seeded-retention-hash' WHERE deck_id = 'retention-d'");
        await repository.AddListingRowsAsync(deckIds[..3].Select((deckId, index) => new ArchidektListingDeck(deckId, DateTimeOffset.Parse($"2027-01-0{index + 1}T00:00:00Z"))).ToArray());
        foreach (var deckId in deckIds[..3])
        {
            Assert.Null(await connection.QuerySingleAsync<string?>("SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId }));
        }

        var scenarios = new[] { "other-failure", "transient", "not-found", "not-found" };
        for (var index = 0; index < deckIds.Length; index++)
        {
            if (index == 3) Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
            var session = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckIds[index], scenarios[index], entries), new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(100));
            var attempts = scenarios[index] == "transient" ? 3 : 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                await session.RunAsync(TimeSpan.FromMilliseconds(1));
            }
        }

        foreach (var deckId in deckIds)
        {
            var row = await connection.QuerySingleAsync<(long Skipped, string Reason, string? Hash)>("SELECT skipped, skip_reason AS Reason, retention_core_hash AS Hash FROM deck_queue WHERE deck_id = @deckId", new { deckId });
            Assert.Equal((1L, DeckSkipReasons.Retention, deckId == "retention-d" ? "seeded-retention-hash" : hashes.Single(item => item.DeckId == deckId).Hash), row);
            Assert.Equal(1L, await ManifestCountAsync(connection, deckId));
            if (deckId == "retention-d") Assert.Equal(0L, await LiveRowCountAsync(connection, deckId));
        }
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
    }

    private static async Task VerifyFallbackAsync(DbConnection connection, RelationalDatabaseConnection info, CategoryKnowledgeRepository repository)
    {
        var entries = LegalCommanderDeck.Complete([new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        var deckIds = new[] { "fallback-first", "fallback-second" };
        await repository.AddDeckIdsAsync(deckIds);
        foreach (var deckId in deckIds)
        {
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
            await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: true, metadata: null, skipReason: DeckSkipReasons.FetchFailure);
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        Assert.Equal(2, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        var cardRepository = (CardCategoryRepository)typeof(CategoryKnowledgeRepository).GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(repository)!;
        var foldCalls = 0;
        cardRepository.FoldFault = (_, _) => { foldCalls++; if (foldCalls == 1) { throw new InvalidOperationException("Injected fold fault"); } };
        var session = new ArchidektDeckCacheSession(repository, new MultiStubDeckImporter(deckIds), new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(100));
        using var cancellation = new CancellationTokenSource();
        var processedDecks = 0;
        var progress = new SynchronousProgress<int>(_ =>
        {
            if (++processedDecks == deckIds.Length)
            {
                cancellation.Cancel();
            }
        });
        try
        {
            await session.RunAsync(TimeSpan.FromSeconds(60), cancellationToken: cancellation.Token, progress: progress);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        Assert.Equal(2, foldCalls);
        var first = await connection.QuerySingleAsync<(long Skipped, string Reason)>("SELECT skipped, skip_reason AS Reason FROM deck_queue WHERE deck_id = @deckId", new { deckId = deckIds[0] });
        Assert.Equal((1L, DeckSkipReasons.FetchFailure), first);
        Assert.Equal(0L, await ManifestCountAsync(connection, deckIds[0]));
        Assert.Equal(3L, await LiveRowCountAsync(connection, deckIds[0]));
        var second = await connection.QuerySingleAsync<(long Skipped, string Reason)>("SELECT skipped, skip_reason AS Reason FROM deck_queue WHERE deck_id = @deckId", new { deckId = deckIds[1] });
        Assert.Equal((1L, DeckSkipReasons.NotFound), second);
        Assert.Equal(1L, await ManifestCountAsync(connection, deckIds[1]));
        Assert.Equal(0L, await LiveRowCountAsync(connection, deckIds[1]));
    }

    private static async Task VerifyUnstoredAsync(DbConnection connection, CategoryKnowledgeRepository repository)
    {
        const string deckId = "refetch-unstored";
        const string directDeckId = "refetch-unstored-direct";
        await repository.AddDeckIdsAsync([deckId, directDeckId]);
        var session = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckId, "not-found", []), new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(100));
        var result = await session.RunAsync(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, result.DecksSkipped);
        var row = await connection.QuerySingleAsync<(long Processed, long Skipped, string Reason)>("SELECT processed, skipped, skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId });
        Assert.Equal((1L, 1L, DeckSkipReasons.NotFound), row);
        Assert.Equal(0L, await ManifestCountAsync(connection, deckId));
        Assert.Equal(0L, await LiveRowCountAsync(connection, deckId));
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(0, await repository.RetireNotFoundDeckAsync(directDeckId));
        var direct = await connection.QuerySingleAsync<(long Processed, long Skipped, string Reason)>("SELECT processed, skipped, skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId = directDeckId });
        Assert.Equal((1L, 1L, DeckSkipReasons.NotFound), direct);
        Assert.Equal(0L, await ManifestCountAsync(connection, directDeckId));
        Assert.Equal(0L, await LiveRowCountAsync(connection, directDeckId));
    }

    private static Task<long> ManifestCountAsync(DbConnection connection, string deckId) => connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM deck_category_archive_manifest WHERE deck_queue_id = (SELECT id FROM deck_queue WHERE deck_id = @deckId)", new { deckId });
    private static Task<long> LiveRowCountAsync(DbConnection connection, string deckId) => connection.QuerySingleAsync<long>("SELECT COUNT(1) FROM sources s JOIN card_deck_totals t ON t.source_id = s.id WHERE s.source = @source", new { source = "archidekt_live:" + deckId });

    private static async Task WithSqliteAsync(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), $"refetch_convergence_{Guid.NewGuid():N}.db");
        var info = new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        try { await using var connection = new SqliteConnection(info.ConnectionString); await connection.OpenAsync(); await VerifyAsync(connection, info, scenario); }
        finally { File.Delete(path); }
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private sealed class StubDeckImporter(string deckId, string scenario, IReadOnlyList<DeckEntry> entries) : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string id, CancellationToken cancellationToken = default) { ThrowIfConfigured(id); return Task.FromResult(entries.ToList()); }
        public Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string id, CancellationToken cancellationToken = default) { ThrowIfConfigured(id); return Task.FromResult(LegalCommanderDeck.Result(entries, DateTimeOffset.Parse("2027-01-01T00:00:00Z"))); }
        private void ThrowIfConfigured(string id)
        {
            Assert.Equal(deckId, id);
            if (scenario == "not-found") throw new ArchidektDeckNotFoundException(id, "Failed to import Archidekt deck " + id + ". Status: 404.");
            if (scenario == "transient") throw new ArchidektTransientFailureException("transient");
            if (scenario == "other-failure") throw new InvalidOperationException("returned 403");
        }
    }

    private sealed class MultiStubDeckImporter(IReadOnlyList<string> deckIds) : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string id, CancellationToken cancellationToken = default) => throw new ArchidektDeckNotFoundException(id, "Failed to import Archidekt deck " + id + ". Status: 404.");

        public Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string id, CancellationToken cancellationToken = default)
        {
            Assert.Contains(id, deckIds);
            throw new ArchidektDeckNotFoundException(id, "Failed to import Archidekt deck " + id + ". Status: 404.");
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
