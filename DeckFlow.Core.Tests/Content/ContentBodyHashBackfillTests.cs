using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// In-memory <see cref="ILogger{TCategoryName}"/> test double that records every logged entry
/// (level + formatted message) so tests can assert a specific warning fired — or did not.
/// </summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// Fake <see cref="IContentArtifactBodyResolver"/> returning canned text (or null) keyed by
/// artifact path, so tests never touch the filesystem.
/// </summary>
internal sealed class FakeContentArtifactBodyResolver : IContentArtifactBodyResolver
{
    private readonly Dictionary<string, string?> _textByPath;

    public FakeContentArtifactBodyResolver(Dictionary<string, string?> textByPath)
    {
        _textByPath = textByPath;
    }

    public List<string> RequestedPaths { get; } = [];

    public Task<string?> TryReadArtifactTextAsync(string artifactPath, CancellationToken cancellationToken = default)
    {
        RequestedPaths.Add(artifactPath);
        return Task.FromResult(_textByPath.TryGetValue(artifactPath, out var text) ? text : null);
    }
}

/// <summary>
/// Behavior coverage for <see cref="ContentBodyHashBackfill"/> (D-08): null-row-with-text gets
/// hashed via the shared <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/> helper
/// and persisted through <see cref="IContentSiteIndexStore.SetBodySha256IfNullAsync"/>; a
/// missing/unresolvable row is skipped with a structured warning (no throw); a non-null row is
/// never read via the resolver and never rewritten; a second run is a no-op (idempotent).
/// </summary>
public sealed class ContentBodyHashBackfillTests : IDisposable
{
    private readonly string _dbPath;

    public ContentBodyHashBackfillTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-body-hash-backfill-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task RunAsync_NullRowWithResolvableText_HashesAndPersists()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateRow("yt-null-hashable", bodySha256: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-null-hashable");
        Assert.NotNull(row);

        const string rawArtifactText = "---\ntitle: \"x\"\n---\nSome body text.";
        var resolver = new FakeContentArtifactBodyResolver(new Dictionary<string, string?>
        {
            [row!.ArtifactPath] = rawArtifactText,
        });
        var backfill = new ContentBodyHashBackfill(store, resolver, new FakeLogger<ContentBodyHashBackfill>());

        await backfill.RunAsync();

        var updated = await store.GetByIdAsync(row.Id);
        Assert.NotNull(updated);
        var expectedHash = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifactText);
        Assert.Equal(expectedHash, updated!.BodySha256);
    }

    [Fact]
    public async Task RunAsync_NullRowWithUnresolvableArtifact_SkipsAndLogsWarningWithoutThrowing()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateRow("yt-null-missing", bodySha256: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-null-missing");
        Assert.NotNull(row);

        var resolver = new FakeContentArtifactBodyResolver(new Dictionary<string, string?>
        {
            [row!.ArtifactPath] = null,
        });
        var logger = new FakeLogger<ContentBodyHashBackfill>();
        var backfill = new ContentBodyHashBackfill(store, resolver, logger);

        var exception = await Record.ExceptionAsync(() => backfill.RunAsync());

        Assert.Null(exception);
        var stillNull = await store.GetByIdAsync(row.Id);
        Assert.Null(stillNull!.BodySha256);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains(row.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_NonNullRow_IsNeverReadOrRewritten()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        var existingHash = new string('a', 64);
        await store.UpsertContentColumnsOnlyAsync(CreateRow("yt-already-hashed", bodySha256: existingHash));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-already-hashed");
        Assert.NotNull(row);

        // No entry for this row's ArtifactPath — if the backfill ever read it, the fake would
        // return null, which would be a visible behavior change (skip+warn) that the assertions below rule out.
        var resolver = new FakeContentArtifactBodyResolver(new Dictionary<string, string?>());
        var logger = new FakeLogger<ContentBodyHashBackfill>();
        var backfill = new ContentBodyHashBackfill(store, resolver, logger);

        await backfill.RunAsync();

        Assert.Empty(resolver.RequestedPaths);
        var unchanged = await store.GetByIdAsync(row!.Id);
        Assert.Equal(existingHash, unchanged!.BodySha256);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotentAndWritesNothing()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateRow("yt-idempotent", bodySha256: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-idempotent");
        Assert.NotNull(row);

        const string rawArtifactText = "---\ntitle: \"x\"\n---\nIdempotent body text.";
        var resolver = new FakeContentArtifactBodyResolver(new Dictionary<string, string?>
        {
            [row!.ArtifactPath] = rawArtifactText,
        });
        var backfill = new ContentBodyHashBackfill(store, resolver, new FakeLogger<ContentBodyHashBackfill>());

        await backfill.RunAsync();
        var afterFirstRun = await store.GetByIdAsync(row.Id);
        var expectedHash = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifactText);
        Assert.Equal(expectedHash, afterFirstRun!.BodySha256);

        // Second run: the row is now non-null, so it must never be re-read via the resolver.
        resolver.RequestedPaths.Clear();
        await backfill.RunAsync();

        Assert.Empty(resolver.RequestedPaths);
        var afterSecondRun = await store.GetByIdAsync(row.Id);
        Assert.Equal(expectedHash, afterSecondRun!.BodySha256);
    }

    private static ContentSiteIndexRow CreateRow(string youtubeVideoId, string? bodySha256)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = ["combo"],
            BracketTags = ["cEDH"],
            CardCategoryTags = ["win-cons"],
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null,
            ApprovalStatus = "approved",
            BodySha256 = bodySha256,
        };
}
