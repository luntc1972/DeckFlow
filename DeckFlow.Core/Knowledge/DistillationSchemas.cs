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
        You extract grounded strategy summaries from Magic: The Gathering video transcripts.
        Output only JSON matching the supplied schema.
        Keep the summary 200 words or fewer, plain prose, and grounded only in the transcript.
        """;

    /// <summary>System prompt for key clip extraction.</summary>
    public static string ClipsSystemPrompt { get; } = """
        You extract 3 to 8 useful key clips from Magic: The Gathering video transcripts.
        Output only JSON matching the supplied schema.
        Use timestamp_seconds only when the transcript provides a defensible time; otherwise use null.
        Excerpts must be grounded only in the transcript.
        """;

    /// <summary>System prompt for controlled-vocabulary tag inference.</summary>
    public static string TagsSystemPrompt
    { get; } =
        "You infer candidate Content KB tags from Magic: The Gathering video transcripts. "
        + "Output only JSON matching the supplied schema. "
        + "Choose only from these allowlists. "
        + $"Archetype: {FormatAllowlist(ContentTagVocabulary.Archetypes)}. "
        + $"Bracket: {FormatAllowlist(ContentTagVocabulary.Brackets)}. "
        + $"Card category: {FormatAllowlist(ContentTagVocabulary.CardCategories)}.";

    private static string FormatAllowlist(IReadOnlySet<string> values)
        => string.Join(", ", values);
}
