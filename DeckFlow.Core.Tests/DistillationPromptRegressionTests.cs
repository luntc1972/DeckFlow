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

        Assert.Equal(expectedSummaryPrompt, DistillationSchemas.SummarySystemPrompt);
        Assert.Equal(expectedClassificationPrompt, DistillationSchemas.ClassificationSystemPrompt);
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

    [Fact]
    public void StatedRulesPrompts_MatchShippedFixtures()
    {
        const string expectedSelectSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{"claims":{"type":"array","items":{"type":"string"}}},
             "required":["claims"]}
            """;
        const string expectedDisambiguateSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{"claims":{"type":"array","items":{"type":"string"}}},
             "required":["claims"]}
            """;
        const string expectedDecomposeSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{"rules":{"type":"array","items":{
                "type":"object","additionalProperties":false,
                "properties":{
                    "category":{"type":"string"},
                    "metric":{"type":"string"},
                    "value":{"type":["number","null"]},
                    "value_min":{"type":["number","null"]},
                    "value_max":{"type":["number","null"]},
                    "comparator":{"type":"string"},
                    "condition":{"type":["string","null"]},
                    "clip_timestamp_seconds":{"type":["integer","null"]},
                    "source_clip":{"type":"string"},
                    "confidence":{"type":"number"},
                    "card_reference":{"type":["string","null"]}},
                "required":["category","metric","value","value_min","value_max","comparator","condition","clip_timestamp_seconds","source_clip","confidence"]}}},
             "required":["rules"]}
            """;
        const string expectedReduceSchema = """
            {"type":"object","additionalProperties":false,
             "properties":{"rules":{"type":"array","items":{
                "type":"object","additionalProperties":false,
                "properties":{
                    "category":{"type":"string"},
                    "metric":{"type":"string"},
                    "value":{"type":["number","null"]},
                    "value_min":{"type":["number","null"]},
                    "value_max":{"type":["number","null"]},
                    "comparator":{"type":"string"},
                    "condition":{"type":["string","null"]},
                    "clip_timestamp_seconds":{"type":["integer","null"]},
                    "source_clip":{"type":"string"},
                    "confidence":{"type":"number"},
                    "card_reference":{"type":["string","null"]}},
                "required":["category","metric","value","value_min","value_max","comparator","condition","clip_timestamp_seconds","source_clip","confidence"]}}},
             "required":["rules"]}
            """;
        const string expectedSelectPrompt = """
            You select transcript sentences that state concrete Magic: The Gathering deckbuilding rules or heuristics.
            Output only JSON matching the supplied schema.
            Keep only claims that assert a measurable deckbuilding target, threshold, comparison, include or cut rule, or conditional heuristic grounded in the transcript.
            Drop opinions, jokes, questions, table-talk, sponsor reads, housekeeping, and instructions that do not state a deckbuilding rule.
            Preserve the transcript's wording closely enough that later stages can trace the claim back to the source; do not add card names, numbers, or conditions that were not stated.
            If a sentence is partly useful, keep only the rule-bearing portion rather than surrounding filler.
            """;
        const string expectedDisambiguatePrompt = """
            You rewrite selected Magic: The Gathering deckbuilding claims so each one is explicit, self-contained, and ready for rule decomposition.
            Output only JSON matching the supplied schema.
            Resolve pronouns, shorthand, and vague references using nearby transcript context when the reference is clear.
            Keep the claim faithful to the transcript; do not invent card names, numbers, archetypes, or conditions that were not stated.
            If a claim remains irreducibly ambiguous after using local context, omit it instead of guessing.
            Return only claims that can still be traced to a specific statement in the transcript.
            """;
        var expectedDecomposePrompt =
            "You decompose Magic: The Gathering deckbuilding claims into atomic measurable stated rules. "
            + "Output only JSON matching the supplied schema. "
            + "Use only the transcript as evidence; do not add card names, thresholds, or conditions that were not stated. "
            + "Emit one rule per distinct measurable claim. "
            + "Choose metric ONLY from this allowlist: "
            + "ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility, karsten:target_lands, karsten:land_delta, karsten:health_score, combo_density:included_per_deck, land_count, interaction, opener_probability, pip_distribution, power_level_philosophy. "
            + "Choose comparator ONLY from this allowlist: "
            + "gte, lte, eq, range. "
            + "Use value for gte, lte, or eq rules, and set value_min plus value_max for range rules. "
            + "Leave condition null unless the transcript states a real qualifier such as an archetype, curve, color, bracket, or matchup constraint. "
            + "Use clip_timestamp_seconds only when the transcript chunk includes an explicit nearby timestamp marker; otherwise return null. "
            + "Set source_clip to a faithful quote or tight paraphrase of the supporting transcript span. "
            + "Set confidence on a 0 to 1 scale. "
            + "When a rule names a specific card, populate card_reference with that card name; otherwise omit card_reference or set it to null. "
            + "If a claim cannot be expressed as an atomic measurable rule with these fields, omit it.";
        var expectedReducePrompt =
            "You merge near-duplicate Magic: The Gathering stated rules collected from multiple transcript chunks. "
            + "Output only JSON matching the supplied schema. "
            + "Use only the provided rule candidates; do not invent new evidence, card names, numbers, or conditions. "
            + "Choose metric ONLY from this allowlist: "
            + "ramp, removal, draw, finishers, win-cons, counter, protection, board-wipe, tutor, recursion, utility, karsten:target_lands, karsten:land_delta, karsten:health_score, combo_density:included_per_deck, land_count, interaction, opener_probability, pip_distribution, power_level_philosophy. "
            + "Choose comparator ONLY from this allowlist: "
            + "gte, lte, eq, range. "
            + "Merge candidates only when they express the same underlying rule; otherwise keep them separate. "
            + "Prefer the clearest phrasing, preserve a stated condition when one materially narrows the rule, and keep card_reference only when the surviving rule still names a specific card. "
            + "Use value for gte, lte, or eq rules, and value_min plus value_max for range rules. "
            + "Carry forward clip_timestamp_seconds only from an explicit timestamp already present in the candidates; never invent one. "
            + "Set source_clip to the best supporting quote or tight paraphrase among the provided candidates, and set confidence on a 0 to 1 scale. "
            + "Return only the reduced rules.";

        Assert.Equal(expectedSelectSchema, DistillationSchemas.StatedRulesSelectSchema);
        Assert.Equal(expectedDisambiguateSchema, DistillationSchemas.StatedRulesDisambiguateSchema);
        Assert.Equal(expectedDecomposeSchema, DistillationSchemas.StatedRulesDecomposeSchema);
        Assert.Equal(expectedReduceSchema, DistillationSchemas.StatedRulesReduceSchema);
        Assert.Equal(expectedSelectPrompt, DistillationSchemas.StatedRulesSelectSystemPrompt);
        Assert.Equal(expectedDisambiguatePrompt, DistillationSchemas.StatedRulesDisambiguateSystemPrompt);
        Assert.Equal(expectedDecomposePrompt, DistillationSchemas.StatedRulesDecomposeSystemPrompt);
        Assert.Equal(expectedReducePrompt, DistillationSchemas.StatedRulesReduceSystemPrompt);
    }
}
