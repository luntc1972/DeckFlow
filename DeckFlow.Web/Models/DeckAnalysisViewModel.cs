using DeckFlow.Core.Analysis;

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
    /// Gets whether the <c>analysis.command-zone-awareness</c> feature flag is enabled. When true the
    /// Step 1 form surfaces the optional companion designator input; when false (the default) that
    /// input is not rendered and the page is byte-identical to baseline. Server-computed from the flag
    /// cache and init-only, so it is never form-bound and a crafted POST cannot enable the feature.
    /// </summary>
    public bool CommandZoneAwarenessEnabled { get; init; }

    /// <summary>
    /// Gets the computed four-axis deck score (Power/Speed/Control/Consistency), when the
    /// <c>analysis.multi-axis-score</c> flag is on and the deck was loaded. Null when the flag is off
    /// (the default), keeping the rendered page byte-identical to baseline. Init-only and server-computed,
    /// so it is never form-bound.
    /// </summary>
    public DeckMultiAxisScore? Score { get; init; }

    /// <summary>
    /// Gets the interaction audit computed at Step 2. Null when the
    /// <c>analysis.interaction-audit</c> flag is off.
    /// </summary>
    public InteractionAudit? InteractionAudit { get; init; }

    /// <summary>
    /// Gets the win-condition/combo map computed at Step 2. Null when the
    /// <c>analysis.wincon-map</c> flag is off.
    /// </summary>
    public WinConMap? WinConMap { get; init; }

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
    /// Gets exact card rules text keyed by card name, sourced from the generated set packet, used to
    /// display what each suggested card does. The view prefers these values over the AI-echoed
    /// <see cref="SetUpgradeTopAdd.CardText"/>; empty when the set packet was unavailable.
    /// </summary>
    public IReadOnlyDictionary<string, string> SetUpgradeCardText { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a warning surfaced when the user's deck import succeeded but with caveats worth flagging.
    /// </summary>
    public string? ImportWarning { get; init; }

    /// <summary>
    /// Gets whether the "Build Analysis Prompt" step (Step 2) is complete. The prompt text is
    /// only present on the generate postback; later steps (3) skip the deck load and return null
    /// prompt text, so a parsed analysis response also counts as proof the prompt was generated.
    /// </summary>
    public bool IsAnalysisPromptStepComplete =>
        !string.IsNullOrWhiteSpace(AnalysisPromptText) || AnalysisResponse is not null;

    /// <summary>
    /// Gets whether the "Build Set Upgrade Prompt" step (Step 4) is complete. Mirrors
    /// <see cref="IsAnalysisPromptStepComplete"/>: a parsed set-upgrade response proves the
    /// set-upgrade prompt was generated even when Step 5 returns null prompt text.
    /// </summary>
    public bool IsSetUpgradePromptStepComplete =>
        !string.IsNullOrWhiteSpace(SetUpgradePromptText) || SetUpgradeResponse is not null;
}
