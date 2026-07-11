using System.IO;
using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// 91-09 operator-gate DRIVER: exercises the full reconcile workflow end-to-end against a real
/// on-disk fixture — a real SQLite "prod" <see cref="ContentSiteIndexStore"/> seeded with rows in
/// all four discrepancy classes, a real git-style <c>content-kb/**</c> body tree, a real
/// <c>index-seed.json</c>, and the real <see cref="ContentKbReconcileStore"/>. Only the Postgres
/// transport is faked (<see cref="FixtureProdReader"/>/<see cref="FixtureProdStoreFactory"/> point the
/// prod reader + store factory at the SQLite fixture; there is no local test Postgres, and this never
/// touches real prod). The real <see cref="ContentKbReconcileOrchestrator"/> (git-tree walk, seed
/// read, classifier, local-store persist, D-06 report) and the real
/// <see cref="ReconcileCoordinator"/> (flag gate, stale-check, ownership-scoped soft-hide) run
/// unchanged. This provides the automated evidence the two 91-09 human-verify checkpoints require.
/// </summary>
public sealed class ReconcileFixtureDriveTests : IDisposable
{
    private const string Youtube = ContentSourceType.Youtube;

    private readonly ITestOutputHelper _output;
    private readonly string _repoRoot;
    private readonly string _prodDbPath;
    private readonly string _localDbPath;
    private readonly ContentSiteIndexStore _prod;

    // Fixture natural keys (YouTube video ids), one per intended discrepancy class + a prod-owned control.
    private const string PubOrphanId = "yt-pub-orphan";       // published-orphan: visible+approved row, no git body
    private const string SeedDriftId = "yt-seed-drift";       // seed-drift: seed_managed=true row, key absent from seed
    private const string HashMismatchId = "yt-hash-mismatch"; // body-hash-mismatch: stored hash != computed git-body hash
    private const string ProdOwnedId = "yt-prod-owned";       // control: seed_managed=false, absent from seed -> must stay visible
    private const string FileOrphanPath = "content-kb/test-channel/yt-file-orphan.md"; // file-orphan: git body, no prod row

    public ReconcileFixtureDriveTests(ITestOutputHelper output)
    {
        _output = output;
        var stamp = Guid.NewGuid().ToString("N");
        _repoRoot = Path.Combine(Path.GetTempPath(), $"reconcile-fixture-{stamp}");
        _prodDbPath = Path.Combine(Path.GetTempPath(), $"reconcile-prod-{stamp}.db");
        _localDbPath = Path.Combine(Path.GetTempPath(), $"reconcile-local-{stamp}.db");
        Directory.CreateDirectory(Path.Combine(_repoRoot, "content-kb", "test-channel"));
        Directory.CreateDirectory(Path.Combine(_repoRoot, "content-kb", "seed"));
        _prod = new ContentSiteIndexStore(_prodDbPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        foreach (var db in new[] { _prodDbPath, _localDbPath })
        {
            if (File.Exists(db))
            {
                File.Delete(db);
            }
        }

        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Drives_FullReconcileWorkflow_AgainstFixture()
    {
        await BuildFixtureAsync();

        var reader = new FixtureProdReader(_prod);
        var storeFactory = new FixtureProdStoreFactory(_prod);
        var git = new FakeGitRepository { CannedRepoRoot = _repoRoot };
        var localStore = new ContentKbReconcileStore(_localDbPath);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Studio:ProdConnectionString"] = "fixture-ignored",
            })
            .Build();

        var orchestrator = new ContentKbReconcileOrchestrator(
            reader, localStore, git, config, NullLogger<ContentKbReconcileOrchestrator>.Instance);
        var coordinator = new ReconcileCoordinator(
            orchestrator, localStore, storeFactory, reader, new StudioProdConnectionSource(config), NullLogger<ReconcileCoordinator>.Instance);

        // ── CHECKPOINT 1: dry-run detects all four classes, read-only, flag OFF ───────────────────
        reader.Flag = false; // sync.reconcile OFF — detection must be flag-independent.
        var dryRun = await coordinator.RunDryRunAsync();

        var byKind = dryRun.Discrepancies
            .GroupBy(d => d.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        _output.WriteLine("── CHECKPOINT 1: dry-run (sync.reconcile OFF) ──");
        _output.WriteLine($"SeedAvailable = {dryRun.SeedAvailable}");
        foreach (var kind in Enum.GetValues<ContentKbReconcileKind>())
        {
            _output.WriteLine($"  {kind}: {(byKind.TryGetValue(kind, out var c) ? c : 0)}");
        }

        Assert.True(dryRun.SeedAvailable, "seed present and parsed");
        Assert.Equal(1, byKind.GetValueOrDefault(ContentKbReconcileKind.PublishedOrphan));
        Assert.Equal(1, byKind.GetValueOrDefault(ContentKbReconcileKind.FileOrphan));
        Assert.Equal(1, byKind.GetValueOrDefault(ContentKbReconcileKind.SeedDrift));
        Assert.Equal(1, byKind.GetValueOrDefault(ContentKbReconcileKind.BodyHashMismatch));

        // D-06 report written under the checkout.
        var reportPath = Path.Combine(_repoRoot, "content-kb", "reconcile-report.md");
        Assert.True(File.Exists(reportPath), "D-06 report file written");
        var report = await File.ReadAllTextAsync(reportPath);
        _output.WriteLine($"D-06 report written to {reportPath} ({report.Length} chars)");
        Assert.Contains("Published Orphans", report);
        Assert.Contains("File Orphans", report);
        Assert.Contains("Seed Drift", report);
        Assert.Contains("Body Hash Mismatches", report);

        // No prod write occurred: every seeded row remains visible after the read-only dry-run.
        await AssertAllRowsVisibleAsync("after read-only dry-run");

        // ── CHECKPOINT 2a: flag OFF Apply is refused ──────────────────────────────────────────────
        var seedDriftId = dryRun.Discrepancies.Single(d => d.Kind == ContentKbReconcileKind.SeedDrift).Id;
        var flagOff = await coordinator.ApplyRemovalsAsync(new HashSet<string> { seedDriftId });
        _output.WriteLine($"── CHECKPOINT 2a: Apply flag OFF -> WasApplied={flagOff.WasApplied}, reason={flagOff.RefusalReason}");
        Assert.False(flagOff.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.FlagNotEnabled, flagOff.RefusalReason);
        await AssertVisibleAsync(SeedDriftId, expected: true, "flag-off Apply must not hide the seed-drift row");

        // ── CHECKPOINT 2b: flag indeterminate (null) Apply is refused ─────────────────────────────
        reader.Flag = null;
        var flagNull = await coordinator.ApplyRemovalsAsync(new HashSet<string> { seedDriftId });
        _output.WriteLine($"── CHECKPOINT 2b: Apply flag NULL -> WasApplied={flagNull.WasApplied}, reason={flagNull.RefusalReason}");
        Assert.False(flagNull.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.FlagNotEnabled, flagNull.RefusalReason);
        await AssertVisibleAsync(SeedDriftId, expected: true, "flag-indeterminate Apply must not hide the seed-drift row");

        // ── CHECKPOINT 2c: stale reviewed set is refused (flag ON) ─────────────────────────────────
        reader.Flag = true;
        var staleSet = new HashSet<string> { "sha256:not-a-current-discrepancy" };
        var stale = await coordinator.ApplyRemovalsAsync(staleSet);
        _output.WriteLine($"── CHECKPOINT 2c: Apply stale set (flag ON) -> WasApplied={stale.WasApplied}, reason={stale.RefusalReason}");
        Assert.False(stale.WasApplied);
        Assert.Equal(ReconcileApplyRefusalReason.StaleReviewSet, stale.RefusalReason);
        await AssertVisibleAsync(SeedDriftId, expected: true, "stale Apply must not hide the seed-drift row");

        // ── CHECKPOINT 2d: matching Apply, flag ON — hides ONLY the seed-owned drift row ──────────
        // Re-run the dry-run so the reviewed set matches current state (mirrors the operator flow).
        var fresh = await coordinator.RunDryRunAsync();
        var freshSeedDriftId = fresh.Discrepancies.Single(d => d.Kind == ContentKbReconcileKind.SeedDrift).Id;
        var applied = await coordinator.ApplyRemovalsAsync(new HashSet<string> { freshSeedDriftId });
        _output.WriteLine($"── CHECKPOINT 2d: Apply matching set (flag ON) -> WasApplied={applied.WasApplied}, hidden={applied.HiddenCount}");
        Assert.True(applied.WasApplied);
        Assert.Equal(1, applied.HiddenCount);

        // The seed-owned drift row is now soft-hidden; the row is RETAINED (not deleted).
        var hiddenRow = await _prod.GetByNaturalKeyAsync(Youtube, SeedDriftId);
        Assert.NotNull(hiddenRow);
        Assert.False(hiddenRow!.IsVisible);
        _output.WriteLine($"  seed-drift row '{SeedDriftId}': IsVisible={hiddenRow.IsVisible} (retained, soft-hidden)");

        // The prod-owned control (seed_managed=false, absent from seed) stays VISIBLE — SYNC-17 invariant.
        await AssertVisibleAsync(ProdOwnedId, expected: true, "prod-owned row must remain visible after Apply");
        // Untargeted classes are untouched.
        await AssertVisibleAsync(PubOrphanId, expected: true, "published-orphan row untouched by Apply");
        await AssertVisibleAsync(HashMismatchId, expected: true, "body-hash-mismatch row untouched by Apply");

        _output.WriteLine("── ALL CHECKPOINTS PASSED: 4 classes detected read-only; flag-off/null/stale refused; flag-on hid only seed-owned; prod-owned stayed visible.");
    }

    // Builds the on-disk fixture: prod rows across all four classes + a prod-owned control, the git
    // content-kb body tree, and index-seed.json (seed present -> SeedAvailable true; keys present for
    // published-orphan + body-hash-mismatch so those rows are NOT also seed-drift; seed-drift + control
    // keys deliberately excluded).
    private async Task BuildFixtureAsync()
    {
        // published-orphan: visible+approved row whose artifact .md is ABSENT from the git tree.
        await SeedProdRowAsync(PubOrphanId, seedManaged: true, gitBody: null, storedHashMatchesBody: false);

        // seed-drift: git body present + correct hash + seed_managed=true, but key absent from seed.
        var seedDriftBody = MdBody(SeedDriftId);
        WriteGitBody(SeedDriftId, seedDriftBody);
        await SeedProdRowAsync(SeedDriftId, seedManaged: true, gitBody: seedDriftBody, storedHashMatchesBody: true);

        // body-hash-mismatch: git body present, stored hash WRONG, key present in seed (so not seed-drift).
        var hashBody = MdBody(HashMismatchId);
        WriteGitBody(HashMismatchId, hashBody);
        await SeedProdRowAsync(HashMismatchId, seedManaged: true, gitBody: hashBody, storedHashMatchesBody: false);

        // prod-owned control: git body present + correct hash + seed_managed=FALSE, key absent from seed.
        var ownedBody = MdBody(ProdOwnedId);
        WriteGitBody(ProdOwnedId, ownedBody);
        await SeedProdRowAsync(ProdOwnedId, seedManaged: false, gitBody: ownedBody, storedHashMatchesBody: true);

        // file-orphan: a git body with NO matching prod row.
        File.WriteAllText(Path.Combine(_repoRoot, FileOrphanPath.Replace('/', Path.DirectorySeparatorChar)), MdBody("orphan"));

        // index-seed.json: seed present. Include published-orphan + body-hash-mismatch keys ONLY.
        var seedEntries = new[]
        {
            new { naturalKeyType = Youtube, naturalKeyValue = PubOrphanId },
            new { naturalKeyType = Youtube, naturalKeyValue = HashMismatchId },
        };
        var seedJson = JsonSerializer.Serialize(seedEntries);
        File.WriteAllText(Path.Combine(_repoRoot, "content-kb", "seed", "index-seed.json"), seedJson);
    }

    private async Task SeedProdRowAsync(string videoId, bool seedManaged, string? gitBody, bool storedHashMatchesBody)
    {
        await _prod.UpsertContentColumnsOnlyAsync(NewRow(videoId));
        var row = await _prod.GetByNaturalKeyAsync(Youtube, videoId);
        Assert.NotNull(row);

        await _prod.SetApprovalStatusAsync(Youtube, videoId, "approved");
        await _prod.SetVisibilityAsync(row!.Id, visible: true);
        await _prod.SetSeedManagedIfNullAsync(row.Id, seedManaged);

        // Stored body hash: correct (matches git body) or deliberately wrong (drives body-hash-mismatch).
        var storedHash = storedHashMatchesBody && gitBody is not null
            ? ContentSiteIndexContentSignature.ComputeBodySha256(gitBody)
            : ContentSiteIndexContentSignature.ComputeBodySha256(MdBody($"WRONG-{videoId}"));
        await _prod.SetBodySha256IfNullAsync(row.Id, storedHash);
    }

    private static ContentSiteIndexRow NewRow(string videoId)
        => new()
        {
            Id = 0,
            Source = "test-channel",
            Title = $"Video {videoId}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-07-01T13:00:00Z"),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
            RssGuid = null,
        };

    private void WriteGitBody(string videoId, string body)
        => File.WriteAllText(
            Path.Combine(_repoRoot, "content-kb", "test-channel", $"{videoId}.md"),
            body);

    private static string MdBody(string token)
        => $"---\ntitle: {token}\n---\n\nBody content for {token}.\n";

    private async Task AssertAllRowsVisibleAsync(string context)
    {
        foreach (var id in new[] { PubOrphanId, SeedDriftId, HashMismatchId, ProdOwnedId })
        {
            await AssertVisibleAsync(id, expected: true, $"{id} visible {context}");
        }
    }

    private async Task AssertVisibleAsync(string videoId, bool expected, string because)
    {
        var row = await _prod.GetByNaturalKeyAsync(Youtube, videoId);
        Assert.NotNull(row);
        Assert.True(row!.IsVisible == expected, because);
    }

    // ── Fixture transport doubles (SQLite stands in for prod Postgres) ─────────────────────────────

    private sealed class FixtureProdReader(ContentSiteIndexStore store) : IProdContentReader
    {
        public bool? Flag { get; set; }

        public async Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(
            string connectionString, CancellationToken cancellationToken = default)
            => await store.GetAllRowsAsync(cancellationToken);

        public Task<bool> ReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Flag == true);

        public Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Flag);
    }

    private sealed class FixtureProdStoreFactory(ContentSiteIndexStore store) : IProdStoreFactory
    {
        public IContentSiteIndexStore Create(string connectionString) => store;
    }
}
