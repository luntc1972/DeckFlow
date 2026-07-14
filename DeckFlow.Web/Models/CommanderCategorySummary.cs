namespace DeckFlow.Web.Models;

/// <summary>Aggregated category count for cards found in harvested commander decks.</summary>
/// <param name="Category">Category label assigned to harvested commander cards.</param>
/// <param name="Count">Total card rows assigned to the category.</param>
/// <param name="DeckCount">Total harvested decks represented by the category.</param>
/// <param name="DeckShare">Share of observed commander decks that include the category.</param>
public sealed record CommanderCategorySummary(string Category, int Count, int DeckCount, double DeckShare);
