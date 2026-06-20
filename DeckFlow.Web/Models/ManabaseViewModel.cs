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

    /// <summary>True when a report is present and should be rendered.</summary>
    public bool HasResult => Report is not null;
}
