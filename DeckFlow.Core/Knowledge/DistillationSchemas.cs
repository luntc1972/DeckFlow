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
