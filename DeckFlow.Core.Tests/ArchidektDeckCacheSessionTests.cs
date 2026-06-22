using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

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
