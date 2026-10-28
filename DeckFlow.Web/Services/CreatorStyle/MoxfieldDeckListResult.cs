namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Public Moxfield deck summaries and upstream enumeration state.</summary>
public sealed record MoxfieldDeckListResult
{
    /// <summary>Decks found before any failure.</summary>
    public required IReadOnlyList<MoxfieldDeckSummary> Decks { get; init; }
    /// <summary>Whether upstream listing did not complete.</summary>
    public required bool HasUpstreamFailure { get; init; }
    /// <summary>Whether the listing reached the configured page limit before its last page.</summary>
    public bool IsTruncated { get; init; }
}
