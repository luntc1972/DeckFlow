namespace DeckFlow.Core.Bracket;

/// <summary>
/// A single tier in the Commander bracket system (Brackets Beta, WotC Oct 2025 / Feb 2026).
/// </summary>
/// <param name="Number">Tier number (1–5), where 1 is the most casual and 5 is cEDH.</param>
/// <param name="Name">Short name for the tier (e.g., "Exhibition", "Core", "cEDH").</param>
/// <param name="Label">Full human-readable label shown in the UI (e.g., "Bracket 1: Exhibition").</param>
/// <param name="Summary">Short description of the tier's expected deck shape and play style.</param>
/// <param name="TurnsExpectation">Expected turn range for wins or losses in this tier.</param>
/// <param name="MaxGameChangers">
/// Maximum Game Changers allowed in this tier; <c>-1</c> means unlimited (Brackets 4 and 5).
/// </param>
public sealed record BracketTier(
    int Number,
    string Name,
    string Label,
    string Summary,
    string TurnsExpectation,
    int MaxGameChangers);

/// <summary>
/// Versioned catalog of Game Changers, Mass Land Denial cards, Extra Turn cards,
/// and the 5 Commander bracket tier definitions — loaded from <c>bracket-data.json</c>.
/// </summary>
/// <param name="EffectiveDate">Date this version of the catalog took effect.</param>
/// <param name="GameChangers">
/// Alphabetically sorted list of Game Changer card names, compared OrdinalIgnoreCase.
/// </param>
/// <param name="MassLandDenialCards">
/// Curated list of mass-land-denial card names; any match forces Bracket 4 or higher.
/// </param>
/// <param name="ExtraTurnCards">
/// Curated list of extra-turn spell names. These are <em>informational only</em> and do not
/// change the bracket number by themselves per the current WotC rubric.
/// </param>
/// <param name="Tiers">
/// The five Commander bracket tier definitions in ascending order (Bracket 1 through 5).
/// </param>
public sealed record GameChangerCatalog(
    DateOnly EffectiveDate,
    IReadOnlyList<string> GameChangers,
    IReadOnlyList<string> MassLandDenialCards,
    IReadOnlyList<string> ExtraTurnCards,
    IReadOnlyList<BracketTier> Tiers);
