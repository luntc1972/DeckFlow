using System;
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ContentKbReconcileClassifierTests
{
    private static readonly IReadOnlySet<string> EmptyPaths = new HashSet<string>(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> EmptyBodies = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly SeedIndexReadResult AvailableEmptySeed = new(true, new HashSet<string>(StringComparer.Ordinal));
    private static readonly SeedIndexReadResult UnavailableSeed = new(false, new HashSet<string>(StringComparer.Ordinal));

    private static ContentSiteIndexRow Row(
        string? youtubeId = "yt-1",
        string? rssGuid = null,
        string title = "Title",
        string artifactPath = "content-kb/slug/yt-1.md",
        string approvalStatus = "approved",
        bool isVisible = true,
        bool? seedManaged = null,
        string? bodySha256 = null) =>
        new()
        {
            Id = 1,
            Source = "youtube",
            Title = title,
            VideoUrl = "https://example.com/v",
            ArtifactPath = artifactPath,
            IndexedUtc = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            ApprovalStatus = approvalStatus,
            IsVisible = isVisible,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = youtubeId,
            RssGuid = rssGuid,
            SeedManaged = seedManaged,
            BodySha256 = bodySha256
        };

    // --- Discrepancy ID determinism (91-XX-09) ---

    [Fact]
    public void BuildId_SameInputs_ProducesIdenticalId()
    {
        var id1 = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", "yt-1", null);
        var id2 = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", "yt-1", null);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void BuildId_DifferentKind_ProducesDifferentId()
    {
        var published = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.PublishedOrphan, "youtube_channel", "yt-1", null);
        var drift = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", "yt-1", null);

        Assert.NotEqual(published, drift);
    }

    [Fact]
    public void BuildId_DifferentNaturalKeyValue_ProducesDifferentId()
    {
        var a = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", "yt-1", null);
        var b = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", "yt-2", null);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildId_FileOrphan_KeyedByArtifactPath_StableAndDistinct()
    {
        var id1 = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, "content-kb/slug/a.md");
        var id2 = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, "content-kb/slug/a.md");
        var different = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, "content-kb/slug/b.md");

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, different);
    }

    [Fact]
    public void BuildId_UsesU0000Separator()
    {
        var id = ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, "youtube_channel", "yt-1", null);

        Assert.Contains('\u0000', id);
    }

    [Fact]
    public void BuildId_FileOrphanMissingArtifactPath_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, null));
    }

    [Fact]
    public void BuildId_RowKeyedKindMissingNaturalKey_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.SeedDrift, null, null, null));
    }

    // --- Classifier: one [Fact] per class ---

    [Fact]
    public void Classify_ApprovedVisibleRowMissingGitBody_IsPublishedOrphan()
    {
        var prod = new[] { Row(approvalStatus: "approved", isVisible: true, artifactPath: "content-kb/slug/yt-1.md") };

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, AvailableEmptySeed, EmptyBodies);

        var entry = Assert.Single(result);
        Assert.Equal(ContentKbReconcileKind.PublishedOrphan, entry.Kind);
        Assert.Equal("content-kb/slug/yt-1.md", entry.ArtifactPath);
    }

    [Fact]
    public void Classify_ApprovedVisibleRowWithGitBody_IsNotPublishedOrphan()
    {
        var prod = new[] { Row(approvalStatus: "approved", isVisible: true, artifactPath: "content-kb/slug/yt-1.md") };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/yt-1.md" };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, AvailableEmptySeed, EmptyBodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.PublishedOrphan);
    }

    [Fact]
    public void Classify_PendingOrHiddenRowMissingGitBody_IsNotPublishedOrphan()
    {
        var prod = new[] { Row(approvalStatus: "pending", isVisible: false, artifactPath: "content-kb/slug/yt-1.md") };

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, AvailableEmptySeed, EmptyBodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.PublishedOrphan);
    }

    [Fact]
    public void Classify_GitPathMatchingProdArtifactPath_IsNotFileOrphan()
    {
        var prod = new[] { Row(artifactPath: "content-kb/slug/yt-1.md") };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/yt-1.md" };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, AvailableEmptySeed, EmptyBodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.FileOrphan);
    }

    [Fact]
    public void Classify_GitPathWithNoMatchingProdArtifactPath_IsFileOrphan()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/orphan.md" };

        var result = ContentKbReconcileClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), paths, AvailableEmptySeed, EmptyBodies);

        var entry = Assert.Single(result);
        Assert.Equal(ContentKbReconcileKind.FileOrphan, entry.Kind);
        Assert.Equal("content-kb/slug/orphan.md", entry.ArtifactPath);
        Assert.Null(entry.NaturalKeyType);
        Assert.Null(entry.NaturalKeyValue);
    }

    [Fact]
    public void Classify_LooseMdPath_IsNotRescuedByNaturalKeyInference()
    {
        // A prod row exists with a DIFFERENT artifact path/natural key than the loose git file —
        // no front-matter/filename inference should rescue the file from file-orphan status; only
        // an exact artifact-path match clears it.
        var prod = new[] { Row(youtubeId: "yt-other", artifactPath: "content-kb/slug/yt-other.md") };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/yt-1.md" };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, AvailableEmptySeed, EmptyBodies);

        Assert.Contains(result, d => d.Kind == ContentKbReconcileKind.FileOrphan && d.ArtifactPath == "content-kb/slug/yt-1.md");
    }

    [Fact]
    public void Classify_SeedManagedRowAbsentFromAvailableSeed_IsSeedDrift()
    {
        var prod = new[] { Row(youtubeId: "yt-1", seedManaged: true) };
        var seed = new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal));

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, seed, EmptyBodies);

        Assert.Contains(result, d => d.Kind == ContentKbReconcileKind.SeedDrift && d.NaturalKeyValue == "yt-1");
    }

    [Fact]
    public void Classify_SeedManagedRowPresentInSeed_IsNotSeedDrift_EvenIfTimestampsDiffer()
    {
        var prod = new[] { Row(youtubeId: "yt-1", seedManaged: true) };
        var seed = new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}\u0000yt-1" });

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, seed, EmptyBodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.SeedDrift);
    }

    [Fact]
    public void Classify_ProdOwnedRowAbsentFromSeed_IsNotSeedDrift()
    {
        // SeedManaged == false (prod-owned) or null (unclassified) rows are NEVER seed-drift
        // candidates (D-01 invariant) — only SeedManaged == true rows are evaluated.
        var prod = new[] { Row(youtubeId: "yt-1", seedManaged: false) };
        var seed = new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal));

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, seed, EmptyBodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.SeedDrift);
    }

    [Fact]
    public void Classify_SeedUnavailable_EmitsZeroSeedDrift_RegardlessOfProdRows()
    {
        var prod = new[]
        {
            Row(youtubeId: "yt-1", seedManaged: true),
            Row(youtubeId: "yt-2", seedManaged: true),
            Row(youtubeId: "yt-3", seedManaged: true)
        };

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, UnavailableSeed, EmptyBodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.SeedDrift);
    }

    [Fact]
    public void Classify_SeedUnavailable_OtherThreeClassesStillComputed()
    {
        var prod = new[]
        {
            Row(youtubeId: "orphan-row", approvalStatus: "approved", isVisible: true, artifactPath: "content-kb/slug/orphan-row.md", seedManaged: true),
            Row(youtubeId: "hash-row", artifactPath: "content-kb/slug/hash-row.md", bodySha256: new string('a', 64), seedManaged: true)
        };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/loose.md", "content-kb/slug/hash-row.md" };
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal) { ["content-kb/slug/hash-row.md"] = "mismatched body" };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, UnavailableSeed, bodies);

        Assert.Contains(result, d => d.Kind == ContentKbReconcileKind.PublishedOrphan);
        Assert.Contains(result, d => d.Kind == ContentKbReconcileKind.FileOrphan);
        Assert.Contains(result, d => d.Kind == ContentKbReconcileKind.BodyHashMismatch);
        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.SeedDrift);
    }

    [Fact]
    public void Classify_BodyHashMatches_IsNotBodyHashMismatch()
    {
        const string bodyText = "## Heading\nSome body content.";
        var hash = ContentSiteIndexContentSignature.ComputeBodySha256(bodyText);
        var prod = new[] { Row(artifactPath: "content-kb/slug/yt-1.md", bodySha256: hash) };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/yt-1.md" };
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal) { ["content-kb/slug/yt-1.md"] = bodyText };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, AvailableEmptySeed, bodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.BodyHashMismatch);
    }

    [Fact]
    public void Classify_BodyHashDiffers_IsBodyHashMismatch()
    {
        const string bodyText = "## Heading\nSome body content.";
        var storedHash = ContentSiteIndexContentSignature.ComputeBodySha256("entirely different content");
        var prod = new[] { Row(artifactPath: "content-kb/slug/yt-1.md", bodySha256: storedHash) };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/yt-1.md" };
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal) { ["content-kb/slug/yt-1.md"] = bodyText };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, AvailableEmptySeed, bodies);

        var entry = Assert.Single(result);
        Assert.Equal(ContentKbReconcileKind.BodyHashMismatch, entry.Kind);
    }

    [Fact]
    public void Classify_NullBodySha256_IsNotEvaluatedForBodyHashMismatch()
    {
        var prod = new[] { Row(artifactPath: "content-kb/slug/yt-1.md", bodySha256: null) };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/yt-1.md" };
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal) { ["content-kb/slug/yt-1.md"] = "any body" };

        var result = ContentKbReconcileClassifier.Classify(prod, paths, AvailableEmptySeed, bodies);

        Assert.DoesNotContain(result, d => d.Kind == ContentKbReconcileKind.BodyHashMismatch);
    }

    [Fact]
    public void Classify_RowWithNoNaturalKey_IsSkippedFromAllRowKeyedClasses_AndWarns()
    {
        var logger = new RecordingLogger<ContentKbReconcileClassifierTests>();
        var prod = new[] { Row(youtubeId: null, rssGuid: null, approvalStatus: "approved", isVisible: true, title: "Orphan row") };

        var result = ContentKbReconcileClassifier.Classify(prod, EmptyPaths, AvailableEmptySeed, EmptyBodies, logger);

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Message.Contains("Orphan row", StringComparison.Ordinal));
    }

    [Fact]
    public void Classify_OrderIndependence_SameIdSetRegardlessOfInputOrdering()
    {
        var prod = new List<ContentSiteIndexRow>
        {
            Row(youtubeId: "missing-body", approvalStatus: "approved", isVisible: true, artifactPath: "content-kb/slug/missing-body.md"),
            Row(youtubeId: "drift-row", seedManaged: true, artifactPath: "content-kb/slug/drift-row.md"),
            Row(youtubeId: "hash-row", artifactPath: "content-kb/slug/hash-row.md", bodySha256: new string('a', 64))
        };
        var paths = new HashSet<string>(StringComparer.Ordinal) { "content-kb/slug/loose.md", "content-kb/slug/hash-row.md" };
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal) { ["content-kb/slug/hash-row.md"] = "some body" };
        var seed = new SeedIndexReadResult(true, new HashSet<string>(StringComparer.Ordinal));

        var forward = ContentKbReconcileClassifier.Classify(prod, paths, seed, bodies)
            .Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

        var reversedProd = Enumerable.Reverse(prod).ToList();
        var reversedPaths = new HashSet<string>(Enumerable.Reverse(paths), StringComparer.Ordinal);
        var reversed = ContentKbReconcileClassifier.Classify(reversedProd, reversedPaths, seed, bodies)
            .Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(forward, reversed);
        Assert.NotEmpty(forward);
    }

    [Fact]
    public void Classify_NullArgs_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ContentKbReconcileClassifier.Classify(null!, EmptyPaths, AvailableEmptySeed, EmptyBodies));
        Assert.Throws<ArgumentNullException>(() => ContentKbReconcileClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), null!, AvailableEmptySeed, EmptyBodies));
        Assert.Throws<ArgumentNullException>(() => ContentKbReconcileClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), EmptyPaths, null!, EmptyBodies));
        Assert.Throws<ArgumentNullException>(() => ContentKbReconcileClassifier.Classify(Array.Empty<ContentSiteIndexRow>(), EmptyPaths, AvailableEmptySeed, null!));
    }
}
