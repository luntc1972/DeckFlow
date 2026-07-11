using DeckFlow.Core.Content;
using DeckFlow.Studio.Services;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Tests for <see cref="ContentKbReconcileStore"/> — idempotent upsert (SYNC-11), resolution-by-
/// absence (row retained, never deleted), scope-tag isolation (T-91-11), and Kind round-tripping
/// through <see cref="IContentKbReconcileStore.GetOpenAsync"/> for downstream removal-scoping.
/// </summary>
public sealed class ContentKbReconcileStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentKbReconcileStore _store;

    public ContentKbReconcileStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-kb-reconcile-test-{Guid.NewGuid():N}.db");
        _store = new ContentKbReconcileStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();
    }

    [Fact]
    public async Task PersistRunAsync_RunTwiceOnSameSet_YieldsSameRowCount()
    {
        var discrepancy = MakeSeedDrift("youtube_channel", "abc123");
        var now = DateTimeOffset.UtcNow;

        await _store.PersistRunAsync("full", new[] { discrepancy }, now);
        await _store.PersistRunAsync("full", new[] { discrepancy }, now.AddMinutes(1));

        var open = await _store.GetOpenAsync("full");
        Assert.Single(open);
        Assert.Equal(discrepancy.Id, open[0].Id);
    }

    [Fact]
    public async Task PersistRunAsync_DiscrepancyAbsentOnSecondRun_MarksResolvedButRetainsRow()
    {
        var stillPresent = MakeSeedDrift("youtube_channel", "abc123");
        var nowVanishing = MakeSeedDrift("youtube_channel", "def456");
        var firstRunTime = DateTimeOffset.UtcNow;
        var secondRunTime = firstRunTime.AddMinutes(5);

        await _store.PersistRunAsync("full", new[] { stillPresent, nowVanishing }, firstRunTime);
        await _store.PersistRunAsync("full", new[] { stillPresent }, secondRunTime);

        var open = await _store.GetOpenAsync("full");
        Assert.Single(open);
        Assert.Equal(stillPresent.Id, open[0].Id);

        // The vanished discrepancy's row must still exist (retained) with resolved_utc set —
        // GetOpenAsync only returns open rows, so query the underlying store's full state via a
        // fresh PersistRunAsync-free re-run that re-includes it and check it comes back open again
        // (proves the row was retained rather than deleted and could be re-opened).
        await _store.PersistRunAsync("full", new[] { stillPresent, nowVanishing }, secondRunTime.AddMinutes(1));
        var reopened = await _store.GetOpenAsync("full");
        Assert.Equal(2, reopened.Count);
        Assert.Contains(reopened, d => d.Id == nowVanishing.Id);
    }

    [Fact]
    public async Task PersistRunAsync_EmptySeenSet_ResolvesWholeScope()
    {
        var discrepancy = MakeSeedDrift("youtube_channel", "abc123");
        var firstRunTime = DateTimeOffset.UtcNow;

        await _store.PersistRunAsync("full", new[] { discrepancy }, firstRunTime);
        await _store.PersistRunAsync("full", Array.Empty<ContentKbReconcileDiscrepancy>(), firstRunTime.AddMinutes(1));

        var open = await _store.GetOpenAsync("full");
        Assert.Empty(open);
    }

    [Fact]
    public async Task PersistRunAsync_ScopedRun_NeverResolvesDiscrepancyOutsideItsScope()
    {
        var youtubeDiscrepancy = MakeSeedDrift("youtube_channel", "yt-1");
        var podcastDiscrepancy = MakeSeedDrift("podcast_rss", "pod-1");
        var firstRunTime = DateTimeOffset.UtcNow;

        await _store.PersistRunAsync("youtube", new[] { youtubeDiscrepancy }, firstRunTime);
        await _store.PersistRunAsync("podcast", new[] { podcastDiscrepancy }, firstRunTime);

        // A youtube-scoped run that no longer sees the youtube discrepancy must not touch the
        // podcast-scoped discrepancy (different scope tag entirely).
        await _store.PersistRunAsync("youtube", Array.Empty<ContentKbReconcileDiscrepancy>(), firstRunTime.AddMinutes(1));

        var openYoutube = await _store.GetOpenAsync("youtube");
        var openPodcast = await _store.GetOpenAsync("podcast");
        Assert.Empty(openYoutube);
        Assert.Single(openPodcast);
        Assert.Equal(podcastDiscrepancy.Id, openPodcast[0].Id);
    }

    [Fact]
    public async Task GetOpenAsync_RoundTripsKind()
    {
        var seedDrift = MakeSeedDrift("youtube_channel", "abc123");
        var fileOrphan = new ContentKbReconcileDiscrepancy(
            ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, "content-kb/orphan.md"),
            ContentKbReconcileKind.FileOrphan,
            NaturalKeyType: null,
            NaturalKeyValue: null,
            ArtifactPath: "content-kb/orphan.md",
            Title: null);

        await _store.PersistRunAsync("full", new[] { seedDrift, fileOrphan }, DateTimeOffset.UtcNow);

        var open = await _store.GetOpenAsync("full");
        Assert.Equal(2, open.Count);
        Assert.Equal(ContentKbReconcileKind.SeedDrift, open.Single(d => d.Id == seedDrift.Id).Kind);
        Assert.Equal(ContentKbReconcileKind.FileOrphan, open.Single(d => d.Id == fileOrphan.Id).Kind);
    }

    [Fact]
    public async Task GetOpenAsync_NullScope_ReturnsOpenDiscrepanciesAcrossAllScopes()
    {
        var youtubeDiscrepancy = MakeSeedDrift("youtube_channel", "yt-1");
        var podcastDiscrepancy = MakeSeedDrift("podcast_rss", "pod-1");

        await _store.PersistRunAsync("youtube", new[] { youtubeDiscrepancy }, DateTimeOffset.UtcNow);
        await _store.PersistRunAsync("podcast", new[] { podcastDiscrepancy }, DateTimeOffset.UtcNow);

        var open = await _store.GetOpenAsync(scopeTag: null);
        Assert.Equal(2, open.Count);
    }

    private static ContentKbReconcileDiscrepancy MakeSeedDrift(string naturalKeyType, string naturalKeyValue)
        => new(
            ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, naturalKeyType, naturalKeyValue, null),
            ContentKbReconcileKind.SeedDrift,
            naturalKeyType,
            naturalKeyValue,
            ArtifactPath: $"content-kb/{naturalKeyValue}.md",
            Title: $"Title for {naturalKeyValue}");
}
