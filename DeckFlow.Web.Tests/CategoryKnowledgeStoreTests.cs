using DeckFlow.Core.Integration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
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

            var rows = await store.GetPagedProcessedCommandersAsync(page: 0, pageSize: 0);
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
            SqliteConnection.ClearAllPools();
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
            SqliteConnection.ClearAllPools();
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

        var rows = await fake.GetPagedProcessedCommandersAsync(page: 3, pageSize: 25);

        var row = Assert.Single(rows);
        Assert.Equal("Commander", row.CommanderName);
        Assert.Equal(7, row.DeckCount);
        Assert.Equal(3, fake.LastPagedCommanderPage);
        Assert.Equal(25, fake.LastPagedCommanderPageSize);
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

    private static CategoryKnowledgeStore CreateStore(string? contentRootPath = null)
        => new(new FakeWebHostEnvironment(contentRootPath ?? Path.Combine(Path.GetTempPath(), "deckflow-content-root")));

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
