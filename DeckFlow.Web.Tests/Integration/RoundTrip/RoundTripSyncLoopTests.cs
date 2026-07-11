using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DeckFlow.Web.Tests.Integration.RoundTrip;

/// <summary>
/// SYNC-16 round-trip integration test (Plan 93-02): walks the ENTIRE Content-KB sync loop --
/// distill (local) -&gt; approve -&gt; Publish (git commit) -&gt; operator push -&gt; deploy-copy + reseed
/// (creates the prod row) -&gt; web body resolution -&gt; DirectPush a second row (flag ON, faked
/// deploy-confirm) -&gt; re-export + second deploy-copy + SECOND reseed (redeploy) -&gt; PullFromProd
/// (field authority) -&gt; Reconcile dry-run (idempotent) -- on the real Testcontainers Postgres +
/// real git tree the 93-01 harness (<see cref="RoundTripHarness"/> plus the seams in
/// <c>RoundTripSeams.cs</c>) bootstraps. Only the LLM, SFTP transport, and deploy-confirm HTTP
/// transport are faked
/// (<see cref="CannedLlmDistillationService"/>, <see cref="RecordingSshArtifactUploader"/>,
/// <see cref="AppTreeDeployedBodyConfirmer"/> -- all in <c>RoundTripSeams.cs</c>); every
/// coordinator (Publish, DirectPush, PullFromProd, Reconcile) and store is the real production type.
/// </summary>
/// <remarks>
/// D-07: this is a LOCAL/MANUAL Docker gate -- the <see cref="PostgresFactAttribute"/> auto-skips
/// in CI (and locally) whenever <c>DECKFLOW_POSTGRES_TESTS</c> is unset or Docker is unavailable.
/// It is the pre-flip proof harness for <c>sync.directpush-gitbody</c> / <c>sync.reconcile</c> --
/// NOT a per-PR lock. Zero production-code change: every assertion below drives real production
/// types through their public constructors.
/// </remarks>
public sealed class RoundTripSyncLoopTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private static readonly JsonSerializerOptions SeedProbeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly PostgresContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly RoundTripHarness _harness = new();
    private readonly string _localDataRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-loop-data-{Guid.NewGuid():N}");
    private readonly string _pullApplyDataRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-loop-pull-{Guid.NewGuid():N}");
    private readonly string _reconcileDbPath = Path.Combine(Path.GetTempPath(), $"roundtrip-loop-reconcile-{Guid.NewGuid():N}.db");

    /// <summary>Creates the round-trip test bound to the shared Postgres container fixture.</summary>
    /// <param name="fixture">Shared Testcontainers Postgres fixture.</param>
    /// <param name="output">xUnit output sink for stage-by-stage checkpoint logging.</param>
    public RoundTripSyncLoopTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [PostgresFact]
    public async Task RoundTrip_DistillToReconcile_HashMatchesEveryHop_NoRevertAfterReseed()
    {
        // ── Boot: real PG schema + real git bootstrap; wire the real coordinators ──────────────
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await _harness.EnsureProdSchemaAsync(connectionString);
        await _harness.InitRepoAsync();
        _harness.SetRepoRootEnv();

        // Why: WriteFile/ComputeRelativeArtifactPath agree only when the factory's artifactRoot
        // param already carries the content-kb/ segment -- exactly how Studio's Program.cs builds
        // ContentKbOrchestratorOptions.ArtifactRoot = Path.Combine(studioDataDirectory, "content-kb").
        var factoryArtifactRoot = Path.Combine(_localDataRoot, "content-kb");
        Directory.CreateDirectory(factoryArtifactRoot);

        var localConnection = RelationalDatabaseConnection.FromSqlitePath(_harness.LocalDbPath);
        var localStore = _harness.CreateLocalStore();
        var prodStore = _harness.CreateProdStore(connectionString);
        var sourceStore = new ContentSourceStore(localConnection);
        var videoStore = new ContentVideoStore(localConnection);

        var git = new GitRepository();
        var config = _harness.BuildConfiguration();
        var options = new ContentKbOrchestratorOptions { ArtifactRoot = factoryArtifactRoot };

        var orchestrator = ContentKbOrchestratorFactory.Create(
            localConnection,
            factoryArtifactRoot,
            distiller: new CannedLlmDistillationService(),
            lister: new ThrowingYouTubeChannelVideoLister(),
            transcriptSource: new ThrowingTranscriptSource(),
            chunker: new ThrowingFfmpegAudioChunker());

        var prodReader = new FixtureProdReader(prodStore);
        var prodStoreFactory = new FixtureProdStoreFactory(prodStore);
        var uploader = new RecordingSshArtifactUploader();
        var confirmer = new AppTreeDeployedBodyConfirmer(prodStore, _harness.AppRoot);

        var publish = new PublishCoordinator(git, orchestrator, localStore, options, new PublishStateDeriver());
        var directPush = new DirectPushCoordinator(
            localStore, uploader, prodStoreFactory, config, options, git, orchestrator, prodReader, confirmer);
        var pull = new PullFromProdCoordinator(localStore, git, prodReader, config, options, NullLogger<PullFromProdCoordinator>.Instance);

        _output.WriteLine("── Boot: real PG schema + real git tree bootstrapped; coordinators wired ──");

        // ── Distill row A into the LOCAL store only (CH3/W3: distill never writes prod) ────────
        var stamp = Guid.NewGuid().ToString("N");
        var sourceId = await sourceStore.InsertSourceAsync(
            $"roundtrip-loop-{stamp}",
            "Round Trip Loop Channel",
            ContentSourceType.Youtube,
            $"https://youtube.com/channel/roundtrip-loop-{stamp}");

        var videoIdA = $"rt-a-{stamp}";
        var videoRowIdA = await videoStore.InsertVideoAsync(
            sourceId, videoIdA, rssGuid: null, "Round Trip Row A",
            $"https://youtu.be/{videoIdA}", DateTimeOffset.UtcNow, TranscriptStatus.Captions);
        await videoStore.InsertTranscriptAsync(videoRowIdA, "captions", CannedTranscript("A"));

        var videoIdB = $"rt-b-{stamp}";
        var videoRowIdB = await videoStore.InsertVideoAsync(
            sourceId, videoIdB, rssGuid: null, "Round Trip Row B",
            $"https://youtu.be/{videoIdB}", DateTimeOffset.UtcNow, TranscriptStatus.Captions);
        await videoStore.InsertTranscriptAsync(videoRowIdB, "captions", CannedTranscript("B"));

        var distillA = await orchestrator.DistillAsync(limit: 10, dryRun: false, isSubscriptionProvider: true, videoIds: [videoIdA]);
        Assert.True(distillA.Success, $"Distill A did not succeed: {distillA.AbortedReason}");
        Assert.Equal(1, distillA.VideosDistilled);

        var rowA = await localStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);
        Assert.NotNull(rowA);
        var artifactFullPathA = Path.Combine(_localDataRoot, rowA!.ArtifactPath);
        Assert.True(File.Exists(artifactFullPathA), $"distilled artifact missing at {artifactFullPathA}");
        var writtenBodyA = await File.ReadAllTextAsync(artifactFullPathA);
        var hashDistillA = ContentSiteIndexContentSignature.ComputeBodySha256(writtenBodyA);
        Assert.Equal(hashDistillA, rowA.BodySha256);

        var prodRowABeforeReseed = await prodStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);
        Assert.Null(prodRowABeforeReseed); // CH3/W3: distill writes LOCAL only -- prod row A is absent

        _output.WriteLine($"── Distill: row A written LOCAL only; hashDistillA={hashDistillA[..8]}… ──");

        // ── Approve + Publish row A (export seed + copy body + commit; operator reviews + pushes) ──
        await localStore.SetApprovalStatusAsync(ContentSourceType.Youtube, videoIdA, "approved");

        var initData = await publish.LoadInitDataAsync(CancellationToken.None);
        var exportResult = await publish.ExportAndDiffAsync(
            initData.RepoRoot, _localDataRoot, new IOrchestratorProgress.NullOrchestratorProgress(), CancellationToken.None);
        Assert.Equal(PublishExportStatus.Success, exportResult.Status);

        var commitResult = await publish.CommitAsync(
            initData.RepoRoot, exportResult.StagedPaths, exportResult.CommitMessage, exportResult.ExportedKeys, CancellationToken.None);
        Assert.False(commitResult.LocalStampFailed);

        // Why: Publish (D-01) never pushes -- the operator reviews then pushes by hand. Without this,
        // DirectPush's foreign-commit guard would later see the unpushed Publish commit ahead of
        // origin and refuse (DirectPushUnreviewedCommitsException). This IS the operator's push.
        await git.PushAsync(initData.RepoRoot, "origin", initData.Branch, CancellationToken.None);

        var seedPath = Path.Combine(_harness.RepoRoot, "content-kb", "seed", "index-seed.json");
        var seedEntriesAfterPublish = await ReadSeedEntriesAsync(seedPath);
        var seedEntryA = Assert.Single(seedEntriesAfterPublish, e => e.NaturalKeyValue == videoIdA);
        Assert.Equal(hashDistillA, seedEntryA.BodySha256); // distill-computed == seed-json hop

        _output.WriteLine("── Publish: row A approved + committed + pushed; seed carries hashDistillA ──");

        // ── Deploy-copy + FIRST reseed; resolve the served body ────────────────────────────────
        await _harness.DeployToAppAsync();

        var stubEnvironment = new StubWebHostEnvironment(_harness.AppRoot);
        var flagCache = new FakeFeatureFlagCache();
        var pathResolver = new ContentKbArtifactPathResolver(
            stubEnvironment, config, flagCache, NullLogger<ContentKbArtifactPathResolver>.Instance);
        var bodyResolver = new ContentKbArtifactBodyResolver(pathResolver);
        var seedLoader = new ContentKbSeedLoader(pathResolver, prodStore, NullLogger<ContentKbSeedLoader>.Instance);

        var reseededCount1 = await seedLoader.LoadIfPresentAsync();
        Assert.Equal(1, reseededCount1); // only row A is in the seed at this point

        var prodRowAAfterReseed = await prodStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);
        Assert.NotNull(prodRowAAfterReseed); // the reseed CREATES prod row A -- the reconstructs-prod proof

        // Why: the reseed itself never sets visibility; simulate the one prior admin action that
        // made this row live, establishing the baseline the no-revert check (below) protects.
        await prodStore.SetVisibilityAsync(prodRowAAfterReseed!.Id, visible: true);
        prodRowAAfterReseed = await prodStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);

        var servedBodyA = await bodyResolver.TryReadArtifactTextAsync(rowA.ArtifactPath);
        Assert.NotNull(servedBodyA);
        Assert.Equal(writtenBodyA, servedBodyA); // served TEXT == published TEXT
        var hashServedA = ContentSiteIndexContentSignature.ComputeBodySha256(servedBodyA!);
        Assert.Equal(hashDistillA, hashServedA);
        Assert.Equal(hashDistillA, prodRowAAfterReseed!.BodySha256); // served-body-recompute == prod-row hop (SC2)

        _output.WriteLine("── Reseed #1: /app deploy created prod row A; served==published, hash matches every hop ──");

        // ── DirectPush row B (sync.directpush-gitbody ON, faked deploy-confirm) ────────────────
        var distillB = await orchestrator.DistillAsync(limit: 10, dryRun: false, isSubscriptionProvider: true, videoIds: [videoIdB]);
        Assert.True(distillB.Success, $"Distill B did not succeed: {distillB.AbortedReason}");
        Assert.Equal(1, distillB.VideosDistilled);
        await localStore.SetApprovalStatusAsync(ContentSourceType.Youtube, videoIdB, "approved");

        prodReader.Flag = true; // sync.directpush-gitbody ON

        var diff = await directPush.ComputeDiffAsync(CancellationToken.None);
        var publishRows = diff.PublishRows;
        Assert.Single(publishRows); // row A is Unchanged (content signature matches prod after reseed); only B is New
        Assert.Equal(videoIdB, publishRows[0].YoutubeVideoId);

        await directPush.UploadArtifactsAsync(publishRows, _localDataRoot, new Progress<SshUploadResult>(), CancellationToken.None);
        await directPush.WriteContentAsync(publishRows, CancellationToken.None);

        var gitResult = await directPush.CommitAndPushBodiesAsync(publishRows, _localDataRoot, CancellationToken.None);
        Assert.Equal(DirectPushGitOutcome.Committed, gitResult.Outcome);

        await _harness.DeployToAppAsync(); // redeploy /app so it now carries B's body + the re-exported seed

        var verifyResult = await directPush.VerifyAndPublishAsync(publishRows, CancellationToken.None);
        Assert.Empty(verifyResult.NotConfirmed);
        Assert.Single(verifyResult.Confirmed);

        var prodRowBAfterConfirm = await prodStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdB);
        Assert.NotNull(prodRowBAfterConfirm);
        Assert.True(prodRowBAfterConfirm!.IsVisible);
        var servedBodyB = await bodyResolver.TryReadArtifactTextAsync(prodRowBAfterConfirm!.ArtifactPath);
        Assert.NotNull(servedBodyB);
        var hashServedB = ContentSiteIndexContentSignature.ComputeBodySha256(servedBodyB!);
        Assert.Equal(prodRowBAfterConfirm.BodySha256, hashServedB);

        var seedEntriesAfterDirectPush = await ReadSeedEntriesAsync(seedPath);
        Assert.Contains(seedEntriesAfterDirectPush, e => e.NaturalKeyValue == videoIdA);
        Assert.Contains(seedEntriesAfterDirectPush, e => e.NaturalKeyValue == videoIdB); // W-2/M1: both keys before the 2nd reseed

        _output.WriteLine("── DirectPush: row B confirmed + visible; re-exported seed carries A AND B ──");

        // ── SECOND reseed (redeploy) -- no-revert (SC3, the M2/C3 load-bearing check) ──────────
        var reseededCount2 = await seedLoader.LoadIfPresentAsync();
        Assert.Equal(2, reseededCount2); // seed now carries both A and B

        var prodRowAAfterSecondReseed = await prodStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);
        Assert.NotNull(prodRowAAfterSecondReseed);
        Assert.True(prodRowAAfterSecondReseed!.IsVisible,
            "NO-REVERT (SC3): the second reseed must NOT revert the PUBLISHED row A back to hidden");
        Assert.Equal(hashDistillA, prodRowAAfterSecondReseed.BodySha256);

        var prodRowBAfterSecondReseed = await prodStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdB);
        Assert.NotNull(prodRowBAfterSecondReseed);
        Assert.True(prodRowBAfterSecondReseed!.IsVisible,
            "NO-REVERT (SC3, load-bearing / M2-C3): the second reseed must NOT revert the DirectPush'd row B back to hidden");
        Assert.Equal(prodRowBAfterConfirm.BodySha256, prodRowBAfterSecondReseed.BodySha256);

        _output.WriteLine("── Reseed #2 (redeploy): neither row A nor row B was reverted -- no-revert-after-reseed proven ──");

        // ════════════════════════════════════════════════════════════════════════════════════
        // Pull field-authority: force a non-body diff on row A (bumped IndexedUtc; body unchanged)
        // ════════════════════════════════════════════════════════════════════════════════════
        var originalLocalRowA = await localStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);
        Assert.NotNull(originalLocalRowA);
        var originalLocalIsVisibleA = originalLocalRowA!.IsVisible;

        // Why: PullAndClassifyAsync returns ONLY entries that DIFFER (PullFromProdCoordinator.cs:123)
        // -- a byte-identical row never surfaces. Bumping prod row A's IndexedUtc (a metadata column,
        // NOT the body) forces a real ProdNewer diff entry while the git body hash stays unchanged,
        // so the divergence stamp comes back Clean (body matches; only metadata differs).
        var bumpedProdRowA = prodRowAAfterSecondReseed! with { IndexedUtc = prodRowAAfterSecondReseed.IndexedUtc.AddMinutes(5) };
        await prodStore.UpsertContentColumnsOnlyAsync(bumpedProdRowA);

        var pullResult = await pull.PullAndClassifyAsync(
            log: new Progress<string>(msg => _output.WriteLine($"[pull] {msg}")),
            onStage: stage => _output.WriteLine($"[pull-stage] {stage}"),
            CancellationToken.None);

        var pullEntryA = Assert.Single(pullResult.Entries, e => e.NaturalKeyValue == videoIdA);
        Assert.Equal(BodyDivergenceStatus.Clean, pullEntryA.BodyDivergence);

        var applyResults = await pull.ApplyAdoptionsAsync(
            new[] { pullEntryA },
            _pullApplyDataRoot,
            progress: new Progress<IReadOnlyList<PullApplyRowResult>>(_ => { }),
            acknowledgedDivergentKeys: new HashSet<string>(),
            CancellationToken.None);
        Assert.True(Assert.Single(applyResults).Success);

        var localRowAAfterAdopt = await localStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoIdA);
        Assert.NotNull(localRowAAfterAdopt);
        Assert.Equal(hashDistillA, localRowAAfterAdopt!.BodySha256); // body <- git (unchanged; hash still matches)
        Assert.Equal(pullEntryA.ProdRow!.ApprovalStatus, localRowAAfterAdopt.ApprovalStatus); // approval <- prod
        Assert.Equal(originalLocalIsVisibleA, localRowAAfterAdopt.IsVisible); // is_visible PRESERVED, never clobbered

        _output.WriteLine("── Pull: Clean-divergence entry adopted -- body<-git, approval<-prod, is_visible preserved ──");

        // ════════════════════════════════════════════════════════════════════════════════════
        // Reconcile dry-run: zero unexpected discrepancies + idempotent re-run
        // ════════════════════════════════════════════════════════════════════════════════════
        var reconcileStore = new ContentKbReconcileStore(_reconcileDbPath);
        var reconcileOrchestrator = new ContentKbReconcileOrchestrator(
            prodReader, reconcileStore, git, config, NullLogger<ContentKbReconcileOrchestrator>.Instance);
        var reconcile = new ReconcileCoordinator(
            reconcileOrchestrator, reconcileStore, prodStoreFactory, prodReader, config, NullLogger<ReconcileCoordinator>.Instance);

        var dryRun1 = await reconcile.RunDryRunAsync();
        Assert.True(dryRun1.SeedAvailable);
        Assert.Empty(dryRun1.Discrepancies); // coherent loop -- zero unexpected discrepancies

        var dryRun2 = await reconcile.RunDryRunAsync();
        Assert.True(dryRun2.SeedAvailable);
        Assert.Empty(dryRun2.Discrepancies); // idempotent re-run -- zero duplicate discrepancies / ghost rows

        var openAfterBothRuns = await reconcile.GetOpenDiscrepanciesAsync();
        Assert.Empty(openAfterBothRuns); // no ghost rows persisted across the two runs

        _output.WriteLine("── Reconcile: zero unexpected discrepancies on both dry-runs (idempotent, no ghost rows) ──");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _localDataRoot, _pullApplyDataRoot })
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        if (File.Exists(_reconcileDbPath))
        {
            File.Delete(_reconcileDbPath);
        }
    }

    private static string CannedTranscript(string label) =>
        $"This is a canned transcript body for round-trip row {label}. It walks through a cEDH " +
        "ramp-into-payoff game plan across several sentences so the distillation validation gate " +
        "has real content to work with, without invoking any real transcript provider.";

    private static async Task<IReadOnlyList<SeedProbeEntry>> ReadSeedEntriesAsync(string seedPath)
    {
        var json = await File.ReadAllTextAsync(seedPath);
        return JsonSerializer.Deserialize<List<SeedProbeEntry>>(json, SeedProbeJsonOptions)
            ?? new List<SeedProbeEntry>();
    }

    // Minimal probe shape over the seed JSON -- only the fields this test asserts on.
    private sealed record SeedProbeEntry
    {
        public string NaturalKeyType { get; init; } = string.Empty;

        public string NaturalKeyValue { get; init; } = string.Empty;

        public string? BodySha256 { get; init; }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
            WebRootPath = contentRootPath;
            WebRootFileProvider = new NullFileProvider();
        }

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = Environments.Development;
    }

    private sealed class ThrowingYouTubeChannelVideoLister : IYouTubeChannelVideoLister
    {
        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
            string channelUrl, int limit, int skip = 0, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip loop test seeds videos directly; ListRecentAsync must not be called.");

        public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(
            IReadOnlyList<string> videoIds, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip loop test seeds videos directly; GetByIdsAsync must not be called.");
    }

    private sealed class ThrowingTranscriptSource : ITranscriptSource
    {
        public string SourceType => ContentSourceType.Youtube;

        public Task<TranscriptFetchResult> FetchTranscriptAsync(
            string naturalKey, TimeSpan? knownDuration, string monthKey, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip loop test seeds transcripts directly; FetchTranscriptAsync must not be called.");
    }

    private sealed class ThrowingFfmpegAudioChunker : IFfmpegAudioChunker
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip loop test does not harvest audio; IsAvailableAsync must not be called.");

        public Task<IReadOnlyList<string>> ChunkAsync(
            string inputPath, string outputDirectory, int segmentSeconds = 300, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip loop test does not harvest audio; ChunkAsync must not be called.");
    }
}
