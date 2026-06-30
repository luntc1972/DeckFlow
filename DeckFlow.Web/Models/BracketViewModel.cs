using DeckFlow.Core.Bracket;

namespace DeckFlow.Web.Models;

/// <summary>
/// View model for the bracket classification page: the form request, an optional computed
/// classification result, and presentation extras (error, tiers, prompt artifact).
/// </summary>
public sealed class BracketViewModel
{
    /// <summary>The active deck-tool tab (always <see cref="DeckPageTab.Bracket"/>).</summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.Bracket;

    /// <summary>The form-bound request, re-rendered so inputs persist across the postback.</summary>
    public BracketRequest Request { get; init; } = new();

    /// <summary>User-facing error message, or null when the request succeeded.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The computed bracket classification, or null before a successful submit.</summary>
    public BracketClassification? Classification { get; init; }

    /// <summary>The five bracket tier definitions from the catalog (for UI rendering).</summary>
    public IReadOnlyList<BracketTier>? Tiers { get; init; }

    /// <summary>The user-requested target bracket number (1–5), or null for classify-only.</summary>
    public int? TargetBracketNumber { get; init; }

    /// <summary>The paste-ready prompt artifact for the selected AI platform, or null.</summary>
    public string? PromptArtifact { get; init; }

    /// <summary>Optional notice from the deck importer (e.g. a fallback path was used).</summary>
    public string? ImportWarning { get; init; }

    /// <summary>True when a classification result is present and should be rendered.</summary>
    public bool HasResult => Classification is not null;

    /// <summary>True when a target bracket number was supplied.</summary>
    public bool HasTarget => TargetBracketNumber.HasValue;

    /// <summary>
    /// True when a classification exists, a target was chosen, and the deck's bracket
    /// exceeds the target (floor violations and starter cuts should be rendered).
    /// </summary>
    public bool IsOverTarget => HasResult && HasTarget &&
        Classification!.BracketNumber > TargetBracketNumber;
}
