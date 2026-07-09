using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Keyed-batch <see cref="ContentSiteIndexStore.SetVisibilityAsync(IReadOnlyList{ValueTuple{string, string}}, bool, CancellationToken)"/>
/// integration tests using per-fact SQLite files. This is the writer DirectPush uses to publish its pushed
/// rows visible in the same batch. Postgres parity remains a manual operator step in this phase.
/// </summary>
public sealed class ContentSiteIndexStoreKeyedVisibilityTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreKeyedVisibilityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-keyvis-{Guid.NewGuid():N}.db");
        _store = new ContentSiteIndexStore(_dbPath);
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
    public async Task SetVisibilityAsync_MakesListedKeysVisible_AndLeavesUntouchedRowsHidden()
    {
        // New rows insert hidden (is_visible defaults 0) — this is the DirectPush starting state.
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-vis-target"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-vis-untouched"));

        var rowsAffected = await _store.SetVisibilityAsync(
            [(ContentSourceType.Youtube, "yt-vis-target")],
            visible: true);

        var target = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-vis-target");
        var untouched = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-vis-untouched");

        Assert.Equal(1, rowsAffected);
        Assert.NotNull(target);
        Assert.NotNull(untouched);
        Assert.True(target!.IsVisible);
        Assert.False(untouched!.IsVisible);
    }

    [Fact]
    public async Task SetVisibilityAsync_WhenMadeVisible_ClearsHiddenFlag()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-vis-unhide"));
        // Drive it hidden first; making visible must clear is_hidden (visible implies not hidden).
        var seeded = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-vis-unhide");
        await _store.SetHiddenAsync(seeded!.Id, hidden: true);

        await _store.SetVisibilityAsync(
            [(ContentSourceType.Youtube, "yt-vis-unhide")],
            visible: true);

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-vis-unhide");

        Assert.NotNull(row);
        Assert.True(row!.IsVisible);
        Assert.False(row.IsHidden);
    }

    [Fact]
    public async Task SetVisibilityAsync_UpdatesAllListedKeys_InOneBatch()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-vis-a"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-vis-b"));

        var rowsAffected = await _store.SetVisibilityAsync(
            [
                (ContentSourceType.Youtube, "yt-vis-a"),
                (ContentSourceType.Youtube, "yt-vis-b")
            ],
            visible: true);

        var a = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-vis-a");
        var b = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-vis-b");

        Assert.Equal(2, rowsAffected);
        Assert.True(a!.IsVisible);
        Assert.True(b!.IsVisible);
    }

    [Fact]
    public async Task SetVisibilityAsync_EmptyKeys_NoOp_ReturnsZero()
    {
        var rowsAffected = await _store.SetVisibilityAsync([], visible: true);

        Assert.Equal(0, rowsAffected);
    }

    [Fact]
    public async Task HideSeedManagedAsync_HidesOnlySeedManagedTrueRows_LeavesProdOwnedAndUnclassifiedVisible()
    {
        // Why (Codex 91-08 HIGH): the destructive Apply hide MUST enforce seed_managed = TRUE in the SQL
        // itself. Passing all three keys — seed-owned, prod-owned, and unclassified — proves the WHERE
        // predicate hides ONLY the seed-owned row, so a prod-owned row can never be hidden by this path
        // even when the caller mistakenly includes its key (the TOCTOU-flip scenario).
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-seed-owned"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-prod-owned"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-unclassified"));

        var seedOwned = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-seed-owned");
        var prodOwned = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-prod-owned");
        await _store.SetSeedManagedIfNullAsync(seedOwned!.Id, seedManaged: true);
        await _store.SetSeedManagedIfNullAsync(prodOwned!.Id, seedManaged: false);
        // yt-unclassified is deliberately left seed_managed = NULL.

        // Drive all three visible first (rows insert hidden) so any wrongful hide is observable.
        var allKeys = new[]
        {
            (ContentSourceType.Youtube, "yt-seed-owned"),
            (ContentSourceType.Youtube, "yt-prod-owned"),
            (ContentSourceType.Youtube, "yt-unclassified"),
        };
        await _store.SetVisibilityAsync(allKeys, visible: true);

        var hidden = await _store.HideSeedManagedAsync(allKeys);

        var seedRow = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-seed-owned");
        var prodRow = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-prod-owned");
        var nullRow = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-unclassified");

        Assert.Equal(1, hidden);
        Assert.False(seedRow!.IsVisible);
        Assert.True(prodRow!.IsVisible);
        Assert.True(nullRow!.IsVisible);
    }

    [Fact]
    public async Task HideSeedManagedAsync_ClearsHiddenFlagOnTheSeedOwnedRow()
    {
        // Soft-hide semantics mirror SetVisibilityAsync: is_hidden is cleared to FALSE alongside
        // is_visible = FALSE (a soft-hidden row is not-visible-and-not-hidden, i.e. removed-from-index).
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-hide-flag"));
        var seeded = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hide-flag");
        await _store.SetSeedManagedIfNullAsync(seeded!.Id, seedManaged: true);
        await _store.SetVisibilityAsync([(ContentSourceType.Youtube, "yt-hide-flag")], visible: true);

        await _store.HideSeedManagedAsync([(ContentSourceType.Youtube, "yt-hide-flag")]);

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hide-flag");
        Assert.False(row!.IsVisible);
        Assert.False(row.IsHidden);
    }

    [Fact]
    public async Task HideSeedManagedAsync_EmptyKeys_NoOp_ReturnsZero()
    {
        var hidden = await _store.HideSeedManagedAsync([]);

        Assert.Equal(0, hidden);
    }

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo" },
            BracketTags = new[] { "cEDH" },
            CardCategoryTags = new[] { "win-cons" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };
}
