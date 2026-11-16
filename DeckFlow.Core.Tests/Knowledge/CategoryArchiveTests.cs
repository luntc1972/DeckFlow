using System.Data.Common;
using Dapper;
using DeckFlow.Core.Models;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Covers additive archive schema upgrades and the reversible binary manifest.
/// </summary>
public sealed class CategoryArchiveTests
{
    [Fact]
    public void Read_LiveOnlyZeroCountCategory_ReturnsCategory()
    {
        var live = new[] { new CategoryDeckCountAggregation.LiveRow(1, 1, "mainboard", "Ramp", 0) };

        var counts = CategoryDeckCountAggregation.Read(live, []);

        Assert.Equal(0, Assert.Single(counts).Value);
        Assert.Contains("ramp", counts.Keys);
    }

    [Fact]
    public void Read_ArchiveRowsNettingToZero_OmitsCategory()
    {
        var archive = new[]
        {
            new CategoryDeckCountAggregation.ArchiveRow { CanonicalCategory = "ramp", IncludedDeckCount = 2, IncludedLabelRows = 1 },
            new CategoryDeckCountAggregation.ArchiveRow { CanonicalCategory = "ramp", IncludedDeckCount = -2, IncludedLabelRows = 1 }
        };

        Assert.Empty(CategoryDeckCountAggregation.Read([], archive));
    }

    [Fact]
    public Task RetentionDrain_CardDeckTotalsUnchanged() => WithArchiveSqliteAsync("totals");

    [Fact]
    public Task NonRetentionRemoval_WritesNoArchive() => WithArchiveSqliteAsync("purge");

    [Fact]
    public Task RetentionDrain_Twice_WritesOneManifest() => WithArchiveSqliteAsync("twice");

    [Fact]
    public Task R3H1_ArtifactOnlyDeckDrained_WithRampElsewhere_DeckCountsUnchanged() => WithArchiveSqliteAsync("r3h1");

    [Fact]
    public Task AliasRows_ArchiveMax_NotSum() => WithArchiveSqliteAsync("aliases");

    [Fact]
    public Task AliasLabels_PreservedByGetCategoriesAsync() => WithArchiveSqliteAsync("labels");

    [Fact]
    public Task SummaryRowAllLiveDrained_StaysQualifiedViaArchivedRows() => WithArchiveSqliteAsync("summary");

    [Fact]
    public Task QualifiedThreshold_CrossesFiveThroughArchivedRowsOnly() => WithArchiveSqliteAsync("threshold");

    [Fact]
    public Task RetentionDrain_AllFourCardReadersUnchanged_CommanderReadersDrop() => WithArchiveSqliteAsync("readers");

    private static async Task VerifyCardReadersAsync(DbConnection connection, CategoryKnowledgeRepository repository, string scenario)
    {
        async Task AddAsync(int id)
        {
            var deckId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
            new DeckEntry[]
            {
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = scenario == "labels" && id == 711 ? "ramp" : "Ramp" },
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "sideboard", Category = "Ramp" }
            }.Where(entry => scenario != "threshold" || entry.Board == "mainboard").ToArray());
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        }
        async Task DrainAsync()
        {
            await repository.RunStoredLiveRowsBackfillBatchAsync(100);
            var ids = await connection.QueryAsync<long>("SELECT id FROM deck_queue WHERE has_stored_live_rows = 1;");
            await repository.RemoveStoredDecksAsync(ids.Select(id => (id, DeckSkipReasons.Retention)).ToArray());
        }
        var count = scenario == "threshold" ? 4 : 5;
        for (var id = 711; id < 711 + count; id++)
        {
            await AddAsync(id);
        }
        var labels = await repository.GetCategoriesAsync("Sol Ring");
        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring");
        var qualified = await repository.GetCategoriesForNamesAsync(["Sol Ring"]);
        var commanderRows = await repository.GetCategoryRowsForCommanderAsync("Test Commander");
        var membership = await repository.GetCategoryDeckMembershipForCommanderAsync("Test Commander");
        Assert.Equal(count, (await repository.GetGlobalCategoryBaselineAsync()).TotalDecks);
        Assert.NotEmpty(commanderRows);
        Assert.NotEmpty(membership);
        await DrainAsync();
        if (scenario == "threshold")
        {
            Assert.Empty((await repository.GetCategoriesForNamesAsync(["Sol Ring"]))["Sol Ring"]);
            await AddAsync(720);
            Assert.Contains("Ramp", (await repository.GetCategoriesForNamesAsync(["Sol Ring"]))["Sol Ring"]);
            await DrainAsync();
        }
        else
        {
            Assert.Equal(labels, await repository.GetCategoriesAsync("Sol Ring"));
            Assert.Equal(counts.OrderBy(row => row.Key), (await repository.GetCategoryDeckCountsAsync("Sol Ring")).OrderBy(row => row.Key));
            var afterTotals = await repository.GetCardDeckTotalsAsync("Sol Ring");
            Assert.Equal(totals.TotalDeckCount, afterTotals.TotalDeckCount);
            Assert.Equal(totals.BoardDeckCounts.OrderBy(row => row.Key), afterTotals.BoardDeckCounts.OrderBy(row => row.Key));
            var afterQualified = await repository.GetCategoriesForNamesAsync(["Sol Ring"]);
            Assert.Equal(qualified["Sol Ring"], afterQualified["Sol Ring"]);
        }
        Assert.Equal(0, (await repository.GetGlobalCategoryBaselineAsync()).TotalDecks);
        Assert.Empty(await repository.GetCategoryRowsForCommanderAsync("Test Commander"));
        Assert.Empty(await repository.GetCategoryDeckMembershipForCommanderAsync("Test Commander"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM card_category_summary WHERE observation_rows <> 0;"));
        Assert.Equal(scenario == "threshold" ? 5 : 10, await connection.ExecuteScalarAsync<long>("SELECT SUM(archived_rows) FROM card_category_summary;"));
    }

    [Fact]
    public Task Reentry_ListingRefresh_UnarchivesAndWritesLive_NoDeckHasBoth() => WithArchiveSqliteAsync("reentry");

    [Fact]
    public Task Reentry_CanonicalizationChanged_RestoresExactly() => WithArchiveSqliteAsync("canonical");

    [Fact]
    public Task DrainThenRemoveArchived_ArchiveEqualsPreDrain() => WithArchiveSqliteAsync("remove");

    [Fact]
    public Task FaultMidUnarchive_RollsBackAll() => WithArchiveSqliteAsync("fault");

    internal static async Task VerifyReentryAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(["719"]);
        const string source = "archidekt_live:719";
        DeckEntry[] entries =
        [
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" },
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "sideboard", Category = "Artifact" }
        ];
        foreach (var deckId in new[] { "720", "721", "722", "723", "718" })
        {
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        }
        if (scenario != "canonical")
        {
            var baselineId = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = '718';");
            Assert.Equal(1, await repository.RemoveStoredDecksAsync([(baselineId, DeckSkipReasons.Retention)]));
        }
        var tables = new[] { "card_deck_totals_archive", "card_category_archive", "card_category_deck_archive", "deck_category_archive_manifest" };
        var before = new List<string>();
        foreach (var table in tables)
        {
            before.Add(await ReadSnapshotAsync(connection, table));
        }
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, entries);
        await repository.MarkDeckProcessedAsync("719", "Test Commander", skip: false);
        var id = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = '719';");
        Assert.Equal(1, await repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.Retention)]));
        if (scenario == "canonical")
        {
            // Why: a recorded key that today's canonicalizer would not produce proves exact reversal.
            await connection.ExecuteAsync("UPDATE card_category_deck_archive SET canonical_category = 'historical-key';");
            var row = await connection.QuerySingleAsync<(int Version, byte[] Payload)>("SELECT format_version, payload FROM deck_category_archive_manifest;");
            var manifest = DeckCategoryArchiveManifest.Decode(row.Version, row.Payload);
            var changed = manifest with { Contributions = manifest.Contributions.Select(value => (value.CardId, "historical-key", value.Board, value.IncludedDeckCount, value.FallbackDeckCount, value.IncludedLabelRows)).ToArray() };
            await connection.ExecuteAsync("UPDATE deck_category_archive_manifest SET payload = @payload;", new { payload = changed.Encode() });
        }
        if (scenario == "remove")
        {
            await repository.RemoveArchivedDecksAsync([(id, DeckSkipReasons.NotCommanderFormat)]);
            Assert.Equal(DeckSkipReasons.NotCommanderFormat, await connection.QuerySingleAsync<string>("SELECT skip_reason FROM deck_queue WHERE id = @id;", new { id }));
            Assert.Null(await connection.ExecuteScalarAsync<string?>("SELECT retention_core_hash FROM deck_queue WHERE id = @id;", new { id }));
        }
        else if (scenario == "fault")
        {
            var cardRepository = (CardCategoryRepository)typeof(CategoryKnowledgeRepository).GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(repository)!;
            var snapshots = new List<string>();
            var rollbackTables = tables.Concat(new[] { "card_category_summary", "card_category_qualified", "card_category_observations", "card_deck_totals", "deck_queue" }).ToArray();
            foreach (var table in rollbackTables)
            {
                snapshots.Add(await ReadSnapshotAsync(connection, table));
            }
            cardRepository.UnarchiveFault = () => throw new InvalidOperationException("Injected unarchive fault");
            await Assert.ThrowsAsync<InvalidOperationException>(() => DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, entries));
            for (var index = 0; index < rollbackTables.Length; index++)
            {
                Assert.Equal(snapshots[index], await ReadSnapshotAsync(connection, rollbackTables[index]));
            }
            return;
        }
        else
        {
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, entries);
            Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_category_archive_manifest m JOIN sources s ON s.deck_queue_id = m.deck_queue_id WHERE EXISTS (SELECT 1 FROM card_category_observations o WHERE o.source_id = s.id) OR EXISTS (SELECT 1 FROM card_deck_totals t WHERE t.source_id = s.id);"));
            Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM card_category_observations o JOIN sources s ON s.id = o.source_id WHERE s.source = @source;", new { source }));
        }
        for (var index = 0; index < tables.Length; index++)
        {
            Assert.Equal(before[index], await ReadSnapshotAsync(connection, tables[index]));
        }
        var expectedArchived = scenario == "canonical" ? 0 : 2;
        Assert.Equal(expectedArchived, await connection.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(archived_rows), 0) FROM card_category_summary;"));
        Assert.Equal(expectedArchived, await connection.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(archived_rows), 0) FROM card_category_qualified;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM card_category_qualified;"));
    }

    [Fact]
    public Task LegalityPurge_OfArchivedDeck_RestoresArchive() => WithArchiveSqliteAsync("archived-purge");

    [Fact]
    public Task CountPurgeCandidates_IncludesArchivedIllegalDecks() => WithArchiveSqliteAsync("archived-count");

    [Fact]
    public Task RunPurge_MaxDecksSpansLiveAndArchived_NoOvershoot() => WithArchiveSqliteAsync("archived-max");

    [Fact]
    public Task RecomputeInvariant_LivePlusArchive_EqualsFromScratch() => WithArchiveSqliteAsync("recompute");

    [Fact]
    public Task CategoryCounts_ConcurrentFold_DoesNotDoubleCount() => WithArchiveSqliteAsync("counts-race");

    [Fact]
    public Task MultipleLiveSources_RejectFold() => WithArchiveSqliteAsync("multi-source");

    [Theory]
    [InlineData("null")]
    [InlineData("unsupported")]
    [InlineData("retention")]
    [InlineData("duplicate")]
    public async Task RemoveArchivedDecks_InvalidInput_Throws(string scenario)
    {
        var repository = new CategoryKnowledgeRepository("unused-invalid-input.db");
        if (scenario == "null")
            await Assert.ThrowsAsync<ArgumentNullException>(() => repository.RemoveArchivedDecksAsync(null!));
        else
        {
            IReadOnlyList<(long DeckQueueId, string SkipReason)> decks = scenario switch
            {
                "unsupported" => [(1, "invalid")],
                "retention" => [(1, DeckSkipReasons.Retention)],
                _ => [(1, DeckSkipReasons.NoCommander), (1, DeckSkipReasons.NotCommanderFormat)]
            };
            await Assert.ThrowsAsync<ArgumentException>(() => repository.RemoveArchivedDecksAsync(decks));
        }
    }

    [Fact]
    public Task FaultMidFold_RollsBackAll() => WithArchiveSqliteAsync("fold-fault");

    [Fact]
    public Task IllegalUrlRejection_ArchivedDeck_ReversesArchive() => WithArchiveSqliteAsync("reject-url");

    [Fact]
    public Task IllegalHarvestReplacement_ArchivedDeck_ReversesArchive() => WithArchiveSqliteAsync("reject-harvest");

    [Fact]
    public Task ArchivedPurge_ReharvestedDeck_PreservesLiveState() => WithArchiveSqliteAsync("stale-purge");

    [Fact]
    public Task ArchivedPurge_ResetDeck_PreservesLiveState() => WithArchiveSqliteAsync("stale-reset");

    private static async Task VerifyArchiveRejectionAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        await repository.EnsureSchemaAsync();
        var archiveTables = new[] { "card_category_archive", "card_category_deck_archive", "card_deck_totals_archive", "deck_category_archive_manifest" };
        var baseline = new List<string>();
        foreach (var table in archiveTables)
        {
            baseline.Add(await ReadSnapshotAsync(connection, table));
        }
        const string deckId = "891";
        const string source = "archidekt_live:" + deckId;
        DeckEntry[] entries = [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }];
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, entries);
        await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        var id = await connection.ExecuteScalarAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId;", new { deckId });
        await repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.Retention)]);
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_category_archive_manifest;"));
        if (scenario.StartsWith("stale-", StringComparison.Ordinal))
        {
            // Reproduce a candidate selected before another transaction re-harvested the deck.
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, entries);
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
            await connection.ExecuteAsync("UPDATE deck_queue SET content_hash = 'fresh', processed = @processed, skipped = 0 WHERE id = @id;", new { id, processed = scenario == "stale-reset" ? 0 : 1 });
            var tables = new[] { "deck_queue", "card_category_observations", "card_deck_totals", "card_category_summary", "card_category_qualified" };
            var snapshots = new List<string>();
            foreach (var table in tables)
            {
                snapshots.Add(await ReadSnapshotAsync(connection, table));
            }
            await repository.RemoveArchivedDecksAsync([(id, DeckSkipReasons.NotCommanderFormat)]);
            for (var index = 0; index < tables.Length; index++)
            {
                Assert.Equal(snapshots[index], await ReadSnapshotAsync(connection, tables[index]));
            }
            return;
        }
        if (scenario == "reject-url")
        {
            await repository.RejectUrlDeckAsync("archidekt_url:https://archidekt.com/decks/891", deckId, "Test Commander", null, DeckSkipReasons.WrongSize);
        }
        else
        {
            // The harvest legality gate writes an empty batch before marking the deck skipped.
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, Array.Empty<DeckEntry>());
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: true, skipReason: DeckSkipReasons.WrongSize);
        }
        for (var index = 0; index < archiveTables.Length; index++)
        {
            Assert.Equal(baseline[index], await ReadSnapshotAsync(connection, archiveTables[index]));
        }
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(archived_rows), 0) FROM card_category_summary;"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(archived_rows), 0) FROM card_category_qualified;"));
        Assert.Empty(await repository.GetCategoriesAsync("Sol Ring"));
        Assert.Empty(await repository.GetCategoryDeckCountsAsync("Sol Ring"));
        Assert.Equal(0, (await repository.GetCardDeckTotalsAsync("Sol Ring")).TotalDeckCount);
    }

    internal static async Task VerifyArchivedPurgeAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var repository = new CategoryKnowledgeRepository(info);
        await repository.EnsureSchemaAsync();
        var tables = new[] { "card_category_observations", "card_deck_totals", "card_category_summary", "card_category_qualified", "card_category_archive", "card_category_deck_archive", "card_deck_totals_archive", "deck_category_archive_manifest", "deck_queue" };
        var baseline = new List<string>();
        foreach (var table in tables.Skip(4).Take(4))
        {
            baseline.Add(await ReadSnapshotAsync(connection, table));
        }
        foreach (var deckId in new[] { "801", "802", "803" })
        {
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
            [
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = deckId == "802" ? "Artifact" : "Ramp" },
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "sideboard", Category = "Artifact" }
            ]);
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        var labels = await repository.GetCategoriesAsync("Sol Ring");
        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring");
        var qualified = (await repository.GetCategoriesForNamesAsync(["Sol Ring"]))["Sol Ring"];
        var ids = (await connection.QueryAsync<long>("SELECT id FROM deck_queue ORDER BY id;")).ToArray();
        if (scenario is "counts-race" or "multi-source")
        {
            var cardRepository = (CardCategoryRepository)typeof(CategoryKnowledgeRepository).GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(repository)!;
            if (scenario == "multi-source")
            {
                await connection.ExecuteAsync("INSERT INTO sources (source, deck_queue_id) VALUES ('archidekt_live:duplicate', @id);", new { id = ids[0] });
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RemoveStoredDecksAsync([(ids[0], DeckSkipReasons.Retention)]));
            }
            else
            {
                cardRepository.CategoryCountsRead = async () => { await repository.RemoveStoredDecksAsync([(ids[0], DeckSkipReasons.Retention)]); };
                var actual = await repository.GetCategoryDeckCountsAsync("Sol Ring");
                Assert.Equal(counts.OrderBy(row => row.Key), actual.OrderBy(row => row.Key));
            }
            return;
        }
        if (scenario == "fold-fault")
        {
            var snapshots = new List<string>();
            foreach (var table in tables)
            {
                snapshots.Add(await ReadSnapshotAsync(connection, table));
            }
            var cardRepository = (CardCategoryRepository)typeof(CategoryKnowledgeRepository).GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(repository)!;
            cardRepository.FoldFault = (foldConnection, transaction) =>
            {
                Assert.Equal(0, foldConnection.ExecuteScalar<long>("SELECT COUNT(*) FROM card_category_observations o JOIN sources s ON s.id = o.source_id WHERE s.deck_queue_id = @id;", new { id = ids[0] }, transaction));
                Assert.Equal(0, foldConnection.ExecuteScalar<long>("SELECT COUNT(*) FROM card_deck_totals t JOIN sources s ON s.id = t.source_id WHERE s.deck_queue_id = @id;", new { id = ids[0] }, transaction));
                throw new InvalidOperationException("Injected fold fault");
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RemoveStoredDecksAsync([(ids[0], DeckSkipReasons.Retention)]));
            for (var index = 0; index < tables.Length; index++)
            {
                Assert.Equal(snapshots[index], await ReadSnapshotAsync(connection, tables[index]));
            }
            return;
        }
        await repository.RemoveStoredDecksAsync([(ids[1], DeckSkipReasons.Retention)]);
        await repository.RemoveStoredDecksAsync([(ids[2], DeckSkipReasons.Retention)]);
        if (scenario == "recompute")
        {
            Assert.Equal(labels, await repository.GetCategoriesAsync("Sol Ring"));
            Assert.Equal(counts.OrderBy(row => row.Key), (await repository.GetCategoryDeckCountsAsync("Sol Ring")).OrderBy(row => row.Key));
            var after = await repository.GetCardDeckTotalsAsync("Sol Ring");
            Assert.Equal(totals.TotalDeckCount, after.TotalDeckCount);
            Assert.Equal(totals.BoardDeckCounts.OrderBy(row => row.Key), after.BoardDeckCounts.OrderBy(row => row.Key));
            Assert.Equal(qualified, (await repository.GetCategoriesForNamesAsync(["Sol Ring"]))["Sol Ring"]);
            Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_category_archive_manifest m JOIN sources s ON s.deck_queue_id = m.deck_queue_id JOIN card_category_observations o ON o.source_id = s.id WHERE s.source LIKE 'archidekt_live:%';"));
            return;
        }
        await connection.ExecuteAsync("UPDATE deck_queue SET archidekt_metadata_captured_utc = '2026-01-01T00:00:00Z', archidekt_deck_format = 2;");
        if (scenario == "archived-count")
        {
            await connection.ExecuteAsync("UPDATE deck_queue SET archidekt_deck_format = 3 WHERE id = @id;", new { id = ids[2] });
            Assert.Equal(2, await repository.CountPurgeCandidatesAsync());
            Assert.Equal(ids[0], Assert.Single(await repository.GetPurgeCandidatesAsync()).DeckQueueId);
            return;
        }
        var service = new DeckPruningService(repository, new DeckPruningOptions { BatchSize = 10, Pause = TimeSpan.Zero }, _ => Task.FromResult<long?>(long.MaxValue));
        var result = await service.RunPurgeAsync(scenario == "archived-max" ? 2 : null);
        Assert.Equal(scenario == "archived-max" ? 2 : 3, result.DecksRemoved);
        Assert.Equal(scenario == "archived-max" ? DeckPruningStopReason.MaxDecksReached : DeckPruningStopReason.Completed, result.StopReason);
        Assert.Equal(scenario == "archived-max" ? 1 : 0, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_category_archive_manifest;"));
        if (scenario == "archived-purge")
        {
            for (var index = 0; index < 4; index++)
            {
                Assert.Equal(baseline[index], await ReadSnapshotAsync(connection, tables[index + 4]));
            }
            Assert.Empty(await repository.GetCategoriesAsync("Sol Ring"));
            Assert.Equal(0, await repository.CountPurgeCandidatesAsync());
        }
    }

    [Fact]
    public Task UrlImport_ArchivedDeck_RefusedAndNothingWritten() => WithArchiveSqliteAsync("url-archived");

    [Fact]
    public Task UrlImport_NonArchivedDeck_MatchesControllerWrites() => WithArchiveSqliteAsync("url-happy");

    internal static async Task VerifyUrlImportAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        var cancellationToken = CancellationToken.None;
        var repository = new CategoryKnowledgeRepository(info);
        await repository.EnsureSchemaAsync(cancellationToken);
        await repository.AddDeckIdsAsync(["719"], cancellationToken);
        DeckEntry[] entries =
        [
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 2, Board = " Mainboard ", Category = "Ramp, Artifact" },
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" },
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "SIDEBOARD", Category = "Artifact" }
        ];
        const string source = "archidekt_url:https://archidekt.com/decks/719";
        if (scenario == "url-happy")
        {
            var counts = new Dictionary<(string Name, string Category, string Board), int>();
            foreach (var entry in entries)
            {
                foreach (var category in DeckFlow.Core.Reporting.CategoryKnowledgeReporter.SplitCategories(entry.Category))
                {
                    var key = (entry.Name, category, entry.Board.Trim().ToLowerInvariant());
                    counts[key] = counts.GetValueOrDefault(key) + entry.Quantity;
                }
            }
            foreach (var item in counts)
            {
                await repository.PersistObservedCategoriesAsync("expected", item.Key.Name, [item.Key.Category], item.Value, item.Key.Board, 1, cancellationToken);
            }
            Assert.False((await repository.PersistUrlImportDeckAsync("719", source, entries, cancellationToken)).Archived);
            const string sql = "SELECT o.card_name, o.category, o.board, o.count, o.deck_count FROM card_category_observations o JOIN sources s ON s.id = o.source_id WHERE s.source = @source ORDER BY o.card_name, o.category, o.board;";
            var expected = (await connection.QueryAsync(sql, new { source = "expected" })).Select(row => System.Text.Json.JsonSerializer.Serialize((object)row)).ToArray();
            var actual = (await connection.QueryAsync(sql, new { source })).Select(row => System.Text.Json.JsonSerializer.Serialize((object)row)).ToArray();
            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM card_deck_totals;"));
            Assert.Null(await connection.ExecuteScalarAsync<long?>("SELECT deck_queue_id FROM sources WHERE source = @source;", new { source }));
            return;
        }
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:719", entries, cancellationToken);
        await repository.MarkDeckProcessedAsync("719", "Test Commander", skip: false, cancellationToken: cancellationToken);
        var id = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = '719';");
        if (scenario == "url-archived")
        {
            Assert.Equal(1, await repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.Retention)], cancellationToken));
            var tables = new[] { "card_category_archive", "card_category_deck_archive", "card_deck_totals_archive", "deck_category_archive_manifest", "card_category_summary", "card_category_qualified" };
            var before = new List<string>();
            foreach (var table in tables)
            {
                before.Add(await ReadSnapshotAsync(connection, table));
            }
            Assert.True((await repository.PersistUrlImportDeckAsync("719", source, entries, cancellationToken)).Archived);
            for (var index = 0; index < tables.Length; index++)
            {
                Assert.Equal(before[index], await ReadSnapshotAsync(connection, tables[index]));
            }
        }
        else
        {
            var cardRepository = (CardCategoryRepository)typeof(CategoryKnowledgeRepository).GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(repository)!;
            using var raceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task PauseAsync()
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(raceTimeout.Token);
            }
            var foldFirst = scenario == "url-fold-first";
            if (foldFirst)
            {
                cardRepository.FoldAfterArchiveLocks = PauseAsync;
            }
            else
            {
                cardRepository.UrlImportBeforeWrite = PauseAsync;
            }
            Task<int>? fold = null;
            Task<UrlImportPersistResult>? import = null;
            try
            {
                if (foldFirst)
                {
                    fold = repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.Retention)], cancellationToken);
                }
                else
                {
                    import = repository.PersistUrlImportDeckAsync("719", source, entries, cancellationToken);
                }
                await entered.Task.WaitAsync(raceTimeout.Token);
                fold ??= repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.Retention)], cancellationToken);
                import ??= repository.PersistUrlImportDeckAsync("719", source, entries, cancellationToken);
                // Observe the second transaction waiting on the archive lock before releasing the first.
                while (!await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT EXISTS (SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid = l.pid WHERE l.locktype = 'advisory' AND l.classid::bigint = @lockClass AND l.objid::bigint = @id AND l.objsubid = 2 AND a.application_name = current_setting('application_name') AND NOT l.granted);",
                    new { lockClass = CardCategoryRepository.DeckArchiveLockClass, id }, cancellationToken: raceTimeout.Token)))
                {
                    await Task.Delay(20, raceTimeout.Token);
                }
                Assert.False(foldFirst ? import.IsCompleted : fold.IsCompleted);
            }
            finally
            {
                release.TrySetResult();
                if (fold is not null)
                {
                    await fold.WaitAsync(raceTimeout.Token);
                }
                if (import is not null)
                {
                    await import.WaitAsync(raceTimeout.Token);
                }
            }
            Assert.Equal(1, await fold!);
            Assert.Equal(foldFirst, (await import!).Archived);
        }
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM deck_category_archive_manifest;"));
        Assert.Equal(scenario == "url-import-first" ? 1 : 0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sources WHERE source = @source;", new { source }));
        Assert.Equal(scenario == "url-import-first" ? 1 : 0, await connection.ExecuteScalarAsync<int>("SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END FROM card_category_observations o JOIN sources s ON s.id = o.source_id WHERE s.source = @source;", new { source }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM card_deck_totals t JOIN sources s ON s.id = t.source_id WHERE s.source = @source;", new { source }));
    }

    private static async Task WithArchiveSqliteAsync(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), $"archive_drain_{Guid.NewGuid():N}.db");
        var info = new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite,
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        try
        {
            await using var connection = new SqliteConnection(info.ConnectionString);
            await connection.OpenAsync();
            await VerifyDrainAsync(connection, info, scenario);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static async Task VerifyDrainAsync(DbConnection connection, RelationalDatabaseConnection info, string scenario)
    {
        if (scenario is "reject-url" or "reject-harvest" or "stale-purge" or "stale-reset")
        {
            await VerifyArchiveRejectionAsync(connection, info, scenario);
            return;
        }
        if (scenario.StartsWith("url-", StringComparison.Ordinal))
        {
            await VerifyUrlImportAsync(connection, info, scenario);
            return;
        }
        if (scenario is "archived-purge" or "archived-count" or "archived-max" or "recompute" or "fold-fault" or "counts-race" or "multi-source")
        {
            await VerifyArchivedPurgeAsync(connection, info, scenario);
            return;
        }
        if (scenario is "reentry" or "canonical" or "remove" or "fault")
        {
            await VerifyReentryAsync(connection, info, scenario);
            return;
        }
        var repository = new CategoryKnowledgeRepository(info);
        await repository.EnsureSchemaAsync();
        if (scenario is "labels" or "summary" or "threshold" or "readers")
        {
            await VerifyCardReadersAsync(connection, repository, scenario);
            return;
        }
        if (scenario is "r3h1" or "aliases")
        {
            await VerifyDeckCountsAsync(connection, repository, scenario);
            return;
        }
        foreach (var deckId in new[] { "711", "712" })
        {
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
            [
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" },
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "sideboard", Category = "Artifact" }
            ]);
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        var id = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = '711';");
        if (scenario == "purge")
        {
            Assert.Equal(1, await repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.NotCommanderFormat)]));
            foreach (var table in new[] { "card_category_archive", "card_deck_totals_archive", "card_category_deck_archive", "deck_category_archive_manifest" })
            {
                Assert.Equal(0, await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table};"));
            }
            return;
        }
        var before = await repository.GetCardDeckTotalsAsync("Sol Ring");
        var service = new DeckPruningService(repository,
            new DeckPruningOptions { PerCommanderCap = 1, Pause = TimeSpan.Zero }, _ => Task.FromResult<long?>(long.MaxValue));
        await service.RunRetentionAsync();
        var after = await repository.GetCardDeckTotalsAsync("Sol Ring");
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_queue WHERE skip_reason = 'retention';"));
        if (scenario == "totals")
        {
            Assert.Equal(before.TotalDeckCount, after.TotalDeckCount);
            Assert.Equal(before.BoardDeckCounts.OrderBy(row => row.Key), after.BoardDeckCounts.OrderBy(row => row.Key));
            foreach (var board in before.BoardDeckCounts.Keys)
            {
                Assert.Equal(before.BoardDeckCounts[board], (await repository.GetCardDeckTotalsAsync("Sol Ring", board)).TotalDeckCount);
            }
        }
        await service.RunRetentionAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_category_archive_manifest;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT SUM(deck_count) FROM card_deck_totals_archive;"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT SUM(raw_rows) FROM card_category_archive;"));
        var payload = await connection.QuerySingleAsync<byte[]>("SELECT payload FROM deck_category_archive_manifest;");
        var manifest = DeckCategoryArchiveManifest.Decode(1, payload);
        Assert.Equal(2, manifest.Totals.Count);
        Assert.Equal(2, manifest.Observations.Count);
    }

    private static async Task VerifyDeckCountsAsync(DbConnection connection, CategoryKnowledgeRepository repository, string scenario)
    {
        foreach (var deckId in new[] { "711", "712" })
        {
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId,
            [
                new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = scenario == "r3h1" && deckId == "711" ? "Artifact" : "Ramp" }
            ]);
            await repository.MarkDeckProcessedAsync(deckId, "Test Commander", skip: false);
        }
        if (scenario == "aliases")
        {
            await connection.ExecuteAsync("INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, count, deck_count, last_seen_utc) SELECT source_id, card_id, card_name, 'ramp', board, count, deck_count, last_seen_utc FROM card_category_observations WHERE category = 'Ramp';");
        }
        await repository.RunStoredLiveRowsBackfillBatchAsync(100);
        var before = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        Assert.Equal(scenario == "aliases" ? 2 : 1, before["ramp"]);
        Assert.DoesNotContain("artifact", before.Keys);
        foreach (var deckId in new[] { "711", "712" })
        {
            var id = await connection.QuerySingleAsync<long>("SELECT id FROM deck_queue WHERE deck_id = @deckId;", new { deckId });
            Assert.Equal(1, await repository.RemoveStoredDecksAsync([(id, DeckSkipReasons.Retention)]));
            Assert.Equal(before.OrderBy(row => row.Key), (await repository.GetCategoryDeckCountsAsync("Sol Ring")).OrderBy(row => row.Key));
            var payload = await connection.QuerySingleAsync<byte[]>("SELECT payload FROM deck_category_archive_manifest WHERE deck_queue_id = @id;", new { id });
            var manifest = DeckCategoryArchiveManifest.Decode(1, payload);
            var contribution = Assert.Single(manifest.Contributions);
            Assert.Equal(scenario == "r3h1" && deckId == "711" ? 0 : 1, contribution.IncludedDeckCount);
            Assert.Equal(1, contribution.FallbackDeckCount);
            Assert.Equal(scenario == "aliases" ? 2 : contribution.IncludedDeckCount, contribution.IncludedLabelRows);
        }
    }

    [Fact]
    public void Manifest_RoundTrip_And_UnknownVersionThrows()
    {
        var observations = new (long CardId, string Category, string Board, int DeckCount)[]
        {
            (1, "Mana Ramp", "mainboard", 2), (1, "Pioche — 水", "sideboard", 3)
        };
        var totals = new (long CardId, string Board, int DeckCount)[] { (1, "mainboard", 2), (2, "sideboard", 3) };
        var contributions = new (long CardId, string CanonicalCategory, string Board, int IncludedDeckCount, int FallbackDeckCount, int IncludedLabelRows)[]
        {
            (1, "ramp", "mainboard", 2, 3, 4),
            (2, "artifact", "sideboard", 0, 1, 0)
        };
        var manifest = new DeckCategoryArchiveManifest(observations, totals, contributions);
        var payload = manifest.Encode();
        var decoded = DeckCategoryArchiveManifest.Decode(DeckCategoryArchiveManifest.FormatVersion, payload);
        Assert.Equal(observations, decoded.Observations);
        Assert.Equal(totals, decoded.Totals);
        Assert.Equal(contributions, decoded.Contributions);
        Assert.Throws<InvalidDataException>(() => DeckCategoryArchiveManifest.Decode(2, payload));
        var empty = new DeckCategoryArchiveManifest([], [], []);
        var emptyDecoded = DeckCategoryArchiveManifest.Decode(1, empty.Encode());
        Assert.Empty(emptyDecoded.Observations);
        Assert.Empty(emptyDecoded.Totals);
        Assert.Empty(emptyDecoded.Contributions);
    }

    [Fact]
    public async Task SchemaUpgrade_FromPre0717Populated_AddsArchiveAndArchivedRows()
    {
        await WithSqliteAsync(false);
    }

    [Fact]
    public async Task SchemaEnsure_RunTwice_IsIdempotent()
    {
        await WithSqliteAsync(true);
    }

    private static async Task WithSqliteAsync(bool ensureTwice)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"category_archive_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "cache.db"),
            Pooling = false
        }.ConnectionString;
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await VerifyUpgradeAsync(connection,
                new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, connectionString),
                new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, connectionString + ";Default Timeout=31"),
                directory, ensureTwice);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static async Task VerifyUpgradeAsync(DbConnection connection,
        RelationalDatabaseConnection database, RelationalDatabaseConnection secondDatabase,
        string directory, bool ensureTwice)
    {
        // Why: these are the populated pre-07-17 aggregate tables, before archived_rows existed.
        await ExecuteAsync(connection, """
            CREATE TABLE card_category_summary (
                card_id INTEGER NOT NULL, category TEXT NOT NULL, observation_rows INTEGER NOT NULL,
                PRIMARY KEY (card_id, category));
            CREATE TABLE card_category_qualified (
                card_id INTEGER NOT NULL, category TEXT NOT NULL, observation_rows INTEGER NOT NULL,
                PRIMARY KEY (card_id, category));
            INSERT INTO card_category_summary VALUES (1, 'Ramp', 5), (2, 'Draw', 4);
            INSERT INTO card_category_qualified VALUES (1, 'Ramp', 5);
            """);
        var schema = new CategoryCacheSchema(database, directory, null);
        await schema.EnsureSchemaAsync();
        foreach (var table in new[] { "card_category_summary", "card_category_qualified" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT card_id, category, observation_rows, archived_rows FROM {table} ORDER BY card_id;";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, Convert.ToInt64(reader.GetValue(0)));
            Assert.Equal("Ramp", reader.GetString(1));
            Assert.Equal(5L, Convert.ToInt64(reader.GetValue(2)));
            Assert.Equal(0L, Convert.ToInt64(reader.GetValue(3)));
            if (table == "card_category_summary")
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal(2L, Convert.ToInt64(reader.GetValue(0)));
                Assert.Equal("Draw", reader.GetString(1));
                Assert.Equal(4L, Convert.ToInt64(reader.GetValue(2)));
                Assert.Equal(0L, Convert.ToInt64(reader.GetValue(3)));
            }
            Assert.False(await reader.ReadAsync());
        }

        var rows = new Dictionary<string, string>
        {
            ["card_category_archive"] = "1, 'Mana Ramp', 'mainboard', 2",
            ["card_category_deck_archive"] = "1, 'ramp', 'mainboard', 1, 2, 3",
            ["card_deck_totals_archive"] = "1, 'mainboard', 2",
            ["deck_category_archive_manifest"] = "42, '2026-11-13T00:00:00Z', 1, @payload"
        };
        foreach (var (table, values) in rows)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText = $"INSERT INTO {table} VALUES ({values});";
            if (table == "deck_category_archive_manifest")
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@payload";
                parameter.Value = new byte[] { 1, 2, 3 };
                command.Parameters.Add(parameter);
            }
            await command.ExecuteNonQueryAsync();
            await Assert.ThrowsAnyAsync<DbException>(() => command.ExecuteNonQueryAsync());
        }

        if (ensureTwice)
        {
            // Why: a distinct connection-string cache key forces DDL to run again, rather than taking the fast path.
            var tables = rows.Keys.Concat(new[] { "card_category_summary", "card_category_qualified" }).ToArray();
            var snapshots = new Dictionary<string, string>();
            foreach (var table in tables)
            {
                snapshots[table] = await ReadSnapshotAsync(connection, table);
            }
            await new CategoryCacheSchema(secondDatabase, directory, null).EnsureSchemaAsync();
            foreach (var table in tables)
            {
                Assert.Equal(snapshots[table], await ReadSnapshotAsync(connection, table));
            }
            foreach (var table in rows.Keys)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM {table};";
                Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }
        }

        await ExecuteAsync(connection, """
            DELETE FROM card_category_qualified;
            UPDATE card_category_summary SET archived_rows = 1 WHERE card_id = 2;
            """);
        await schema.EnsureCardCategoryQualifiedBackfilledAsync();
        await using var backfill = connection.CreateCommand();
        backfill.CommandText = "SELECT observation_rows, archived_rows FROM card_category_qualified WHERE card_id = 2;";
        await using var backfilled = await backfill.ExecuteReaderAsync();
        Assert.True(await backfilled.ReadAsync());
        Assert.Equal(4L, Convert.ToInt64(backfilled.GetValue(0)));
        Assert.Equal(1L, Convert.ToInt64(backfilled.GetValue(1)));
    }

    private static async Task<string> ReadSnapshotAsync(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table} ORDER BY 1, 2;";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(System.Text.Json.JsonSerializer.Serialize(values));
        }
        return string.Join("\n", rows);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
