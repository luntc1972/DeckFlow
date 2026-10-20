using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies retention removal of stored live-deck category rows.
/// </summary>
public sealed class DeckRetentionRepositoryTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public DeckRetentionRepositoryTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_OneOfTwoDecks_RemovesRowsAndMarksRetention()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "r1", "Atraxa, Praetors' Voice", null, ("Sol Ring", "Ramp"), ("Arcane Signet", "Ramp"));
        await SeedStoredDeckAsync(repository, "r2", "Atraxa, Praetors' Voice", null, ("Sol Ring", "Ramp"), ("Rhystic Study", "Draw"));

        Assert.Equal(1, await RemoveAsync(repository, (await QueueIdAsync("r1"), DeckSkipReasons.Retention)));
        var row = await ReadQueueAsync("r1");
        Assert.Equal(DeckSkipReasons.Retention, row.SkipReason);
        Assert.True(row.Processed);
        Assert.True(row.Skipped);
        Assert.Null(row.ContentHash);
        Assert.Equal(0, await CountSourceRowsAsync("archidekt_live:r1"));
        Assert.NotEqual(0, await CountSourceRowsAsync("archidekt_live:r2"));
        Assert.Equal(1, await repository.GetCommanderDeckCountAsync("Atraxa, Praetors' Voice"));
        Assert.Equal(1, await CountCommanderSummaryAsync("Atraxa, Praetors' Voice"));
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_EmptyList_ReturnsZero()
    {
        var repository = CreateUnusableRepository();
        Assert.Equal(0, await RemoveAsync(repository));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_NullList_ThrowsBeforeIo()
    {
        var repository = CreateUnusableRepository();
        await Assert.ThrowsAsync<ArgumentNullException>(() => RemoveNullAsync(repository));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_DuplicateQueueId_ThrowsBeforeIo()
    {
        var repository = CreateUnusableRepository();
        await Assert.ThrowsAsync<ArgumentException>(() => RemoveAsync(repository, (5L, DeckSkipReasons.Retention), (5L, DeckSkipReasons.NoCommander)));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_DisallowedReasonOnUnusableConnection_ThrowsBeforeIo()
    {
        var repository = CreateUnusableRepository();
        await Assert.ThrowsAsync<ArgumentException>(() => RemoveAsync(repository, (5L, "unknown")));
    }

    [Theory]
    [InlineData(DeckSkipReasons.FetchFailure)]
    [InlineData(DeckSkipReasons.WrongSize)]
    [InlineData("unknown")]
    public async Task RemoveStoredDecksAsync_DisallowedReason_ThrowsBeforeWriting(string reason)
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "disallowed", "Atraxa", null, ("Sol Ring", "Ramp"));

        await Assert.ThrowsAsync<ArgumentException>(async () => await RemoveAsync(repository, (await QueueIdAsync("disallowed"), reason)));
        Assert.Equal(0, (await ReadQueueAsync("disallowed")).Skipped ? 1 : 0);
        Assert.NotEqual(0, await CountSourceRowsAsync("archidekt_live:disallowed"));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_RequeuedBeforeMark_NullsHashAndStaysPending()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "requeued", "Atraxa", null, ("Sol Ring", "Ramp"));
        await repository.SetContentHashAsync("requeued", "seeded-hash");
        await CreateTriggerAsync("""
            CREATE TRIGGER requeue_after_total_delete AFTER DELETE ON card_deck_totals
            BEGIN UPDATE deck_queue SET processed = 0, skipped = 0 WHERE deck_id = 'requeued'; END;
            """);

        Assert.Equal(0, await RemoveAsync(repository, (await QueueIdAsync("requeued"), DeckSkipReasons.Retention)));
        var row = await ReadQueueAsync("requeued");
        Assert.False(row.Processed);
        Assert.False(row.Skipped);
        Assert.Null(row.ContentHash);
        Assert.Null(row.RetentionCoreHash);
        Assert.Equal(0, await CountSourceRowsAsync("archidekt_live:requeued"));
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_IneligibleRows_LeftUntouched()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "pending", "Atraxa", null, ("Sol Ring", "Ramp"));
        await SetQueueStateAsync("pending", processed: false, skipped: false, null);
        await SeedStoredDeckAsync(repository, "skipped", "Atraxa", null, ("Arcane Signet", "Ramp"));
        await SetQueueStateAsync("skipped", processed: true, skipped: true, DeckSkipReasons.FetchFailure);
        await SeedStoredDeckAsync(repository, "empty", "Atraxa", null, Array.Empty<(string Card, string? Category)>());

        Assert.Equal(0, await RemoveAsync(repository, (await QueueIdAsync("pending"), DeckSkipReasons.Retention)));
        Assert.Equal(0, await RemoveAsync(repository, (await QueueIdAsync("pending"), DeckSkipReasons.NoCommander)));
        Assert.Equal(0, await RemoveAsync(repository, (await QueueIdAsync("pending"), DeckSkipReasons.NotCommanderFormat)));
        Assert.Equal(0, await RemoveAsync(repository, (await QueueIdAsync("skipped"), DeckSkipReasons.Retention)));
        Assert.Equal(0, await RemoveAsync(repository, (await QueueIdAsync("empty"), DeckSkipReasons.NoCommander)));
        Assert.NotEqual(0, await CountSourceRowsAsync("archidekt_live:pending"));
        Assert.NotEqual(0, await CountSourceRowsAsync("archidekt_live:skipped"));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_SharedCardAcrossBatch_QualifiedRowDropsBelowThreshold()
    {
        var repository = CreateRepository();
        for (var index = 0; index < 6; index++)
        {
            await SeedStoredDeckAsync(repository, $"shared-{index}", "Atraxa", null, ("Sol Ring", "Ramp"));
        }

        Assert.Equal(2, await RemoveAsync(repository, (await QueueIdAsync("shared-0"), DeckSkipReasons.Retention), (await QueueIdAsync("shared-1"), DeckSkipReasons.Retention)));
        Assert.Equal(4, await CountSummaryRowsAsync("Sol Ring", "Ramp"));
        await AssertCategorySummaryMatchesObservationsAsync();
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_DeckWithUrlRows_KeepsUrlRows()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "url", "Atraxa", null, ("Sol Ring", "Ramp"));
        var urlSource = "archidekt_url:https://archidekt.com/decks/url";
        await repository.PersistObservedCategoriesAsync(urlSource, "Sol Ring", ["Ramp"]);

        Assert.Equal(1, await RemoveAsync(repository, (await QueueIdAsync("url"), DeckSkipReasons.Retention)));
        Assert.Equal(0, await CountSourceRowsAsync("archidekt_live:url"));
        Assert.NotEqual(0, await CountSourceRowsAsync(urlSource));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_HashedDeck_ClearsContentHash()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "hashed", "Atraxa", null, ("Sol Ring", "Ramp"));
        await repository.SetContentHashAsync("hashed", "seeded-hash");
        Assert.NotNull(await repository.GetContentHashAsync("hashed"));

        await RemoveAsync(repository, (await QueueIdAsync("hashed"), DeckSkipReasons.Retention));
        Assert.Null(await repository.GetContentHashAsync("hashed"));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_QueueMarkFails_RollsBackEverything()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "failure-1", "Atraxa", null, ("Sol Ring", "Ramp"));
        await SeedStoredDeckAsync(repository, "failure-2", "Atraxa", null, ("Arcane Signet", "Ramp"));
        var before = await SnapshotAsync();
        await CreateTriggerAsync("""CREATE TRIGGER fail_queue_update BEFORE UPDATE ON deck_queue BEGIN SELECT RAISE(ABORT, 'simulated queue mark failure'); END;""");

        await Assert.ThrowsAnyAsync<Exception>(async () => await RemoveAsync(repository, (await QueueIdAsync("failure-1"), DeckSkipReasons.Retention), (await QueueIdAsync("failure-2"), DeckSkipReasons.Retention)));
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_PurgeReason_RemovesSkippedStoredDeck()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "purge", "Atraxa", null, ("Sol Ring", "Ramp"));
        await SetQueueStateAsync("purge", processed: false, skipped: true, DeckSkipReasons.FetchFailure);
        await SeedStoredDeckAsync(repository, "pending-purge", "Atraxa", null, ("Arcane Signet", "Ramp"));
        await SetQueueStateAsync("pending-purge", processed: false, skipped: false, null);

        Assert.Equal(1, await RemoveAsync(repository, (await QueueIdAsync("purge"), DeckSkipReasons.NotCommanderFormat), (await QueueIdAsync("pending-purge"), DeckSkipReasons.NotCommanderFormat)));
        var row = await ReadQueueAsync("purge");
        Assert.True(row.Skipped);
        Assert.Equal(DeckSkipReasons.NotCommanderFormat, row.SkipReason);
        Assert.Null(row.RetentionCoreHash);
        Assert.NotEqual(0, await CountSourceRowsAsync("archidekt_live:pending-purge"));
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_Retention_RecordsCoreFingerprint()
    {
        var repository = CreateRepository();
        var entries = new[]
        {
            CreateEntry("Atraxa, Praetors' Voice", null, "commander"), CreateEntry("Sol Ring", "Ramp"),
            CreateEntry("Rhystic Study", "Draw,Engine"), CreateEntry("Untyped", null), CreateEntry("Side", "Ramp", "sideboard"),
            CreateEntry("Maybe", "Ramp", "maybeboard"), CreateEntry("Arcane Signet", "Ramp"), CreateEntry("ARCANE SIGNET", "Ramp")
        };
        await SeedStoredDeckAsync(repository, "fingerprint", "Atraxa", entries);

        await RemoveAsync(repository, (await QueueIdAsync("fingerprint"), DeckSkipReasons.Retention));
        var hash = (await ReadQueueAsync("fingerprint")).RetentionCoreHash;
        Assert.Equal(DeckCategoryCacheWriter.ComputeCoreFingerprint(entries), hash);
        Assert.Equal(DeckCategoryCacheWriter.ComputeCoreFingerprint(entries.Where(entry => entry.Board is not "sideboard" and not "maybeboard")), hash);
        Assert.Equal(DeckCategoryCacheWriter.ComputeCoreFingerprint(entries.Select(entry => entry.Name == "Untyped" ? entry with { Quantity = 2 } : entry)), hash);
        Assert.NotEqual(DeckCategoryCacheWriter.ComputeCoreFingerprint(entries.Select(entry => entry with { Name = entry.Name == "Sol Ring" ? "Mana Crypt" : entry.Name, NormalizedName = CardNormalizer.Normalize(entry.Name == "Sol Ring" ? "Mana Crypt" : entry.Name) }).ToArray()), hash);

        await SeedStoredDeckAsync(repository, "purge-fingerprint", "Atraxa", null, ("Sol Ring", "Ramp"));
        await RemoveAsync(repository, (await QueueIdAsync("purge-fingerprint"), DeckSkipReasons.NoCommander));
        Assert.Null((await ReadQueueAsync("purge-fingerprint")).RetentionCoreHash);
    }

    [Fact]
    public async Task RemoveStoredDecksAsync_AlreadyPrunedDeck_LeftUntouched()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "pruned", "Atraxa", null, ("Sol Ring", "Ramp"));
        await RemoveAsync(repository, (await QueueIdAsync("pruned"), DeckSkipReasons.Retention));
        var first = await ReadQueueAsync("pruned");

        var prunedId = await QueueIdAsync("pruned");
        Assert.Equal(0, await RemoveAsync(repository, (prunedId, DeckSkipReasons.Retention)));
        Assert.Equal(0, await RemoveAsync(repository, (prunedId, DeckSkipReasons.NoCommander)));
        Assert.Equal(first, await ReadQueueAsync("pruned"));
    }

    [Fact]
    public async Task GetRetentionCandidatesAsync_ExcludesIneligibleDecks()
    {
        var repository = CreateRepository();
        var older = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = older.AddDays(1);
        await SeedStoredDeckAsync(repository, "stored-older", "Atraxa", older, ("Sol Ring", "Ramp"));
        await SeedStoredDeckAsync(repository, "stored-newer", "Atraxa", newer, ("Arcane Signet", "Ramp"));
        await SeedStoredDeckAsync(repository, "skipped", "Atraxa", newer, ("Rhystic Study", "Draw"));
        await SetQueueStateAsync("skipped", processed: true, skipped: true, null);
        await SeedStoredDeckAsync(repository, "requeued", "Atraxa", newer, ("Mana Crypt", "Ramp"));
        await SetQueueStateAsync("requeued", processed: false, skipped: false, null);
        await repository.MarkUrlDeckProcessedAsync("url-only", "Atraxa");
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_url:url-only", [CreateEntry("Sol Ring", "Ramp")]);

        var candidates = await repository.GetRetentionCandidatesAsync("Atraxa");

        Assert.Equal(["stored-newer", "stored-older"], candidates.Select(candidate => candidate.DeckId).Order());
        Assert.Equal(older, Assert.Single(candidates, candidate => candidate.DeckId == "stored-older").RankUtc);
        Assert.Equal(newer, Assert.Single(candidates, candidate => candidate.DeckId == "stored-newer").RankUtc);
    }

    [Fact]
    public async Task GetRetentionCandidatesAsync_LinkedSourceWithoutRows_Excluded()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "empty", "Atraxa", null, Array.Empty<(string Card, string? Category)>());

        Assert.Empty(await repository.GetRetentionCandidatesAsync("Atraxa"));
        Assert.Empty(await repository.GetRetentionCommanderKeysAsync(0));
    }

    [Fact]
    public async Task GetRetentionCommanderKeysAsync_OnlyGroupsOverMinimum()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "a1", "Atraxa", null, ("Sol Ring", "Ramp"));
        await SeedStoredDeckAsync(repository, "a2", "Atraxa", null, ("Arcane Signet", "Ramp"));
        await SeedStoredDeckAsync(repository, "a3", "Atraxa", null, ("Rhystic Study", "Draw"));
        await SeedStoredDeckAsync(repository, "b1", "Breya", null, ("Mana Crypt", "Ramp"));

        Assert.Equal(["atraxa"], await repository.GetRetentionCommanderKeysAsync(2));
        Assert.Equal(["atraxa", "breya"], await repository.GetRetentionCommanderKeysAsync(0));
    }

    [Fact]
    public async Task GetRetentionCommanderKeysAsync_CommanderCaseVariants_GroupTogether()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "first", "Atraxa, Praetors' Voice", null, ("Sol Ring", "Ramp"));
        await SeedStoredDeckAsync(repository, "second", "ATRAXA, PRAETORS' VOICE", null, ("Arcane Signet", "Ramp"));

        Assert.Equal(["atraxa, praetors' voice"], await repository.GetRetentionCommanderKeysAsync(1));
    }

    [Fact]
    public async Task GetRetentionCommanderKeysAsync_UrlOnlyDecks_NotCounted()
    {
        var repository = CreateRepository();
        await SeedStoredDeckAsync(repository, "stored-1", "Atraxa", null, ("Sol Ring", "Ramp"));
        await SeedStoredDeckAsync(repository, "stored-2", "Atraxa", null, ("Arcane Signet", "Ramp"));
        await repository.MarkUrlDeckProcessedAsync("url-1", "Atraxa");
        await repository.MarkUrlDeckProcessedAsync("url-2", "Atraxa");

        Assert.Empty(await repository.GetRetentionCommanderKeysAsync(2));
        Assert.Equal(["atraxa"], await repository.GetRetentionCommanderKeysAsync(1));
        Assert.Equal(2, (await repository.GetRetentionCandidatesAsync("Atraxa")).Count);
    }

    private CategoryKnowledgeRepository CreateRepository() => new(_databasePath);

    private CategoryKnowledgeRepository CreateUnusableRepository()
    {
        var existingFile = Path.Combine(_tempDirectory, "not-a-directory");
        File.WriteAllText(existingFile, string.Empty);
        return new CategoryKnowledgeRepository(Path.Combine(existingFile, "knowledge.db"));
    }

    private async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, DateTimeOffset? updatedUtc, params (string Card, string? Category)[] entries)
        => await SeedStoredDeckAsync(repository, deckId, commander, entries.Select(entry => CreateEntry(entry.Card, entry.Category)).ToArray(), updatedUtc);

    private async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, IReadOnlyList<DeckEntry> entries, DateTimeOffset? updatedUtc = null)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, entries);
        await repository.MarkDeckProcessedAsync(deckId, commander, skip: false, new ArchidektDeckMetadata(null, 3, false, null, updatedUtc, DateTimeOffset.UtcNow));
    }

    private static DeckEntry CreateEntry(string name, string? category, string board = "mainboard") => new() { Name = name, NormalizedName = CardNormalizer.Normalize(name), Quantity = 1, Board = board, Category = category };

    private static Task<int> RemoveAsync(CategoryKnowledgeRepository repository, params (long DeckQueueId, string Reason)[] removals)
        => repository.RemoveStoredDecksAsync(removals);

    private static Task<int> RemoveNullAsync(CategoryKnowledgeRepository repository)
        => repository.RemoveStoredDecksAsync(null!);

    private async Task CreateTriggerAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<(bool Processed, bool Skipped, string? SkipReason, string? ContentHash, string? RetentionCoreHash)> ReadQueueAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT processed, skipped, skip_reason, content_hash, retention_core_hash FROM deck_queue WHERE deck_id = @deckId";
        command.Parameters.AddWithValue("@deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0) != 0, reader.GetInt64(1) != 0, reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private async Task<long> QueueIdAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM deck_queue WHERE deck_id = @deckId";
        command.Parameters.AddWithValue("@deckId", deckId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountSourceRowsAsync(string source)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM card_category_observations WHERE source_id = (SELECT id FROM sources WHERE source = @source)";
        command.Parameters.AddWithValue("@source", source);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountSummaryRowsAsync(string card, string category)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT s.observation_rows FROM card_category_summary s JOIN cards c ON c.id = s.card_id WHERE c.normalized_card_name = @card AND s.category = @category";
        command.Parameters.AddWithValue("@card", CardNormalizer.Normalize(card));
        command.Parameters.AddWithValue("@category", category);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountCommanderSummaryAsync(string commanderName)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT deck_count FROM processed_commander_summary WHERE LOWER(commander_name) = LOWER(@commanderName)";
        command.Parameters.AddWithValue("@commanderName", commanderName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task SetQueueStateAsync(string deckId, bool processed, bool skipped, string? reason)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE deck_queue SET processed = @processed, skipped = @skipped, skip_reason = @reason WHERE deck_id = @deckId";
        command.Parameters.AddWithValue("@processed", processed);
        command.Parameters.AddWithValue("@skipped", skipped);
        command.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("@deckId", deckId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> SnapshotAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_concat(deck_id || ':' || processed || ':' || skipped || ':' || COALESCE(skip_reason, '') || ':' || COALESCE(content_hash, '') || ':' || COALESCE(retention_core_hash, ''), '|') FROM deck_queue";
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private async Task AssertCategorySummaryMatchesObservationsAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT card_id, category, COUNT(*) FROM card_category_observations GROUP BY card_id, category EXCEPT SELECT card_id, category, observation_rows FROM card_category_summary";
        Assert.Null(await command.ExecuteScalarAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A pooled SQLite handle can outlive the completed test on Windows.
        }
    }
}
