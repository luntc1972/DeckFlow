using Microsoft.Data.Sqlite;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="CategoryKnowledgeRepository"/> covering read, write,
/// and deduplication of card-category knowledge rows against a temporary SQLite database.
/// </summary>
public sealed class CategoryKnowledgeRepositoryTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public CategoryKnowledgeRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task AddDeckIdsAsync_ReturnsOnlyNovelIds()
    {
        var repository = CreateRepository();

        Assert.Equal(3, await repository.AddDeckIdsAsync(new[] { "one", "two", "three" }));
        Assert.Equal(0, await repository.AddDeckIdsAsync(new[] { "one", "two", "three" }));
        Assert.Equal(1, await repository.AddDeckIdsAsync(new[] { "one", "two", "four", "four" }));
    }

    [Fact]
    public async Task AddDeckIdsAsync_ProcessedRequeueIsNotNovel()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "processed" });
        await repository.MarkDecksProcessedAsync(new[] { "processed" });

        Assert.Equal(0, await repository.AddDeckIdsAsync(new[] { "processed" }));
    }

    [Fact]
    public async Task AddDeckIdsAsync_DoesNotRequeueRecentlyProcessedDeck()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "123" });
        await repository.MarkDecksProcessedAsync(new[] { "123" });
        await repository.AddDeckIdsAsync(new[] { "123" });

        var queuedIds = await repository.GetNextUnprocessedDeckIdsAsync(10);

        Assert.Empty(queuedIds);
    }

    [Fact]
    public async Task AddDeckIdsAsync_RequeuesDeckAfterCooldownExpires()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "123" });
        await repository.MarkDecksProcessedAsync(new[] { "123" });
        await SetLastCheckedUtcAsync("123", DateTimeOffset.UtcNow.AddDays(-6));

        await repository.AddDeckIdsAsync(new[] { "123" });
        var queuedIds = await repository.GetNextUnprocessedDeckIdsAsync(10);

        Assert.Single(queuedIds);
        Assert.Equal("123", queuedIds[0]);
    }

    [Fact]
    public async Task GetRecentDeckCrawlPageAsync_DefaultsToSecondPage()
    {
        var repository = CreateRepository();

        var page = await repository.GetRecentDeckCrawlPageAsync();

        Assert.Equal(2, page);
    }

    [Fact]
    public async Task SetRecentDeckCrawlPageAsync_PersistsPage()
    {
        var repository = CreateRepository();

        await repository.SetRecentDeckCrawlPageAsync(7);

        var page = await repository.GetRecentDeckCrawlPageAsync();
        Assert.Equal(7, page);
    }

    [Fact]
    public async Task HasSourceDataAsync_ReturnsTrue_WhenSourceRowsExist()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:123", "Sol Ring", new[] { "Ramp" });

        var exists = await repository.HasSourceDataAsync("archidekt_live:123");

        Assert.True(exists);
    }

    [Fact]
    public async Task DeleteSourceDataAsync_RemovesExistingSourceRows()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:123", "Sol Ring", new[] { "Ramp" });
        await repository.DeleteSourceDataAsync("archidekt_live:123");

        var exists = await repository.HasSourceDataAsync("archidekt_live:123");

        Assert.False(exists);
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM card_category_summary;";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PersistObservedCategoriesAsync_MaintainsSummaryForNewAndRepeatedObservation()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("edhrec", "Sol Ring", new[] { "Ramp" });
        await repository.PersistObservedCategoriesAsync("edhrec", "Sol Ring", new[] { "Ramp" });

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT observation_rows FROM card_category_summary WHERE category = 'Ramp';";

        Assert.Equal(1L, await command.ExecuteScalarAsync());
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task DeleteSourceDataAsync_OneOfTwoSources_KeepsOneSummaryObservation()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("summary-source-one", "Sol Ring", new[] { "Ramp" });
        await repository.PersistObservedCategoriesAsync("summary-source-two", "Sol Ring", new[] { "Ramp" });
        await repository.DeleteSourceDataAsync("summary-source-one");

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT observation_rows FROM card_category_summary WHERE category = 'Ramp';";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task DeleteSourceDataAsync_QualifiedRowCrossesThresholdAndIsRemoved()
    {
        var repository = CreateRepository();
        var sources = Enumerable.Range(0, CategoryCacheSchema.DefaultMinObservationRows)
            .Select(index => $"qualified-source-{index}")
            .ToArray();

        foreach (var source in sources)
        {
            await repository.PersistObservedCategoriesAsync(source, "Sol Ring", new[] { "Ramp" });
        }

        await AssertCategorySummaryMatchesObservationsAsync();
        await repository.DeleteSourceDataAsync(sources[0]);
        await AssertCategorySummaryMatchesObservationsAsync();

        foreach (var source in sources.Skip(1))
        {
            await repository.DeleteSourceDataAsync(source);
        }

        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task EnsureSchemaAsync_SqliteBackfillsDirectObservationWhenSummaryIsEmpty()
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { "schema-seed" });

        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO cards (normalized_card_name, display_name) VALUES ('direct summary card', 'Direct Summary Card'); INSERT INTO sources (source) VALUES ('direct-summary-source'); INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc) VALUES (1, 1, 'Direct Summary Card', 'Ramp', 'mainboard', 1, 1, '2026-01-01T00:00:00.0000000+00:00'); DELETE FROM card_category_summary;";
            await command.ExecuteNonQueryAsync();
        }

        var migrationPath = _databasePath + ".migration";
        File.Copy(_databasePath, migrationPath);
        await new CategoryKnowledgeRepository(migrationPath).GetCategoriesAsync("Direct Summary Card");

        await using var verifyConnection = new SqliteConnection($"Data Source={migrationPath}");
        await verifyConnection.OpenAsync();
        var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT observation_rows FROM card_category_summary WHERE category = 'Ramp';";
        Assert.Equal(1L, await verifyCommand.ExecuteScalarAsync());
    }

    [Fact]
    public async Task EnsureCardCategoryQualifiedBackfilledAsync_SqliteBackfillsQualifiedRowsOnlyOnce()
    {
        await CreateRepository().AddDeckIdsAsync(new[] { "qualified-backfill-seed" });
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO cards (normalized_card_name, display_name) VALUES ('qualified first', 'Qualified First'), ('qualified second', 'Qualified Second'); INSERT INTO card_category_summary (card_id, category, observation_rows) VALUES (1, 'Ramp', 5), (1, 'Tutor', 4), (2, 'Draw', 6);";
            await command.ExecuteNonQueryAsync();
        }

        var backfillPath = _databasePath + ".qualified-backfill";
        File.Copy(_databasePath, backfillPath);
        await new CategoryKnowledgeRepository(backfillPath).EnsureCardCategoryQualifiedBackfilledAsync();

        await using (var connection = new SqliteConnection($"Data Source={backfillPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            // Why: a missing qualifying row is what a re-run backfill would restore, so its absence proves the no-op.
            command.CommandText = "DELETE FROM card_category_qualified WHERE category = 'Draw';";
            await command.ExecuteNonQueryAsync();
        }

        var noOpPath = backfillPath + ".no-op";
        File.Copy(backfillPath, noOpPath);
        await new CategoryKnowledgeRepository(noOpPath).EnsureCardCategoryQualifiedBackfilledAsync();

        await using var verifyConnection = new SqliteConnection($"Data Source={noOpPath}");
        await verifyConnection.OpenAsync();
        var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT card_id, category, observation_rows FROM card_category_qualified ORDER BY card_id, category;";
        await using var reader = await verifyCommand.ExecuteReaderAsync();
        var rows = new List<(long CardId, string Category, long ObservationRows)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2)));
        }

        Assert.Equal(new[] { (1L, "Ramp", 5L) }, rows);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_EmptyQualifiedTable_UsesSummaryWithoutBackfilling()
    {
        await CreateRepository().AddDeckIdsAsync(new[] { "lookup-summary-seed" });
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO cards (normalized_card_name, display_name) VALUES ('lookup qualified', 'Lookup Qualified'); INSERT INTO card_category_summary (card_id, category, observation_rows) VALUES (1, 'Ramp', 5), (1, 'Draw', 4);";
            await command.ExecuteNonQueryAsync();
        }

        var lookupPath = _databasePath + ".summary-lookup";
        File.Copy(_databasePath, lookupPath);
        var results = await new CategoryKnowledgeRepository(lookupPath).GetCategoriesForNamesAsync(new[] { "Lookup Qualified" });
        Assert.Equal(new[] { "Ramp" }, results["Lookup Qualified"]);

        await using var verifyConnection = new SqliteConnection($"Data Source={lookupPath}");
        await verifyConnection.OpenAsync();
        var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT COUNT(*) FROM card_category_qualified;";
        Assert.Equal(0L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_QualifiedTableBackfilled_ReadsQualifiedRows()
    {
        var repository = CreateRepository();
        var sources = Enumerable.Range(0, CategoryCacheSchema.DefaultMinObservationRows)
            .Select(index => $"qualified-lookup-source-{index}")
            .ToArray();

        foreach (var source in sources)
        {
            await repository.PersistObservedCategoriesAsync(source, "Lookup Qualified", new[] { "Ramp" });
        }

        await repository.EnsureCardCategoryQualifiedBackfilledAsync();
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM card_category_summary WHERE category = 'Ramp';";
            await command.ExecuteNonQueryAsync();
        }

        var results = await repository.GetCategoriesForNamesAsync(new[] { "Lookup Qualified" });

        Assert.Equal(new[] { "Ramp" }, results["Lookup Qualified"]);
    }

    [Fact]
    public async Task PersistObservedCategoriesAsync_EmptyQualifiedTable_BackfillsBeforeSummaryWrite()
    {
        await CreateRepository().AddDeckIdsAsync(new[] { "writer-summary-seed" });
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO cards (normalized_card_name, display_name) VALUES ('writer qualified', 'Writer Qualified'); INSERT INTO card_category_summary (card_id, category, observation_rows) VALUES (1, 'Ramp', 5), (1, 'Draw', 4);";
            await command.ExecuteNonQueryAsync();
        }

        var writerPath = _databasePath + ".summary-writer";
        File.Copy(_databasePath, writerPath);
        await new CategoryKnowledgeRepository(writerPath).PersistObservedCategoriesAsync("writer-source", "New Card", new[] { "Ramp" });

        await using var verifyConnection = new SqliteConnection($"Data Source={writerPath}");
        await verifyConnection.OpenAsync();
        var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT card_id, category, observation_rows FROM card_category_summary WHERE observation_rows >= 5 EXCEPT SELECT card_id, category, observation_rows FROM card_category_qualified;";
        await using (var missingRows = await verifyCommand.ExecuteReaderAsync())
        {
            Assert.False(await missingRows.ReadAsync());
        }
        verifyCommand.CommandText = "SELECT card_id, category, observation_rows FROM card_category_qualified EXCEPT SELECT card_id, category, observation_rows FROM card_category_summary WHERE observation_rows >= 5;";
        await using var extraRows = await verifyCommand.ExecuteReaderAsync();
        Assert.False(await extraRows.ReadAsync());
    }

    [Fact]
    public async Task EnsureSchemaAsync_FailedIndexCreation_RetriesOnNextCall()
    {
        await CreateRepository().AddDeckIdsAsync(new[] { "schema-seed" });
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DROP INDEX ux_cards_normalized; INSERT INTO cards (normalized_card_name, display_name) VALUES ('duplicate', 'First'), ('duplicate', 'Second');";
            await command.ExecuteNonQueryAsync();
        }

        var retryPath = _databasePath + ".retry";
        File.Copy(_databasePath, retryPath);
        var retryRepository = new CategoryKnowledgeRepository(retryPath);
        await Assert.ThrowsAsync<SqliteException>(() => retryRepository.GetCategoriesAsync("duplicate"));

        await using (var connection = new SqliteConnection($"Data Source={retryPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM cards WHERE display_name = 'Second';";
            await command.ExecuteNonQueryAsync();
        }

        await retryRepository.GetCategoriesAsync("duplicate");
        await using var verifyConnection = new SqliteConnection($"Data Source={retryPath}");
        await verifyConnection.OpenAsync();
        var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ux_cards_normalized';";
        Assert.Equal(1L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task CategoryObservationWrites_BatchReplaceDeleteAndReharvest_MatchSummary()
    {
        var repository = CreateRepository();
        var observations = new[]
        {
            ("Sol Ring", "Ramp", "mainboard", 1, 1),
            ("Sol Ring", "Draw", "mainboard", 1, 1),
        };

        await repository.PersistDeckCategoryBatchAsync("summary-batch", observations, new[] { ("Sol Ring", "mainboard") });
        await AssertCategorySummaryMatchesObservationsAsync();

        await repository.ReplaceSourceRowsAsync("summary-batch", new[] { new CategoryKnowledgeRow("Ramp", "Sol Ring", 1, 1) });
        await AssertCategorySummaryMatchesObservationsAsync();

        await repository.DeleteSourceDataAsync("summary-batch");
        await AssertCategorySummaryMatchesObservationsAsync();

        await repository.PersistDeckCategoryBatchAsync("summary-batch", observations, new[] { ("Sol Ring", "mainboard") });
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task PersistDeckCategoryBatchAsync_TwoBoardsAndTwoSources_DeleteFirstSourceKeepsOneObservation()
    {
        var repository = CreateRepository();
        var observations = new[]
        {
            ("Sol Ring", "Ramp", "mainboard", 1, 1),
            ("Sol Ring", "Ramp", "sideboard", 1, 1),
        };

        await repository.PersistDeckCategoryBatchAsync("two-board-first", observations, new[] { ("Sol Ring", "mainboard") });
        await repository.PersistDeckCategoryBatchAsync("two-board-second", observations[..1], new[] { ("Sol Ring", "mainboard") });
        await AssertSummaryObservationRowsAsync("Ramp", 3);

        await repository.DeleteSourceDataAsync("two-board-first");

        await AssertSummaryObservationRowsAsync("Ramp", 1);
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task PersistDeckCategoryBatchAsync_QuantityDoesNotMultiplyObservationRows()
    {
        var repository = CreateRepository();
        var observations = new[] { ("Sol Ring", "Ramp", "mainboard", 4, 1) };

        await repository.PersistDeckCategoryBatchAsync("quantity-summary", observations, new[] { ("Sol Ring", "mainboard") });
        await AssertSummaryObservationRowsAsync("Ramp", 1);

        await repository.PersistDeckCategoryBatchAsync("quantity-summary", new[] { ("Sol Ring", "Ramp", "mainboard", 3, 1) }, new[] { ("Sol Ring", "mainboard") });

        await AssertSummaryObservationRowsAsync("Ramp", 1);

        await repository.PersistDeckCategoryBatchAsync("quantity-summary-second", new[] { ("Sol Ring", "Ramp", "mainboard", 3, 1) }, new[] { ("Sol Ring", "mainboard") });

        await AssertSummaryObservationRowsAsync("Ramp", 2);
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ReturnsAggregatesOrderedByCountLastProcessedAndName()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var atraxaMax = DateTimeOffset.Parse("2026-01-04T00:00:00Z");
        var bragoChulaneMax = DateTimeOffset.Parse("2026-01-07T00:00:00Z");
        var muldrothaMax = DateTimeOffset.Parse("2026-01-05T00:00:00Z");
        var dinaMax = DateTimeOffset.Parse("2026-01-09T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "atraxa-1", "Atraxa", insertedUtc, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "atraxa-2", "Atraxa", insertedUtc, atraxaMax);
        await SeedProcessedDeckAsync(repository, "atraxa-3", "Atraxa", insertedUtc, DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "brago-1", "Brago", insertedUtc, bragoChulaneMax);
        await SeedProcessedDeckAsync(repository, "brago-2", "Brago", insertedUtc, DateTimeOffset.Parse("2026-01-06T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "chulane-1", "Chulane", insertedUtc, bragoChulaneMax);
        await SeedProcessedDeckAsync(repository, "chulane-2", "Chulane", insertedUtc, DateTimeOffset.Parse("2026-01-06T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "muldrotha-1", "Muldrotha", insertedUtc, muldrothaMax);
        await SeedProcessedDeckAsync(repository, "muldrotha-2", "Muldrotha", insertedUtc, DateTimeOffset.Parse("2026-01-04T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "dina-1", "Dina", insertedUtc, dinaMax);

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 10);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("Atraxa", row.CommanderName);
                Assert.Equal(3, row.DeckCount);
                Assert.Equal(atraxaMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Brago", row.CommanderName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(bragoChulaneMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Chulane", row.CommanderName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(bragoChulaneMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Muldrotha", row.CommanderName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(muldrothaMax.ToString("O"), row.LastProcessedUtc);
            },
            row =>
            {
                Assert.Equal("Dina", row.CommanderName);
                Assert.Equal(1, row.DeckCount);
                Assert.Equal(dinaMax.ToString("O"), row.LastProcessedUtc);
            });
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_AppliesPageSizeSlicing()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "one-1", "One", insertedUtc, insertedUtc.AddDays(4));
        await SeedProcessedDeckAsync(repository, "one-2", "One", insertedUtc, insertedUtc.AddDays(4));
        await SeedProcessedDeckAsync(repository, "one-3", "One", insertedUtc, insertedUtc.AddDays(4));
        await SeedProcessedDeckAsync(repository, "two-1", "Two", insertedUtc, insertedUtc.AddDays(3));
        await SeedProcessedDeckAsync(repository, "two-2", "Two", insertedUtc, insertedUtc.AddDays(3));
        await SeedProcessedDeckAsync(repository, "three-1", "Three", insertedUtc, insertedUtc.AddDays(2));
        await SeedProcessedDeckAsync(repository, "four-1", "Four", insertedUtc, insertedUtc.AddDays(1));

        var pageTwo = await repository.GetPagedProcessedCommanderRowsAsync(page: 2, pageSize: 2);

        Assert.Equal(new[] { "Three", "Four" }, pageTwo.Select(row => row.CommanderName));
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_GroupsCommanderNamesCaseInsensitively()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var lastProcessedUtc = DateTimeOffset.Parse("2026-01-04T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "atraxa-1", "Atraxa", insertedUtc, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        await SeedProcessedDeckAsync(repository, "atraxa-2", "atraxa", insertedUtc, lastProcessedUtc);

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 10);
        var count = await repository.GetDistinctProcessedCommanderCountAsync();

        var row = Assert.Single(rows);
        Assert.Equal("atraxa", row.CommanderName);
        Assert.Equal(2, row.DeckCount);
        Assert.Equal(lastProcessedUtc.ToString("O"), row.LastProcessedUtc);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ReturnsEmptyListForEmptyQueue()
    {
        var repository = CreateRepository();

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 2);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ExcludesUnprocessedAndNullCommanderRows()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "processed", "Known Commander", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "processed-null", commanderName: null, insertedUtc, lastCheckedUtc: null);
        await repository.AddDeckIdsAsync(new[] { "unprocessed" });
        await SetDeckQueueFieldsAsync("unprocessed", insertedUtc, commanderName: "Unprocessed Commander", lastCheckedUtc: insertedUtc);

        var rows = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 10);

        var row = Assert.Single(rows);
        Assert.Equal("Known Commander", row.CommanderName);
        Assert.Equal(1, row.DeckCount);
        Assert.Equal(insertedUtc.ToString("O"), row.LastProcessedUtc);
    }

    [Fact]
    public async Task GetPagedProcessedCommanderRowsAsync_ClampsInvalidPagingInputs()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "deck-001", "One", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "deck-002", "Two", insertedUtc, insertedUtc.AddDays(1));

        var pageZero = await repository.GetPagedProcessedCommanderRowsAsync(page: 0, pageSize: 2);
        var pageOne = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 2);
        var zeroPageSize = await repository.GetPagedProcessedCommanderRowsAsync(page: 1, pageSize: 0);

        Assert.Equal(pageOne.Select(row => row.CommanderName), pageZero.Select(row => row.CommanderName));
        var row = Assert.Single(zeroPageSize);
        Assert.Equal("Two", row.CommanderName);
    }

    [Fact]
    public async Task GetDistinctProcessedCommanderCountAsync_ReturnsDistinctCommanderCount()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "atraxa-1", "Atraxa", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "atraxa-2", "Atraxa", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "brago-1", "Brago", insertedUtc, insertedUtc);
        await SeedProcessedDeckAsync(repository, "processed-null", commanderName: null, insertedUtc, lastCheckedUtc: insertedUtc);
        await repository.AddDeckIdsAsync(new[] { "unprocessed" });
        await SetDeckQueueFieldsAsync("unprocessed", insertedUtc, commanderName: "Chulane", lastCheckedUtc: insertedUtc);

        var count = await repository.GetDistinctProcessedCommanderCountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetCategoryRowsForCommanderAsync_ReturnsCardWithOnlyCardTypeCategory()
    {
        var repository = CreateRepository();
        var insertedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await SeedProcessedDeckAsync(repository, "TESTDECK", "Krenko, Mob Boss", insertedUtc, insertedUtc);
        await repository.PersistObservedCategoriesAsync("archidekt_live:TESTDECK", "Sol Ring", new[] { "Artifact" });

        var rows = await repository.GetCategoryRowsForCommanderAsync("Krenko, Mob Boss");

        var row = Assert.Single(rows);
        Assert.Equal("Artifact", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_ExcludesRarePairsAndCoversEveryRequestedName()
    {
        var repository = CreateRepository();
        var minObservationRows = GetCardCategoryRepository(repository).MinObservationRows;
        Assert.Equal(5, minObservationRows);
        await SeedThresholdObservationsAsync(repository, "Rhystic Study", new[] { "Card Draw" });
        await SeedThresholdObservationsAsync(repository, "Cyclonic Rift", new[] { "Removal" });
        await SeedThresholdObservationsAsync(repository, "Cyclonic Rift", new[] { "Board Wipe" }, count: minObservationRows - 1);

        var names = new[] { "Rhystic Study", "Cyclonic Rift", "Not In Cache" };
        var batch = await repository.GetCategoriesForNamesAsync(names);

        // Every requested name gets an entry, keyed by the requested spelling.
        Assert.Equal(names.Length, batch.Count);
        Assert.Equal(new[] { "Card Draw" }, batch["Rhystic Study"]);
        Assert.Equal(new[] { "Removal" }, batch["Cyclonic Rift"]);
        Assert.Equal(CategoryFilter.IncludedOrFallback(Array.Empty<string>()), batch["Not In Cache"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_CutLabOptions_DropsLowShareTailCategory()
    {
        var repository = CreateRepository();
        await SeedThresholdObservationsAsync(repository, "Sol Ring", new[] { "Ramp" }, count: 17);
        await SeedThresholdObservationsAsync(repository, "Sol Ring", new[] { "Artifacts" }, count: 3);

        var batch = await repository.GetCategoriesForNamesAsync(
            new[] { "Sol Ring" },
            CategoryLookupOptions.CutLabMeaningful);

        Assert.Equal(new[] { "Ramp" }, batch["Sol Ring"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_CutLabOptions_ExcludesTypeTagsBeforeShareAndTake()
    {
        var repository = CreateRepository();
        await SeedThresholdObservationsAsync(repository, "Test Card", new[] { "Creature" }, count: 60);
        await SeedThresholdObservationsAsync(repository, "Test Card", new[] { "Artifact" }, count: 40);
        await SeedThresholdObservationsAsync(repository, "Test Card", new[] { "Removal" }, count: 20);
        await SeedThresholdObservationsAsync(repository, "Test Card", new[] { "Tokens" }, count: 15);
        await SeedThresholdObservationsAsync(repository, "Test Card", new[] { "Weird Tail" }, count: 5);

        var batch = await repository.GetCategoriesForNamesAsync(
            new[] { "Test Card" },
            CategoryLookupOptions.CutLabMeaningful);

        Assert.Equal(new[] { "Removal", "Tokens" }, batch["Test Card"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_MatchesThresholdedObservationReference_ForParityCases()
    {
        var repository = CreateRepository();
        await SeedThresholdObservationsAsync(repository, "Sol Ring", new[] { "Ramp", "ramp" });
        await SeedThresholdObservationsAsync(repository, "Sol Ring", new[] { "Removal" });
        await repository.PersistObservedCategoriesAsync("archidekt_live:rare", "Arcane Signet", new[] { "Ramp" });
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO cards (normalized_card_name, display_name) VALUES ('card with no observations', 'Card With No Observations');";
            await command.ExecuteNonQueryAsync();
        }

        var names = new[] { "Sol Ring", "Arcane Signet", "Card With No Observations", "Absent From Cards" };
        var batch = await repository.GetCategoriesForNamesAsync(names);

        Assert.Equal(names.Length, batch.Count);
        Assert.Equal(new[] { "Ramp", "ramp", "Removal" }, batch["Sol Ring"]);
        foreach (var name in names)
        {
            Assert.Equal(await GetThresholdedObservationCategoriesAsync(repository, name), batch[name]);
        }
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_TagBelowShareFloor_ExcludesTag()
    {
        var repository = CreateRepository();
        await SeedSummaryRowsAsync(repository, "Share Floor", ("Top", 10001), ("Noise", 5));

        var categories = await repository.GetCategoriesForNamesAsync(new[] { "Share Floor" });

        Assert.Equal(new[] { "Top" }, categories["Share Floor"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_TagAtShareFloor_IncludesTag()
    {
        var repository = CreateRepository();
        await SeedSummaryRowsAsync(repository, "Share Boundary", ("Top", 10000), ("Boundary", 5));

        var categories = await repository.GetCategoriesForNamesAsync(new[] { "Share Boundary" });

        Assert.Equal(new[] { "Boundary", "Top" }, categories["Share Boundary"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_QualifiedTableMatchesSummaryLookup()
    {
        var repository = CreateRepository();
        var names = new[] { "Equivalence First", "Equivalence Second" };
        for (var index = 0; index < 5; index++)
        {
            await repository.PersistObservedCategoriesAsync($"equivalence-first-ramp-{index}", names[0], new[] { "Ramp" });
            await repository.PersistObservedCategoriesAsync($"equivalence-second-draw-{index}", names[1], new[] { "Draw" });
            if (index < 4)
            {
                await repository.PersistObservedCategoriesAsync($"equivalence-first-low-{index}", names[0], new[] { "Low" });
                await repository.PersistObservedCategoriesAsync($"equivalence-second-low-{index}", names[1], new[] { "Low" });
            }
        }

        var actual = await repository.GetCategoriesForNamesAsync(names);
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = CardCategoryRepository.BuildCategoryLookupSql(
            "IN ($first, $second)",
            "card_category_summary");
        command.Parameters.AddWithValue("$first", CardNormalizer.Normalize(names[0]));
        command.Parameters.AddWithValue("$second", CardNormalizer.Normalize(names[1]));
        command.Parameters.AddWithValue("@minObservationRows", CategoryCacheSchema.DefaultMinObservationRows);
        command.Parameters.AddWithValue("@shareDenominator", 2000);
        await using var reader = await command.ExecuteReaderAsync();
        var expected = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            var normalizedCardName = reader.GetString(0);
            if (!expected.TryGetValue(normalizedCardName, out var categories))
            {
                categories = new List<string>();
                expected.Add(normalizedCardName, categories);
            }

            categories.Add(reader.GetString(1));
        }

        foreach (var name in names)
        {
            Assert.Equal(expected[CardNormalizer.Normalize(name)], actual[name]);
        }
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_ManySubMinimumTags_PreservesQualifyingTags()
    {
        var sql = CardCategoryRepository.BuildCategoryLookupSql("IN @normalized");
        var minFilter = sql.IndexOf("s.observation_rows >= @minObservationRows", StringComparison.Ordinal);
        var subqueryEnd = sql.IndexOf(") s\n", StringComparison.Ordinal);
        Assert.InRange(minFilter, 0, subqueryEnd - 1);

        var repository = CreateRepository();
        await SeedSummaryRowsAsync(repository, "Sparse Noise", ("Top", 10000), ("Boundary", 5));
        var before = await repository.GetCategoriesForNamesAsync(new[] { "Sparse Noise" });

        var noise = Enumerable.Range(1, 20)
            .Select(index => ($"Noise {index:D2}", index % 4 + 1))
            .ToArray();
        await SeedSummaryRowsAsync(repository, "Sparse Noise", noise);
        var after = await repository.GetCategoriesForNamesAsync(new[] { "Sparse Noise" });

        Assert.Equal(new[] { "Boundary", "Top" }, before["Sparse Noise"]);
        Assert.Equal(before["Sparse Noise"], after["Sparse Noise"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_TopBelowTenThousand_KeepsMinimumRowTag()
    {
        var repository = CreateRepository();
        await SeedSummaryRowsAsync(repository, "Small Top", ("Top", 9999), ("Minimum", 5));

        var categories = await repository.GetCategoriesForNamesAsync(new[] { "Small Top" });

        Assert.Equal(new[] { "Minimum", "Top" }, categories["Small Top"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_TwoCards_UsesEachCardsTop()
    {
        var repository = CreateRepository();
        await SeedSummaryRowsAsync(repository, "Huge Card", ("Top", 200000), ("Noise", 5));
        await SeedSummaryRowsAsync(repository, "Small Card", ("Top", 20), ("Useful", 5));

        var categories = await repository.GetCategoriesForNamesAsync(new[] { "Huge Card", "Small Card" });

        Assert.Equal(new[] { "Top" }, categories["Huge Card"]);
        Assert.Equal(new[] { "Top", "Useful" }, categories["Small Card"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_ManyQualifyingTags_UsesMaxInsteadOfSum()
    {
        var repository = CreateRepository();
        await SeedSummaryRowsAsync(repository, "Many Tags", ("Top", 10000), ("Alpha", 5),
            ("Bravo", 5), ("Charlie", 5), ("Delta", 5));

        var categories = await repository.GetCategoriesForNamesAsync(new[] { "Many Tags" });

        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "Top" }, categories["Many Tags"]);
    }

    [Fact]
    public void GetCategoriesForNamesAsync_DefaultCommandTimeout_IsThreeSeconds()
    {
        var repository = CreateRepository();
        var cardCategory = typeof(CategoryKnowledgeRepository)
            .GetField("_cardCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(repository);

        var timeout = cardCategory?.GetType()
            .GetProperty("CategoriesBatchCommandTimeoutSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(cardCategory);

        Assert.Equal(3, timeout);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_KeyedCaseInsensitively_AndSkipsBlankNames()
    {
        var repository = CreateRepository();
        await SeedThresholdObservationsAsync(repository, "Sol Ring", new[] { "Ramp" });

        var batch = await repository.GetCategoriesForNamesAsync(new[] { "SOL RING", "  ", "" });

        // Blank names contribute nothing; the one real name resolves regardless of casing.
        var entry = Assert.Single(batch);
        Assert.Equal("SOL RING", entry.Key);
        Assert.Equal(await repository.GetCategoriesAsync("Sol Ring"), batch["sol ring"]);
    }

    [Fact]
    public async Task GetCategoriesForNamesAsync_EmptyInput_ReturnsEmpty()
    {
        var repository = CreateRepository();

        var batch = await repository.GetCategoriesForNamesAsync(Array.Empty<string>());

        Assert.Empty(batch);
    }

    [Fact]
    public async Task GetCategoryDeckCountsAsync_ReturnsCanonicalizedCountsAtCardDeckTotalsGrain()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Card Draw" }, board: "mainboard", deckCountIncrement: 3);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp" }, board: "mainboard", deckCountIncrement: 2);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "mainboard", deckCountIncrement: 5);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Sol Ring", new[] { "Draw" }, board: "sideboard", deckCountIncrement: 2);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-2", "Sol Ring", board: "sideboard", deckCountIncrement: 2);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-3", "Sol Ring", new[] { "Draw" }, board: "mainboard", deckCountIncrement: 99);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-4", "Sol Ring", new[] { "Artifact" }, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-4", "Sol Ring", board: "mainboard", deckCountIncrement: 1);

        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring");

        Assert.Equal(5, counts["draw"]);
        Assert.Equal(2, counts["ramp"]);
        Assert.DoesNotContain("artifact", counts.Keys);
        Assert.True(counts.Values.All(count => count <= totals.TotalDeckCount));
        Assert.Equal(8, totals.TotalDeckCount);
    }

    [Fact]
    public async Task GetCategoryDeckCountsAsync_WhenCategoriesDifferOnlyByCase_AggregatesInsteadOfThrowing()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Draw" }, board: "mainboard", deckCountIncrement: 3);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "mainboard", deckCountIncrement: 3);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Sol Ring", new[] { "draw" }, board: "mainboard", deckCountIncrement: 2);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-2", "Sol Ring", board: "mainboard", deckCountIncrement: 2);

        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring");

        Assert.Equal(1, counts.Keys.Count(key => string.Equals(key, "draw", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(5, counts["draw"]);
        Assert.Equal(5, totals.TotalDeckCount);
        Assert.True(counts["draw"] <= totals.TotalDeckCount);
    }

    [Fact]
    public async Task GetCategoryDeckCountsAsync_WhenOneGrainCarriesTwoCanonicalAliases_CountsGrainOnce()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Card Draw" }, board: "mainboard", deckCountIncrement: 3);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Draw" }, board: "mainboard", deckCountIncrement: 2);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "mainboard", deckCountIncrement: 3);

        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring");

        Assert.Equal(3, counts["draw"]);
        Assert.True(counts["draw"] <= totals.TotalDeckCount);
    }

    [Fact]
    public async Task GetCategoryDeckCountsAsync_WhenAliasesSpanGrains_SumsAcrossGrains()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Card Draw" }, board: "mainboard", deckCountIncrement: 3);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "mainboard", deckCountIncrement: 3);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Sol Ring", new[] { "Draw" }, board: "mainboard", deckCountIncrement: 2);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-2", "Sol Ring", board: "mainboard", deckCountIncrement: 2);

        var counts = await repository.GetCategoryDeckCountsAsync("Sol Ring");

        Assert.Equal(5, counts["draw"]);
    }

    [Fact]
    public async Task EnsureSchemaAsync_CreatesDeckQueueIndexes()
    {
        var repository = CreateRepository();

        await repository.EnsureSchemaAsync();

        var indexNames = await GetDeckQueueIndexNamesAsync();
        Assert.DoesNotContain("ix_deck_queue_processed", indexNames);
        Assert.Contains("ix_deck_queue_processed_inserted_deck", indexNames);
        Assert.DoesNotContain("ix_deck_queue_processed_commander", indexNames);
        Assert.DoesNotContain("ix_deck_queue_processed_commander_lower", indexNames);
        Assert.Contains("ix_deck_queue_commander_lower_processed", indexNames);
    }

    [Fact]
    public async Task EnsureSchemaAsync_CreatesCardLookupIndexes()
    {
        var repository = CreateRepository();

        await repository.EnsureSchemaAsync();

        var indexNames = await GetCardLookupIndexNamesAsync();
        Assert.Contains("ux_cards_normalized", indexNames);
        Assert.DoesNotContain("ix_obs_card", indexNames);
        Assert.Contains("ix_obs_card_board", indexNames);
        Assert.Contains("ix_obs_card_category", indexNames);
        Assert.DoesNotContain("ix_totals_card", indexNames);
        Assert.Contains("ix_totals_card_board", indexNames);
    }

    [Fact]
    public async Task EnsureSchemaAsync_SecondaryIndexFails_CompletesWithoutThrowing()
    {
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE ix_obs_card_board (id INTEGER);";
            await command.ExecuteNonQueryAsync();
        }

        var repository = CreateRepository();
        await repository.EnsureSchemaAsync();

        await using var verifyConnection = new SqliteConnection($"Data Source={_databasePath}");
        await verifyConnection.OpenAsync();
        var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name IN ('ux_cards_normalized', 'ux_sources_source', 'ux_deck_queue_deck_id', 'ux_obs_grain', 'ux_totals_grain');";
        Assert.Equal(5L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync()));

        verifyCommand.CommandText = "DROP TABLE ix_obs_card_board;";
        await verifyCommand.ExecuteNonQueryAsync();
        await repository.EnsureSchemaAsync();

        verifyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_obs_card_board';";
        Assert.Equal(0L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task EnsureSchemaAsync_IndexCreationFails_LeavesTablesButThrows()
    {
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE card_deck_totals (
                    source TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = CreateRepository();

        await Assert.ThrowsAsync<SqliteException>(() => repository.EnsureSchemaAsync());
        var tableNames = await GetTableNamesAsync();
        Assert.Contains("cards", tableNames);
        Assert.Contains("sources", tableNames);
        Assert.Contains("deck_queue", tableNames);
        Assert.Contains("card_category_observations", tableNames);
        Assert.Contains("card_deck_totals", tableNames);
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_NullMetadata_PreservesCapturedValues()
    {
        var repository = CreateRepository();
        var captured = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var metadata = new ArchidektDeckMetadata(3, 1, true, captured, captured, captured);
        await repository.AddDeckIdsAsync(new[] { "metadata-processed" });
        await repository.MarkDeckProcessedAsync("metadata-processed", "Commander", metadata: metadata);
        await repository.MarkDeckProcessedAsync("metadata-processed", null, skip: true, metadata: null);

        var row = await GetMetadataRowAsync("metadata-processed");
        Assert.Equal(3L, row.Bracket);
        Assert.Equal(1L, row.Format);
        Assert.Equal(1L, row.Theorycrafted);
        Assert.Equal(captured.ToString("O"), row.CapturedUtc);
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_NullMetadataSkip_PreservesCommanderName()
    {
        var repository = CreateRepository();
        await repository.AddDeckIdsAsync(new[] { "commander-processed" });
        await repository.MarkDeckProcessedAsync("commander-processed", "Original Commander");
        await repository.MarkDeckProcessedAsync("commander-processed", null, skip: true, metadata: null);

        var commanderName = await GetCommanderNameAsync("commander-processed");
        Assert.Equal("Original Commander", commanderName);
    }

    [Fact]
    public async Task MarkUrlDeckProcessedAsync_NonNullRecord_OverwritesNullFieldsAndNullRecordPreserves()
    {
        var repository = CreateRepository();
        var first = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var second = first.AddHours(1);
        await repository.MarkUrlDeckProcessedAsync("metadata-url", "Commander", new ArchidektDeckMetadata(3, 1, true, first, first, first));
        await repository.MarkUrlDeckProcessedAsync("metadata-url", "Commander", new ArchidektDeckMetadata(null, 2, false, second, second, second));

        var recaptured = await GetMetadataRowAsync("metadata-url");
        Assert.Null(recaptured.Bracket);
        Assert.Equal(2L, recaptured.Format);
        Assert.Equal(second.ToString("O"), recaptured.CapturedUtc);

        await repository.MarkUrlDeckProcessedAsync("metadata-url", "Commander", metadata: null);
        var preserved = await GetMetadataRowAsync("metadata-url");
        Assert.Equal(recaptured.CapturedUtc, preserved.CapturedUtc);
        Assert.Null(preserved.Bracket);
    }

    private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);

    private static CardCategoryRepository GetCardCategoryRepository(CategoryKnowledgeRepository repository)
    {
        var field = typeof(CategoryKnowledgeRepository).GetField(
            "_cardCategory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<CardCategoryRepository>(field?.GetValue(repository));
    }

    private static async Task SeedThresholdObservationsAsync(
        CategoryKnowledgeRepository repository,
        string cardName,
        IReadOnlyList<string> categories,
        int? count = null)
    {
        var observationCount = count ?? GetCardCategoryRepository(repository).MinObservationRows;
        for (var i = 0; i < observationCount; i++)
        {
            await repository.PersistObservedCategoriesAsync($"archidekt_live:{i}", cardName, categories);
        }
    }

    private async Task SeedSummaryRowsAsync(
        CategoryKnowledgeRepository repository,
        string cardName,
        params (string Category, int Rows)[] rows)
    {
        await repository.PersistObservedCategoriesAsync($"summary-seed-{cardName}", cardName, new[] { rows[0].Category });
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        foreach (var (category, observationRows) in rows)
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO card_category_summary (card_id, category, observation_rows)
                SELECT id, @category, @observationRows FROM cards WHERE normalized_card_name = @normalized
                ON CONFLICT(card_id, category) DO UPDATE SET observation_rows = excluded.observation_rows;
                DELETE FROM card_category_qualified
                WHERE card_id = (SELECT id FROM cards WHERE normalized_card_name = @normalized) AND category = @category;
                INSERT INTO card_category_qualified (card_id, category, observation_rows)
                SELECT id, @category, @observationRows FROM cards
                WHERE normalized_card_name = @normalized AND @observationRows >= @minObservationRows;
                """;
            command.Parameters.AddWithValue("@category", category);
            command.Parameters.AddWithValue("@observationRows", observationRows);
            command.Parameters.AddWithValue("@normalized", CardNormalizer.Normalize(cardName));
            // Why: direct summary seeding bypasses ApplySummaryDeltasAsync, so mirror its side-table invariant here.
            command.Parameters.AddWithValue("@minObservationRows", CategoryCacheSchema.DefaultMinObservationRows);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<IReadOnlyList<string>> GetThresholdedObservationCategoriesAsync(CategoryKnowledgeRepository repository, string cardName)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT category FROM (
                SELECT counts.category, counts.observation_rows,
                    MAX(counts.observation_rows) OVER (PARTITION BY counts.card_id) AS top_rows
                FROM (
                    SELECT o.card_id, o.category, COUNT(*) AS observation_rows
                    FROM card_category_observations o
                    JOIN cards c ON c.id = o.card_id
                    WHERE c.normalized_card_name = @normalized
                    GROUP BY o.card_id, o.category
                ) counts
            ) ranked
            WHERE observation_rows >= @minObservationRows
              AND CAST(observation_rows AS BIGINT) * @shareDenominator >= top_rows
            ORDER BY LOWER(category), category
            """;
        command.Parameters.AddWithValue("@normalized", CardNormalizer.Normalize(cardName));
        command.Parameters.AddWithValue("@minObservationRows", GetCardCategoryRepository(repository).MinObservationRows);
        command.Parameters.AddWithValue("@shareDenominator", GetCardCategoryRepository(repository).ObservationShareDenominator);
        var categories = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(reader.GetString(0));
        }
        return CategoryFilter.IncludedOrFallback(categories);
    }

    private async Task AssertCategorySummaryMatchesObservationsAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT card_id, category, COUNT(*) FROM card_category_observations GROUP BY card_id, category
            EXCEPT
            SELECT card_id, category, observation_rows FROM card_category_summary;
            """;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.False(await reader.ReadAsync());
        }
        command.CommandText = """
            SELECT card_id, category, observation_rows FROM card_category_summary
            EXCEPT
            SELECT card_id, category, COUNT(*) FROM card_category_observations GROUP BY card_id, category;
            """;
        await using (var reverseReader = await command.ExecuteReaderAsync())
        {
            Assert.False(await reverseReader.ReadAsync());
        }

        command.CommandText = """
            SELECT card_id, category, observation_rows FROM card_category_summary
            WHERE observation_rows >= @minObservationRows
            EXCEPT
            SELECT card_id, category, observation_rows FROM card_category_qualified;
            """;
        command.Parameters.AddWithValue("@minObservationRows", CategoryCacheSchema.DefaultMinObservationRows);
        await using (var qualifiedReader = await command.ExecuteReaderAsync())
        {
            Assert.False(await qualifiedReader.ReadAsync());
        }
        command.CommandText = """
            SELECT card_id, category, observation_rows FROM card_category_qualified
            EXCEPT
            SELECT card_id, category, observation_rows FROM card_category_summary
            WHERE observation_rows >= @minObservationRows;
            """;
        await using var qualifiedReverseReader = await command.ExecuteReaderAsync();
        Assert.False(await qualifiedReverseReader.ReadAsync());
    }

    private async Task AssertSummaryObservationRowsAsync(string category, long expected)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT observation_rows FROM card_category_summary WHERE category = @category;";
        command.Parameters.AddWithValue("@category", category);
        Assert.Equal(expected, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private async Task<string?> GetCommanderNameAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT commander_name FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        return (string?)await command.ExecuteScalarAsync();
    }

    private async Task<(long? Bracket, long? Format, long? Theorycrafted, string? CapturedUtc)> GetMetadataRowAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT archidekt_edh_bracket, archidekt_deck_format, archidekt_theorycrafted, archidekt_metadata_captured_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.IsDBNull(0) ? null : reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetInt64(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private async Task SeedProcessedDeckAsync(
        CategoryKnowledgeRepository repository,
        string deckId,
        string? commanderName,
        DateTimeOffset insertedUtc,
        DateTimeOffset? lastCheckedUtc)
    {
        await repository.AddDeckIdsAsync(new[] { deckId });
        await repository.MarkDeckProcessedAsync(deckId, commanderName);
        await SetDeckQueueFieldsAsync(deckId, insertedUtc, commanderName, lastCheckedUtc);
    }

    private async Task SetLastCheckedUtcAsync(string deckId, DateTimeOffset timestamp)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE deck_queue
            SET last_checked_utc = $timestamp
            WHERE deck_id = $deckId;
            """;
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetDeckQueueFieldsAsync(
        string deckId,
        DateTimeOffset insertedUtc,
        string? commanderName,
        DateTimeOffset? lastCheckedUtc)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE deck_queue
            SET inserted_utc = $insertedUtc,
                commander_name = $commanderName,
                last_checked_utc = $lastCheckedUtc
            WHERE deck_id = $deckId;
            """;
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$insertedUtc", insertedUtc.ToString("O"));
        command.Parameters.AddWithValue("$commanderName", (object?)commanderName ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastCheckedUtc", lastCheckedUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();

        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            command.CommandText = """
                DELETE FROM processed_commander_summary WHERE LOWER(commander_name) = LOWER($commanderName);
                INSERT INTO processed_commander_summary (commander_name, deck_count, last_processed_utc)
                SELECT MAX(commander_name), COUNT(1), MAX(last_checked_utc)
                FROM deck_queue
                WHERE processed = 1 AND commander_name IS NOT NULL AND LOWER(commander_name) = LOWER($commanderName)
                GROUP BY LOWER(commander_name);
                """;
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<IReadOnlyList<string>> GetDeckQueueIndexNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'index'
              AND name IN (
                'ix_deck_queue_processed',
                'ix_deck_queue_processed_inserted_deck',
                'ix_deck_queue_processed_commander',
                'ix_deck_queue_processed_commander_lower',
                'ix_deck_queue_commander_lower_processed')
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<IReadOnlyList<string>> GetCardLookupIndexNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'index'
              AND name IN (
                'ux_cards_normalized',
                'ix_obs_card',
                'ix_obs_card_board',
                'ix_obs_card_category',
                'ix_totals_card',
                'ix_totals_card_board')
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<IReadOnlyList<string>> GetTableNamesAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                'deck_queue',
                'cards',
                'sources',
                'card_category_observations',
                'card_category_summary',
                'card_category_qualified',
                'card_deck_totals')
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
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
