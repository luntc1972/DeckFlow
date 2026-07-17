namespace DeckFlow.Core.History;

/// <summary>Where the tracked deck lives (e.g. site "moxfield" plus its public URL).</summary>
public sealed record DeckHistorySource
{
    /// <summary>Source site key, e.g. "moxfield" or "archidekt".</summary>
    public string? Site { get; init; }

    /// <summary>Public deck URL, when the deck was imported by URL.</summary>
    public string? Url { get; init; }
}
