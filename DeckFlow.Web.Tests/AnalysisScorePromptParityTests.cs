// Why: ADR-0001 — analysis prompt variants are intentionally decoupled; this test instantiates
// each concrete variant directly without a shared helper (mirrors the same principle in production).
// SCORE-04: the four-axis score block must appear in all three paste artifacts, the same band
// figures must be present in each, and the flag-OFF (null scoreBlockText) path must stay byte-identical.
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// 3-platform parity tests (SCORE-04): asserts the deck-score block appears in all three prompt
/// variants (ChatGpt / Claude / Gemini) when supplied, that all four axis figures survive into each
/// variant, and that the null-path output is byte-identical to the with-score output minus the
/// contiguous score block. No shared variant helper — each variant is instantiated directly.
/// </summary>
public sealed class AnalysisScorePromptParityTests
{
    // Why: ADR-0001 — no shared helper; concrete variants instantiated inline.
    private static AnalysisPromptVariantRegistry BuildRegistry() =>
        new(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });

    private static DeckAnalysisRequest BuildMinimalRequest() =>
        new()
        {
            Format = "Commander",
            TargetCommanderBracket = "cEDH",
        };

    private static string BuildDecklistText() => "1 Sol Ring";

    private static string BuildReferenceText() => "Reference text";

    private static string BuildSchemaJson() => "{}";

    private static string Build(string platformName, string? scoreBlockText)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        return registry.Build(
            platform,
            BuildMinimalRequest(),
            BuildDecklistText(),
            BuildReferenceText(),
            BuildSchemaJson(),
            commanderName: null,
            selectedQuestionIds: [],
            bannedCards: [],
            comboResult: null,
            includeCardVersions: false,
            enrichments: new AnalysisPromptEnrichments(ScoreBlockText: scoreBlockText));
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
        // remainder is byte-identical to the null path. Claude/Gemini do not emit the section.
        if (platformName == PacketByteIdentityFixtures.ChatGpt)
        {
            var validationBlock = PacketByteIdentityFixtures.ChatGptHeuristicValidationBlock;
            var validationIndex = excised.IndexOf(validationBlock, StringComparison.Ordinal);
            Assert.True(validationIndex >= 0, "Expected the heuristic validation section in the ChatGPT output.");
            excised = excised.Remove(validationIndex, validationBlock.Length);
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
