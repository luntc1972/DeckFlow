using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers the multi-axis score round-trip across the Step-3 early-return path, where there is no live
/// Scryfall data to recompute from: the score is restored from the round-tripped <c>ScoreJson</c> hidden
/// field. <c>ScoreJson</c> is untrusted client input — malformed or oversized payloads must yield a null
/// score, never an exception (threat T-77-04-01). Reuses the <c>CreateService</c> harness via partial.
/// </summary>
public sealed partial class DeckAnalysisPacketServiceTests
{
    private const string SavedDeckProfileJson = """
{
  "deck_profile": {
    "format": "Commander",
    "commander": "Atraxa, Praetors' Voice",
    "game_plan": "Midrange value",
    "primary_axes": ["counters", "value"],
    "speed": "medium",
    "strengths": ["Resilient board presence"],
    "weaknesses": ["Mana base is slow"],
    "deck_needs": [],
    "weak_slots": [],
    "synergy_tags": ["proliferate"],
    "question_answers": [],
    "deck_versions": []
  }
}
""";

    private const string MultiAxisScoreFlagKey = "analysis.multi-axis-score";

    private static FakeFeatureFlagCache ScoreFlag(bool enabled) =>
        new(new Dictionary<string, bool> { [MultiAxisScoreFlagKey] = enabled });

    private static DeckMultiAxisScore SampleScore() =>
        new(
            PowerBand: 4,
            SpeedBand: 3,
            ControlBand: 2,
            ConsistencyBand: 5,
            PowerRationale: new DeckScoreRationale("4 Game Changers, 2 two-card combos, 9 fast-mana sources"),
            SpeedRationale: new DeckScoreRationale("avg MV 2.6, 9 fast-mana, 7 ramp/draw under 3 MV"),
            ControlRationale: new DeckScoreRationale("11 interaction pieces, 4 board wipes, 3 counters"),
            ConsistencyRationale: new DeckScoreRationale("8 tutors, 2 redundant combo lines, smooth 2.6 curve"),
            BracketNumber: 4,
            BracketCrossCheckText: "score aligns with the Bracket 4 classification.",
            ScoreAlignsBracket: true);

    [Fact]
    public async Task BuildAsync_Step3EarlyReturn_RestoresScore_FromValidScoreJson()
    {
        var service = CreateService(
            flagCache: ScoreFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));
        var score = SampleScore();

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            ScoreJson = JsonSerializer.Serialize(score)
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Equal(score, result.Score);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    [InlineData("[1, 2, 3]")]
    public async Task BuildAsync_Step3EarlyReturn_YieldsNullScore_ForMalformedScoreJson(string scoreJson)
    {
        var service = CreateService(
            flagCache: ScoreFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            ScoreJson = scoreJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.Score);
    }

    [Fact]
    public async Task BuildAsync_Step3EarlyReturn_YieldsNullScore_ForOversizedScoreJson()
    {
        var service = CreateService(
            flagCache: ScoreFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        // A valid-but-oversized payload (> the 8192-char cap): the length guard rejects it before
        // deserialization runs, so the result Score is null rather than an enormous restored object.
        var oversized = SampleScore() with
        {
            PowerRationale = new DeckScoreRationale(new string('x', 9000))
        };

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            ScoreJson = JsonSerializer.Serialize(oversized)
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.Score);
    }

    [Fact]
    public async Task BuildAsync_Step3EarlyReturn_YieldsNullScore_WhenFlagOff_EvenWithValidScoreJson()
    {
        // A crafted POST can carry a valid ScoreJson while the flag is OFF; the restore must stay gated
        // so the score UI never surfaces and the OFF path stays byte-identical (threat T-77-04-01).
        var service = CreateService(
            flagCache: ScoreFlag(false),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            ScoreJson = JsonSerializer.Serialize(SampleScore())
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.Score);
    }

    [Theory]
    // Well-formed JSON that omits a nested rationale or carries an out-of-range band: the view
    // dereferences each rationale's SignalText, so an unchecked null would crash the request.
    [InlineData("""{"PowerBand":4,"SpeedBand":3,"ControlBand":2,"ConsistencyBand":5,"PowerRationale":null,"SpeedRationale":{"SignalText":"s"},"ControlRationale":{"SignalText":"s"},"ConsistencyRationale":{"SignalText":"s"},"BracketNumber":4,"BracketCrossCheckText":"ok","ScoreAlignsBracket":true}""")]
    [InlineData("""{"PowerBand":4,"SpeedBand":3,"ControlBand":2,"ConsistencyBand":5,"PowerRationale":{"SignalText":null},"SpeedRationale":{"SignalText":"s"},"ControlRationale":{"SignalText":"s"},"ConsistencyRationale":{"SignalText":"s"},"BracketNumber":4,"BracketCrossCheckText":"ok","ScoreAlignsBracket":true}""")]
    [InlineData("""{"PowerBand":99,"SpeedBand":3,"ControlBand":2,"ConsistencyBand":5,"PowerRationale":{"SignalText":"s"},"SpeedRationale":{"SignalText":"s"},"ControlRationale":{"SignalText":"s"},"ConsistencyRationale":{"SignalText":"s"},"BracketNumber":4,"BracketCrossCheckText":"ok","ScoreAlignsBracket":true}""")]
    [InlineData("""{"PowerBand":4,"SpeedBand":3,"ControlBand":2,"ConsistencyBand":5,"PowerRationale":{"SignalText":"s"},"SpeedRationale":{"SignalText":"s"},"ControlRationale":{"SignalText":"s"},"ConsistencyRationale":{"SignalText":"s"},"BracketNumber":4,"BracketCrossCheckText":null,"ScoreAlignsBracket":true}""")]
    public async Task BuildAsync_Step3EarlyReturn_YieldsNullScore_ForStructurallyInvalidScoreJson(string scoreJson)
    {
        var service = CreateService(
            flagCache: ScoreFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            ScoreJson = scoreJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.Score);
    }
}
