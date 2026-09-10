using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge.ProfileFusion;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Round-trip fidelity, null-condition mapping, re-import idempotency, and condition-scoping
/// coverage for <see cref="CreatorStyleStatedRuleStore"/> against a temporary SQLite database.
/// </summary>
public sealed class CreatorStyleStatedRuleStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CreatorStyleStatedRuleStore _store;

    public CreatorStyleStatedRuleStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"creator-style-stated-rule-store-{Guid.NewGuid():N}.db");
        _store = new CreatorStyleStatedRuleStore(_dbPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task UpsertAsync_ThenGetBySlug_RoundTripsEveryFieldByteEqual()
    {
        const string slug = "field-fidelity";
        var expected = new StatedRuleCandidate
        {
            Category = "removal",
            Metric = "removal",
            Value = 8,
            ValueMin = 6,
            ValueMax = 10,
            Comparator = "range",
            Condition = "archetype:control",
            ClipTimestampSeconds = 128,
            SourceClip = "Keep interaction dense against combo pods.",
            Confidence = 0.9,
            CardReference = "Swords to Plowshares",
            CardGrounded = true,
            VideoDateUtc = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Provenance = "hand-authored",
        };

        await _store.UpsertAsync(expected, slug);
        var actual = Assert.Single(await _store.GetBySlugAsync(slug));

        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Metric, actual.Metric);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.ValueMin, actual.ValueMin);
        Assert.Equal(expected.ValueMax, actual.ValueMax);
        Assert.Equal(expected.Comparator, actual.Comparator);
        Assert.Equal(expected.Condition, actual.Condition);
        Assert.Equal(expected.ClipTimestampSeconds, actual.ClipTimestampSeconds);
        Assert.Equal(expected.SourceClip, actual.SourceClip);
        Assert.Equal(expected.Confidence, actual.Confidence, 6);
        Assert.Equal(expected.CardReference, actual.CardReference);
        Assert.Equal(expected.CardGrounded, actual.CardGrounded);
        Assert.Equal(expected.VideoDateUtc, actual.VideoDateUtc);
        Assert.Equal(expected.Provenance, actual.Provenance);
    }

    [Fact]
    public async Task UpsertAsync_NullCondition_RoundTripsAsNullNotEmptyString()
    {
        const string slug = "null-condition";
        var rule = CreateRule(metric: "ramp", condition: null);

        await _store.UpsertAsync(rule, slug);
        var actual = Assert.Single(await _store.GetBySlugAsync(slug));

        Assert.Null(actual.Condition);
    }

    [Fact]
    public async Task UpsertAsync_ReimportSameUnconditionedRule_StaysOneRowAndUpdatesInPlace()
    {
        const string slug = "reimport-idempotency";
        var rule = CreateRule(metric: "draw", condition: null, sourceClip: "First cut of the source clip.");

        await _store.UpsertAsync(rule, slug);
        await _store.UpsertAsync(rule, slug);
        var afterTwoImports = await _store.GetBySlugAsync(slug);
        Assert.Single(afterTwoImports);

        var updated = rule with { SourceClip = "Revised source clip after a re-distill." };
        await _store.UpsertAsync(updated, slug);
        var afterThirdImport = Assert.Single(await _store.GetBySlugAsync(slug));

        Assert.Equal(updated.SourceClip, afterThirdImport.SourceClip);
    }

    [Fact]
    public async Task UpsertAsync_TwoConditionsSameMetric_SurviveAsTwoRowsAndTwoFusedTargets()
    {
        const string slug = "condition-scoping";
        var controlRule = CreateRule(metric: "counter", condition: "archetype:control", value: 8, comparator: "gte");
        var stormRule = CreateRule(metric: "counter", condition: "archetype:storm", value: 3, comparator: "gte");

        await _store.UpsertAsync(controlRule, slug);
        await _store.UpsertAsync(stormRule, slug);
        var storedRules = await _store.GetBySlugAsync(slug);

        Assert.Equal(2, storedRules.Count);

        var fusedTargets = ProfileFusionEngine.Fuse([], storedRules);

        Assert.Equal(2, fusedTargets.Count);
    }

    private static StatedRuleCandidate CreateRule(
        string metric,
        string? condition,
        string comparator = "range",
        double? value = null,
        double valueMin = 3,
        double valueMax = 5,
        string sourceClip = "Fixture source clip.",
        string videoDateUtc = "2026-07-05T00:00:00Z")
    {
        return new StatedRuleCandidate
        {
            Category = "deckbuilding",
            Metric = metric,
            Value = value,
            ValueMin = comparator == "gte" ? null : valueMin,
            ValueMax = comparator == "gte" ? null : valueMax,
            Comparator = comparator,
            Condition = condition,
            SourceClip = sourceClip,
            Confidence = 0.8,
            VideoDateUtc = DateTimeOffset.Parse(videoDateUtc),
        };
    }
}
