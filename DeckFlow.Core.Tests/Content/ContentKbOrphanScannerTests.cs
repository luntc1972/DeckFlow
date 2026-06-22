using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="ContentKbOrphanScanner"/> — artifact-presence classification,
/// published-vs-hidden orphan gating, content-base resolution (no double-prefix), and the
/// artifact-path traversal guard.
/// </summary>
public sealed class ContentKbOrphanScannerTests : IDisposable
{
    private readonly string _contentBase;

    public ContentKbOrphanScannerTests()
    {
        _contentBase = Path.Combine(Path.GetTempPath(), $"content-kb-orphan-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentBase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentBase))
        {
            Directory.Delete(_contentBase, recursive: true);
        }
    }

    [Fact]
    public void Scan_ExistingArtifactUnderContentBase_ClassifiedOk()
    {
        // Regression: row.ArtifactPath already starts with content-kb/, so the file lives at
        // <contentBase>/content-kb/{slug}/{id}.md. Any logic that prepends content-kb/ a second
        // time would resolve <contentBase>/content-kb/content-kb/... and mis-report this as missing.
        const string slug = "salubrioussnail";
        const string id = "abc123";
        WriteArtifact(slug, id);
        var row = Row(artifactPath: $"content-kb/{slug}/{id}.md", isVisible: true);

        var result = ContentKbOrphanScanner.Scan(new[] { row }, _contentBase);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.RowsWithArtifact);
        Assert.Equal(0, result.MissingCount);
        Assert.Equal(0, result.PublishedOrphanCount);
        Assert.True(result.Rows[0].Exists);
        Assert.False(result.Rows[0].IsPublishedOrphan);
    }

    [Fact]
    public void Scan_DataRootAndContentKbDir_NotTestedHere_ResolutionMirrorsResolver()
    {
        // The content base is the PARENT of content-kb/. Passing _contentBase resolves the file
        // written under _contentBase/content-kb/... — mirroring ContentKbArtifactPathResolver.
        const string slug = "the-command-zone";
        const string id = "zzz999";
        WriteArtifact(slug, id);
        var row = Row(artifactPath: $"content-kb/{slug}/{id}.md", isVisible: true);

        var result = ContentKbOrphanScanner.Scan(new[] { row }, _contentBase);

        Assert.True(result.Rows[0].Exists);
    }

    [Fact]
    public void Scan_VisibleRowMissingArtifact_CountsAsPublishedOrphan()
    {
        var row = Row(artifactPath: "content-kb/commander-baumi/missing1.md", isVisible: true, isHidden: false);

        var result = ContentKbOrphanScanner.Scan(new[] { row }, _contentBase);

        Assert.Equal(1, result.MissingCount);
        Assert.Equal(1, result.PublishedOrphanCount);
        Assert.Equal(0, result.HiddenOrphanCount);
        Assert.True(result.Rows[0].IsPublishedOrphan);
    }

    [Fact]
    public void Scan_HiddenRowMissingArtifact_CountsAsHiddenOrphan()
    {
        var row = Row(artifactPath: "content-kb/commander-baumi/missing2.md", isVisible: false, isHidden: false);

        var result = ContentKbOrphanScanner.Scan(new[] { row }, _contentBase);

        Assert.Equal(1, result.MissingCount);
        Assert.Equal(0, result.PublishedOrphanCount);
        Assert.Equal(1, result.HiddenOrphanCount);
        Assert.False(result.Rows[0].IsPublishedOrphan);
    }

    [Fact]
    public void Scan_VisibleButHiddenRowMissing_NotPublishedOrphan()
    {
        // is_visible=TRUE but is_hidden=TRUE must NOT count as published (research anti-pattern note).
        var row = Row(artifactPath: "content-kb/commander-baumi/missing3.md", isVisible: true, isHidden: true);

        var result = ContentKbOrphanScanner.Scan(new[] { row }, _contentBase);

        Assert.Equal(0, result.PublishedOrphanCount);
        Assert.Equal(1, result.HiddenOrphanCount);
    }

    [Fact]
    public void Scan_RootedArtifactPath_Throws()
    {
        var row = Row(artifactPath: "/etc/passwd", isVisible: true);

        Assert.Throws<ArgumentException>(() => ContentKbOrphanScanner.Scan(new[] { row }, _contentBase));
    }

    [Fact]
    public void Scan_DotDotArtifactPath_Throws()
    {
        var row = Row(artifactPath: "content-kb/../secret.md", isVisible: true);

        Assert.Throws<ArgumentException>(() => ContentKbOrphanScanner.Scan(new[] { row }, _contentBase));
    }

    private void WriteArtifact(string slug, string id)
    {
        var dir = Path.Combine(_contentBase, "content-kb", slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), "---\nbody\n");
    }

    private static ContentSiteIndexRow Row(
        string artifactPath,
        bool isVisible = false,
        bool isHidden = false,
        string approvalStatus = "approved")
        => new()
        {
            Id = 0,
            Source = "Test Source",
            Title = "Test Title",
            VideoUrl = "https://www.youtube.com/watch?v=test",
            ArtifactPath = artifactPath,
            PublishedUtc = DateTimeOffset.Parse("2026-06-22T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-06-22T13:00:00Z"),
            IsVisible = isVisible,
            IsHidden = isHidden,
            ApprovalStatus = approvalStatus,
            ArchetypeTags = new[] { "combo" },
            BracketTags = new[] { "cEDH" },
            CardCategoryTags = new[] { "win-cons" },
            YoutubeVideoId = "test",
            RssGuid = null
        };
}
