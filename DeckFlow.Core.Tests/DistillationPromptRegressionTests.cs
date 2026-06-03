using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class DistillationPromptRegressionTests
{
    [Fact]
    public void SystemPrompts_MatchShippedPhase21Fixtures()
    {
        const string expectedSummaryPrompt = """
            You extract grounded strategy summaries from Magic: The Gathering video transcripts.
            Output only JSON matching the supplied schema.
            Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
            """;
        const string expectedClipsPrompt = """
            You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
            Output only JSON matching the supplied schema.
            Use timestamp_seconds only when the transcript provides a defensible time; otherwise use null.
            Excerpts must be grounded only in the transcript.
            """;
        var expectedTagsPrompt =
            "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
            + "Output only JSON matching the supplied schema. "
            + "Choose only from these allowlists. "
            + "Archetype: voltron, aristocrats, stax, combo, control, tokens, spellslinger, reanimator, blink, tribal, lands, ramp, aggro, midrange, value-engine. "
            + "Bracket: Exhibition, Core, Upgraded, Optimized, cEDH. "
            + "Card category: ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility.";

        Assert.Equal(expectedSummaryPrompt, DistillationSchemas.SummarySystemPrompt);
        Assert.Equal(expectedClipsPrompt, DistillationSchemas.ClipsSystemPrompt);
        Assert.Equal(expectedTagsPrompt, DistillationSchemas.TagsSystemPrompt);
    }

    [Fact]
    public void ResponseFormatSchemas_MatchShippedPhase21Fixtures()
    {
        const string expectedSummarySchema = """
            {"type":"object","additionalProperties":false,
             "properties":{"summary":{"type":"string"}},
             "required":["summary"]}
            """;
        const string expectedClipsSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{"clips":{"type":"array","items":{
                "type":"object","additionalProperties":false,
                "properties":{
                    "timestamp_seconds":{"type":["integer","null"]},
                    "excerpt":{"type":"string"}},
                "required":["timestamp_seconds","excerpt"]}}},
             "required":["clips"]}
            """;
        const string expectedTagsSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{
                "archetype":{"type":"array","items":{"type":"string"}},
                "bracket":{"type":"array","items":{"type":"string"}},
                "card_category":{"type":"array","items":{"type":"string"}}},
             "required":["archetype","bracket","card_category"]}
            """;

        Assert.Equal(expectedSummarySchema, DistillationSchemas.SummarySchema);
        Assert.Equal(expectedClipsSchema, DistillationSchemas.ClipsSchema);
        Assert.Equal(expectedTagsSchema, DistillationSchemas.TagsSchema);
    }
}
