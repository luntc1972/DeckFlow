namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Token usage reported by the OpenAI chat completion for one distillation call.
/// </summary>
public sealed record TokenUsage(int InputTokens, int OutputTokens);

/// <summary>
/// Summary extraction result with per-call token usage.
/// </summary>
public sealed record SummaryResult(string Summary, TokenUsage Usage);

/// <summary>
/// Key clip extracted from a transcript.
/// </summary>
public sealed record ClipItem(int? TimestampSeconds, string Excerpt);

/// <summary>
/// Clip extraction result with per-call token usage.
/// </summary>
public sealed record ClipsResult(IReadOnlyList<ClipItem> Clips, TokenUsage Usage);

/// <summary>
/// Tag inference result with per-call token usage.
/// </summary>
public sealed record TagsResult(
    IReadOnlyList<string> Archetype,
    IReadOnlyList<string> Bracket,
    IReadOnlyList<string> CardCategory,
    TokenUsage Usage);
