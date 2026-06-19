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

    /// <summary>System prompt for summary extraction.</summary>
    public static string SummarySystemPrompt { get; } = """
        You extract paste-ready deckbuilding summaries from Magic: The Gathering video transcripts for a cEDH/Commander player who will paste the result into an AI chatbot for deck advice.
        Output only JSON matching the supplied schema.
        Emphasize specific card names, deckbuilding decisions, stated principles or heuristics, and notable includes or cuts that matter for future deckbuilding advice.
        Do not recap plot, host personality, sponsor reads, or channel housekeeping.
        Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
        """;

    /// <summary>System prompt for transcript classification.</summary>
    public static string ClassificationSystemPrompt { get; } = """
        You classify Magic: The Gathering video transcripts for the Content KB.
        Output only JSON matching the supplied schema.
        KEEP any transcript that contains at least one substantial deckbuilding lesson, including named cards with reasoning, slot philosophy, cut decisions, synergy decisions, or deckbuilding principles or heuristics applied to a deck context.
        DROP transcripts that are mostly trivia or quiz content, news or set commentary with no deckbuilding application, meta or format philosophy with no actionable deckbuilding advice, intro or announcement or promotional material, or budget-pool reveals without deckbuilding guidance.
        When in doubt, keep.
        """;

    /// <summary>System prompt for key clip extraction.</summary>
    public static string ClipsSystemPrompt { get; } = """
        You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
        Output only JSON matching the supplied schema.
        Every clip must include a non-zero integer timestamp_seconds citing the [mm:ss] marker nearest the advice moment.
        Prefer clips where a specific card is named with a reason, or where a heuristic, principle, or decision is stated; penalize generic advice with no specific application.
        Prefer clips from the middle roughly 80% of the runtime, and avoid intros, housekeeping, sponsor reads, and closers.
        Return only clips with a defensible non-zero timestamp grounded in the transcript.
        Excerpts must be grounded only in the transcript.
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

    private static string FormatAllowlist(IReadOnlySet<string> values)
        => string.Join(", ", values);
}
