using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class DistillationPromptRegressionTests
{
    [Fact]
    public void SystemPrompts_MatchShippedPhase21Fixtures()
    {
        const string expectedSummaryPrompt = """
            You extract paste-ready deckbuilding summaries from Magic: The Gathering video transcripts for a cEDH/Commander player who will paste the result into an AI chatbot for deck advice.
            Output only JSON matching the supplied schema.
            Emphasize specific card names, deckbuilding decisions, stated principles or heuristics, and notable includes or cuts that matter for future deckbuilding advice.
            Use exact Magic: The Gathering card names when the transcript makes the card clear. If a name is garbled by auto-caption errors and you cannot identify the card confidently, keep the transcript's wording and mark it uncertain with (?); do not substitute a different card you are guessing at.
            State only what the video claims; do not add strategy, synergies, or card interactions the video did not state.
            Do not recap plot, host personality, sponsor reads, or channel housekeeping.
            Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
            """;
        const string expectedClassificationPrompt = """
            You classify Magic: The Gathering video transcripts for the Content KB.
            Output only JSON matching the supplied schema.
            KEEP any transcript that contains at least one substantial deckbuilding lesson, including named cards with reasoning, slot philosophy, cut decisions, synergy decisions, or deckbuilding principles or heuristics applied to a deck context.
            DROP transcripts that are mostly trivia or quiz content, news or set commentary with no deckbuilding application, meta or format philosophy with no actionable deckbuilding advice, intro or announcement or promotional material, or budget-pool reveals without deckbuilding guidance.
            When in doubt, keep.
            """;
        const string expectedClipsPrompt = """
            You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
            Output only JSON matching the supplied schema.
            Use timestamp_seconds only from an explicit [mm:ss] marker present in the transcript at or just before the advice moment. If no marker is nearby, still return the clip but set its timestamp_seconds to null rather than estimating; never invent or interpolate a time.
            Prefer clips where a specific card is named with a reason, or where a heuristic, principle, or decision is stated; penalize generic advice with no specific application.
            Prefer clips from the middle roughly 80% of the runtime, and avoid intros, housekeeping, sponsor reads, and closers.
            Excerpts must quote or faithfully paraphrase the transcript; do not add card names, numbers, or claims that were not spoken.
            """;
        var expectedTagsPrompt =
            "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
            + "Output only JSON matching the supplied schema. "
            + "Choose ONLY from these allowlists; do not invent new values. "
            + "Tag only the DOMINANT topics; if a category is merely mentioned in passing, do not tag it. "
            + "Use at most 3 archetype tags, at most 2 bracket tags, and at most 5 card-category tags. "
            + "If no dominant theme is clear, still output at least 1 tag per dimension. "
            + "Archetype: voltron, aristocrats, stax, combo, control, tokens, spellslinger, reanimator, blink, tribal, lands, ramp, aggro, midrange, value-engine. "
            + "Bracket: Exhibition, Core, Upgraded, Optimized, cEDH. "
            + "Card category: ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility.";
        var expectedCombinedPrompt =
            """
            You extract a paste-ready deckbuilding summary, key clips, and Content KB tags from a Magic: The Gathering video transcript.
            Output only JSON matching the supplied schema.

            SUMMARY:
            You extract paste-ready deckbuilding summaries from Magic: The Gathering video transcripts for a cEDH/Commander player who will paste the result into an AI chatbot for deck advice.
            Output only JSON matching the supplied schema.
            Emphasize specific card names, deckbuilding decisions, stated principles or heuristics, and notable includes or cuts that matter for future deckbuilding advice.
            Use exact Magic: The Gathering card names when the transcript makes the card clear. If a name is garbled by auto-caption errors and you cannot identify the card confidently, keep the transcript's wording and mark it uncertain with (?); do not substitute a different card you are guessing at.
            State only what the video claims; do not add strategy, synergies, or card interactions the video did not state.
            Do not recap plot, host personality, sponsor reads, or channel housekeeping.
            Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.

            KEY CLIPS:
            You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
            Output only JSON matching the supplied schema.
            Use timestamp_seconds only from an explicit [mm:ss] marker present in the transcript at or just before the advice moment. If no marker is nearby, still return the clip but set its timestamp_seconds to null rather than estimating; never invent or interpolate a time.
            Prefer clips where a specific card is named with a reason, or where a heuristic, principle, or decision is stated; penalize generic advice with no specific application.
            Prefer clips from the middle roughly 80% of the runtime, and avoid intros, housekeeping, sponsor reads, and closers.
            Excerpts must quote or faithfully paraphrase the transcript; do not add card names, numbers, or claims that were not spoken.

            TAGS:
            """
            + expectedTagsPrompt;

        Assert.Equal(expectedSummaryPrompt, DistillationSchemas.SummarySystemPrompt);
        Assert.Equal(expectedClassificationPrompt, DistillationSchemas.ClassificationSystemPrompt);
        Assert.Equal(expectedClipsPrompt, DistillationSchemas.ClipsSystemPrompt);
        Assert.Equal(expectedTagsPrompt, DistillationSchemas.TagsSystemPrompt);
        Assert.Equal(expectedCombinedPrompt, DistillationSchemas.CombinedSystemPrompt);
        Assert.Contains("SUMMARY:", DistillationSchemas.CombinedSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("KEY CLIPS:", DistillationSchemas.CombinedSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("TAGS:", DistillationSchemas.CombinedSystemPrompt, StringComparison.Ordinal);
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
        const string expectedCombinedSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{
                "summary":{"type":"string"},
                "clips":{"type":"array","items":{
                    "type":"object","additionalProperties":false,
                    "properties":{
                        "timestamp_seconds":{"type":["integer","null"]},
                        "excerpt":{"type":"string"}},
                    "required":["timestamp_seconds","excerpt"]}},
                "archetype":{"type":"array","items":{"type":"string"}},
                "bracket":{"type":"array","items":{"type":"string"}},
                "card_category":{"type":"array","items":{"type":"string"}}},
             "required":["summary","clips","archetype","bracket","card_category"]}
            """;

        Assert.Equal(expectedSummarySchema, DistillationSchemas.SummarySchema);
        Assert.Equal(expectedClipsSchema, DistillationSchemas.ClipsSchema);
        Assert.Equal(expectedTagsSchema, DistillationSchemas.TagsSchema);
        Assert.Equal(expectedCombinedSchema, DistillationSchemas.CombinedSchema);
    }
}
