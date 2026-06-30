namespace DeckFlow.Web.Models;

/// <summary>
/// Razor view model for the deck-primer page.
/// </summary>
public sealed class DeckPrimerViewModel
{
    /// <summary>
    /// Gets the active tab for the shared deck tool navigation.
    /// </summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.DeckPrimer;

    /// <summary>
    /// Gets the original form-bound request for the deck-primer workflow.
    /// </summary>
    public DeckPrimerRequest Request { get; init; } = new();

    /// <summary>
    /// Gets the user-facing error message for form or upstream failures.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a short human-readable summary of the user's deck input.
    /// </summary>
    public string? InputSummary { get; init; }

    /// <summary>
    /// Gets the AI-friendly chat title suggested for the primer conversation.
    /// </summary>
    public string? SuggestedChatTitle { get; init; }

    /// <summary>
    /// Gets the rendered primer prompt text the user copies into the AI.
    /// </summary>
    public string? PrimerPromptText { get; init; }

    /// <summary>
    /// Gets whether <c>tool.primer.stale-flag</c> is enabled for this render; gates both the hidden
    /// staleness field and banner so flag-OFF markup stays byte-identical.
    /// </summary>
    public bool StaleDetectionEnabled { get; init; }

    /// <summary>
    /// Gets the generation-time deck multiset hash used to re-arm the hidden field when stale
    /// detection is enabled; null when flag-OFF to preserve byte-identical output.
    /// </summary>
    public string? GeneratedPrimerHash { get; init; }

    /// <summary>
    /// Gets whether the current deck differs from the deck that generated the restored primer; false
    /// on all flag-OFF paths so output stays byte-identical.
    /// </summary>
    public bool IsStale { get; init; }

    /// <summary>
    /// Gets the add/remove/quantity-change count for a stale restored primer, or null when flag-OFF or
    /// when no saved generation snapshot is available.
    /// </summary>
    public int? ChangedCardCount { get; init; }

    /// <summary>
    /// Gets a short summary of the timing taken by upstream calls during this workflow step.
    /// </summary>
    public string? TimingSummary { get; init; }

    /// <summary>
    /// Gets a warning surfaced when the user's deck import succeeded with caveats.
    /// </summary>
    public string? ImportWarning { get; init; }
}
