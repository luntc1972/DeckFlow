using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="ReviewCoordinator"/> — the Review Queue orchestration extracted
/// from the page code-behind (H1 split). The headline coverage is <c>ReadArtifactSafe</c>, the
/// security-sensitive path-containment + read that previously could only be exercised through a full
/// bUnit render.
/// </summary>
public sealed class ReviewCoordinatorTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly string _artifactRoot;

    public ReviewCoordinatorTests()
    {
        // Unique temp data root per test instance: {tmp}/<guid-less unique>/content-kb is ArtifactRoot.
        _dataRoot = Path.Combine(Path.GetTempPath(), "deckflow-review-coord-" + Guid.NewGuid().ToString("N"));
        _artifactRoot = Path.Combine(_dataRoot, "content-kb");
        Directory.CreateDirectory(_artifactRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private ReviewCoordinator Build(FakeContentSiteIndexStore store)
        => new(store, new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot });

    private static ContentSiteIndexRow Youtube(long id, string videoId, string status = "pending")
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ApprovalStatus = status,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    // ── LoadRowsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadRowsAsync_EnsuresSchemaThenReturnsAllRows()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Youtube(1, "vid1"));
        store.Rows.Add(Youtube(2, "vid2"));
        var coordinator = Build(store);

        var rows = await coordinator.LoadRowsAsync(CancellationToken.None);

        Assert.Equal(1, store.EnsureSchemaCallCount);
        Assert.Equal(2, rows.Count);
    }

    // ── SetApprovalStatusAsync (single + batch) ──────────────────────────────

    [Fact]
    public async Task SetApprovalStatusAsync_Single_DelegatesToStore()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store);

        await coordinator.SetApprovalStatusAsync(ContentSourceType.Youtube, "vid1", "approved", CancellationToken.None);

        Assert.Single(store.SingleApprovalCalls);
        Assert.Equal((ContentSourceType.Youtube, "vid1", "approved"), store.SingleApprovalCalls[0]);
    }

    [Fact]
    public async Task SetApprovalStatusAsync_Batch_DelegatesToStore()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store);
        var keys = new[] { (ContentSourceType.Youtube, "vid1"), (ContentSourceType.Youtube, "vid2") };

        await coordinator.SetApprovalStatusAsync(keys, "rejected", CancellationToken.None);

        Assert.Single(store.BatchApprovalCalls);
        Assert.Equal("rejected", store.BatchApprovalCalls[0].Status);
        Assert.Equal(2, store.BatchApprovalCalls[0].Keys.Count);
    }

    // ── ReadArtifactSafe — security containment ──────────────────────────────

    [Fact]
    public void ReadArtifactSafe_ValidRelativePath_ReturnsFileContent()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store);
        var dir = Path.Combine(_artifactRoot, "test-channel");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "vid1.md"), "hello preview");

        // Stored ArtifactPath already carries the content-kb/ prefix; resolved under the data root.
        var text = coordinator.ReadArtifactSafe("content-kb/test-channel/vid1.md");

        Assert.Equal("hello preview", text);
    }

    [Fact]
    public void ReadArtifactSafe_MissingFile_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());

        var text = coordinator.ReadArtifactSafe("content-kb/test-channel/does-not-exist.md");

        Assert.Null(text);
    }

    [Fact]
    public void ReadArtifactSafe_RootedPath_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());

        // An absolute path must be rejected outright (no read attempted).
        var rooted = Path.Combine(_artifactRoot, "test-channel", "vid1.md");
        var text = coordinator.ReadArtifactSafe(rooted);

        Assert.Null(text);
    }

    [Theory]
    [InlineData("content-kb/../../etc/passwd")]
    [InlineData("../secret.md")]
    [InlineData("content-kb/test-channel/../../../escape.md")]
    public void ReadArtifactSafe_TraversalPath_ReturnsNull(string traversal)
    {
        var coordinator = Build(new FakeContentSiteIndexStore());

        var text = coordinator.ReadArtifactSafe(traversal);

        Assert.Null(text);
    }

    [Fact]
    public void ReadArtifactSafe_BackslashTraversalPath_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());

        // Backslashes are normalized to forward slashes before the ".." segment check.
        var text = coordinator.ReadArtifactSafe(@"content-kb\..\..\escape.md");

        Assert.Null(text);
    }

    // ── ReadPromptSafe — baked sibling vs reconstruct ────────────────────────

    [Fact]
    public void ReadPromptSafe_BakedSiblingPresent_ReturnsSiblingVerbatim()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());
        var dir = Path.Combine(_artifactRoot, "test-channel");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "vid1.md"), "---\ntitle: T\n---\n## Summary\nNotes.");
        File.WriteAllText(Path.Combine(dir, "vid1.prompt.md"), "BAKED PROMPT");

        var prompt = coordinator.ReadPromptSafe(
            "content-kb/test-channel/vid1.md", "Video", "test-channel", "https://youtu.be/vid1");

        Assert.Equal("BAKED PROMPT", prompt);
    }

    [Fact]
    public void ReadPromptSafe_NoSibling_ReconstructsFromNotes()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());
        var dir = Path.Combine(_artifactRoot, "test-channel");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "vid1.md"), "---\ntitle: T\n---\n## Summary\nOff-axis builds.");

        var prompt = coordinator.ReadPromptSafe(
            "content-kb/test-channel/vid1.md", "Video", "test-channel", "https://youtu.be/vid1");

        Assert.NotNull(prompt);
        Assert.Contains("TASK:", prompt, StringComparison.Ordinal);
        Assert.Contains("Off-axis builds.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadPromptSafe_MissingNotes_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());

        var prompt = coordinator.ReadPromptSafe(
            "content-kb/test-channel/does-not-exist.md", "Video", "test-channel", "https://youtu.be/x");

        Assert.Null(prompt);
    }

    [Fact]
    public void ReadPromptSafe_TraversalPath_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());

        var prompt = coordinator.ReadPromptSafe(
            "content-kb/test-channel/../../../escape.md", "Video", "test-channel", "https://youtu.be/x");

        Assert.Null(prompt);
    }

    [Fact]
    public void ReadArtifactSafe_NonContentKbPrefix_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());
        // A file that exists under the data root but OUTSIDE the content-kb/ subtree must be
        // rejected — a corrupted index row cannot read sibling directories.
        var dataRoot = Directory.GetParent(_artifactRoot)!.FullName;
        File.WriteAllText(Path.Combine(dataRoot, "secrets.md"), "top secret");

        Assert.Null(coordinator.ReadArtifactSafe("secrets.md"));
    }

    [Fact]
    public void ReadPromptSafe_NonContentKbPrefix_ReturnsNull()
    {
        var coordinator = Build(new FakeContentSiteIndexStore());
        var dataRoot = Directory.GetParent(_artifactRoot)!.FullName;
        File.WriteAllText(Path.Combine(dataRoot, "secrets.md"), "top secret");

        Assert.Null(coordinator.ReadPromptSafe("secrets.md", "V", "S", "https://x"));
    }
}
