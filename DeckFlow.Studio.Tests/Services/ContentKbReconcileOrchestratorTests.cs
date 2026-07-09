using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="ContentKbReconcileOrchestrator"/> — the SYNC-11 reconcile dry-run I/O
/// orchestrator. Covers: all four discrepancy classes detected + persisted to the local store with
/// <c>SeedAvailable == true</c>; an unavailable/missing seed yields <c>SeedAvailable == false</c> and
/// ZERO seed-drift while the other three classes are still computed (T-91-25/T-91-26); prod is read
/// exactly once per run (T-91-15); <c>index-seed.json</c> is excluded from file-orphan enumeration;
/// and the D-06 report is written with a section+count per class, rendering a seed-unavailable
/// notice instead of a misleading empty seed-drift section.
/// </summary>
public sealed class ContentKbReconcileOrchestratorTests : IDisposable
{
    private readonly string _repoRoot;
    private readonly string _dbPath;

    public ContentKbReconcileOrchestratorTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "reconcile-orchestrator-tests", Path.GetRandomFileName());
        _repoRoot = Path.Combine(root, "repo");
        Directory.CreateDirectory(_repoRoot);
        _dbPath = Path.Combine(root, "content-kb.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try
        {
            var root = Path.GetDirectoryName(_repoRoot)!;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static ContentSiteIndexRow Youtube(
        long id,
        string videoId,
        string artifactPath,
        bool isVisible = false,
        string approvalStatus = "pending",
        bool? seedManaged = null,
        string? bodySha256 = null,
        string title = "Title")
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = title,
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = artifactPath,
            IndexedUtc = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            IsVisible = isVisible,
            ApprovalStatus = approvalStatus,
            SeedManaged = seedManaged,
            BodySha256 = bodySha256,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private void WriteBody(string artifactPath, string content)
    {
        var full = Path.Combine(_repoRoot, artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteSeed(params (string Type, string Value)[] entries)
    {
        var seedDir = Path.Combine(_repoRoot, "content-kb", "seed");
        Directory.CreateDirectory(seedDir);
        var items = entries.Select(e => $$"""{"naturalKeyType":"{{e.Type}}","naturalKeyValue":"{{e.Value}}"}""");
        File.WriteAllText(Path.Combine(seedDir, "index-seed.json"), $"[{string.Join(",", items)}]");
    }

    private ContentKbReconcileOrchestrator Build(FakeProdContentReader reader, IContentKbReconcileStore? store = null)
        => new(
            reader,
            store ?? new ContentKbReconcileStore(_dbPath),
            new FakeGitRepository { CannedRepoRoot = _repoRoot },
            new ConfigurationBuilder().Build(),
            NullLogger<ContentKbReconcileOrchestrator>.Instance);

    [Fact]
    public async Task RunDryRunAsync_DetectsAllFourClasses_SeedAvailableTrue_PersistsToStore()
    {
        // Published-orphan: approved + visible, no git body written.
        var publishedOrphan = Youtube(
            1, "missing-vid", "content-kb/test-channel/missing-vid.md",
            isVisible: true, approvalStatus: "approved");

        // Seed-drift: seed_managed = true, body present (so NOT also a published-orphan), natural
        // key absent from the seed file below.
        var seedDrift = Youtube(
            2, "present-vid", "content-kb/test-channel/present-vid.md",
            isVisible: true, approvalStatus: "approved", seedManaged: true);
        WriteBody(seedDrift.ArtifactPath, "unchanged body");

        // Body-hash-mismatch: stored hash does not match the computed hash of the git body.
        var bodyHashMismatch = Youtube(
            3, "hash-vid", "content-kb/test-channel/hash-vid.md",
            isVisible: true, approvalStatus: "approved", bodySha256: "deadbeef-not-a-real-hash");
        WriteBody(bodyHashMismatch.ArtifactPath, "actual git body text");

        // File-orphan: a .md body with no matching prod row at all.
        WriteBody("content-kb/test-channel/orphan-file.md", "orphaned body");

        WriteSeed(("youtube_channel", "some-other-vid"));

        var reader = new FakeProdContentReader();
        reader.Rows.Add(publishedOrphan);
        reader.Rows.Add(seedDrift);
        reader.Rows.Add(bodyHashMismatch);

        var store = new ContentKbReconcileStore(_dbPath);
        var orchestrator = Build(reader, store);

        var result = await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        Assert.True(result.SeedAvailable);
        Assert.Equal(4, result.Discrepancies.Count);
        Assert.Single(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.PublishedOrphan && d.NaturalKeyValue == "missing-vid");
        Assert.Single(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.SeedDrift && d.NaturalKeyValue == "present-vid");
        Assert.Single(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.BodyHashMismatch && d.NaturalKeyValue == "hash-vid");
        Assert.Single(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.FileOrphan && d.ArtifactPath == "content-kb/test-channel/orphan-file.md");

        var open = await store.GetOpenAsync("full");
        Assert.Equal(4, open.Count);
    }

    [Fact]
    public async Task RunDryRunAsync_SeedUnavailable_YieldsZeroSeedDrift_OtherClassesStillComputed()
    {
        // No index-seed.json written at all -> unavailable.
        var publishedOrphan = Youtube(
            1, "missing-vid", "content-kb/test-channel/missing-vid.md",
            isVisible: true, approvalStatus: "approved");

        var seedDrift = Youtube(
            2, "present-vid", "content-kb/test-channel/present-vid.md",
            isVisible: true, approvalStatus: "approved", seedManaged: true);
        WriteBody(seedDrift.ArtifactPath, "unchanged body");

        var bodyHashMismatch = Youtube(
            3, "hash-vid", "content-kb/test-channel/hash-vid.md",
            isVisible: true, approvalStatus: "approved", bodySha256: "deadbeef-not-a-real-hash");
        WriteBody(bodyHashMismatch.ArtifactPath, "actual git body text");

        var reader = new FakeProdContentReader();
        reader.Rows.Add(publishedOrphan);
        reader.Rows.Add(seedDrift);
        reader.Rows.Add(bodyHashMismatch);

        var orchestrator = Build(reader);

        var result = await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        Assert.False(result.SeedAvailable);
        Assert.DoesNotContain(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.SeedDrift);
        Assert.Contains(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.PublishedOrphan);
        Assert.Contains(result.Discrepancies, d => d.Kind == ContentKbReconcileKind.BodyHashMismatch);
    }

    [Fact]
    public async Task RunDryRunAsync_ReadsProdExactlyOnce()
    {
        var reader = new FakeProdContentReader();
        reader.Rows.Add(Youtube(1, "vid-a", "content-kb/test-channel/vid-a.md", isVisible: true, approvalStatus: "approved"));
        WriteBody("content-kb/test-channel/vid-a.md", "body");

        var orchestrator = Build(reader);
        await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        Assert.Equal(1, reader.ReadCallCount);
    }

    [Fact]
    public async Task RunDryRunAsync_ExcludesSeedIndexJsonFromFileOrphanEnumeration()
    {
        WriteSeed(("youtube_channel", "unrelated"));

        var reader = new FakeProdContentReader();
        var orchestrator = Build(reader);

        var result = await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        Assert.DoesNotContain(
            result.Discrepancies,
            d => d.Kind == ContentKbReconcileKind.FileOrphan
                && d.ArtifactPath != null
                && d.ArtifactPath.EndsWith("index-seed.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunDryRunAsync_ItsOwnReport_NeverSelfClassifiesAsFileOrphanOnNextRun()
    {
        var reader = new FakeProdContentReader();
        var orchestrator = Build(reader);

        await orchestrator.RunDryRunAsync("full", CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(_repoRoot, "content-kb", "reconcile-report.md")));

        var second = await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        Assert.DoesNotContain(
            second.Discrepancies,
            d => d.Kind == ContentKbReconcileKind.FileOrphan
                && d.ArtifactPath == "content-kb/reconcile-report.md");
    }

    [Fact]
    public async Task RunDryRunAsync_WritesReport_WithSectionAndCountPerClass()
    {
        var publishedOrphan = Youtube(
            1, "missing-vid", "content-kb/test-channel/missing-vid.md",
            isVisible: true, approvalStatus: "approved");
        WriteBody("content-kb/test-channel/orphan-file.md", "orphaned body");
        WriteSeed(("youtube_channel", "some-other-vid"));

        var reader = new FakeProdContentReader();
        reader.Rows.Add(publishedOrphan);

        var orchestrator = Build(reader);
        await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        var reportPath = Path.Combine(_repoRoot, "content-kb", "reconcile-report.md");
        Assert.True(File.Exists(reportPath));
        var text = File.ReadAllText(reportPath);

        Assert.Contains("Published Orphans", text, StringComparison.Ordinal);
        Assert.Contains("(1)", text, StringComparison.Ordinal);
        Assert.Contains("File Orphans", text, StringComparison.Ordinal);
        Assert.Contains("Seed Drift", text, StringComparison.Ordinal);
        Assert.Contains("Body Hash Mismatches", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunDryRunAsync_SeedUnavailable_ReportRendersSeedUnavailableNotice_NotEmptySection()
    {
        // No seed file written -> unavailable.
        var reader = new FakeProdContentReader();
        reader.Rows.Add(Youtube(
            1, "present-vid", "content-kb/test-channel/present-vid.md",
            isVisible: true, approvalStatus: "approved", seedManaged: true));
        WriteBody("content-kb/test-channel/present-vid.md", "body");

        var orchestrator = Build(reader);
        await orchestrator.RunDryRunAsync("full", CancellationToken.None);

        var reportPath = Path.Combine(_repoRoot, "content-kb", "reconcile-report.md");
        var text = File.ReadAllText(reportPath);

        Assert.Contains("SEED UNAVAILABLE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Seed Drift (seed-managed row absent from seed) (0)", text, StringComparison.Ordinal);
    }
}
