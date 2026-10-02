using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.CommanderCategoryNorms;

/// <summary>Harvested commander-category norms.</summary>
/// <param name="HarvestKey">Stable key used by the harvest.</param>
/// <param name="DeckCount">Number of harvested decks.</param>
/// <param name="Categories">Categories already filtered and sorted by the service.</param>
public sealed record CommanderCategoryNormsResult(string HarvestKey, int DeckCount, IReadOnlyList<CommanderCategorySummary> Categories);
