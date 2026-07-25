using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Strict JSON schemas used by the content distillation chat calls.
/// </summary>
public static class DistillationSchemas
{
    /// <summary>
    /// Strict schema for summary extraction.
    /// </summary>
    public const string SummarySchema = """
        {"type":"object","additionalProperties":false,
         "properties":{"summary":{"type":"string"}},
         "required":["summary"]}
        """;

    /// <summary>
    /// Strict schema for transcript classification.
    /// </summary>
    public const string ClassificationSchema = """
        {"type":"object","additionalProperties":false,
         "properties":{
            "verdict":{"type":"string","enum":["keep","drop"]},
            "reason":{"type":"string"}},
         "required":["verdict","reason"]}
        """;

    /// <summary>
    /// Strict schema for key clip extraction.
    /// </summary>
    public const string ClipsSchema = """
        {"type":"object","additionalProperties":false,
         "properties":{"clips":{"type":"array","items":{
            "type":"object","additionalProperties":false,
            "properties":{
                "timestamp_seconds":{"type":["integer","null"]},
                "excerpt":{"type":"string"}},
            "required":["timestamp_seconds","excerpt"]}}},
         "required":["clips"]}
        """;

    /// <summary>
    /// Strict schema for controlled-vocabulary tag inference.
    /// </summary>
    public const string TagsSchema = """
        {"type":"object","additionalProperties":false,
         "properties":{
            "archetype":{"type":"array","items":{"type":"string"}},
            "bracket":{"type":"array","items":{"type":"string"}},
            "card_category":{"type":"array","items":{"type":"string"}}},
         "required":["archetype","bracket","card_category"]}
        """;

    /// <summary>
    /// Strict schema for stated-rule claim selection.
    /// </summary>
    public const string StatedRulesSelectSchema = """
        {"type":"object","additionalProperties":false,
         "properties":{"claims":{"type":"array","items":{"type":"string"}}},
         "required":["claims"]}
        """;

    /// <summary>
    /// Strict schema for stated-rule claim disambiguation.
    /// </summary>
    public const string StatedRulesDisambiguateSchema = """
        {"type":"object","additionalProperties":false,
         "properties":{"claims":{"type":"array","items":{"type":"string"}}},
         "required":["claims"]}
        """;

    /// <summary>
    /// Strict schema for stated-rule decomposition.
    /// </summary>
    public const string StatedRulesDecomposeSchema = """
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

    /// <summary>
    /// Strict schema for stated-rule reduction.
    /// </summary>
    public const string StatedRulesReduceSchema = """
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

    /// <summary>
    /// Strict schema for combined summary, key clip, and tag extraction.
    /// </summary>
    public const string CombinedSchema = """
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

    /// <summary>System prompt for summary extraction.</summary>
    public static string SummarySystemPrompt { get; } = """
        You extract paste-ready deckbuilding summaries from Magic: The Gathering video transcripts for a cEDH/Commander player who will paste the result into an AI chatbot for deck advice.
        Output only JSON matching the supplied schema.
        Emphasize specific card names, deckbuilding decisions, stated principles or heuristics, and notable includes or cuts that matter for future deckbuilding advice.
        Use exact Magic: The Gathering card names when the transcript makes the card clear. If a name is garbled by auto-caption errors and you cannot identify the card confidently, keep the transcript's wording and mark it uncertain with (?); do not substitute a different card you are guessing at.
        State only what the video claims; do not add strategy, synergies, or card interactions the video did not state.
        Do not recap plot, host personality, sponsor reads, or channel housekeeping.
        Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
        """;

    /// <summary>System prompt for transcript classification.</summary>
    public static string ClassificationSystemPrompt { get; } = """
        You classify Magic: The Gathering video transcripts for the Content KB.
        Output only JSON matching the supplied schema.
        KEEP any transcript that contains at least one substantial Commander/cEDH lesson a player can apply, including named cards with reasoning, slot philosophy, cut decisions, synergy decisions, deckbuilding principles or heuristics, mulligan decisions, threat assessment, play-pattern or sequencing advice, politics or table strategy, game-theory or meta reasoning, or stated gameplay/philosophy principles even when no specific card names are present.
        DROP transcripts that are mostly trivia or quiz content, set or news or spoiler commentary with no practical application, pure promotional or announcement or housekeeping or intro material, or budget-pool reveals without deckbuilding or gameplay guidance.
        When in doubt, keep.
        """;

    /// <summary>System prompt for key clip extraction.</summary>
    public static string ClipsSystemPrompt { get; } = """
        You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
        Output only JSON matching the supplied schema.
        Use timestamp_seconds only from an explicit [mm:ss] marker present in the transcript at or just before the advice moment. If no marker is nearby, still return the clip but set its timestamp_seconds to null rather than estimating; never invent or interpolate a time.
        Prefer clips where a specific card is named with a reason, or where a heuristic, principle, or decision is stated; penalize generic advice with no specific application.
        Prefer clips from the middle roughly 80% of the runtime, and avoid intros, housekeeping, sponsor reads, and closers.
        Excerpts must quote or faithfully paraphrase the transcript; do not add card names, numbers, or claims that were not spoken.
        """;

    /// <summary>System prompt for controlled-vocabulary tag inference.</summary>
    public static string TagsSystemPrompt
    { get; } =
        "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
        + "Output only JSON matching the supplied schema. "
        + "Choose ONLY from these allowlists; do not invent new values. "
        + "Tag only the DOMINANT topics; if a category is merely mentioned in passing, do not tag it. "
        + "Use at most 3 archetype tags, at most 2 bracket tags, and at most 5 card-category tags. "
        + "If no dominant theme is clear, still output at least 1 tag per dimension. "
        + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
        + $"Bracket: {FormatAllowlist(ContentTagVocabulary.Brackets)}. "
        + $"Card category: {FormatAllowlist(ContentTagVocabulary.CardCategories)}.";

    /// <summary>System prompt for stated-rule claim selection.</summary>
    public static string StatedRulesSelectSystemPrompt { get; } = """
        You select transcript sentences that state concrete Magic: The Gathering deckbuilding rules or heuristics.
        Output only JSON matching the supplied schema.
        Keep only claims that assert a measurable deckbuilding target, threshold, comparison, include or cut rule, or conditional heuristic grounded in the transcript.
        Drop opinions, jokes, questions, table-talk, sponsor reads, housekeeping, and instructions that do not state a deckbuilding rule.
        Preserve the transcript's wording closely enough that later stages can trace the claim back to the source; do not add card names, numbers, or conditions that were not stated.
        If a sentence is partly useful, keep only the rule-bearing portion rather than surrounding filler.
        """;

    /// <summary>System prompt for stated-rule claim disambiguation.</summary>
    public static string StatedRulesDisambiguateSystemPrompt { get; } = """
        You rewrite selected Magic: The Gathering deckbuilding claims so each one is explicit, self-contained, and ready for rule decomposition.
        Output only JSON matching the supplied schema.
        Resolve pronouns, shorthand, and vague references using nearby transcript context when the reference is clear.
        Keep the claim faithful to the transcript; do not invent card names, numbers, archetypes, or conditions that were not stated.
        If a claim remains irreducibly ambiguous after using local context, omit it instead of guessing.
        Return only claims that can still be traced to a specific statement in the transcript.
        """;

    /// <summary>System prompt for stated-rule decomposition.</summary>
    public static string StatedRulesDecomposeSystemPrompt
    { get; } =
        "You decompose Magic: The Gathering deckbuilding claims into atomic measurable stated rules. "
        + "Output only JSON matching the supplied schema. "
        + "Use only the transcript as evidence; do not add card names, thresholds, or conditions that were not stated. "
        + "Emit one rule per distinct measurable claim. "
        + "Choose metric ONLY from this allowlist: "
        + $"{FormatAllowlist(StatedRulesMetricVocabulary.Metrics)}. "
        + "Choose comparator ONLY from this allowlist: "
        + $"{FormatAllowlist(StatedRulesMetricVocabulary.Comparators)}. "
        + "Use value for gte, lte, or eq rules, and set value_min plus value_max for range rules. "
        + "Leave condition null unless the transcript states a real qualifier such as an archetype, curve, color, bracket, or matchup constraint. "
        + "Use clip_timestamp_seconds only when the transcript chunk includes an explicit nearby timestamp marker; otherwise return null. "
        + "Set source_clip to a faithful quote or tight paraphrase of the supporting transcript span. "
        + "Set confidence on a 0 to 1 scale. "
        + "When a rule names a specific card, populate card_reference with that card name; otherwise omit card_reference or set it to null. "
        + "If a claim cannot be expressed as an atomic measurable rule with these fields, omit it.";

    /// <summary>System prompt for stated-rule reduction.</summary>
    // Why: the reduce pass is a DeckFlow addition for cross-chunk dedupe, not part of Claimify.
    public static string StatedRulesReduceSystemPrompt
    { get; } =
        "You merge near-duplicate Magic: The Gathering stated rules collected from multiple transcript chunks. "
        + "Output only JSON matching the supplied schema. "
        + "Use only the provided rule candidates; do not invent new evidence, card names, numbers, or conditions. "
        + "Choose metric ONLY from this allowlist: "
        + $"{FormatAllowlist(StatedRulesMetricVocabulary.Metrics)}. "
        + "Choose comparator ONLY from this allowlist: "
        + $"{FormatAllowlist(StatedRulesMetricVocabulary.Comparators)}. "
        + "Merge candidates only when they express the same underlying rule; otherwise keep them separate. "
        + "Prefer the clearest phrasing, preserve a stated condition when one materially narrows the rule, and keep card_reference only when the surviving rule still names a specific card. "
        + "Use value for gte, lte, or eq rules, and value_min plus value_max for range rules. "
        + "Carry forward clip_timestamp_seconds only from an explicit timestamp already present in the candidates; never invent one. "
        + "Set source_clip to the best supporting quote or tight paraphrase among the provided candidates, and set confidence on a 0 to 1 scale. "
        + "Return only the reduced rules.";

    /// <summary>System prompt for combined summary, clip, and tag extraction.</summary>
    public static string CombinedSystemPrompt
    { get; } =
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
        + "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
        + "Output only JSON matching the supplied schema. "
        + "Choose ONLY from these allowlists; do not invent new values. "
        + "Tag only the DOMINANT topics; if a category is merely mentioned in passing, do not tag it. "
        + "Use at most 3 archetype tags, at most 2 bracket tags, and at most 5 card-category tags. "
        + "If no dominant theme is clear, still output at least 1 tag per dimension. "
        + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
        + $"Bracket: {FormatAllowlist(ContentTagVocabulary.Brackets)}. "
        + $"Card category: {FormatAllowlist(ContentTagVocabulary.CardCategories)}.";

    private static string FormatAllowlist(IReadOnlySet<string> values)
        => string.Join(", ", values);
}
