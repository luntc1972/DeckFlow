namespace DeckFlow.Web.Models;

/// <summary>
/// Razor view model for the deck-analysis page; wraps the request DTO plus per-step prompt artifacts, the parsed AI analysis response, and the optional set-upgrade response state.
/// </summary>
public sealed class DeckAnalysisViewModel
{
    /// <summary>
    /// Gets the active tab for the shared deck tool navigation.
    /// </summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.DeckAnalysis;

    /// <summary>
    /// Gets the original form-bound request for the deck-analysis workflow.
    /// </summary>
    public DeckAnalysisRequest Request { get; init; } = new();

    /// <summary>
    /// Gets the user-facing error message for form or upstream failures.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a short human-readable summary of the user's deck input shown after Step 1.
    /// </summary>
    public string? InputSummary { get; init; }

    /// <summary>
    /// Gets the AI-friendly chat title suggested for the analysis conversation.
    /// </summary>
    public string? SuggestedChatTitle { get; init; }

    /// <summary>
    /// Gets the reference text bundle (banlist, combo data, normalized decklist) shown to the AI.
    /// </summary>
    public string? ReferenceText { get; init; }

    /// <summary>
    /// Gets the rendered analysis prompt text the user copies into the AI.
    /// </summary>
    public string? AnalysisPromptText { get; init; }

    /// <summary>
    /// Gets the JSON schema describing the expected deck-profile shape the AI should return.
    /// </summary>
    public string? DeckProfileSchemaJson { get; init; }

    /// <summary>
    /// Gets the rendered set-upgrade prompt text the user copies into the AI.
    /// </summary>
    public string? SetUpgradePromptText { get; init; }

    /// <summary>
    /// Gets a short summary of the timing taken by upstream calls during this workflow step.
    /// </summary>
    public string? TimingSummary { get; init; }

    /// <summary>
    /// Gets the parsed deck-analysis JSON response from the AI, when available.
    /// </summary>
    public DeckAnalysisResponse? AnalysisResponse { get; init; }

    /// <summary>
    /// Gets the parsed set-upgrade JSON response from the AI, when available.
    /// </summary>
    public SetUpgradeResponse? SetUpgradeResponse { get; init; }

    /// <summary>
    /// Gets the injected expert-context clips for the What Experts Say panel rendered in plan 30-04.
    /// </summary>
    public IReadOnlyList<ContentKbExcerpt>? ExpertContextClips { get; init; }

    /// <summary>
    /// Gets a warning surfaced when the user's deck import succeeded but with caveats worth flagging.
    /// </summary>
    public string? ImportWarning { get; init; }
}
