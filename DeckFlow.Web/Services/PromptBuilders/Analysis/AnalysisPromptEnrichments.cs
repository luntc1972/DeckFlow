namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

/// <summary>
/// Bundles the flag-gated enrichment blocks folded into an analysis prompt so
/// <see cref="IAnalysisPromptVariant.Build"/> takes one parameter instead of growing a new trailing
/// optional <c>string?</c> per phase. Every field is independently null/empty whenever its owning
/// feature flag is off, matching the flag-OFF byte-identity contract each variant already enforces.
/// </summary>
/// <param name="CompanionName">
/// Companion name carried as command-zone side metadata; null when the command-zone-awareness flag
/// (Phase 73) is off.
/// </param>
/// <param name="ScoreBlockText">
/// Pre-built four-axis deck-score text block; null/empty when the multi-axis-score flag (Phase 77)
/// is off.
/// </param>
/// <param name="InteractionAuditText">
/// Pre-built interaction-audit text block; null/empty when the interaction-audit flag (Phase 79) is
/// off.
/// </param>
/// <param name="WinConMapText">
/// Pre-built win-condition/combo-map text block; null/empty when the wincon-map flag (Phase 80) is
/// off.
/// </param>
internal sealed record AnalysisPromptEnrichments(
    string? CompanionName = null,
    string? ScoreBlockText = null,
    string? InteractionAuditText = null,
    string? WinConMapText = null);
