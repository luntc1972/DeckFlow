using System.IO;
using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Microsoft.Data.Sqlite;
using Serilog;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class FuseProfileRunnerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CreatorStyleProfileStore _profileStore;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _videoStore;

    public FuseProfileRunnerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"fuse-profile-runner-{Guid.NewGuid():N}.db");
        _profileStore = new CreatorStyleProfileStore(_dbPath);
        _sourceStore = new ContentSourceStore(_dbPath);
        _videoStore = new ContentVideoStore(_dbPath);
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
    public async Task RunFuseProfileAsync_PersistsFusedTargetsForKnownSlug()
    {
        const string slug = "salubrioussnail";
        var profile = new CreatorStyleProfile
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 39,
            InsufficientSample = false,
            StatedRules = Array.Empty<StatedRule>(),
            MeasuredMetrics =
            [
                CreateMeasuredMetric("category_ratio:draw", 11.1, effectiveSampleSize: 10.5),
                CreateMeasuredMetric("category_ratio:counter", 12.0, effectiveSampleSize: 9.5),
            ],
            FusedTargets = Array.Empty<FusedTarget>(),
            UpdatedUtc = DateTimeOffset.Parse("2026-07-11T12:34:56Z"),
        };
        await _profileStore.UpsertAsync(profile);
        var sourceId = await _sourceStore.InsertSourceAsync(
            slug,
            "Salubrious Snail",
            ContentSourceType.Youtube,
            "https://example.test/salubrioussnail");
        var videoId = await _videoStore.InsertVideoAsync(
            sourceId,
            "video-001",
            null,
            "Snail clip",
            "https://www.youtube.com/watch?v=video-001",
            DateTimeOffset.Parse("2026-07-05T00:00:00Z"),
            TranscriptStatus.Captions);
        await _videoStore.InsertStatedRuleAsync(
            videoId,
            CreateRule("draw", "range", value: null, valueMin: 13, valueMax: 18, condition: null, sourceClip: "Need 13 to 18 draw spells."),
            0);
        await _videoStore.InsertStatedRuleAsync(
            videoId,
            CreateRule("counter", "gte", value: 8, valueMin: null, valueMax: null, condition: "archetype:control", sourceClip: "Play at least eight counters in control."),
            1);

        var exitCode = await ContentKbCommandRunners.RunFuseProfileAsync(
            new FileInfo(_dbPath),
            slug,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var persisted = await _profileStore.GetBySlugAsync(slug);

        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.FusedTargets.Count);
        Assert.Contains(
            persisted.FusedTargets,
            target => target.Metric == "draw"
                && target.Verdict == "conflict"
                && target.Source == "measured-weighted"
                && target.Conflict is not null);
        Assert.Contains(
            persisted.FusedTargets,
            target => target.Metric == "counter"
                && target.Verdict == "insufficient-measured"
                && target.VerdictReason == "no-condition-breakdown");
        Assert.True(persisted.UpdatedUtc > profile.UpdatedUtc);
    }

    [Fact]
    public async Task RunFuseProfileAsync_UnknownSlug_ReturnsOne()
    {
        var exitCode = await ContentKbCommandRunners.RunFuseProfileAsync(
            new FileInfo(_dbPath),
            "missing-creator",
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Null(await _profileStore.GetBySlugAsync("missing-creator"));
    }

    private static MeasuredMetric CreateMeasuredMetric(string metric, double value, double effectiveSampleSize)
        => new()
        {
            Metric = metric,
            Value = value,
            NumDecks = 39,
            Distribution = new MetricDistribution
            {
                Mean = value,
                Min = value,
                Max = value,
                StdDev = 0.1,
                EffectiveSampleSize = effectiveSampleSize,
            }
        };

    private static StatedRuleCandidate CreateRule(
        string metric,
        string comparator,
        double? value,
        double? valueMin,
        double? valueMax,
        string? condition,
        string sourceClip)
        => new()
        {
            Category = "curve",
            Metric = metric,
            Value = value,
            ValueMin = valueMin,
            ValueMax = valueMax,
            Comparator = comparator,
            Condition = condition,
            ClipTimestampSeconds = 120,
            SourceClip = sourceClip,
            Confidence = 0.91,
            CardReference = null,
            CardGrounded = null,
            VideoDateUtc = DateTimeOffset.Parse("2026-07-05T00:00:00Z"),
        };
}
