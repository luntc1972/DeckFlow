using DeckFlow.Core.Knowledge.StatedRulesExtraction;

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
/// Transcript classification result with a keep/drop verdict and reason.
/// </summary>
public sealed record ClassificationResult(string Verdict, string Reason);

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

/// <summary>
/// Stated-rule claim selection result with per-call token usage.
/// </summary>
public sealed record SelectResult(IReadOnlyList<string> Claims, TokenUsage Usage);

/// <summary>
/// Stated-rule claim disambiguation result with per-call token usage.
/// </summary>
public sealed record DisambiguateResult(IReadOnlyList<string> Claims, TokenUsage Usage);

/// <summary>
/// Stated-rule decomposition result with per-call token usage.
/// </summary>
public sealed record DecomposeResult(IReadOnlyList<StatedRuleCandidate> Rules, TokenUsage Usage);

/// <summary>
/// Stated-rule reduction result with per-call token usage.
/// </summary>
public sealed record ReduceResult(IReadOnlyList<StatedRuleCandidate> Rules, TokenUsage Usage);
