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
    [Fact(Skip = "07-20 part B")] public Task NotFound_UnstoredDeck_MarkedWithoutManifest() => Task.CompletedTask;
    [Fact(Skip = "07-20 part B")] public Task RetentionArchived_RequeuedThenFails_KeepsRetentionReason() => Task.CompletedTask;
    [Fact(Skip = "07-20 part B")] public Task NotFoundArchived_Reappears_UnarchivesOnReentry() => Task.CompletedTask;
    [Fact(Skip = "07-20 part B")] public Task NotFound_RetireFails_FallsBackToFetchFailureAndContinues() => Task.CompletedTask;

    internal static async Task VerifyAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
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
        Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        var session = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckId, scenario, entries), new EmptyRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(100));
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
        else if (scenario == "success")
        {
            Assert.Equal(1L, row.Processed); Assert.Equal(0L, row.Skipped); Assert.Equal(1L, row.Live); Assert.Equal(1L, row.Metadata); Assert.Equal(0, manifests); Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
        }
        else
        {
            Assert.Equal(1L, row.Processed); Assert.Equal(1L, row.Skipped); Assert.Equal(DeckSkipReasons.FetchFailure, row.Reason); Assert.Equal(1L, row.Live); Assert.Equal(0, manifests); Assert.Equal(3, liveRows); Assert.Equal(1, await repository.CountRefetchCandidatesAsync());
            if (scenario == "transient") Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        }
    }

    private static async Task WithSqliteAsync(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), $"refetch_convergence_{Guid.NewGuid():N}.db");
        var info = new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        try { await using var connection = new SqliteConnection(info.ConnectionString); await connection.OpenAsync(); await VerifyAsync(connection, info, scenario); }
        finally { File.Delete(path); }
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

    private sealed class EmptyRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<ArchidektListingDeck>> ImportRecentListingPageAsync(int page, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ArchidektListingDeck>>([]);
    }
}
