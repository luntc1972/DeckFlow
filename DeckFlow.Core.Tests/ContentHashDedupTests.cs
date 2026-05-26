using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for content-hash deduplication in the Archidekt category cache.
/// </summary>
public sealed class ContentHashDedupTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public ContentHashDedupTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public void ComputeHash_OrderIndependent()
    {
        var original = new[]
        {
            CreateEntry("Sol Ring", "Ramp"),
            CreateEntry("Guardian Project", "Draw", board: "sideboard")
        };
        var reordered = new[]
        {
            CreateEntry("Guardian Project", "Draw", board: "sideboard"),
            CreateEntry("Sol Ring", "Ramp")
        };

        Assert.Equal(
            DeckCategoryCacheWriter.ComputeCanonicalHash(original),
            DeckCategoryCacheWriter.ComputeCanonicalHash(reordered));
    }

    [Fact]
    public void ComputeHash_DistinguishesContent()
    {
        var baseline = new[] { CreateEntry("Sol Ring", "Ramp", quantity: 1) };
        var differentName = new[] { CreateEntry("Arcane Signet", "Ramp", quantity: 1) };
        var differentCategory = new[] { CreateEntry("Sol Ring", "Draw", quantity: 1) };
        var differentBoard = new[] { CreateEntry("Sol Ring", "Ramp", quantity: 1, board: "sideboard") };
        var differentQuantity = new[] { CreateEntry("Sol Ring", "Ramp", quantity: 2) };
        var baselineHash = DeckCategoryCacheWriter.ComputeCanonicalHash(baseline);

        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentName));
        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentCategory));
        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentBoard));
        Assert.NotEqual(baselineHash, DeckCategoryCacheWriter.ComputeCanonicalHash(differentQuantity));
    }

    [Fact]
    public void ComputeHash_SplitsMultiCategory()
    {
        var combined = new[] { CreateEntry("Esper Sentinel", "Ramp,Draw", quantity: 2) };
        var split = new[]
        {
            CreateEntry("Esper Sentinel", "Ramp", quantity: 2),
            CreateEntry("Esper Sentinel", "Draw", quantity: 2)
        };

        Assert.Equal(
            DeckCategoryCacheWriter.ComputeCanonicalHash(combined),
            DeckCategoryCacheWriter.ComputeCanonicalHash(split));
    }

    [Fact]
    public void ComputeHash_AggregatesDuplicates()
    {
        var duplicates = new[]
        {
            CreateEntry("Mystic Remora", "Draw", quantity: 1),
            CreateEntry("Mystic Remora", "Draw", quantity: 2)
        };
        var aggregated = new[] { CreateEntry("Mystic Remora", "Draw", quantity: 3) };

        Assert.Equal(
            DeckCategoryCacheWriter.ComputeCanonicalHash(duplicates),
            DeckCategoryCacheWriter.ComputeCanonicalHash(aggregated));
    }

    [Fact]
    public void ComputeHash_UncategorizedCardChangesHash()
    {
        var baseline = new[] { CreateEntry("Sol Ring", "Ramp") };
        var withUncategorizedCard = new[]
        {
            CreateEntry("Sol Ring", "Ramp"),
            CreateEntry("Command Tower", string.Empty)
        };

        Assert.NotEqual(
            DeckCategoryCacheWriter.ComputeCanonicalHash(baseline),
            DeckCategoryCacheWriter.ComputeCanonicalHash(withUncategorizedCard));
    }

    [Fact]
    public void ComputeHash_BoardMoveChangesHash()
    {
        var mainboard = new[] { CreateEntry("Command Tower", string.Empty, board: "mainboard") };
        var sideboard = new[] { CreateEntry("Command Tower", string.Empty, board: "sideboard") };

        Assert.NotEqual(
            DeckCategoryCacheWriter.ComputeCanonicalHash(mainboard),
            DeckCategoryCacheWriter.ComputeCanonicalHash(sideboard));
    }

    [Fact]
    public void ComputeHash_DelimiterInjectionSafe()
    {
        var first = new[] { CreateEntry("A|B", "c") };
        var second = new[] { CreateEntry("A", "b|c") };

        Assert.NotEqual(
            DeckCategoryCacheWriter.ComputeCanonicalHash(first),
            DeckCategoryCacheWriter.ComputeCanonicalHash(second));
    }

    [Fact]
    public void ComputeHash_Deterministic()
    {
        var entries = new[] { CreateEntry("Rhystic Study", "Draw") };

        var first = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);
        var second = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.All(first, character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public async Task GetContentHash_ReturnsNullWhenUnset()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "100" });

        var hash = await repository.GetContentHashAsync("100");

        Assert.Null(hash);
    }

    [Fact]
    public async Task SetThenGetContentHash_RoundTrips()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "101" });

        await repository.SetContentHashAsync("101", new string('a', 64));

        Assert.Equal(new string('a', 64), await repository.GetContentHashAsync("101"));
    }

    [Fact]
    public async Task SetContentHashNull_ClearsHash()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { "102" });
        await repository.SetContentHashAsync("102", new string('b', 64));

        await repository.SetContentHashAsync("102", null);

        Assert.Null(await repository.GetContentHashAsync("102"));
    }

    [Fact]
    public async Task EnsureSchema_IsIdempotentForContentHash()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);

        await repository.EnsureSchemaAsync();
        await repository.EnsureSchemaAsync();

        await repository.AddDeckIdsAsync(new[] { "103" });
        Assert.Null(await repository.GetContentHashAsync("103"));
    }

    [Fact]
    public async Task RunAsync_UnchangedDeck_SkipsFactTableWrites()
    {
        var deckId = "200";
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var deckImporter = new FakeDeckImporter();
        deckImporter.SetEntries(deckId, CreateDeckEntries());
        var recentImporter = new FakeRecentDecksImporter(deckId);
        var session = CreateSession(repository, deckImporter, recentImporter);

        await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);
        var originalHash = await repository.GetContentHashAsync(deckId);
        Assert.NotNull(originalHash);

        var agedUtc = DateTimeOffset.UtcNow.AddDays(-6);
        await SetLastCheckedUtcAsync(deckId, agedUtc);
        await repository.AddDeckIdsAsync(new[] { deckId });
        var before = await ReadFactSnapshotAsync();
        Assert.NotEmpty(before.Observations);
        Assert.NotEmpty(before.Totals);

        var result = await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);

        var after = await ReadFactSnapshotAsync();
        var queueRow = await ReadDeckQueueRowAsync(deckId);
        Assert.Equal(before, after);
        Assert.Equal(1, result.DecksUnchanged);
        Assert.Equal(0, result.DecksProcessed);
        Assert.Equal(0, result.DecksAdded);
        Assert.Equal(0, result.DecksUpdated);
        Assert.Equal(originalHash, await repository.GetContentHashAsync(deckId));
        Assert.True(DateTimeOffset.Parse(queueRow.LastCheckedUtc!) > agedUtc);
    }

    [Fact]
    public async Task RunAsync_ChangedDeck_RewritesAndUpdatesHash()
    {
        var deckId = "201";
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var deckImporter = new FakeDeckImporter();
        deckImporter.SetEntries(deckId, CreateDeckEntries());
        var session = CreateSession(repository, deckImporter, new FakeRecentDecksImporter(deckId));

        await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);
        var originalHash = await repository.GetContentHashAsync(deckId);
        Assert.NotNull(originalHash);

        await SetLastCheckedUtcAsync(deckId, DateTimeOffset.UtcNow.AddDays(-6));
        await repository.AddDeckIdsAsync(new[] { deckId });
        deckImporter.SetEntries(deckId, new[]
        {
            CreateEntry("Arcane Signet", "Ramp"),
            CreateEntry("Command Tower", string.Empty)
        });

        var result = await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);

        Assert.Equal(1, result.DecksUpdated);
        Assert.Equal(1, result.DecksProcessed);
        Assert.NotEqual(originalHash, await repository.GetContentHashAsync(deckId));
        Assert.Empty(await repository.GetCategoriesAsync("Sol Ring"));
        Assert.Equal(new[] { "Ramp" }, await repository.GetCategoriesAsync("Arcane Signet"));
    }

    [Fact]
    public async Task ChangedPath_PartialFailureLeavesNullHash()
    {
        var deckId = "202";
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var deckImporter = new FakeDeckImporter();
        deckImporter.SetEntries(deckId, CreateDeckEntries());
        var session = CreateSession(repository, deckImporter, new FakeRecentDecksImporter(deckId));

        await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);
        Assert.NotNull(await repository.GetContentHashAsync(deckId));
        await SetLastCheckedUtcAsync(deckId, DateTimeOffset.UtcNow.AddDays(-6));
        await repository.AddDeckIdsAsync(new[] { deckId });
        deckImporter.SetEntries(deckId, new[] { CreateEntry("Arcane Signet", "Ramp") });
        await CreateFailingObservationInsertTriggerAsync();

        await Assert.ThrowsAsync<SqliteException>(() => session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1));

        Assert.Null(await repository.GetContentHashAsync(deckId));
    }

    [Fact]
    public async Task NullHash_RecomputesOnce()
    {
        var deckId = "203";
        var source = $"archidekt_live:{deckId}";
        var entries = CreateDeckEntries();
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { deckId });
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, source, entries);
        Assert.Null(await repository.GetContentHashAsync(deckId));

        var deckImporter = new FakeDeckImporter();
        deckImporter.SetEntries(deckId, entries);
        var session = CreateSession(repository, deckImporter, new FakeRecentDecksImporter());
        var firstResult = await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);
        var hash = await repository.GetContentHashAsync(deckId);
        Assert.NotNull(hash);

        await SetLastCheckedUtcAsync(deckId, DateTimeOffset.UtcNow.AddDays(-6));
        await repository.AddDeckIdsAsync(new[] { deckId });
        var before = await ReadFactSnapshotAsync();
        var secondResult = await session.RunAsync(TimeSpan.FromMilliseconds(150), fetchBatchSize: 1);

        Assert.Equal(1, firstResult.DecksUpdated);
        Assert.Equal(1, secondResult.DecksUnchanged);
        Assert.Equal(0, secondResult.DecksProcessed);
        Assert.Equal(hash, await repository.GetContentHashAsync(deckId));
        Assert.Equal(before, await ReadFactSnapshotAsync());
    }

    [Fact]
    public async Task FiveDayCooldown_RequeueRespectsLastChecked()
    {
        var deckId = "204";
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(new[] { deckId });
        await repository.MarkDeckProcessedAsync(deckId, commanderName: null);

        await repository.AddDeckIdsAsync(new[] { deckId });
        var withinCooldown = await ReadDeckQueueRowAsync(deckId);
        Assert.Equal(1, withinCooldown.Processed);

        await SetLastCheckedUtcAsync(deckId, DateTimeOffset.UtcNow.AddDays(-6));
        await repository.AddDeckIdsAsync(new[] { deckId });
        var afterCooldown = await ReadDeckQueueRowAsync(deckId);

        Assert.Equal(0, afterCooldown.Processed);
        Assert.Equal(0, afterCooldown.Skipped);
    }

    private static DeckEntry CreateEntry(
        string cardName,
        string? category,
        int quantity = 1,
        string board = "mainboard") => new()
        {
            Name = cardName,
            NormalizedName = CardNormalizer.Normalize(cardName),
            Quantity = quantity,
            Board = board,
            Category = category
        };

    private static IReadOnlyList<DeckEntry> CreateDeckEntries() => new[]
    {
        CreateEntry("Sol Ring", "Ramp"),
        CreateEntry("Command Tower", string.Empty)
    };

    private static ArchidektDeckCacheSession CreateSession(
        CategoryKnowledgeRepository repository,
        FakeDeckImporter deckImporter,
        FakeRecentDecksImporter recentImporter)
        => new(repository, deckImporter, recentImporter, idlePollDelay: TimeSpan.FromMilliseconds(5));

    private async Task SetLastCheckedUtcAsync(string deckId, DateTimeOffset lastCheckedUtc)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE deck_queue SET last_checked_utc = @lastCheckedUtc WHERE deck_id = @deckId;";
        command.Parameters.AddWithValue("@lastCheckedUtc", lastCheckedUtc.ToString("O"));
        command.Parameters.AddWithValue("@deckId", deckId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateFailingObservationInsertTriggerAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER fail_observation_insert
            BEFORE INSERT ON card_category_observations
            BEGIN
                SELECT RAISE(ABORT, 'simulated observation insert failure');
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<FactSnapshot> ReadFactSnapshotAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        return new FactSnapshot(
            string.Join('\n', await ReadRowsAsync(
                connection,
                """
                SELECT source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc
                FROM card_category_observations
                ORDER BY source_id, card_id, category, board;
                """)),
            string.Join('\n', await ReadRowsAsync(
                connection,
                """
                SELECT source_id, card_id, board, deck_count, last_seen_utc
                FROM card_deck_totals
                ORDER BY source_id, card_id, board;
                """)));
    }

    private static async Task<IReadOnlyList<string>> ReadRowsAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = reader.IsDBNull(index) ? "<null>" : Convert.ToString(reader.GetValue(index)) ?? string.Empty;
            }

            rows.Add(string.Join('\t', values));
        }

        return rows;
    }

    private async Task<DeckQueueRow> ReadDeckQueueRowAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT processed, skipped, last_checked_utc, content_hash
            FROM deck_queue
            WHERE deck_id = @deckId;
            """;
        command.Parameters.AddWithValue("@deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DeckQueueRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private sealed record FactSnapshot(string Observations, string Totals);

    private sealed record DeckQueueRow(int Processed, int Skipped, string? LastCheckedUtc, string? ContentHash);

    private sealed class FakeDeckImporter : IArchidektDeckImporter
    {
        private readonly Dictionary<string, List<DeckEntry>> _entriesByDeckId = new(StringComparer.Ordinal);

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(_entriesByDeckId[urlOrDeckId].ToList());

        public void SetEntries(string deckId, IReadOnlyList<DeckEntry> entries)
        {
            _entriesByDeckId[deckId] = entries.ToList();
        }
    }

    private sealed class FakeRecentDecksImporter : IArchidektRecentDecksImporter
    {
        private readonly Queue<IReadOnlyList<string>> _pageOneResponses = new();

        public FakeRecentDecksImporter(params string[] deckIds)
        {
            if (deckIds.Length > 0)
            {
                _pageOneResponses.Enqueue(deckIds);
            }
        }

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
        {
            if (page == 1 && _pageOneResponses.Count > 0)
            {
                return Task.FromResult(_pageOneResponses.Dequeue());
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
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
            // ignored
        }
    }
}
