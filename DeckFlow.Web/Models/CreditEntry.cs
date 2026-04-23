namespace DeckFlow.Web.Models;

/// <summary>Upstream data source acknowledged on the About page.</summary>
public sealed record CreditEntry(string Name, string Url, string Description);
