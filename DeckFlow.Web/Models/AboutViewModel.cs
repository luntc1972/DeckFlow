namespace DeckFlow.Web.Models;

/// <summary>View model for the About page.</summary>
public sealed record AboutViewModel(
    string Tagline,
    string Version,
    string RepositoryUrl,
    IReadOnlyList<CreditEntry> Credits);
