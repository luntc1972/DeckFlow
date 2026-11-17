// Why: ADR-0001 — analysis prompt variants are intentionally decoupled; this test instantiates
// each concrete variant directly without a shared helper (mirrors the same principle in production).
// SCORE-04: the four-axis score block must appear in all three paste artifacts, the same band
// figures must be present in each, and the flag-OFF (null scoreBlockText) path must stay byte-identical.
using System.Text.RegularExpressions;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// 3-platform parity tests (SCORE-04): asserts the deck-score block appears in all three prompt
/// variants (ChatGpt / Claude / Gemini) when supplied, that all four axis figures survive into each
/// variant, and that the null-path output is byte-identical to the with-score output minus the
/// contiguous score block and score-conditional validation. No shared variant helper — each variant is instantiated directly.
/// </summary>
public sealed class AnalysisScorePromptParityTests
{
    private static string Build(string platformName, string? scoreBlockText, string? interactionAuditText = null)
        => AnalysisPromptParityHarness.Build(platformName,
            new AnalysisPromptEnrichments(ScoreBlockText: scoreBlockText, InteractionAuditText: interactionAuditText));

    private const string FourAxisValidationPattern =
        @"(?im)^[^\r\n]*\bPower\b[^\r\n]*\bSpeed\b[^\r\n]*\bControl\b[^\r\n]*\bConsistency\b[^.\r\n]*\bdeck\b[^.\r\n]*\.";

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void ScoreValidation_NamesAllFourAxes_AllVariants(string platformName)
    {
        var result = Build(platformName, "SCOREBLOCK_PARITY_SENTINEL");

        Assert.Single(Regex.Matches(result, FourAxisValidationPattern));
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void ScoreValidation_Absent_WhenNoScoreBlock(string platformName)
    {
        var result = Build(platformName, scoreBlockText: null);

        Assert.Empty(Regex.Matches(result, FourAxisValidationPattern));
    }

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void ScoreValidation_ScoreOffWithOtherEnrichment_Line5Unchanged(string platformName)
    {
        var result = Build(platformName, scoreBlockText: null, interactionAuditText: "INTERACTION_AUDIT_SENTINEL");

        Assert.Empty(Regex.Matches(result, FourAxisValidationPattern));
        if (platformName == PacketByteIdentityFixtures.ChatGpt)
        {
            Assert.Contains("5. Validate the estimated power/speed scores.", result, StringComparison.Ordinal);
        }
    }

    // ── Score block present in all three variants when supplied ─────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Score_Block_AppearsInAllThreeVariants(string platformName)
    {
        var scoreBlockText = "DECK SCORE (coarse 0-5 bands - magnitude, not quality)\n  Power: 4/5 High";

        var result = Build(platformName, scoreBlockText);

        Assert.Contains("DECK SCORE", result, StringComparison.Ordinal);
    }

    // ── OFF-path byte identity: null output == with-score output minus the block ─

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Score_NullPath_ByteIdenticalToExcisedScorePath(string platformName)
    {
        // A unique single-line sentinel so the contiguous inserted block is unambiguous.
        const string sentinel = "SCOREBLOCK_PARITY_SENTINEL";

        var withScore = Build(platformName, sentinel);
        var nullPath = Build(platformName, scoreBlockText: null);

        // Each variant's guard appends exactly: AppendLine() then AppendLine(scoreBlockText),
        // i.e. the contiguous inserted bytes are Environment.NewLine + sentinel + Environment.NewLine.
        var insertedBlock = Environment.NewLine + sentinel + Environment.NewLine;

        // The block must appear exactly once (proves "only that block" was added).
        var firstIndex = withScore.IndexOf(insertedBlock, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, $"Expected the inserted score block in the {platformName} output.");
        var lastIndex = withScore.LastIndexOf(insertedBlock, StringComparison.Ordinal);
        Assert.Equal(firstIndex, lastIndex);

        // Excise that single contiguous block; the remainder must be byte-identical to the null path.
        var excised = withScore.Remove(firstIndex, insertedBlock.Length);

        // Why: the ChatGPT variant couples the HEURISTIC VALIDATION section to heuristic-content
        // presence, so the enriched path adds that section too; excise it before asserting the
        // remainder is byte-identical to the null path. Claude/Gemini add a score-conditional
        // sentence too, which must also be excised on purpose.
        if (platformName == PacketByteIdentityFixtures.ChatGpt)
        {
            var validationBlock = PacketByteIdentityFixtures.ChatGptHeuristicValidationBlockWithScore;
            var validationIndex = excised.IndexOf(validationBlock, StringComparison.Ordinal);
            Assert.True(validationIndex >= 0, "Expected the heuristic validation section in the ChatGPT output.");
            excised = excised.Remove(validationIndex, validationBlock.Length);
        }
        else
        {
            var validationSentence = platformName == PacketByteIdentityFixtures.Claude
                ? "Check the Power, Speed, Control, and Consistency scores against the actual deck."
                : "Review the Power, Speed, Control, and Consistency scores against the actual deck.";
            var insertedSentence = validationSentence + Environment.NewLine;
            Assert.Contains(insertedSentence, excised, StringComparison.Ordinal);
            excised = excised.Replace(insertedSentence, string.Empty, StringComparison.Ordinal);
        }

        Assert.Equal(nullPath, excised);
    }

    // ── All four axis figures survive into every variant ────────────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Score_AllFourAxisFigures_MatchAcrossAllThreeVariants(string platformName)
    {
        var scoreBlockText =
            "DECK SCORE (coarse 0-5 bands - magnitude, not quality)\n"
            + "  Power: 4/5 High\n"
            + "  Speed: 3/5 Moderate\n"
            + "  Control: 2/5 Modest\n"
            + "  Consistency: 5/5 Extreme";

        var result = Build(platformName, scoreBlockText);

        foreach (var figure in new[] { "Power: 4/5", "Speed: 3/5", "Control: 2/5", "Consistency: 5/5" })
        {
            Assert.Contains(figure, result, StringComparison.Ordinal);
        }
    }
}
