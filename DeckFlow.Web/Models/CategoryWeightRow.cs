namespace DeckFlow.Web.Models;

/// <summary>Weighted merged category metadata for Suggest Categories UI rendering.</summary>
/// <param name="Category">The merged category label shown to the user.</param>
/// <param name="DeckCount">The cached-store deck count for this category, or null when unavailable.</param>
/// <param name="Percent">The cached-store popularity percentage for this category, or null when unavailable.</param>
/// <param name="SourceCount">The number of contributing sources that suggested this category.</param>
/// <param name="SourceTotal">The total number of sources that contributed at least one merged category.</param>
public sealed record CategoryWeightRow(
    string Category,
    int? DeckCount,
    int? Percent,
    int SourceCount,
    int SourceTotal);
