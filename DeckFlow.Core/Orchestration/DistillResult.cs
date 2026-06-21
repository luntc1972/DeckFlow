namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Null-safe distill outcome contract. Callers must explicitly construct <see cref="Success"/>,
/// collections are always initialized, and optional abort text is nullable instead of encoded by null collections.
/// </summary>
public sealed record DistillResult
{
    /// <summary>Gets whether the distill operation completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the number of sources processed.</summary>
    public int SourcesProcessed { get; init; }

    /// <summary>Gets the number of videos distilled successfully.</summary>
    public int VideosDistilled { get; init; }

    /// <summary>Gets the number of videos filtered out before distill storage.</summary>
    public int VideosFiltered { get; init; }

    /// <summary>Gets the number of videos that failed distillation.</summary>
    public int DistillFailed { get; init; }

    /// <summary>Gets the number of LLM calls made.</summary>
    public int LlmCalls { get; init; }

    /// <summary>Gets the billed USD spend for the completed LLM calls.</summary>
    public decimal LlmSpendUsd { get; init; }

    /// <summary>Gets the number of videos that would run during a dry-run projection.</summary>
    public int WouldRun { get; init; }

    /// <summary>Gets the projected USD spend for a dry-run projection.</summary>
    public decimal ProjectedSpendUsd { get; init; }

    /// <summary>Gets the failed video identifiers, always initialized to a non-null list.</summary>
    public IReadOnlyList<string> FailedVideoIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the per-video distilled outcomes (natural key + clip count) for every successfully
    /// distilled video, in source/video processing order. Filtered (keep/drop=drop), failed, and
    /// dry-run videos produce no entry. Always initialized to a non-null list.
    /// </summary>
    public IReadOnlyList<DistilledVideoResult> DistilledVideos { get; init; } = Array.Empty<DistilledVideoResult>();

    /// <summary>Gets the abort reason for metered-provider refusal or other explicit early termination.</summary>
    public string? AbortedReason { get; init; }

    /// <summary>Gets whether the result represents a dry-run instead of a mutating distill execution.</summary>
    public bool DryRun { get; init; }
}
