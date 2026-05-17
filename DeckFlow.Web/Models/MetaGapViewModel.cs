namespace DeckFlow.Web.Models;

/// <summary>
/// Razor view model for the cEDH meta-gap page; wraps the request DTO plus the resolved commander name, edhtop16 reference rows, prompt artifacts, schema, and the parsed AI meta-gap response state.
/// </summary>
public sealed class MetaGapViewModel
{
    /// <summary>
    /// Gets the active tab for the shared deck tool navigation.
    /// </summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.CedhMetaGap;

    /// <summary>
    /// Gets the original form-bound request for the cEDH meta-gap workflow.
    /// </summary>
    public MetaGapRequest Request { get; init; } = new();

    /// <summary>
    /// Gets the user-facing error message for form or upstream failures.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a short human-readable summary of the user's inputs shown after Step 1.
    /// </summary>
    public string? InputSummary { get; init; }

    /// <summary>
    /// Gets the canonical commander name resolved from the user's input via Scryfall.
    /// </summary>
    public string? ResolvedCommanderName { get; init; }

    /// <summary>
    /// Gets the rendered meta-gap prompt text the user copies into the AI.
    /// </summary>
    public string? PromptText { get; init; }

    /// <summary>
    /// Gets the JSON schema describing the expected meta-gap response shape the AI should return.
    /// </summary>
    public string? SchemaJson { get; init; }

    /// <summary>
    /// Gets the edhtop16 reference rows fetched for the chosen commander and filters.
    /// </summary>
    public IReadOnlyList<EdhTop16Entry> FetchedEntries { get; init; } = Array.Empty<EdhTop16Entry>();

    /// <summary>
    /// Gets the parsed meta-gap JSON response from the AI, when available.
    /// </summary>
    public MetaGapResponse? AnalysisResponse { get; init; }
}
