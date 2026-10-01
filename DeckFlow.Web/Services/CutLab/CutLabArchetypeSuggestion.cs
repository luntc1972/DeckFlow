namespace DeckFlow.Web.Services.CutLab;

/// <summary>A composition-based recommendation for a Cut Lab deck archetype.</summary>
/// <param name="Slug">Suggested archetype catalog slug.</param>
/// <param name="Confidence">Confidence supported by available classification data.</param>
/// <param name="Reason">One-sentence explanation of the deciding pattern.</param>
public sealed record CutLabArchetypeSuggestion(string Slug, CutLabArchetypeConfidence Confidence, string Reason);
