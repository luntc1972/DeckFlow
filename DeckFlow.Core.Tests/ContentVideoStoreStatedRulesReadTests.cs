using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Storage;
using DeckFlow.Core.Tests.Integration;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for reading persisted stated rules from <see cref="ContentVideoStore"/>.
/// </summary>
public sealed class ContentVideoStoreStatedRulesReadTests : IDisposable, IClassFixture<PostgresContainerFixture>
{
    private readonly string _dbPath;
    private readonly PostgresContainerFixture _fixture;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _videoStore;

    public ContentVideoStoreStatedRulesReadTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-video-stated-rules-read-{Guid.NewGuid():N}.db");
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
    public async Task GetStatedRulesBySourceSlug_ReturnsAllRulesForCreator_WithFieldsIntact()
    {
        var sourceId = await InsertSourceAsync("salubrioussnail");
        var videoId = await InsertVideoAsync(_videoStore, sourceId, "salubrioussnail-video");
        var expected = new[]
        {
            CreateRangeRule("mana_curve", "midrange mirrors", "curve clip", 0.82, DateTimeOffset.Parse("2026-05-26T12:00:00Z")),
            CreateSingleValueRule("lands", "gte", 37, "control shells", "lands clip", 0.91, DateTimeOffset.Parse("2026-05-27T12:00:00Z")),
            CreateSingleValueRule("ramp_piece_count", "lte", 11, null, "ramp clip", 0.74, DateTimeOffset.Parse("2026-05-28T12:00:00Z")),
        };

        for (var i = 0; i < expected.Length; i++)
        {
            await _videoStore.InsertStatedRuleAsync(videoId, expected[i], i);
        }

        var actual = await _videoStore.GetStatedRulesBySourceSlugAsync("salubrioussnail");

        Assert.Equal(expected.Length, actual.Count);
        Assert.Collection(
            actual,
            rule => AssertRule(expected[0], rule),
            rule => AssertRule(expected[1], rule),
            rule => AssertRule(expected[2], rule));
    }

    [Fact]
    public async Task GetStatedRulesBySourceSlug_UnknownSlug_ReturnsEmpty()
    {
        var actual = await _videoStore.GetStatedRulesBySourceSlugAsync("missing-creator");

        Assert.NotNull(actual);
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetStatedRulesBySourceSlug_OtherCreatorRules_NotReturned()
    {
        var includedSourceId = await InsertSourceAsync("salubrioussnail");
        var excludedSourceId = await InsertSourceAsync("othercreator");
        var includedVideoId = await InsertVideoAsync(_videoStore, includedSourceId, "included-video");
        var excludedVideoId = await InsertVideoAsync(_videoStore, excludedSourceId, "excluded-video");
        var includedRule = CreateSingleValueRule("lands", "gte", 37, "control shells", "included clip", 0.91, DateTimeOffset.Parse("2026-05-27T12:00:00Z"));
        var excludedRule = CreateSingleValueRule("interaction", "lte", 8, "stax", "excluded clip", 0.67, DateTimeOffset.Parse("2026-05-28T12:00:00Z"));

        await _videoStore.InsertStatedRuleAsync(includedVideoId, includedRule, 0);
        await _videoStore.InsertStatedRuleAsync(excludedVideoId, excludedRule, 0);

        var actual = await _videoStore.GetStatedRulesBySourceSlugAsync("salubrioussnail");

        var rule = Assert.Single(actual);
        AssertRule(includedRule, rule);
    }

    [PostgresFact]
    public async Task GetStatedRulesBySourceSlug_ReturnsAllRulesForCreator_WithFieldsIntact_OnPostgres()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var descriptor = new RelationalDatabaseConnection(
            RelationalDatabaseProvider.Postgres,
            connectionString);
        var sourceStore = new ContentSourceStore(descriptor);
        var videoStore = new ContentVideoStore(descriptor);
        var sourceSlug = $"salubrioussnail-{Guid.NewGuid():N}";
        var sourceId = await InsertSourceAsync(sourceStore, sourceSlug);
        var videoId = await InsertVideoAsync(videoStore, sourceId, $"postgres-video-{Guid.NewGuid():N}");
        var expected = new[]
        {
            CreateRangeRule("mana_curve", "midrange mirrors", "curve clip", 0.82, DateTimeOffset.Parse("2026-05-26T12:00:00Z")),
            CreateSingleValueRule("lands", "gte", 37, "control shells", "lands clip", 0.91, DateTimeOffset.Parse("2026-05-27T12:00:00Z")),
        };

        for (var i = 0; i < expected.Length; i++)
        {
            await videoStore.InsertStatedRuleAsync(videoId, expected[i], i);
        }

        var actual = await videoStore.GetStatedRulesBySourceSlugAsync(sourceSlug);

        Assert.Equal(expected.Length, actual.Count);
        Assert.Collection(
            actual,
            rule => AssertRule(expected[0], rule),
            rule => AssertRule(expected[1], rule));
    }

    private async Task<long> InsertSourceAsync(string slug)
        => await InsertSourceAsync(_sourceStore, slug);

    private static async Task<long> InsertSourceAsync(ContentSourceStore sourceStore, string slug)
        => await sourceStore.InsertSourceAsync(
            slug,
            $"Source {slug}",
            ContentSourceType.Youtube,
            $"https://example.test/{slug}");

    private static async Task<long> InsertVideoAsync(ContentVideoStore videoStore, long sourceId, string youtubeVideoId)
        => await videoStore.InsertVideoAsync(
            sourceId,
            youtubeVideoId,
            null,
            $"Video {youtubeVideoId}",
            $"https://www.youtube.com/watch?v={youtubeVideoId}",
            DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            TranscriptStatus.Captions);

    private static StatedRuleCandidate CreateRangeRule(
        string metric,
        string? condition,
        string sourceClip,
        double confidence,
        DateTimeOffset videoDateUtc)
        => new()
        {
            Category = "ramp",
            Metric = metric,
            Value = null,
            ValueMin = 9,
            ValueMax = 12,
            Comparator = "range",
            Condition = condition,
            ClipTimestampSeconds = 134,
            SourceClip = sourceClip,
            Confidence = confidence,
            CardReference = "Cultivate",
            CardGrounded = true,
            VideoDateUtc = videoDateUtc,
        };

    private static StatedRuleCandidate CreateSingleValueRule(
        string metric,
        string comparator,
        double value,
        string? condition,
        string sourceClip,
        double confidence,
        DateTimeOffset videoDateUtc)
        => new()
        {
            Category = "ramp",
            Metric = metric,
            Value = value,
            ValueMin = null,
            ValueMax = null,
            Comparator = comparator,
            Condition = condition,
            ClipTimestampSeconds = 215,
            SourceClip = sourceClip,
            Confidence = confidence,
            CardReference = null,
            CardGrounded = null,
            VideoDateUtc = videoDateUtc,
        };

    private static void AssertRule(StatedRuleCandidate expected, StatedRuleCandidate actual)
    {
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Metric, actual.Metric);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.ValueMin, actual.ValueMin);
        Assert.Equal(expected.ValueMax, actual.ValueMax);
        Assert.Equal(expected.Comparator, actual.Comparator);
        Assert.Equal(expected.Condition, actual.Condition);
        Assert.Equal(expected.ClipTimestampSeconds, actual.ClipTimestampSeconds);
        Assert.Equal(expected.SourceClip, actual.SourceClip);
        Assert.Equal(expected.Confidence, actual.Confidence);
        Assert.Equal(expected.CardReference, actual.CardReference);
        Assert.Equal(expected.CardGrounded, actual.CardGrounded);
        Assert.Equal(expected.VideoDateUtc, actual.VideoDateUtc);
    }
}
