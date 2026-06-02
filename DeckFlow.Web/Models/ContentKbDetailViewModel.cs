using Microsoft.AspNetCore.Html;

namespace DeckFlow.Web.Models;

/// <summary>
/// View model for a public Content KB artifact detail page.
/// </summary>
public sealed record ContentKbDetailViewModel
{
    /// <summary>Entry title.</summary>
    public required string Title { get; init; }

    /// <summary>Source display name.</summary>
    public required string SourceName { get; init; }

    /// <summary>Canonical source URL.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>Human-readable publication date.</summary>
    public required string PublishedDisplay { get; init; }

    /// <summary>Primary bracket tag.</summary>
    public required string Bracket { get; init; }

    /// <summary>Primary archetype tag.</summary>
    public required string Archetype { get; init; }

    /// <summary>Rendered artifact markdown.</summary>
    public required HtmlString RenderedHtml { get; init; }

    /// <summary>Frontmatter-stripped artifact text for the copy button.</summary>
    public required string CleanBodyText { get; init; }

    /// <summary>Whether the artifact file was unavailable on disk.</summary>
    public bool ArtifactUnavailable { get; init; }
}
