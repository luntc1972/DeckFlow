using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="ArchidektDeckCacheSession"/> covering harvest-run pagination,
/// per-deck import, and knowledge-cache persistence against a temporary SQLite database.
/// </summary>
public sealed class ArchidektDeckCacheSessionTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public ArchidektDeckCacheSessionTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task RunAsync_WaitsForFullDurationWhenQueueRunsDry()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();

        var session = new ArchidektDeckCacheSession(
            repository,
            new FakeDeckImporter(),
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(20));

        var stopwatch = Stopwatch.StartNew();
        await session.RunAsync(TimeSpan.FromMilliseconds(70), cancellationToken: CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 60, $"Expected the session to stay alive near the requested duration, but it completed in {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task RunAsync_WaitsForFullDurationWhenRecentDeckFetchFails()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();

        var session = new ArchidektDeckCacheSession(
            repository,
            new FakeDeckImporter(),
            new ThrowingRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(20));

        var stopwatch = Stopwatch.StartNew();
        await session.RunAsync(TimeSpan.FromMilliseconds(70), cancellationToken: CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 60, $"Expected the session to keep retrying near the requested duration after recent-deck fetch errors, but it completed in {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task RunAsync_UsesFetchBatchSizeForDeckProcessing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "100", "101", "102" });

        var importer = new FakeDeckImporter();
        var session = new ArchidektDeckCacheSession(
            repository,
            importer,
            new FakeRecentDecksImporter(),
            idlePollDelay: TimeSpan.FromMilliseconds(5));

        // Deterministic stop instead of racing a fixed wall-clock window: the old 300 ms budget made
        // this flaky under CI load (the per-deck duration check broke the loop after only 2 of 3
        // decks). RunAsync reports progress synchronously AFTER each deck is fully persisted, so
        // cancelling when the count reaches 3 means all three are done and the run breaks cleanly out
        // of the same per-deck check with the complete result (no idle Task.Delay is hit, so no throw).
        using var cancellation = new CancellationTokenSource();
        var stopWhenAllProcessed = new SynchronousProgress<int>(processed =>
        {
            if (processed >= 3)
            {
                cancellation.Cancel();
            }
        });

        // The duration is only a safety cap; the progress-driven cancel ends the run as soon as the
        // three queued decks are processed, regardless of how slow the runner is.
        var result = await session.RunAsync(
            TimeSpan.FromSeconds(5),
            queueBatchSize: 1,
            fetchBatchSize: 3,
            cancellationToken: cancellation.Token,
            progress: stopWhenAllProcessed);

        Assert.Equal(3, result.DecksProcessed);
        Assert.Equal(3, importer.ImportCalls);
    }

    [Fact]
    public async Task RunAsync_MetadataBearingImport_PersistsMetadata()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var metadata = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        var importer = new FakeDeckImporter { Metadata = metadata };
        await repository.AddDeckIdsAsync(new[] { "metadata-deck" });

        await new ArchidektDeckCacheSession(repository, importer, new FakeRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1)).RunAsync(TimeSpan.FromMilliseconds(30));

        var row = await ReadDeckQueueRowAsync("metadata-deck");
        Assert.Equal(1, importer.ImportCalls);
        Assert.Equal(metadata.EdhBracket, row.EdhBracket);
        Assert.Equal(metadata.DeckFormat, row.DeckFormat);
        Assert.Equal(metadata.Theorycrafted, row.Theorycrafted);
        Assert.Equal(metadata.CapturedUtc, row.CapturedUtc);
    }

    [Fact]
    public async Task RunAsync_UnchangedCardList_RefreshesMetadataWithoutRewritingFacts()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var first = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var second = new ArchidektDeckMetadata(4, 1, false, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        var importer = new FakeDeckImporter { Metadata = first };
        await repository.AddDeckIdsAsync(new[] { "unchanged-deck" });
        var session = new ArchidektDeckCacheSession(repository, importer, new FakeRecentDecksImporter(), idlePollDelay: TimeSpan.FromMilliseconds(1));
        await session.RunAsync(TimeSpan.FromMilliseconds(30));
        var before = await ReadDeckQueueRowAsync("unchanged-deck");
        await SetLastCheckedUtcAsync("unchanged-deck", DateTimeOffset.UtcNow.AddDays(-6));
        await repository.AddDeckIdsAsync(new[] { "unchanged-deck" });
        importer.Metadata = second;
        await session.RunAsync(TimeSpan.FromMilliseconds(30));
        var after = await ReadDeckQueueRowAsync("unchanged-deck");

        Assert.Equal(2, importer.ImportCalls);
        Assert.Equal(before.ContentHash, after.ContentHash);
        Assert.Equal(second.EdhBracket, after.EdhBracket);
        Assert.Equal(second.CapturedUtc, after.CapturedUtc);
    }

    private async Task SetLastCheckedUtcAsync(string deckId, DateTimeOffset lastCheckedUtc)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE deck_queue SET last_checked_utc = $lastCheckedUtc WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$lastCheckedUtc", lastCheckedUtc.ToString("O"));
        command.Parameters.AddWithValue("$deckId", deckId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<DeckQueueRow> ReadDeckQueueRowAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT content_hash, archidekt_edh_bracket, archidekt_deck_format, archidekt_theorycrafted, archidekt_created_utc, archidekt_updated_utc, archidekt_metadata_captured_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DeckQueueRow(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetBoolean(3), reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)), reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)), reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)));
    }

    private sealed record DeckQueueRow(string? ContentHash, int? EdhBracket, int? DeckFormat, bool? Theorycrafted, DateTimeOffset? CreatedUtc, DateTimeOffset? UpdatedUtc, DateTimeOffset? CapturedUtc);

    /// <summary>
    /// Invokes the handler synchronously on the calling thread (unlike <see cref="Progress{T}"/>,
    /// which posts asynchronously), so the test can react to each progress tick in-order and stop the
    /// run deterministically rather than on a wall clock.
    /// </summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }

    private sealed class FakeDeckImporter : IArchidektDeckImporter
    {
        public int ImportCalls { get; private set; }

        public ArchidektDeckMetadata? Metadata { get; set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            return Task.FromResult(new List<DeckEntry>
            {
                new()
                {
                    Name = $"Card {urlOrDeckId}",
                    NormalizedName = CardNormalizer.Normalize($"Card {urlOrDeckId}"),
                    Quantity = 1,
                    Board = "mainboard",
                    Category = "Ramp"
                }
            });
        }

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(urlOrDeckId, cancellationToken), Metadata);
    }

    private sealed class FakeRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <summary>
    /// Exception-injection double that throws <see cref="HttpRequestException"/> on every import call;
    /// used to test that <see cref="ArchidektDeckCacheSession"/> handles Archidekt fetch failures gracefully.
    /// </summary>
    private sealed class ThrowingRecentDecksImporter : IArchidektRecentDecksImporter
    {
        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated Archidekt recent deck failure.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated Archidekt recent deck failure.");

        public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated Archidekt recent deck failure.");
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
