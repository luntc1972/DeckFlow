namespace DeckFlow.Web.Models;

/// <summary>
/// Razor view model for the deck-comparison page; wraps the request DTO plus the resolved decklists, combo summaries, comparison prompt artifacts, schema, and the parsed AI comparison response state.
/// </summary>
public sealed class DeckComparisonViewModel
{
    /// <summary>
    /// Gets the active tab for the shared deck tool navigation.
    /// </summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.DeckComparison;

    /// <summary>
    /// Gets the original form-bound request for the deck-comparison workflow.
    /// </summary>
    public DeckComparisonRequest Request { get; init; } = new();

    /// <summary>
    /// Gets the user-facing error message for form or upstream failures.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a short human-readable summary of the two deck inputs shown after Step 1.
    /// </summary>
    public string? InputSummary { get; init; }

    /// <summary>
    /// Gets the normalized decklist text for deck A.
    /// </summary>
    public string? DeckAListText { get; init; }

    /// <summary>
    /// Gets the normalized decklist text for deck B.
    /// </summary>
    public string? DeckBListText { get; init; }

    /// <summary>
    /// Gets the combo summary text for deck A produced via the Commander Spellbook lookup.
    /// </summary>
    public string? DeckAComboText { get; init; }

    /// <summary>
    /// Gets the combo summary text for deck B produced via the Commander Spellbook lookup.
    /// </summary>
    public string? DeckBComboText { get; init; }

    /// <summary>
    /// Gets the assembled comparison-context block shown to the AI alongside the prompt.
    /// </summary>
    public string? ComparisonContextText { get; init; }

    /// <summary>
    /// Gets the rendered comparison prompt text the user copies into the AI.
    /// </summary>
    public string? ComparisonPromptText { get; init; }

    /// <summary>
    /// Gets the rendered follow-up prompt text used for the Step 3 deeper-dive question.
    /// </summary>
    public string? FollowUpPromptText { get; init; }

    /// <summary>
    /// Gets the JSON schema describing the expected comparison-response shape the AI should return.
    /// </summary>
    public string? ComparisonSchemaJson { get; init; }

    /// <summary>
    /// Gets the parsed comparison JSON response from the AI, when available.
    /// </summary>
    public DeckComparisonResponse? ComparisonResponse { get; init; }

    /// <summary>
    /// Gets a short summary of the timing taken by upstream calls during this workflow step.
    /// </summary>
    public string? TimingSummary { get; init; }
}
