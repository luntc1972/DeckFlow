namespace DeckFlow.Core.Knowledge;

/// <summary>Optional constraints for a batch category lookup.</summary>
/// <param name="MinimumObservationShare">Minimum share of all category observations for a card.</param>
/// <param name="MaximumCategoriesPerCard">Maximum retained categories for each card.</param>
public sealed record CategoryLookupOptions(double MinimumObservationShare, int MaximumCategoriesPerCard)
{
    /// <summary>Minimum category-observation share required by Cut Lab.</summary>
    public const double CutLabMinimumObservationShare = 0.15;

    /// <summary>Maximum meaningful crowd tags Cut Lab retains per card.</summary>
    public const int CutLabMaximumCategoriesPerCard = 3;

    /// <summary>Cut Lab's noise-reduction limits for crowd-sourced category tags.</summary>
    public static CategoryLookupOptions CutLabMeaningful { get; } = new(CutLabMinimumObservationShare, CutLabMaximumCategoriesPerCard);
}
