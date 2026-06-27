using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Models;

/// <summary>
/// View model for the mana-base page: the form request, an optional computed report, and
/// presentation extras (error, summary, unresolved cards, ChatGPT swap prompt).
/// </summary>
public sealed class ManabaseViewModel
{
    /// <summary>The active deck-tool tab (always <see cref="DeckPageTab.Manabase"/>).</summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.Manabase;

    /// <summary>The form-bound request, re-rendered so inputs persist across the postback.</summary>
    public ManabaseRequest Request { get; init; } = new();

    /// <summary>User-facing error message, or null when the request succeeded.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The computed report, or null before a successful analysis.</summary>
    public ManabaseReport? Report { get; init; }

    /// <summary>Short summary of what was analyzed (card/land counts).</summary>
    public string? InputSummary { get; init; }

    /// <summary>Card names Scryfall could not resolve (excluded from the math).</summary>
    public IReadOnlyList<string> Unresolved { get; init; } = Array.Empty<string>();

    /// <summary>Optional importer notice (e.g. a Moxfield fallback path was used).</summary>
    public string? ImportWarning { get; init; }

    /// <summary>Paste-ready prompt asking an LLM for specific land swaps.</summary>
    public string? ChatGptSwapPrompt { get; init; }

    /// <summary>Auto-detected alt/reduced-cost suggestions used to pre-populate the override box.</summary>
    public IReadOnlyList<CostSuggestion> Suggestions { get; init; } = Array.Empty<CostSuggestion>();

    /// <summary>Optional synthesized plain-language verdict for the analyzed deck.</summary>
    public ManabaseVerdict? PlainLanguageVerdict { get; init; }

    /// <summary>Optional ramp/draw slot-budget advisory for Casual-mode verdicts.</summary>
    public ManabaseRampDrawBudget? RampDrawBudget { get; init; }

    /// <summary>Whether the UI should surface the plain-language glossary/verdict affordances.</summary>
    public bool ShowPlainLanguage { get; init; }

    /// <summary>The detected suggestions rendered as override-box lines (<c>Name: cost</c>).</summary>
    public string SuggestedOverridesText =>
        string.Join("\n", Suggestions.Select(s => $"{s.Name}: {s.EffectiveCost}"));

    /// <summary>
    /// What to show in the override box: the user's own text when they supplied any, otherwise the
    /// detected suggestions pre-filled (preserve-vs-prepopulate).
    /// </summary>
    public string OverridesBoxText =>
        string.IsNullOrWhiteSpace(Request.CostOverridesText)
            ? SuggestedOverridesText
            : Request.CostOverridesText;

    /// <summary>True when there is at least one detected suggestion to surface to the user.</summary>
    public bool HasSuggestions => Suggestions.Count > 0;

    /// <summary>True when a report is present and should be rendered.</summary>
    public bool HasResult => Report is not null;

    /// <summary>
    /// True after the "Load deck" step resolved the deck and detected cost suggestions, but before a
    /// full analysis ran. Drives the review-then-analyze hint.
    /// </summary>
    public bool Loaded { get; init; }

    /// <summary>
    /// True when the castability table should render: a report exists, it was run in Casual mode,
    /// and it carries at least one castability row. cEDH hides the table (v1) and shows a note.
    /// </summary>
    public bool ShowCastability => Report is { Mode: ManabaseMode.Casual, Castability.Count: > 0 };
}
