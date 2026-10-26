using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Services.Http;
using DeckFlow.Web.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Polly.Registry;
using Xunit;

namespace DeckFlow.Web.Tests;

// MTG_DATA_DIR is process-wide, so these tests are serialized to avoid cross-test interference.
/// <summary>
/// xUnit collection definition that serializes <see cref="CategoryKnowledgeStoreTests"/> to prevent
/// parallel interference on the shared <c>MTG_DATA_DIR</c> environment variable.
/// </summary>
[CollectionDefinition("CategoryKnowledgeStoreTests", DisableParallelization = true)]
public sealed class CategoryKnowledgeStoreTestsCollection
{
}

/// <summary>
/// Tests for <see cref="CategoryKnowledgeStore"/> covering database-path resolution and category-data persistence.
/// </summary>
[Collection("CategoryKnowledgeStoreTests")]
public sealed class CategoryKnowledgeStoreTests
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    [Fact]
    public void DatabasePath_UsesMtgDataDirWhenSet()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempDir = Path.Combine(Path.GetTempPath(), "deckflow-data-" + Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", tempDir);

            var store = CreateStore("/repo/content-root");
            var expectedRoot = Path.GetFullPath(tempDir);

            Assert.NotNull(store.DatabasePath);
            Assert.StartsWith(expectedRoot, store.DatabasePath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("category-knowledge.db", store.DatabasePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public void DatabasePath_DefaultsFromContentRootPathWhenMtgDataDirUnset()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);

            var contentRoot = Path.Combine(Path.GetTempPath(), "deckflow-content-" + Guid.NewGuid().ToString("N"));
            var store = CreateStore(contentRoot);

            Assert.NotNull(store.DatabasePath);
            Assert.Contains("artifacts", store.DatabasePath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("category-knowledge.db", store.DatabasePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task GetDatabaseSizeBytesAsync_WithExistingCustomSqliteFile_ReturnsFileLength()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-size-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempRoot, "custom.db");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllBytesAsync(databasePath, new byte[4096]);

        await WithSqliteConnectionAsync($"Data Source={databasePath}", async () =>
        {
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            Assert.Equal(4096L, await store.GetDatabaseSizeBytesAsync());
        });
    }

    [Fact]
    public async Task GetDatabaseSizeBytesAsync_WithMissingSqliteFile_ReturnsNullWithoutCreatingFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-size-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempRoot, "missing.db");
        Directory.CreateDirectory(tempRoot);

        await WithSqliteConnectionAsync($"Data Source={databasePath}", async () =>
        {
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            Assert.Null(await store.GetDatabaseSizeBytesAsync());
            Assert.False(File.Exists(databasePath));
        });
    }

    [Fact]
    public async Task GetDatabaseSizeBytesAsync_WithInMemorySqlite_ReturnsNull()
    {
        await WithSqliteConnectionAsync("Data Source=:memory:", async () =>
        {
            var store = CreateStore();
            Assert.Null(await store.GetDatabaseSizeBytesAsync());
        });
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    public async Task GetCategoriesAsync_ThrowsForBlankCardName(string? cardName, Type expectedExceptionType)
    {
        var store = CreateStore();

        if (expectedExceptionType == typeof(ArgumentNullException))
        {
            var nullException = await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetCategoriesAsync(cardName!));
            Assert.Equal("cardName", nullException.ParamName);
            return;
        }

        var valueException = await Assert.ThrowsAsync<ArgumentException>(() => store.GetCategoriesAsync(cardName!));
        Assert.Equal("cardName", valueException.ParamName);
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    public async Task GetCategoryDeckCountsAsync_ThrowsForBlankCardName(string? cardName, Type expectedExceptionType)
    {
        var store = CreateStore();

        if (expectedExceptionType == typeof(ArgumentNullException))
        {
            var nullException = await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetCategoryDeckCountsAsync(cardName!));
            Assert.Equal("cardName", nullException.ParamName);
            return;
        }

        var valueException = await Assert.ThrowsAsync<ArgumentException>(() => store.GetCategoryDeckCountsAsync(cardName!));
        Assert.Equal("cardName", valueException.ParamName);
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    public async Task GetCategoryRowsAsync_ThrowsForBlankCardName(string? cardName, Type expectedExceptionType)
    {
        var store = CreateStore();

        if (expectedExceptionType == typeof(ArgumentNullException))
        {
            var nullException = await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetCategoryRowsAsync(cardName!));
            Assert.Equal("cardName", nullException.ParamName);
            return;
        }

        var valueException = await Assert.ThrowsAsync<ArgumentException>(() => store.GetCategoryRowsAsync(cardName!));
        Assert.Equal("cardName", valueException.ParamName);
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    public async Task GetCardDeckTotalsAsync_ThrowsForBlankCardName(string? cardName, Type expectedExceptionType)
    {
        var store = CreateStore();

        if (expectedExceptionType == typeof(ArgumentNullException))
        {
            var nullException = await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetCardDeckTotalsAsync(cardName!));
            Assert.Equal("cardName", nullException.ParamName);
            return;
        }

        var valueException = await Assert.ThrowsAsync<ArgumentException>(() => store.GetCardDeckTotalsAsync(cardName!));
        Assert.Equal("cardName", valueException.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PersistObservedCategoriesAsync_IgnoresBlankSource(string? source)
    {
        var store = CreateStore();

        await store.PersistObservedCategoriesAsync(source!, "Card Name", ["Draw"], quantity: 1);
    }

    [Theory]
    [InlineData("", new[] { "Draw" }, 1)]
    [InlineData("Card Name", new string[0], 1)]
    [InlineData("Card Name", new[] { "Draw" }, 0)]
    public async Task PersistObservedCategoriesAsync_IgnoresEmptyCardNameEmptyCategoriesOrNonPositiveQuantity(string cardName, string[] categories, int quantity)
    {
        var store = CreateStore();

        await store.PersistObservedCategoriesAsync("source", cardName, categories, quantity);
    }

    [Fact]
    public async Task GetPagedProcessedCommandersAsync_MapsRepositoryRowsAndClampsPagingInputs()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));

            await store.MarkUrlDeckProcessedAsync("deck-001", "Commander One");
            await store.MarkUrlDeckProcessedAsync("deck-002", "Commander One");
            await store.MarkUrlDeckProcessedAsync("deck-003", "Commander Two");

            var rows = await store.GetFilteredProcessedCommandersAsync(page: 0, pageSize: 0, CommanderGridQuery.Default);
            var count = await store.GetDistinctProcessedCommanderCountAsync();

            var row = Assert.Single(rows);
            Assert.Equal("Commander One", row.CommanderName);
            Assert.Equal(2, row.DeckCount);
            Assert.False(string.IsNullOrWhiteSpace(row.LastProcessedUtc));
            Assert.Equal(2, count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task ProcessedCommanderGridQueries_ReturnCachedValuesUntilExpiry()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            await store.MarkUrlDeckProcessedAsync("deck-001", "Commander One");

            var initialRows = await store.GetFilteredProcessedCommandersAsync(page: 1, pageSize: 20, CommanderGridQuery.Default);
            var initialCount = await store.GetDistinctProcessedCommanderCountAsync();
            await store.MarkUrlDeckProcessedAsync("deck-002", "Commander Two");

            Assert.Single(await store.GetFilteredProcessedCommandersAsync(page: 1, pageSize: 20, CommanderGridQuery.Default));
            Assert.Equal(1, await store.GetDistinctProcessedCommanderCountAsync());
            Assert.Single(initialRows);
            Assert.Equal(1, initialCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Theory]
    [InlineData("commander o", 1)]
    [InlineData("commander t", 1)]
    [InlineData("other", 1)]
    [InlineData("commander", 2)]
    [InlineData("*", 0)]
    [InlineData(null, 3)]
    public async Task FilteredCommanderGrid_ReturnsExpectedRows(string? search, int expectedCount)
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            await store.MarkUrlDeckProcessedAsync("deck-001", "Commander One");
            await store.MarkUrlDeckProcessedAsync("deck-002", "Commander Two");
            await store.MarkUrlDeckProcessedAsync("deck-003", "Other Leader");

            var query = CommanderGridQuery.FromRequest(search, null, null);
            Assert.Equal(expectedCount, (await store.GetFilteredProcessedCommandersAsync(1, 20, query)).Count);
            Assert.Equal(expectedCount, await store.GetFilteredProcessedCommanderCountAsync(query));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task GetTotalProcessedDeckCountSinceAsync_WithMixedProcessedRows_ReturnsOnlyProcessedRowsAtOrAfterCutoff()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var cutoffUtc = new DateTime(2026, 01, 15, 12, 00, 00, DateTimeKind.Utc);

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));

            _ = await store.GetTotalProcessedDeckCountAsync();

            Assert.NotNull(store.DatabasePath);

            await using var connection = new SqliteConnection($"Data Source={store.DatabasePath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
                VALUES
                    ('deck-before', @beforeCutoff, 1, 0, NULL, 'Commander Before'),
                    ('deck-at-cutoff', @atCutoff, 1, 0, NULL, 'Commander At Cutoff'),
                    ('deck-after', @afterCutoff, 1, 0, NULL, 'Commander After'),
                    ('deck-unprocessed', @unprocessedAfterCutoff, 0, 0, NULL, 'Commander Pending');
                """;
            command.Parameters.AddWithValue("@beforeCutoff", cutoffUtc.AddMinutes(-1).ToString("O"));
            command.Parameters.AddWithValue("@atCutoff", cutoffUtc.ToString("O"));
            command.Parameters.AddWithValue("@afterCutoff", cutoffUtc.AddMinutes(1).ToString("O"));
            command.Parameters.AddWithValue("@unprocessedAfterCutoff", cutoffUtc.AddMinutes(2).ToString("O"));
            await command.ExecuteNonQueryAsync();

            var count = await store.GetTotalProcessedDeckCountSinceAsync(cutoffUtc);

            Assert.Equal(2, count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
            if (Directory.Exists(tempRoot))
            {
                foreach (var db in Directory.EnumerateFiles(tempRoot, "*.db", SearchOption.AllDirectories))
                {
                    ClearPool(db);
                }
            }
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MarkUrlDeckProcessedAsync_MetadataBearingImport_PersistsAllMetadataColumns()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var metadata = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-03T00:00:00Z"));

        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            await store.MarkUrlDeckProcessedAsync("metadata-url", "Commander", metadata);
            var row = await ReadMetadataRowAsync(Assert.IsType<string>(store.DatabasePath), "metadata-url");

            Assert.Equal(metadata.EdhBracket, row.EdhBracket);
            Assert.Equal(metadata.DeckFormat, row.DeckFormat);
            Assert.Equal(metadata.Theorycrafted, row.Theorycrafted);
            Assert.Equal(metadata.CreatedUtc, row.CreatedUtc);
            Assert.Equal(metadata.UpdatedUtc, row.UpdatedUtc);
            Assert.Equal(metadata.CapturedUtc, row.CapturedUtc);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
            if (Directory.Exists(tempRoot))
            {
                foreach (var db in Directory.EnumerateFiles(tempRoot, "*.db", SearchOption.AllDirectories))
                {
                    ClearPool(db);
                }
            }
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task<MetadataRow> ReadMetadataRowAsync(string databasePath, string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT archidekt_edh_bracket, archidekt_deck_format, archidekt_theorycrafted, archidekt_created_utc, archidekt_updated_utc, archidekt_metadata_captured_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new MetadataRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetBoolean(2), DateTimeOffset.Parse(reader.GetString(3)), DateTimeOffset.Parse(reader.GetString(4)), DateTimeOffset.Parse(reader.GetString(5)));
    }

    private sealed record MetadataRow(int EdhBracket, int DeckFormat, bool Theorycrafted, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, DateTimeOffset CapturedUtc);

    [Fact]
    public async Task FakeCategoryKnowledgeStore_ReturnsConfiguredPagedCommandersAndRecordsInputs()
    {
        var fake = new FakeCategoryKnowledgeStore
        {
            PagedCommandersResult = new[]
            {
                new HarvestedCommanderRow("Commander", 7, "2026-01-01T00:00:00.0000000Z")
            }
        };

        var rows = await fake.GetFilteredProcessedCommandersAsync(page: 3, pageSize: 25, CommanderGridQuery.Default);

        var row = Assert.Single(rows);
        Assert.Equal("Commander", row.CommanderName);
        Assert.Equal(7, row.DeckCount);
        Assert.Equal(3, fake.LastPagedCommanderPage);
        Assert.Equal(25, fake.LastPagedCommanderPageSize);
    }

    [Fact]
    public async Task AllFilteredCommanders_PrefixQuery_ReturnsMatchesInGridOrder()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            await store.MarkUrlDeckProcessedAsync("deck-001", "Alpha One");
            await store.MarkUrlDeckProcessedAsync("deck-002", "Alpha Two");
            await store.MarkUrlDeckProcessedAsync("deck-003", "Alpha Three");
            await store.MarkUrlDeckProcessedAsync("deck-004", "Beta Four");

            var rows = await store.GetAllFilteredProcessedCommandersAsync(CommanderGridQuery.FromRequest("Alpha", null, null), 20);

            Assert.Equal(new[] { "Alpha Three", "Alpha Two", "Alpha One" }, rows.Select(row => row.CommanderName));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task AllFilteredCommanders_MaxRows_ReturnsAtMostRequestedRows()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            await store.MarkUrlDeckProcessedAsync("deck-001", "Alpha One");
            await store.MarkUrlDeckProcessedAsync("deck-002", "Alpha Two");
            await store.MarkUrlDeckProcessedAsync("deck-003", "Alpha Three");

            Assert.Equal(2, (await store.GetAllFilteredProcessedCommandersAsync(CommanderGridQuery.FromRequest("Alpha", null, null), 2)).Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task AllFilteredCommanders_AfterNewMatchingCommander_ReturnsFreshRows()
    {
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
            var store = CreateStore(Path.Combine(tempRoot, "content"));
            var query = CommanderGridQuery.FromRequest("Alpha", null, null);
            await store.MarkUrlDeckProcessedAsync("deck-001", "Alpha One");
            Assert.Single(await store.GetAllFilteredProcessedCommandersAsync(query, 20));

            await store.MarkUrlDeckProcessedAsync("deck-002", "Alpha Two");

            Assert.Equal(2, (await store.GetAllFilteredProcessedCommandersAsync(query, 20)).Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task AllFilteredCommanders_MaxRowsBelowOne_ThrowsArgumentOutOfRangeException()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetAllFilteredProcessedCommandersAsync(CommanderGridQuery.FromRequest(null, null, null), 0));
    }

    [Fact]
    public async Task FakeCategoryKnowledgeStore_ReturnsConfiguredCategoryDeckCounts()
    {
        var fake = new FakeCategoryKnowledgeStore();
        fake.CategoryDeckCountsByName["Sol Ring"] = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["draw"] = 12
        };

        var counts = await fake.GetCategoryDeckCountsAsync("Sol Ring");

        Assert.Equal(12, counts["draw"]);
    }

    [Fact]
    public async Task GetTotalProcessedDeckCountAsync_ExcludesSkippedDecks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var store = CreateStore(Path.Combine(tempRoot, "content"));
        var deckIdPrefix = Guid.NewGuid().ToString("N");

        _ = await store.GetTotalProcessedDeckCountAsync();

        Assert.NotNull(store.DatabasePath);
        await using var connection = new SqliteConnection($"Data Source={store.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
            VALUES ('{deckIdPrefix}-processed', '2026-01-01T00:00:00.0000000Z', 1, 0, NULL, 'Commander'),
                   ('{deckIdPrefix}-skipped', '2026-01-01T00:00:00.0000000Z', 1, 1, NULL, 'Commander');
            """;
        await command.ExecuteNonQueryAsync();

        Assert.Equal(1, await store.GetTotalProcessedDeckCountAsync());
    }

    [Fact]
    public async Task GetTotalProcessedDeckCountSinceAsync_ExcludesSkippedDecks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var store = CreateStore(Path.Combine(tempRoot, "content"));
        var cutoffUtc = new DateTime(2026, 01, 15, 12, 00, 00, DateTimeKind.Utc);
        var deckIdPrefix = Guid.NewGuid().ToString("N");

        _ = await store.GetTotalProcessedDeckCountAsync();

        Assert.NotNull(store.DatabasePath);
        await using var connection = new SqliteConnection($"Data Source={store.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
            VALUES ('{deckIdPrefix}-processed', @cutoff, 1, 0, NULL, 'Commander'),
                   ('{deckIdPrefix}-skipped', @cutoff, 1, 1, NULL, 'Commander');
            """;
        command.Parameters.AddWithValue("@cutoff", cutoffUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();

        Assert.Equal(1, await store.GetTotalProcessedDeckCountSinceAsync(cutoffUtc));
    }

    [Fact]
    public async Task GetSkipReasonCountsAsync_GroupsByReasonAndReportsNullAsUnrecorded()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var store = CreateStore(Path.Combine(tempRoot, "content"));
        var deckIdPrefix = Guid.NewGuid().ToString("N");

        _ = await store.GetTotalProcessedDeckCountAsync();

        Assert.NotNull(store.DatabasePath);
        await using var connection = new SqliteConnection($"Data Source={store.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, skip_reason, last_checked_utc, commander_name)
            VALUES ('{deckIdPrefix}-wrong-size-one', '2026-01-01T00:00:00.0000000Z', 0, 1, 'wrong_size', NULL, 'Commander'),
                   ('{deckIdPrefix}-wrong-size-two', '2026-01-01T00:00:00.0000000Z', 0, 1, 'wrong_size', NULL, 'Commander'),
                   ('{deckIdPrefix}-fetch-failure', '2026-01-01T00:00:00.0000000Z', 0, 1, 'fetch_failure', NULL, 'Commander'),
                   ('{deckIdPrefix}-legacy', '2026-01-01T00:00:00.0000000Z', 0, 1, NULL, NULL, 'Commander'),
                   ('{deckIdPrefix}-processed', '2026-01-01T00:00:00.0000000Z', 1, 0, NULL, NULL, 'Commander');
            """;
        await command.ExecuteNonQueryAsync();

        var counts = await store.GetSkipReasonCountsAsync();

        Assert.Equal(3, counts.Count);
        Assert.Equal(2, counts["wrong_size"]);
        Assert.Equal(1, counts["fetch_failure"]);
        Assert.Equal(1, counts["unrecorded"]);
    }

    [Fact]
    public async Task GetSkipReasonCountsAsync_DefaultInterfaceImplementation_ReturnsEmpty()
    {
        ICategoryKnowledgeStore store = new FakeCategoryKnowledgeStore();

        var counts = await store.GetSkipReasonCountsAsync();

        Assert.Empty(counts);
    }

    [Fact]
    public async Task RejectUrlDeckAsync_StoredUrlAndLiveRows_RemovesBothAndRecordsReason()
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string source = "archidekt_url:https://archidekt.com/decks/123";
            await SeedRejectedDeckRowsAsync(store, repository, source, "123");

            await store.RejectUrlDeckAsync(source, "123", "Test Commander", TestMetadata(), DeckSkipReasons.WrongSize);

            Assert.False(await repository.HasSourceDataAsync(source));
            Assert.False(await repository.HasSourceDataAsync("archidekt_live:123"));
            Assert.Null(await repository.GetContentHashAsync("123"));
            Assert.Equal(new Dictionary<string, int> { [DeckSkipReasons.WrongSize] = 1 }, await store.GetSkipReasonCountsAsync());
            Assert.Equal(0, await store.GetDistinctProcessedCommanderCountAsync());
            Assert.Equal(0, await store.GetTotalObservationCountAsync());
        });
    }

    [Theory]
    [InlineData(DeckSkipReasons.FetchFailure)]
    [InlineData(DeckSkipReasons.Retention)]
    [InlineData("")]
    public async Task RejectUrlDeckAsync_NonGateReason_ThrowsBeforeWriting(string skipReason)
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string source = "archidekt_url:https://archidekt.com/decks/123";
            await SeedRejectedDeckRowsAsync(store, repository, source, "123");

            await Assert.ThrowsAsync<ArgumentException>(() => store.RejectUrlDeckAsync(source, "123", "Test Commander", TestMetadata(), skipReason));

            Assert.True(await repository.HasSourceDataAsync(source));
            Assert.Equal("h1", await repository.GetContentHashAsync("123"));
        });
    }

    [Fact]
    public async Task MarkUrlDeckProcessedAsync_AfterRejection_ClearsSkipReasonAndCountsDeck()
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string source = "archidekt_url:https://archidekt.com/decks/123";
            await SeedRejectedDeckRowsAsync(store, repository, source, "123");
            await store.RejectUrlDeckAsync(source, "123", "Test Commander", TestMetadata(), DeckSkipReasons.WrongSize);

            await store.MarkUrlDeckProcessedAsync("123", "Test Commander", TestMetadata());

            Assert.Empty(await store.GetSkipReasonCountsAsync());
            Assert.Equal(1, await store.GetDistinctProcessedCommanderCountAsync());
        });
    }

    [Fact]
    public async Task RejectUrlDeckAsync_EquivalentUrlSpelling_RemovesEarlierUrlRows()
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string storedSource = "archidekt_url:https://archidekt.com/decks/123";
            const string rejectedSource = "archidekt_url:https://archidekt.com/decks/123/";
            await SeedRejectedDeckRowsAsync(store, repository, storedSource, "123");
            await store.PersistObservedCategoriesAsync(rejectedSource, "Mana Crypt", ["Ramp"], 1, "mainboard", 1);

            await store.RejectUrlDeckAsync(rejectedSource, "123", "Test Commander", TestMetadata(), DeckSkipReasons.WrongSize);

            Assert.False(await repository.HasSourceDataAsync(storedSource));
            Assert.False(await repository.HasSourceDataAsync(rejectedSource));
        });
    }

    [Fact]
    public async Task RejectUrlDeckAsync_OtherDeckUrlRows_AreUntouched()
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string rejectedSource = "archidekt_url:https://archidekt.com/decks/123";
            const string otherSource = "archidekt_url:https://archidekt.com/decks/456";
            await SeedRejectedDeckRowsAsync(store, repository, rejectedSource, "123");
            await store.PersistObservedCategoriesAsync(otherSource, "Mana Crypt", ["Ramp"], 1, "mainboard", 1);

            await store.RejectUrlDeckAsync(rejectedSource, "123", "Test Commander", TestMetadata(), DeckSkipReasons.WrongSize);

            Assert.True(await repository.HasSourceDataAsync(otherSource));
        });
    }

    [Fact]
    public async Task RejectUrlDeckAsync_HashClearFails_RollsBackRowRemoval()
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string source = "archidekt_url:https://archidekt.com/decks/123";
            await SeedRejectedDeckRowsAsync(store, repository, source, "123");
            await using var connection = new SqliteConnection($"Data Source={store.DatabasePath}");
            await connection.OpenAsync();
            await using var trigger = connection.CreateCommand();
            trigger.CommandText = "CREATE TRIGGER reject_hash_clear BEFORE UPDATE OF content_hash ON deck_queue WHEN NEW.content_hash IS NULL BEGIN SELECT RAISE(ABORT, 'hash clear failure'); END;";
            await trigger.ExecuteNonQueryAsync();

            await Assert.ThrowsAsync<SqliteException>(() => store.RejectUrlDeckAsync(source, "123", "Test Commander", TestMetadata(), DeckSkipReasons.WrongSize));

            Assert.True(await repository.HasSourceDataAsync(source));
            Assert.True(await repository.HasSourceDataAsync("archidekt_live:123"));
            Assert.Equal("h1", await repository.GetContentHashAsync("123"));
            Assert.Equal(2, await store.GetTotalObservationCountAsync());
        });
    }

    [Fact]
    public async Task RejectUrlDeckAsync_InvalidReason_OnFreshStore_ThrowsBeforeSchemaCreation()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", tempRoot);
            var store = CreateStore(Path.Combine(tempRoot, "content"));

            await Assert.ThrowsAsync<ArgumentException>(() => store.RejectUrlDeckAsync("archidekt_url:https://archidekt.com/decks/123", "123", "Test Commander", TestMetadata(), DeckSkipReasons.FetchFailure));

            Assert.False(File.Exists(Assert.IsType<string>(store.DatabasePath)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
        }
    }

    [Fact]
    public async Task RejectUrlDeckAsync_UrlSourceForOtherDeck_ThrowsBeforeWriting()
    {
        await WithTemporaryUrlStoreAsync(async (store, repository) =>
        {
            const string source = "archidekt_url:https://archidekt.com/decks/456";
            await SeedRejectedDeckRowsAsync(store, repository, source, "123");

            await Assert.ThrowsAsync<ArgumentException>(() => store.RejectUrlDeckAsync(source, "123", "Test Commander", TestMetadata(), DeckSkipReasons.WrongSize));

            Assert.True(await repository.HasSourceDataAsync(source));
            Assert.Equal("h1", await repository.GetContentHashAsync("123"));
        });
    }

    private static ArchidektDeckMetadata TestMetadata()
        => new(3, 1, false, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private static async Task SeedRejectedDeckRowsAsync(CategoryKnowledgeStore store, CategoryKnowledgeRepository repository, string source, string deckId)
    {
        await store.MarkUrlDeckProcessedAsync(deckId, "Test Commander", TestMetadata());
        await repository.SetContentHashAsync(deckId, "h1");
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await store.PersistObservedCategoriesAsync(source, "Sol Ring", ["Ramp"], 1, "mainboard", 1);
    }

    private static async Task WithTemporaryUrlStoreAsync(Func<CategoryKnowledgeStore, CategoryKnowledgeRepository, Task> action)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-store-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempRoot, "category-knowledge.db");
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", tempRoot);
            await WithSqliteConnectionAsync($"Data Source={databasePath}", async () =>
            {
                var store = CreateStore(Path.Combine(tempRoot, "content"));
                await action(store, new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath)));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
            ClearPool(databasePath);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1L, 0)]
    [InlineData(42L, 42)]
    [InlineData(2147483648L, int.MaxValue)]
    public void CoerceCount_SaturatesLargeAndNegativeCounts(object? result, int expected)
    {
        var count = CategoryKnowledgeStore.CoerceCount(result);

        Assert.Equal(expected, count);
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_CommanderWith101Decks_RemovesOldest()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-retention-" + Guid.NewGuid().ToString("N"));
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
        try
        {
            var store = CreateStore(Path.Combine(tempRoot, "content"), new HarvestRetentionOptions { MinFreeDiskBytes = 0, PauseMilliseconds = 0 });
            var repository = await SeedStoredDecksAsync(store, 101, "Commander", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

            var result = await store.RunPostHarvestRetentionAsync();

            Assert.Equal(1, result!.DecksRemoved);
            Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
            Assert.Equal(DeckSkipReasons.Retention, await GetSkipReasonAsync(store, "deck-000"));
            Assert.Equal(100, await repository.GetCommanderDeckCountAsync("Commander"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
            ClearPool(Path.Combine(tempRoot, "artifacts", "category-knowledge.db"));
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_PostHarvestMaxDecksZero_ReturnsNullWithoutRemoving()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-retention-" + Guid.NewGuid().ToString("N"));
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
        try
        {
            var store = CreateStore(Path.Combine(tempRoot, "content"), new HarvestRetentionOptions { PostHarvestMaxDecks = 0 });
            var repository = await SeedStoredDecksAsync(store, 101, "Commander", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

            Assert.Null(await store.RunPostHarvestRetentionAsync());
            Assert.Equal(101, await repository.GetCommanderDeckCountAsync("Commander"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
            ClearPool(Path.Combine(tempRoot, "artifacts", "category-knowledge.db"));
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_PostHarvestMaxDecksZero_TouchesNoDatabase()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "artifacts"), "not-a-directory");
        var contentRoot = Path.Combine(tempRoot, "root");
        var disabled = CreateStore(contentRoot, new HarvestRetentionOptions { PostHarvestMaxDecks = 0 });
        var enabled = CreateStore(contentRoot, new HarvestRetentionOptions { PostHarvestMaxDecks = 1 });

        try
        {
            Assert.Null(await disabled.RunPostHarvestRetentionAsync());
            await Assert.ThrowsAnyAsync<Exception>(() => enabled.RunPostHarvestRetentionAsync());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunPostHarvestRetentionAsync_MaxDecksBound_StopsWithMaxDecksReached()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "deckflow-retention-" + Guid.NewGuid().ToString("N"));
        var original = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);
        try
        {
            var store = CreateStore(Path.Combine(tempRoot, "content"), new HarvestRetentionOptions { PerCommanderCap = 1, PostHarvestMaxDecks = 2, MinFreeDiskBytes = 0, PauseMilliseconds = 0 });
            await SeedStoredDecksAsync(store, 4, "Commander", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

            var result = await store.RunPostHarvestRetentionAsync();

            Assert.Equal(2, result!.DecksRemoved);
            Assert.Equal(DeckPruningStopReason.MaxDecksReached, result.StopReason);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", original);
            ClearPool(Path.Combine(tempRoot, "artifacts", "category-knowledge.db"));
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SelectFreeDiskProbe_Sqlite_UsesDriveProbe()
    {
        var probe = CategoryKnowledgeStore.SelectFreeDiskProbe(RelationalDatabaseConnection.FromSqlitePath(Path.GetTempFileName()), CreateRenderProbe(), null);

        Assert.IsType<DeckPruningDiskProbe>(probe.Target);
    }

    [Fact]
    public void SelectFreeDiskProbe_PostgresWithRenderProbe_UsesRenderProbe()
    {
        var renderProbe = CreateRenderProbe();
        var probe = CategoryKnowledgeStore.SelectFreeDiskProbe(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, "Host=192.0.2.1"), renderProbe, null);

        Assert.Same(renderProbe, probe.Target);
    }

    [Fact]
    public async Task SelectFreeDiskProbe_PostgresWithoutRenderProbe_ReturnsNull()
    {
        var probe = CategoryKnowledgeStore.SelectFreeDiskProbe(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, "Host=192.0.2.1"), null, null);

        Assert.Null(await probe(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private static CategoryKnowledgeStore CreateStore(string? contentRootPath = null)
        => new(
            new FakeWebHostEnvironment(contentRootPath ?? Path.Combine(Path.GetTempPath(), "deckflow-content-root")),
            new MemoryCache(new MemoryCacheOptions()));

    private static CategoryKnowledgeStore CreateStore(string? contentRootPath, HarvestRetentionOptions retentionOptions)
        => new(
            new FakeWebHostEnvironment(contentRootPath ?? Path.Combine(Path.GetTempPath(), "deckflow-content-root")),
            new MemoryCache(new MemoryCacheOptions()),
            retentionOptions: Options.Create(retentionOptions));

    private static RenderDiskHeadroomProbe CreateRenderProbe()
    {
        var services = new ServiceCollection();
        services.AddDeckFlowResiliencePipelines();
        var pipeline = services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();
        return new RenderDiskHeadroomProbe(new FakeHttpClientFactory([]), pipeline, null, "fake-key", "fake-id", TimeProvider.System, Task.Delay);
    }

    private static async Task<CategoryKnowledgeRepository> SeedStoredDecksAsync(CategoryKnowledgeStore store, int count, string commander, DateTimeOffset firstUpdatedUtc)
    {
        var repository = new CategoryKnowledgeRepository(Assert.IsType<string>(store.DatabasePath));
        for (var index = 0; index < count; index++)
        {
            var deckId = $"deck-{index:D3}";
            await repository.AddDeckIdsAsync([deckId]);
            await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
            await repository.MarkDeckProcessedAsync(deckId, commander, skip: false, new ArchidektDeckMetadata(null, 3, false, null, firstUpdatedUtc.AddDays(index), DateTimeOffset.UtcNow));
        }

        return repository;
    }

    private static async Task<string?> GetSkipReasonAsync(CategoryKnowledgeStore store, string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={store.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId";
        command.Parameters.AddWithValue("@deckId", deckId);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task WithSqliteConnectionAsync(string connectionString, Func<Task> action)
    {
        var originalProvider = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER");
        var originalConnectionString = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING");

        try
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", "Sqlite");
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", connectionString);
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", originalProvider);
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", originalConnectionString);
        }
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
