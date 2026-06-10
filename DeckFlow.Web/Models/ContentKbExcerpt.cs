namespace DeckFlow.Web.Models;

/// <summary>
/// Serializable expert-context clip excerpt persisted in the analysis packet zip and shown in the UI.
/// </summary>
public sealed record ContentKbExcerpt
{
    // Why: System.Text.Json skips get-only properties for this round-tripped DTO; every member must stay { get; init; }.

    /// <summary>The source channel or publisher name.</summary>
    public required string Source { get; init; }

    /// <summary>The content item title.</summary>
    public required string Title { get; init; }

    /// <summary>The fully constructed deep-link URL for the quoted clip.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>The clip timestamp label as rendered in the source artifact.</summary>
    public required string TimestampLabel { get; init; }

    /// <summary>The clipped excerpt text, capped to the prompt-safe excerpt length.</summary>
    public required string Excerpt { get; init; }

    /// <summary>The UTC harvest timestamp associated with the artifact.</summary>
    public required DateTimeOffset HarvestDate { get; init; }

    /// <summary>The computed relevance score for the source artifact.</summary>
    public double Score { get; init; }

    /// <summary>How this clip entered the selection (pinned / followed / auto / evergreen).</summary>
    public string ClipOrigin { get; init; } = "auto";
}
