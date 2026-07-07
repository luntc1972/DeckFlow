using System;
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ContentSyncDiffClassifierTests
{
    private static ContentSiteIndexRow Row(
        string? youtubeId = "yt-1",
        string? rssGuid = null,
        string title = "Title",
        string artifactPath = "content-kb/slug/yt-1.md",
        DateTimeOffset? indexedUtc = null,
        IReadOnlyList<string>? archetypeTags = null,
        IReadOnlyList<string>? bracketTags = null,
        IReadOnlyList<string>? cardCategoryTags = null,
        string approvalStatus = "approved") =>
        new()
        {
            Id = 1,
            Source = "youtube",
            Title = title,
            VideoUrl = "https://example.com/v",
            ArtifactPath = artifactPath,
            IndexedUtc = indexedUtc ?? new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            ApprovalStatus = approvalStatus,
            ArchetypeTags = archetypeTags ?? Array.Empty<string>(),
            BracketTags = bracketTags ?? Array.Empty<string>(),
            CardCategoryTags = cardCategoryTags ?? Array.Empty<string>(),
            YoutubeVideoId = youtubeId,
            RssGuid = rssGuid
        };

    [Fact]
    public void Classify_BothEmpty_ReturnsEmpty()
    {
        var result = ContentSyncDiffClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), Array.Empty<ContentSiteIndexRow>());
        Assert.Empty(result);
    }

    [Fact]
    public void Classify_KeyInProdOnly_IsMissingLocally()
    {
        var prod = new[] { Row(youtubeId: "yt-prod") };

        var result = ContentSyncDiffClassifier.Classify(prod, Array.Empty<ContentSiteIndexRow>());

        var entry = Assert.Single(result);
        Assert.Equal(SyncDiffKind.MissingLocally, entry.Kind);
        Assert.Equal("yt-prod", entry.NaturalKeyValue);
        Assert.NotNull(entry.ProdRow);
        Assert.Null(entry.LocalRow);
    }

    [Fact]
    public void Classify_KeyInLocalOnly_IsLocalOnly()
    {
        var local = new[] { Row(youtubeId: "yt-local") };

        var result = ContentSyncDiffClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), local);

        var entry = Assert.Single(result);
        Assert.Equal(SyncDiffKind.LocalOnly, entry.Kind);
        Assert.Equal("yt-local", entry.NaturalKeyValue);
        Assert.Null(entry.ProdRow);
        Assert.NotNull(entry.LocalRow);
    }

    [Fact]
    public void Classify_ProdTimestampNewer_IsProdNewer()
    {
        var prod = new[] { Row(indexedUtc: new DateTimeOffset(2026, 6, 20, 15, 0, 0, TimeSpan.Zero)) };
        var local = new[] { Row(indexedUtc: new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero)) };

        var entry = Assert.Single(ContentSyncDiffClassifier.Classify(prod, local));

        Assert.Equal(SyncDiffKind.ProdNewer, entry.Kind);
        Assert.False(entry.LocalIsNewer);
    }

    [Fact]
    public void Classify_LocalTimestampNewer_IsDivergedLocalNewer()
    {
        var prod = new[] { Row(indexedUtc: new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero)) };
        var local = new[] { Row(indexedUtc: new DateTimeOffset(2026, 6, 20, 15, 0, 0, TimeSpan.Zero)) };

        var entry = Assert.Single(ContentSyncDiffClassifier.Classify(prod, local));

        Assert.Equal(SyncDiffKind.Diverged, entry.Kind);
        Assert.True(entry.LocalIsNewer);
    }

    [Fact]
    public void Classify_EqualTimestampDifferentContent_IsDivergedNotNewer()
    {
        var ts = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
        var prod = new[] { Row(indexedUtc: ts, title: "Prod title") };
        var local = new[] { Row(indexedUtc: ts, title: "Local title") };

        var entry = Assert.Single(ContentSyncDiffClassifier.Classify(prod, local));

        Assert.Equal(SyncDiffKind.Diverged, entry.Kind);
        Assert.False(entry.LocalIsNewer);
    }

    [Fact]
    public void Classify_IdenticalPair_EmitsNothing_R3()
    {
        var ts = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
        var prod = new[] { Row(indexedUtc: ts, archetypeTags: new[] { "aggro" }) };
        var local = new[] { Row(indexedUtc: ts, archetypeTags: new[] { "aggro" }) };

        var result = ContentSyncDiffClassifier.Classify(prod, local);

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_EqualTimestampSameInstantDifferentOffset_TreatedAsEqual()
    {
        // 12:00Z and 07:00-05:00 are the same instant — must not be misclassified (F-51-PG-01 class).
        var prod = new[] { Row(indexedUtc: new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero)) };
        var local = new[] { Row(indexedUtc: new DateTimeOffset(2026, 6, 20, 7, 0, 0, TimeSpan.FromHours(-5))) };

        var result = ContentSyncDiffClassifier.Classify(prod, local);

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_MixedSet_OmitsIdenticalEmitsDiffering()
    {
        var ts = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
        var prod = new[]
        {
            Row(youtubeId: "yt-same", indexedUtc: ts),
            Row(youtubeId: "yt-diff", indexedUtc: new DateTimeOffset(2026, 6, 20, 15, 0, 0, TimeSpan.Zero))
        };
        var local = new[]
        {
            Row(youtubeId: "yt-same", indexedUtc: ts),
            Row(youtubeId: "yt-diff", indexedUtc: ts)
        };

        var result = ContentSyncDiffClassifier.Classify(prod, local);

        var entry = Assert.Single(result);
        Assert.Equal("yt-diff", entry.NaturalKeyValue);
        Assert.Equal(SyncDiffKind.ProdNewer, entry.Kind);
    }

    [Fact]
    public void Classify_PodcastAndYoutubeKeys_ClassifiedIndependently()
    {
        var prod = new[]
        {
            Row(youtubeId: "yt-1", rssGuid: null),
            Row(youtubeId: null, rssGuid: "rss-only-prod")
        };
        var local = new[]
        {
            Row(youtubeId: "yt-1", rssGuid: null)
        };

        var result = ContentSyncDiffClassifier.Classify(prod, local);

        var entry = Assert.Single(result);
        Assert.Equal("rss-only-prod", entry.NaturalKeyValue);
        Assert.Equal(ContentSourceType.Podcast, entry.NaturalKeyType);
        Assert.Equal(SyncDiffKind.MissingLocally, entry.Kind);
    }

    [Fact]
    public void Classify_YoutubeIdEqualsPodcastGuid_DoNotCollide()
    {
        // SYNC-05 / D-05: a YouTube id string equal to a podcast RSS guid must NOT cross-match under the
        // old bare-PinId keying — the composite (type, value) key keeps them separate.
        var prod = new[]
        {
            Row(youtubeId: "COLLIDE", rssGuid: null, title: "YT row"),
            Row(youtubeId: null, rssGuid: "COLLIDE", title: "Podcast row")
        };
        var local = new[]
        {
            Row(youtubeId: "COLLIDE", rssGuid: null, title: "YT row")
        };

        var result = ContentSyncDiffClassifier.Classify(prod, local);

        // The YouTube row is in sync (identical) and omitted; the podcast row survives as MissingLocally.
        var entry = Assert.Single(result);
        Assert.Equal(ContentSourceType.Podcast, entry.NaturalKeyType);
        Assert.Equal("COLLIDE", entry.NaturalKeyValue);
        Assert.Equal(SyncDiffKind.MissingLocally, entry.Kind);
    }

    [Fact]
    public void Classify_RowWithNoNaturalKey_IsSkipped_AndWarns_WhenLoggerSupplied()
    {
        // D-08: a row with neither a YouTube id nor an RSS guid is skipped, and a warning naming the row
        // is logged when a logger is supplied (silent skip when none).
        var logger = new RecordingLogger<ContentSyncDiffClassifierTests>();
        var prod = new[] { Row(youtubeId: null, rssGuid: null, title: "Orphan row") };

        var result = ContentSyncDiffClassifier.Classify(prod, Array.Empty<ContentSiteIndexRow>(), logger);

        Assert.Empty(result);
        var warning = Assert.Single(logger.Entries);
        Assert.Contains("Orphan row", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_TitleTakenFromProdWhenPresent()
    {
        var prod = new[] { Row(youtubeId: "yt-1", title: "Prod title", indexedUtc: new DateTimeOffset(2026, 6, 20, 15, 0, 0, TimeSpan.Zero)) };
        var local = new[] { Row(youtubeId: "yt-1", title: "Local title", indexedUtc: new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero)) };

        var entry = Assert.Single(ContentSyncDiffClassifier.Classify(prod, local));

        Assert.Equal("Prod title", entry.Title);
    }

    [Fact]
    public void Classify_TitleFallsBackToLocalForLocalOnly()
    {
        var local = new[] { Row(youtubeId: "yt-1", title: "Local title") };

        var entry = Assert.Single(ContentSyncDiffClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), local));

        Assert.Equal("Local title", entry.Title);
    }

    [Fact]
    public void Classify_AllFourKinds_AreReachable()
    {
        var ts = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 6, 20, 15, 0, 0, TimeSpan.Zero);
        var prod = new[]
        {
            Row(youtubeId: "missing-local", indexedUtc: ts),
            Row(youtubeId: "prod-newer", indexedUtc: newer),
            Row(youtubeId: "diverged", indexedUtc: ts)
        };
        var local = new[]
        {
            Row(youtubeId: "prod-newer", indexedUtc: ts),
            Row(youtubeId: "diverged", indexedUtc: newer),
            Row(youtubeId: "local-only", indexedUtc: ts)
        };

        var byKey = ContentSyncDiffClassifier.Classify(prod, local).ToDictionary(e => e.NaturalKeyValue, e => e.Kind);

        Assert.Equal(SyncDiffKind.MissingLocally, byKey["missing-local"]);
        Assert.Equal(SyncDiffKind.ProdNewer, byKey["prod-newer"]);
        Assert.Equal(SyncDiffKind.Diverged, byKey["diverged"]);
        Assert.Equal(SyncDiffKind.LocalOnly, byKey["local-only"]);
    }

    [Fact]
    public void Classify_NullArgs_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ContentSyncDiffClassifier.Classify(null!, Array.Empty<ContentSiteIndexRow>()));
        Assert.Throws<ArgumentNullException>(() => ContentSyncDiffClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), null!));
    }
}
