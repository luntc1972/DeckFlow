using System.Data.Common;
using Dapper;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class RefetchConvergenceTests
{
    private const string DeckNotFoundTemplate = "Harvest.DeckNotFound deckId={DeckId} disposition={Disposition}";

    [Fact] public Task NotFound_CountedDeck_DropsOutOfCommanderCounts() => WithSqliteAsync("counted");
    [Fact] public Task Refetch_ThenNotFound_ArchivesAndLeavesNoCandidate() => WithSqliteAsync("not-found");
    [Fact] public Task Refetch_ThenSuccess_CapturesMetadataAndLeavesNoCandidate() => WithSqliteAsync("success");
    [Fact] public Task Refetch_ThenTransientThreeTimes_StaysRetryableAndNotArchived() => WithSqliteAsync("transient");
    [Fact] public Task Refetch_ThenOtherFailure_FetchFailureNotArchived() => WithSqliteAsync("other-failure");
    [Fact] public Task NotFound_UnstoredDeck_MarkedWithoutManifest() => WithSqliteAsync("unstored");
    [Fact] public Task RetentionArchived_RequeuedThenFails_KeepsRetentionReason() => WithSqliteAsync("retention");
    [Fact] public Task NotFoundArchived_Reappears_UnarchivesOnReentry() => WithSqliteAsync("reappear");
    [Fact] public Task NotFound_RetireFails_MarksNotFoundAndContinues() => WithSqliteAsync("fallback");
    [Fact] public Task NotFound_ProgressThrowsAfterRetirement_SkipsOnceWithoutFallback() => WithSqliteAsync("retired-progress-failure");

    [Fact]
    public void RetentionPreservingSkipReasonSql_UsesOriginalSqlText()
    {
        Assert.Equal(
            "CASE WHEN retention_core_hash IS NOT NULL AND @skipReason IN ('fetch_failure', 'not_found') THEN 'retention' ELSE @skipReason END",
            DeckQueueRepository.RetentionPreservingSkipReasonSql);
    }

    internal static async Task VerifyAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        if (scenario == "counted")
        {
            await VerifyCountedAsync(connection, repository);
            return;
        }
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
        if (scenario == "retired-progress-failure")
        {
            await VerifyRetiredProgressFailureAsync(connection, repository);
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
        Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        var importerScenario = scenario == "reappear" ? "not-found" : scenario;
        var logger = new FakeStructuredLogger();
        var session = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckId, importerScenario, entries), new EmptyRecentDecksImporter(), logger: logger, idlePollDelay: TimeSpan.FromMilliseconds(100));
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
        }
        else if (scenario is "success" or "reappear")
        {
            Assert.Equal(1L, row.Processed); Assert.Equal(0L, row.Skipped); Assert.Equal(1L, row.Live); Assert.Equal(1L, row.Metadata); Assert.Equal(0, manifests); Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
        }
        else
        {
            Assert.Equal(scenario == "transient" ? 0L : 1L, row.Processed); Assert.Equal(1L, row.Skipped); Assert.Equal(DeckSkipReasons.FetchFailure, row.Reason); Assert.Equal(1L, row.Live); Assert.Equal(0, manifests); Assert.Equal(3, liveRows); Assert.Equal(1, await repository.CountRefetchCandidatesAsync());
            if (scenario == "transient") Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
        }

        var notFoundEntries = logger.DeckNotFoundEntries();
        if (importerScenario == "not-found")
        {
            AssertDeckNotFoundLog(Assert.Single(notFoundEntries), deckId, "archived");
        }
        else
        {
            Assert.Empty(notFoundEntries);
        }
    }

    private static async Task VerifyCountedAsync(DbConnection connection, CategoryKnowledgeRepository repository)
    {
        const string deckId = "counted-a";
        const string otherDeckId = "counted-b";
        const string cardName = "Sol Ring";
        const string category = "Ramp";
        var entries = LegalCommanderDeck.Complete([new DeckEntry { Name = cardName, NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = category }]);
        await repository.AddDeckIdsAsync([deckId, otherDeckId]);
        foreach (var id in new[] { deckId, otherDeckId })
        {
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + id, entries);
            await repository.MarkDeckProcessedAsync(id, LegalCommanderDeck.CommanderName, skip: false, LegalCommanderDeck.Metadata());
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);

        const string summarySql = "SELECT deck_count FROM processed_commander_summary WHERE commander_name = @name";
        var summaryParameters = new { name = LegalCommanderDeck.CommanderName };
        var queueId = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId", new { deckId });
        var totals = await repository.GetCardDeckTotalsAsync(cardName);
        Assert.Equal(2L, await connection.QuerySingleAsync<long>(summarySql, summaryParameters));
        var beforeRows = await repository.GetCategoryRowsForCommanderAsync(LegalCommanderDeck.CommanderName);
        Assert.Equal(2, beforeRows.Single(row => row.CardName == cardName && row.Category == category).DeckCount);
        var beforeMembership = await repository.GetCategoryDeckMembershipForCommanderAsync(LegalCommanderDeck.CommanderName);
        var beforeMatches = beforeMembership.Where(row => row.CardName == cardName && row.Category == category).ToArray();
        Assert.Equal(2, beforeMatches.Length);
        Assert.Contains(beforeMatches, row => row.DeckId == queueId);

        Assert.Equal(1, await repository.RetireNotFoundDeckAsync(deckId));
        var afterSummary = await connection.QuerySingleAsync<long>(summarySql, summaryParameters);
        var afterRows = await repository.GetCategoryRowsForCommanderAsync(LegalCommanderDeck.CommanderName);
        var afterMembership = await repository.GetCategoryDeckMembershipForCommanderAsync(LegalCommanderDeck.CommanderName);
        var afterMatches = afterMembership.Where(row => row.CardName == cardName && row.Category == category).ToArray();
        Assert.Multiple(
            () => Assert.Equal(1L, afterSummary),
            () => Assert.Equal(1, afterRows.Single(row => row.CardName == cardName && row.Category == category).DeckCount),
            () => Assert.Single(afterMatches),
            () => Assert.DoesNotContain(afterMatches, row => row.DeckId == queueId));

        var row = await connection.QuerySingleAsync<(long Processed, long Skipped, string Reason)>(
            "SELECT processed, skipped, skip_reason FROM deck_queue WHERE deck_id = @deckId", new { deckId });
        Assert.Equal((1L, 1L, DeckSkipReasons.NotFound), row);
        Assert.Equal(1L, await ManifestCountAsync(connection, deckId));
        Assert.Equal(0L, await LiveRowCountAsync(connection, deckId));
        Assert.Equal(totals.TotalDeckCount, (await repository.GetCardDeckTotalsAsync(cardName)).TotalDeckCount);
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
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

    private static async Task VerifyRetiredProgressFailureAsync(DbConnection connection, CategoryKnowledgeRepository repository)
    {
        const string deckId = "retired-progress-failure";
        var entries = LegalCommanderDeck.Complete([new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
        await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip: true, metadata: null, skipReason: DeckSkipReasons.FetchFailure);
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        Assert.Equal(1, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);

        var logger = new FakeStructuredLogger();
        var session = new ArchidektDeckCacheSession(repository, new MultiStubDeckImporter([deckId]), new EmptyRecentDecksImporter(), logger: logger);
        var progressCalls = 0;
        var progress = new SynchronousProgress<int>(_ =>
        {
            if (++progressCalls == 1)
            {
                // RunAsync handles this exception and returns the tally for inspection.
                throw new ArchidektTransientFailureException("Injected progress fault");
            }
        });

        var result = await session.RunAsync(TimeSpan.FromSeconds(1), progress: progress);

        Assert.Equal(1L, await ManifestCountAsync(connection, deckId));
        Assert.Multiple(
            () => Assert.Equal(1, result.DecksSkipped),
            () => Assert.Equal(1, progressCalls),
            () => Assert.Collection(logger.DeckNotFoundEntries(), entry => AssertDeckNotFoundLog(entry, deckId, "archived")));
    }

    private static async Task VerifyFallbackAsync(DbConnection connection, RelationalDatabaseConnection info, CategoryKnowledgeRepository repository)
    {
        var entries = LegalCommanderDeck.Complete([new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        const string countedDeckId = "fallback-counted";
        var deckIds = new[] { "fallback-first", "fallback-second", countedDeckId };
        await repository.AddDeckIdsAsync(deckIds);
        foreach (var deckId in deckIds)
        {
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
            var skip = deckId != countedDeckId;
            await repository.MarkDeckProcessedAsync(deckId, LegalCommanderDeck.CommanderName, skip, metadata: null, skipReason: skip ? DeckSkipReasons.FetchFailure : null);
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);

        const string summarySql = "SELECT deck_count FROM processed_commander_summary WHERE commander_name = @name";
        var summaryParameters = new { name = LegalCommanderDeck.CommanderName };
        Assert.Equal(1L, await connection.QuerySingleAsync<long>(summarySql, summaryParameters));
        Assert.Equal(deckIds.Length, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);

        var cardRepository = (CardCategoryRepository)typeof(CategoryKnowledgeRepository).GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(repository)!;
        var foldCalls = 0;
        var foldException = new InvalidOperationException("Injected fold fault");
        cardRepository.FoldFault = (_, _) =>
        {
            if (++foldCalls != 2)
            {
                throw foldException;
            }
        };
        var logger = new FakeStructuredLogger();
        var session = new ArchidektDeckCacheSession(repository, new MultiStubDeckImporter(deckIds), new EmptyRecentDecksImporter(), logger: logger, idlePollDelay: TimeSpan.FromMilliseconds(100));

        var result = await session.RunAsync(TimeSpan.FromSeconds(2));

        // The two fallbacks and the successful archive each count as one skip.
        Assert.Equal(deckIds.Length, result.DecksSkipped);
        Assert.Equal(deckIds.Length, foldCalls);
        Assert.Collection(
            logger.DeckNotFoundEntries(),
            entry => AssertDeckNotFoundLog(entry, deckIds[0], "marked_archive_failed", foldException),
            entry => AssertDeckNotFoundLog(entry, deckIds[1], "archived"),
            entry => AssertDeckNotFoundLog(entry, countedDeckId, "marked_archive_failed", foldException));
        foreach (var deckId in new[] { deckIds[0], countedDeckId })
        {
            var row = await connection.QuerySingleAsync<(long Skipped, string Reason, long HasStoredLiveRows, string CommanderName)>(
                "SELECT skipped, skip_reason AS Reason, has_stored_live_rows, commander_name FROM deck_queue WHERE deck_id = @deckId", new { deckId });
            Assert.Equal((1L, DeckSkipReasons.NotFound, 1L, LegalCommanderDeck.CommanderName), row);
            Assert.Equal(0L, await ManifestCountAsync(connection, deckId));
            Assert.Equal(3L, await LiveRowCountAsync(connection, deckId));
        }

        Assert.Empty(await connection.QueryAsync<long>(summarySql, summaryParameters));
        Assert.Equal(0, await repository.CountRefetchCandidatesAsync());
        Assert.Equal(0, await repository.CountQueuedForRefetchAsync());
        Assert.Equal(0, (await repository.RunRefetchMarkBatchAsync(500)).DecksMarked);
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
        var logger = new FakeStructuredLogger();
        var session = new ArchidektDeckCacheSession(repository, new StubDeckImporter(deckId, "not-found", []), new EmptyRecentDecksImporter(), logger: logger, idlePollDelay: TimeSpan.FromMilliseconds(100));
        var result = await session.RunAsync(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, result.DecksSkipped);
        AssertDeckNotFoundLog(Assert.Single(logger.DeckNotFoundEntries()), deckId, "marked");
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

    private static void AssertDeckNotFoundLog(StructuredLogEntry entry, string deckId, string disposition, Exception? exception = null)
    {
        Assert.Equal(DeckNotFoundTemplate, entry.OriginalFormat);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(deckId, entry.Properties["DeckId"]);
        Assert.Equal(disposition, entry.Properties["Disposition"]);
        Assert.Same(exception, entry.Exception);
    }

    private sealed record StructuredLogEntry(
        LogLevel Level,
        Exception? Exception,
        string OriginalFormat,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class FakeStructuredLogger : ILogger
    {
        private readonly List<StructuredLogEntry> _entries = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> properties)
            {
                var values = properties.ToDictionary(property => property.Key, property => property.Value);
                if (values.Remove("{OriginalFormat}", out var format) && format is string originalFormat)
                {
                    _entries.Add(new StructuredLogEntry(logLevel, exception, originalFormat, values));
                }
            }
        }

        public IReadOnlyList<StructuredLogEntry> DeckNotFoundEntries() =>
            _entries.Where(entry => entry.OriginalFormat.StartsWith("Harvest.DeckNotFound ", StringComparison.Ordinal)).ToArray();
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
