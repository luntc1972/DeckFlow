using Microsoft.Data.Sqlite;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

/// <summary>
/// RED until 26-02: pins normalized category-cache schema DDL and old-shape read/write parity.
/// </summary>
public sealed class CategoryCacheSchemaParityTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public CategoryCacheSchemaParityTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task EnsureSchema_OnFreshSqlite_AllowsSurrogateIdPlusUniqueGrain()
    {
        var repository = CreateRepository();

        await repository.EnsureSchemaAsync();
        await using var connection = await OpenConnectionAsync();

        await ExecuteNonQueryAsync(
            connection,
            "INSERT INTO cards (normalized_card_name, display_name) VALUES ('sol ring', 'Sol Ring');");
        await ExecuteNonQueryAsync(
            connection,
            "INSERT INTO sources (source, deck_queue_id) VALUES ('archidekt_live:deck-1', NULL);");
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc)
            VALUES (1, 1, 'Sol Ring', 'Ramp', 'mainboard', 1, 1, '2026-01-01T00:00:00.0000000Z');
            """);
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc)
            VALUES (1, 1, 'Sol Ring', 'Ramp', 'sideboard', 1, 1, '2026-01-01T00:00:00.0000000Z');
            """);

        var ids = await QueryInt64ListAsync(
            connection,
            "SELECT id FROM card_category_observations ORDER BY id;");
        Assert.Equal(new[] { 1L, 2L }, ids);

        await Assert.ThrowsAsync<SqliteException>(() => ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc)
            VALUES (1, 1, 'Sol Ring', 'Ramp', 'mainboard', 1, 1, '2026-01-01T00:00:00.0000000Z');
            """));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteNonQueryAsync(
            connection,
            "INSERT INTO cards (normalized_card_name, display_name) VALUES ('sol ring', 'SOL RING');"));
    }

    [Fact]
    public async Task EnsureSchema_OnFreshSqlite_CreatesLoweredCommanderIndex()
    {
        var repository = CreateRepository();

        await repository.EnsureSchemaAsync();
        await using var connection = await OpenConnectionAsync();

        var indexCount = await QuerySingleInt64Async(
            connection,
            """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'index'
              AND name = 'ix_deck_queue_commander_lower_processed';
            """);

        Assert.Equal(1, indexCount);
    }

    [Fact]
    public async Task GetCategoriesAsync_SolRing_ReturnsSameCategories()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync(
            "archidekt_live:deck-1",
            "Sol Ring",
            new[] { "Artifact", "Ramp" },
            quantity: 1,
            board: "mainboard",
            deckCountIncrement: 1);

        var categories = await repository.GetCategoriesAsync("Sol Ring");

        Assert.Equal(new[] { "Ramp" }, categories);
    }

    [Fact]
    public async Task BoardFilter_MainboardAndOther_ReturnsOnlyRequestedBoard()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp" }, quantity: 3, board: "sideboard", deckCountIncrement: 1);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "mainboard", deckCountIncrement: 1);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "sideboard", deckCountIncrement: 1);

        var rows = await repository.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring", boardFilter: "mainboard");

        var row = Assert.Single(rows);
        Assert.Equal("Ramp", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
        Assert.Equal(1, row.Count);
        Assert.Equal(1, row.DeckCount);
        Assert.Equal(1, totals.TotalDeckCount);
        Assert.Single(totals.BoardDeckCounts);
        Assert.Equal(1, totals.BoardDeckCounts["mainboard"]);
    }

    [Fact]
    public async Task GetCategoryRowsForCardAsync_PreservesDisplayGrainRowShape()
    {
        var repository = CreateRepository();
        var rows = new[]
        {
            new CategoryKnowledgeRow("Ramp", "Sol Ring", 5, 2),
            new CategoryKnowledgeRow("Draw", "Sol Ring", 2, 1),
        };

        await repository.ReplaceSourceRowsAsync("archidekt_live:deck-1", rows, board: "mainboard", deckCount: 1);

        var results = await repository.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, row => row.Category == "Ramp" && row.CardName == "Sol Ring" && row.Count == 5 && row.DeckCount == 2);
        Assert.Contains(results, row => row.Category == "Draw" && row.CardName == "Sol Ring" && row.Count == 2 && row.DeckCount == 1);
    }

    [Fact]
    public async Task GetCategoryRowsForCommanderAsync_IntegerJoin_ReturnsAggregate()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "deck-1" });
        await repository.MarkDeckProcessedAsync("deck-1", "Krenko, Mob Boss");
        await repository.PersistObservedCategoriesAsync(
            "archidekt_live:deck-1",
            "Sol Ring",
            new[] { "Artifact" },
            quantity: 1,
            board: "mainboard",
            deckCountIncrement: 1);

        var rows = await repository.GetCategoryRowsForCommanderAsync("Krenko, Mob Boss");

        var row = Assert.Single(rows);
        Assert.Equal("Artifact", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
        Assert.Equal(1, row.Count);
        Assert.Equal(1, row.DeckCount);
    }

    [Fact]
    public async Task LiveSource_WriteLinksSourceToExistingDeckQueueRow()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "delta-2" });
        await repository.PersistObservedCategoriesAsync(
            "archidekt_live:delta-2",
            "Sol Ring",
            new[] { "Artifact" },
            quantity: 1,
            board: "mainboard",
            deckCountIncrement: 1);

        await using (var connection = await OpenConnectionAsync())
        {
            var linkedRows = await QuerySingleInt64Async(
                connection,
                """
                SELECT COUNT(1)
                FROM sources s
                JOIN deck_queue q ON q.id = s.deck_queue_id
                WHERE s.source = 'archidekt_live:delta-2'
                  AND q.deck_id = 'delta-2';
                """);
            Assert.Equal(1, linkedRows);
        }

        await repository.MarkDeckProcessedAsync("delta-2", "Krenko, Mob Boss");

        var rows = await repository.GetCategoryRowsForCommanderAsync("Krenko, Mob Boss");

        var row = Assert.Single(rows);
        Assert.Equal("Artifact", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
        Assert.Equal(1, row.Count);
        Assert.Equal(1, row.DeckCount);
    }

    [Fact]
    public async Task DisplaySpellingVariants_AreNotCollapsed()
    {
        var repository = CreateRepository();

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "SOL RING", new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);

        var rows = await repository.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Category == "Ramp" && row.CardName == "Sol Ring" && row.Count == 1 && row.DeckCount == 1);
        Assert.Contains(rows, row => row.Category == "Ramp" && row.CardName == "SOL RING" && row.Count == 1 && row.DeckCount == 1);
    }

    [Fact]
    public async Task ThreeSourceKinds_AggregateAndIsolateCorrectly()
    {
        var repository = CreateRepository();
        var urlSource = "archidekt_url:https://archidekt.com/decks/url-deck/test";

        await repository.AddDeckIdsAsync(new[] { "live-1", "queued-only" });
        await repository.MarkDeckProcessedAsync("live-1", "Kinnan, Bonder Prodigy");
        await repository.PersistObservedCategoriesAsync("archidekt_live:live-1", "Sol Ring", new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("edhrec", "Sol Ring", new[] { "Ramp" }, quantity: 1, board: "mainboard");
        await repository.PersistObservedCategoriesAsync(urlSource, "Sol Ring", new[] { "Ramp" }, quantity: 2, board: "mainboard", deckCountIncrement: 1);

        var categories = await repository.GetCategoriesAsync("Sol Ring");
        var cardRows = await repository.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");
        var commanderRows = await repository.GetCategoryRowsForCommanderAsync("Kinnan, Bonder Prodigy");
        var nextDeckIds = await repository.GetNextUnprocessedDeckIdsAsync(10);

        Assert.Equal(new[] { "Ramp" }, categories);
        var cardRow = Assert.Single(cardRows);
        Assert.Equal("Ramp", cardRow.Category);
        Assert.Equal("Sol Ring", cardRow.CardName);
        Assert.Equal(4, cardRow.Count);
        Assert.Equal(2, cardRow.DeckCount);
        var commanderRow = Assert.Single(commanderRows);
        Assert.Equal("Ramp", commanderRow.Category);
        Assert.Equal("Sol Ring", commanderRow.CardName);
        Assert.Equal(1, commanderRow.Count);
        Assert.Equal(1, commanderRow.DeckCount);
        Assert.Equal(new[] { "queued-only" }, nextDeckIds);
        Assert.DoesNotContain("edhrec", nextDeckIds);
        Assert.DoesNotContain(urlSource, nextDeckIds);
    }

    [Fact]
    public async Task GetCategoryDeckMembershipForCommanderAsync_ReturnsDistinctUnionAndMatchesFallbackFiltering()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2", "deck-3" });
        await repository.MarkDeckProcessedAsync("deck-1", "Kinnan, Bonder Prodigy");
        await repository.MarkDeckProcessedAsync("deck-2", "Kinnan, Bonder Prodigy");
        await repository.MarkDeckProcessedAsync("deck-3", "Kinnan, Bonder Prodigy");

        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp", "Artifact" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Sol Ring", new[] { "Ramp", "Artifact" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-3", "Sol Ring", new[] { "Ramp", "Artifact" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Arcane Signet", new[] { "ramp", "Artifact" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Arcane Signet", new[] { "ramp", "Artifact" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);

        var rows = await repository.GetCategoryRowsForCommanderAsync("Kinnan, Bonder Prodigy");
        var memberships = await repository.GetCategoryDeckMembershipForCommanderAsync("Kinnan, Bonder Prodigy");

        Assert.DoesNotContain(rows, row => row.CardName == "Sol Ring" && row.Category == "Artifact");
        Assert.DoesNotContain(rows, row => row.CardName == "Arcane Signet" && row.Category == "Artifact");
        Assert.DoesNotContain(memberships, membership => membership.CardName == "Sol Ring" && membership.Category == "Artifact");
        Assert.DoesNotContain(memberships, membership => membership.CardName == "Arcane Signet" && membership.Category == "Artifact");

        var rampRowDeckTotal = rows
            .Where(row => string.Equals(row.Category, "Ramp", StringComparison.OrdinalIgnoreCase))
            .Sum(row => row.DeckCount);
        var rampDistinctDecks = memberships
            .Where(membership => string.Equals(membership.Category, "Ramp", StringComparison.OrdinalIgnoreCase))
            .Select(membership => membership.DeckId)
            .Distinct()
            .Count();

        Assert.Equal(5, rampRowDeckTotal);
        Assert.Equal(3, rampDistinctDecks);
        Assert.True(rampDistinctDecks < rampRowDeckTotal);
    }

    [Fact]
    public async Task GetCategoryDeckMembershipForCommanderAsync_BoardFilterPreservesDefaultBehavior()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2" });
        await repository.MarkDeckProcessedAsync("deck-1", "Kinnan, Bonder Prodigy");
        await repository.MarkDeckProcessedAsync("deck-2", "Kinnan, Bonder Prodigy");
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Swan Song", new[] { "Interaction" }, quantity: 1, board: "sideboard", deckCountIncrement: 1);

        var mainboardMemberships = await repository.GetCategoryDeckMembershipForCommanderAsync(
            "Kinnan, Bonder Prodigy",
            boardFilter: "mainboard",
            cancellationToken: CancellationToken.None);
        var defaultMemberships = await repository.GetCategoryDeckMembershipForCommanderAsync("Kinnan, Bonder Prodigy");

        var mainboardMembership = Assert.Single(mainboardMemberships);
        Assert.Equal("Sol Ring", mainboardMembership.CardName);
        Assert.DoesNotContain(mainboardMemberships, membership => membership.CardName == "Swan Song");
        Assert.Equal(2, defaultMemberships.Count);
        Assert.Contains(defaultMemberships, membership => membership.CardName == "Sol Ring");
        Assert.Contains(defaultMemberships, membership => membership.CardName == "Swan Song");
    }

    [Fact]
    public async Task GetCategoryDeckMembershipForCommanderAsync_BoardFilterMainboard_ExcludesSideboardAndMaybeboardRows()
    {
        var repository = CreateRepository();
        var deckIds = Enumerable.Range(1, 220)
            .Select(index => $"deck-{index}")
            .ToArray();

        await repository.AddDeckIdsAsync(deckIds);
        foreach (var deckId in deckIds)
        {
            await repository.MarkDeckProcessedAsync(deckId, "Kinnan, Bonder Prodigy");
        }

        for (var index = 1; index <= 100; index++)
        {
            await repository.PersistObservedCategoriesAsync(
                $"archidekt_live:deck-{index}",
                $"Mainboard Card {index}",
                new[] { "RampPlan" },
                quantity: 1,
                board: "mainboard",
                deckCountIncrement: 1);
        }

        for (var index = 101; index <= 160; index++)
        {
            await repository.PersistObservedCategoriesAsync(
                $"archidekt_live:deck-{index}",
                $"Sideboard Card {index}",
                new[] { "InteractionPlan" },
                quantity: 1,
                board: "sideboard",
                deckCountIncrement: 1);
        }

        for (var index = 161; index <= 220; index++)
        {
            await repository.PersistObservedCategoriesAsync(
                $"archidekt_live:deck-{index}",
                $"Maybeboard Card {index}",
                new[] { "ValueEngine" },
                quantity: 1,
                board: "maybeboard",
                deckCountIncrement: 1);
        }

        var allMemberships = await repository.GetCategoryDeckMembershipForCommanderAsync("Kinnan, Bonder Prodigy");
        var mainboardMemberships = await repository.GetCategoryDeckMembershipForCommanderAsync(
            "Kinnan, Bonder Prodigy",
            boardFilter: "mainboard",
            cancellationToken: CancellationToken.None);

        Assert.Equal(220, allMemberships.Count);
        Assert.Contains(allMemberships, membership => membership.CardName == "Sideboard Card 101");
        Assert.Contains(allMemberships, membership => membership.CardName == "Maybeboard Card 161");
        Assert.Equal(100, mainboardMemberships.Count);
        Assert.DoesNotContain(mainboardMemberships, membership => membership.CardName == "Sideboard Card 101");
        Assert.DoesNotContain(mainboardMemberships, membership => membership.CardName == "Maybeboard Card 161");
    }

    [Fact]
    public async Task GetCategoryDeckMembershipForCommanderAsync_BoardFilterMainboard_ExcludesNonMainboardRowsForSingleOversizedDeck()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "deck-oversized" });
        await repository.MarkDeckProcessedAsync("deck-oversized", "Kinnan, Bonder Prodigy");

        for (var index = 1; index <= 100; index++)
        {
            await repository.PersistObservedCategoriesAsync(
                "archidekt_live:deck-oversized",
                $"Oversized Mainboard Card {index}",
                new[] { "RampPlan" },
                quantity: 1,
                board: "mainboard",
                deckCountIncrement: 1);
        }

        for (var index = 101; index <= 160; index++)
        {
            await repository.PersistObservedCategoriesAsync(
                "archidekt_live:deck-oversized",
                $"Oversized Sideboard Card {index}",
                new[] { "InteractionPlan" },
                quantity: 1,
                board: "sideboard",
                deckCountIncrement: 1);
        }

        for (var index = 161; index <= 220; index++)
        {
            await repository.PersistObservedCategoriesAsync(
                "archidekt_live:deck-oversized",
                $"Oversized Maybeboard Card {index}",
                new[] { "ValueEngine" },
                quantity: 1,
                board: "maybeboard",
                deckCountIncrement: 1);
        }

        var allMemberships = await repository.GetCategoryDeckMembershipForCommanderAsync("Kinnan, Bonder Prodigy");
        var mainboardMemberships = await repository.GetCategoryDeckMembershipForCommanderAsync(
            "Kinnan, Bonder Prodigy",
            boardFilter: "mainboard",
            cancellationToken: CancellationToken.None);

        Assert.Equal(220, allMemberships.Count);
        Assert.Contains(allMemberships, membership => membership.CardName == "Oversized Sideboard Card 101");
        Assert.Contains(allMemberships, membership => membership.CardName == "Oversized Maybeboard Card 161");
        Assert.Equal(100, mainboardMemberships.Count);
        Assert.DoesNotContain(mainboardMemberships, membership => membership.CardName == "Oversized Sideboard Card 101");
        Assert.DoesNotContain(mainboardMemberships, membership => membership.CardName == "Oversized Maybeboard Card 161");
    }

    [Fact]
    public async Task GetContentHashesByIdsAsync_ReturnsHashedNullAndMissingEntriesAsExpected()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2" });
        await repository.MarkDeckProcessedAsync("deck-1", "Kinnan, Bonder Prodigy");
        await repository.MarkDeckProcessedAsync("deck-2", "Kinnan, Bonder Prodigy");
        await repository.SetContentHashAsync("deck-1", "hash-1");

        await using var connection = await OpenConnectionAsync();
        var deckQueueIds = await QueryInt64ListAsync(
            connection,
            "SELECT id FROM deck_queue WHERE deck_id IN ('deck-1', 'deck-2') ORDER BY deck_id;");

        var hashes = await repository.GetContentHashesByIdsAsync(new[] { deckQueueIds[0], deckQueueIds[1], 999999L });

        Assert.Equal("hash-1", hashes[deckQueueIds[0]]);
        Assert.True(hashes.ContainsKey(deckQueueIds[1]));
        Assert.Null(hashes[deckQueueIds[1]]);
        Assert.False(hashes.ContainsKey(999999L));
    }

    [Fact]
    public async Task UrlImportSource_ContributesToCardRowsWithoutQueueing()
    {
        var repository = CreateRepository();
        var source = "archidekt_url:https://archidekt.com/decks/777777/test";

        await repository.PersistObservedCategoriesAsync(source, "Sol Ring", new[] { "Ramp" }, quantity: 2, board: "mainboard", deckCountIncrement: 1);

        var rows = await repository.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");
        var nextDeckIds = await repository.GetNextUnprocessedDeckIdsAsync(10);

        var row = Assert.Single(rows);
        Assert.Equal("Ramp", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
        Assert.Equal(2, row.Count);
        Assert.Equal(1, row.DeckCount);
        Assert.Empty(nextDeckIds);
    }

    [Fact]
    public async Task SameCardAcrossTwoDecks_InternsToOneCardRow()
    {
        var repository = CreateRepository();

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2" });
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-1", "Sol Ring", new[] { "Ramp" }, quantity: 1, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistObservedCategoriesAsync("archidekt_live:deck-2", "Sol Ring", new[] { "Ramp" }, quantity: 2, board: "mainboard", deckCountIncrement: 1);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-1", "Sol Ring", board: "mainboard", deckCountIncrement: 1);
        await repository.PersistCardDeckTotalsAsync("archidekt_live:deck-2", "Sol Ring", board: "mainboard", deckCountIncrement: 1);

        var rows = await repository.GetCategoryRowsForCardAsync("Sol Ring", boardFilter: "mainboard");
        var totals = await repository.GetCardDeckTotalsAsync("Sol Ring", boardFilter: "mainboard");
        await using var connection = await OpenConnectionAsync();
        var cardRows = await QuerySingleInt64Async(connection, "SELECT COUNT(1) FROM cards WHERE normalized_card_name = 'sol ring';");
        var sourceRows = await QuerySingleInt64Async(connection, "SELECT COUNT(1) FROM sources WHERE source IN ('archidekt_live:deck-1', 'archidekt_live:deck-2');");

        var row = Assert.Single(rows);
        Assert.Equal("Ramp", row.Category);
        Assert.Equal("Sol Ring", row.CardName);
        Assert.Equal(3, row.Count);
        Assert.Equal(2, row.DeckCount);
        Assert.Equal(2, totals.TotalDeckCount);
        Assert.Equal(2, totals.BoardDeckCounts["mainboard"]);
        Assert.Equal(1, cardRows);
        Assert.Equal(2, sourceRows);
    }

    [Fact]
    public async Task EnsureSchema_OnLegacyDeckQueue_AddsMetadataColumnsWithoutBackfill()
    {
        await using (var connection = await OpenConnectionAsync())
        {
            await ExecuteNonQueryAsync(
                connection,
                """
                CREATE TABLE deck_queue (
                    id INTEGER PRIMARY KEY,
                    deck_id TEXT NOT NULL,
                    inserted_utc TEXT NOT NULL,
                    processed INTEGER NOT NULL DEFAULT 0,
                    skipped INTEGER NOT NULL DEFAULT 0,
                    last_checked_utc TEXT,
                    commander_name TEXT NULL,
                    content_hash TEXT NULL
                );
                INSERT INTO deck_queue (deck_id, inserted_utc) VALUES ('legacy-deck', '2026-08-01T12:00:00.0000000+00:00');
                """);
        }

        var repository = CreateRepository();
        await repository.EnsureSchemaAsync();
        await repository.EnsureSchemaAsync();

        await using var migrated = await OpenConnectionAsync();
        var columns = await QueryStringListAsync(migrated, "SELECT name FROM pragma_table_info('deck_queue');");
        Assert.Contains("archidekt_edh_bracket", columns);
        Assert.Contains("archidekt_deck_format", columns);
        Assert.Contains("archidekt_theorycrafted", columns);
        Assert.Contains("archidekt_created_utc", columns);
        Assert.Contains("archidekt_updated_utc", columns);
        Assert.Contains("archidekt_metadata_captured_utc", columns);
        Assert.Equal(1L, await QuerySingleInt64Async(migrated, "SELECT COUNT(1) FROM deck_queue WHERE deck_id = 'legacy-deck' AND archidekt_metadata_captured_utc IS NULL;"));
    }

    private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<long>> QueryInt64ListAsync(SqliteConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;

        var values = new List<long>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetInt64(0));
        }

        return values;
    }

    private static async Task<IReadOnlyList<string>> QueryStringListAsync(SqliteConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task<long> QuerySingleInt64Async(SqliteConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
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
