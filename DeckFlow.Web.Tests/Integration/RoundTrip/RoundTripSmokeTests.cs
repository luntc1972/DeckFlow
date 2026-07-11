using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using Xunit;

namespace DeckFlow.Web.Tests.Integration.RoundTrip;

/// <summary>
/// Harness boot proof for the SYNC-16 round-trip integration test (Plan 93-01). With a real
/// Postgres schema pre-created and a real temp git repo bootstrapped, one canned distill run over
/// a seeded source + video + transcript writes an artifact and upserts a LOCAL-store row whose
/// <c>body_sha256</c> is non-null and equals <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/>
/// recomputed over the written artifact body. The prod PG row is intentionally NOT created here —
/// distill writes the local store + artifact root only; the prod row appears only via
/// Publish/DirectPush + reseed, which the full loop in 93-02 exercises end-to-end. Auto-skips when
/// <c>DECKFLOW_POSTGRES_TESTS</c> is unset or Docker is unavailable (D-07).
/// </summary>
public sealed class RoundTripSmokeTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly PostgresContainerFixture _fixture;
    private readonly RoundTripHarness _harness = new();
    private readonly string _artifactRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-artifacts-{Guid.NewGuid():N}");

    /// <summary>Creates the smoke test bound to the shared Postgres container fixture.</summary>
    /// <param name="fixture">Shared Testcontainers Postgres fixture.</param>
    public RoundTripSmokeTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Harness_Boots_Distill_Git_And_Postgres_Schema()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        await _harness.EnsureProdSchemaAsync(connectionString);
        await _harness.InitRepoAsync();

        Directory.CreateDirectory(_artifactRoot);

        // Why: WriteFile writes {root}/{slug}/{id}.md while the stored ArtifactPath is
        // content-kb/{slug}/{id}.md, so the two agree only when the factory's artifactRoot param
        // already carries the content-kb/ segment -- exactly how Studio's Program.cs builds
        // ContentKbOrchestratorOptions.ArtifactRoot = Path.Combine(studioDataDirectory, "content-kb").
        // The File.Exists assertion below then resolves the stored relative path against _artifactRoot
        // (the parent of that segment). Passing bare _artifactRoot silently writes to the wrong tree.
        var factoryArtifactRoot = Path.Combine(_artifactRoot, "content-kb");
        Directory.CreateDirectory(factoryArtifactRoot);

        var localConnection = RelationalDatabaseConnection.FromSqlitePath(_harness.LocalDbPath);
        var orchestrator = ContentKbOrchestratorFactory.Create(
            localConnection,
            factoryArtifactRoot,
            distiller: new CannedLlmDistillationService(),
            lister: new ThrowingYouTubeChannelVideoLister(),
            transcriptSource: new ThrowingTranscriptSource(),
            chunker: new ThrowingFfmpegAudioChunker());

        var sourceStore = new ContentSourceStore(localConnection);
        var videoStore = new ContentVideoStore(localConnection);

        var stamp = Guid.NewGuid().ToString("N");
        var sourceId = await sourceStore.InsertSourceAsync(
            $"roundtrip-{stamp}",
            "Round Trip Test Channel",
            ContentSourceType.Youtube,
            $"https://youtube.com/channel/roundtrip-{stamp}");

        var youtubeVideoId = $"rt-{stamp}";
        var videoId = await videoStore.InsertVideoAsync(
            sourceId,
            youtubeVideoId,
            rssGuid: null,
            title: "Round Trip Smoke Video",
            videoUrl: $"https://youtu.be/{youtubeVideoId}",
            publishedUtc: DateTimeOffset.UtcNow,
            transcriptStatus: TranscriptStatus.Captions);

        await videoStore.InsertTranscriptAsync(
            videoId,
            source: "captions",
            body:
                "This is a canned transcript body for the SYNC-16 round-trip harness boot proof. " +
                "It walks through a cEDH ramp-into-payoff game plan across several sentences so the " +
                "distillation validation gate has real content to work with, without invoking any " +
                "real transcript provider.");

        var result = await orchestrator.DistillAsync(
            limit: 10,
            dryRun: false,
            isSubscriptionProvider: true,
            videoIds: [youtubeVideoId]);

        Assert.True(result.Success, $"Distill did not succeed: {result.AbortedReason}");
        Assert.Equal(1, result.VideosDistilled);

        var localStore = _harness.CreateLocalStore();
        var row = await localStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, youtubeVideoId);
        Assert.NotNull(row);
        Assert.NotNull(row!.BodySha256);

        var writtenArtifactPath = Path.Combine(_artifactRoot, row.ArtifactPath);
        Assert.True(File.Exists(writtenArtifactPath), $"Expected distilled artifact at {writtenArtifactPath}");
        var writtenBody = await File.ReadAllTextAsync(writtenArtifactPath);
        var expectedBodySha256 = ContentSiteIndexContentSignature.ComputeBodySha256(writtenBody);
        Assert.Equal(expectedBodySha256, row.BodySha256);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        if (Directory.Exists(_artifactRoot))
        {
            Directory.Delete(_artifactRoot, recursive: true);
        }
    }

    private sealed class ThrowingYouTubeChannelVideoLister : IYouTubeChannelVideoLister
    {
        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
            string channelUrl, int limit, int skip = 0, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip smoke test seeds videos directly; ListRecentAsync must not be called.");

        public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(
            IReadOnlyList<string> videoIds, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip smoke test seeds videos directly; GetByIdsAsync must not be called.");
    }

    private sealed class ThrowingTranscriptSource : ITranscriptSource
    {
        public string SourceType => ContentSourceType.Youtube;

        public Task<TranscriptFetchResult> FetchTranscriptAsync(
            string naturalKey, TimeSpan? knownDuration, string monthKey, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip smoke test seeds transcripts directly; FetchTranscriptAsync must not be called.");
    }

    private sealed class ThrowingFfmpegAudioChunker : IFfmpegAudioChunker
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip smoke test does not harvest audio; IsAvailableAsync must not be called.");

        public Task<IReadOnlyList<string>> ChunkAsync(
            string inputPath, string outputDirectory, int segmentSeconds = 300, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The round-trip smoke test does not harvest audio; ChunkAsync must not be called.");
    }
}
