using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="GitBodyCoverageAudit"/> — the read-only pre-flip git-coverage audit
/// (90-CONTEXT.md D-11 / SYNC-07 rollout precondition). Covers: only approved+visible+missing rows
/// are reported; present rows are excluded; hidden/pending rows are excluded; an unsafe artifact
/// path is reported (not silently resolved); and the audit performs no prod writes (structural —
/// <see cref="FakeProdContentReader"/> exposes no write API at all).
/// </summary>
public sealed class GitBodyCoverageAuditTests
{
    private static ContentSiteIndexRow MakeRow(
        long id,
        string videoId,
        string artifactPath,
        bool isVisible,
        string approvalStatus,
        string title = "Video")
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = $"{title} {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = artifactPath,
            IndexedUtc = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            IsVisible = isVisible,
            ApprovalStatus = approvalStatus,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private static string MakeRepoRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "git-body-coverage-audit-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteBody(string repoRoot, string artifactPath)
    {
        var full = Path.Combine(repoRoot, artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "body");
    }

    [Fact]
    public async Task RunAsync_OnlyReportsApprovedVisibleMissingRows()
    {
        var repoRoot = MakeRepoRoot();
        var present = MakeRow(1, "present-vid", "content-kb/test-channel/present-vid.md", isVisible: true, approvalStatus: "approved");
        var missing = MakeRow(2, "missing-vid", "content-kb/test-channel/missing-vid.md", isVisible: true, approvalStatus: "approved");
        WriteBody(repoRoot, present.ArtifactPath);
        // missing's body is deliberately NOT written.

        var reader = new FakeProdContentReader();
        reader.Rows.Add(present);
        reader.Rows.Add(missing);

        var audit = new GitBodyCoverageAudit(reader);
        var report = await audit.RunAsync("unused-conn-str", repoRoot, CancellationToken.None);

        var row = Assert.Single(report.MissingRows);
        Assert.Equal("missing-vid", row.NaturalKeyValue);
        Assert.Equal("content-kb/test-channel/missing-vid.md", row.ExpectedPath);
        Assert.Equal(1, report.MissingCount);
    }

    [Fact]
    public async Task RunAsync_PresentBody_ExcludedFromReport()
    {
        var repoRoot = MakeRepoRoot();
        var present = MakeRow(1, "present-vid", "content-kb/test-channel/present-vid.md", isVisible: true, approvalStatus: "approved");
        WriteBody(repoRoot, present.ArtifactPath);

        var reader = new FakeProdContentReader();
        reader.Rows.Add(present);

        var audit = new GitBodyCoverageAudit(reader);
        var report = await audit.RunAsync("unused-conn-str", repoRoot, CancellationToken.None);

        Assert.Empty(report.MissingRows);
    }

    [Fact]
    public async Task RunAsync_HiddenRow_ExcludedEvenIfBodyMissing()
    {
        var repoRoot = MakeRepoRoot();
        var hidden = MakeRow(1, "hidden-vid", "content-kb/test-channel/hidden-vid.md", isVisible: false, approvalStatus: "approved");

        var reader = new FakeProdContentReader();
        reader.Rows.Add(hidden);

        var audit = new GitBodyCoverageAudit(reader);
        var report = await audit.RunAsync("unused-conn-str", repoRoot, CancellationToken.None);

        Assert.Empty(report.MissingRows);
    }

    [Fact]
    public async Task RunAsync_PendingApprovalRow_ExcludedEvenIfVisibleAndBodyMissing()
    {
        var repoRoot = MakeRepoRoot();
        var pending = MakeRow(1, "pending-vid", "content-kb/test-channel/pending-vid.md", isVisible: true, approvalStatus: "pending");

        var reader = new FakeProdContentReader();
        reader.Rows.Add(pending);

        var audit = new GitBodyCoverageAudit(reader);
        var report = await audit.RunAsync("unused-conn-str", repoRoot, CancellationToken.None);

        Assert.Empty(report.MissingRows);
    }

    [Fact]
    public async Task RunAsync_UnsafeArtifactPath_ReportedAsMissing_NeverProbedOutsideRoot()
    {
        var repoRoot = MakeRepoRoot();
        // Traversal path: even if a file happened to exist elsewhere on disk, this must be flagged,
        // never resolved/probed outside the content-kb root (T-90-05).
        var unsafeRow = MakeRow(1, "unsafe-vid", "content-kb/../../etc/passwd", isVisible: true, approvalStatus: "approved");

        var reader = new FakeProdContentReader();
        reader.Rows.Add(unsafeRow);

        var audit = new GitBodyCoverageAudit(reader);
        var report = await audit.RunAsync("unused-conn-str", repoRoot, CancellationToken.None);

        var row = Assert.Single(report.MissingRows);
        Assert.Equal("content-kb/../../etc/passwd", row.ExpectedPath);
    }

    [Fact]
    public async Task RunAsync_NeverCallsAnyWriteApi_StructurallyReadOnly()
    {
        // FakeProdContentReader exposes ONLY ReadAllAsync (no upsert/delete/set/schema-ensure) —
        // this test proves the audit's only interaction is that single read call.
        var repoRoot = MakeRepoRoot();
        var reader = new FakeProdContentReader();
        reader.Rows.Add(MakeRow(1, "vid-a", "content-kb/test-channel/vid-a.md", isVisible: true, approvalStatus: "approved"));

        var audit = new GitBodyCoverageAudit(reader);
        await audit.RunAsync("unused-conn-str", repoRoot, CancellationToken.None);

        Assert.Equal(1, reader.ReadCallCount);
    }
}
