namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>
/// Plain Scryfall-backed card reference data injected into evolution prompt variants.
/// </summary>
internal sealed record EvolutionCardReference(
    string Name,
    string ManaCost,
    string TypeLine,
    string OracleText);
