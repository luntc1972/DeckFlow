namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request for the deck-history page: deck input, optional deck metadata, the
/// AI target for the evolution prompt, a hidden round-trip history JSON field, and optional
/// explicit version ids for pairwise diff inspection.
/// </summary>
public sealed class DeckHistoryRequest
{
    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>Public deck URL used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.</summary>
    public string DeckUrl { get; set; } = string.Empty;

    /// <summary>Pasted deck export text used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PasteText"/>.</summary>
    public string DeckText { get; set; } = string.Empty;

    /// <summary>Optional user-supplied deck name for new history files.</summary>
    public string DeckName { get; set; } = string.Empty;

    /// <summary>Free-text note describing the current snapshot.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Optional short label for the current snapshot.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>AI platform for the generated evolution prompt (e.g. "ChatGPT", "Claude", "Gemini").</summary>
    public string TargetAiPlatform { get; set; } = string.Empty;

    /// <summary>Hidden round-trip history JSON field used when no upload replaces it.</summary>
    public string HistoryJson { get; set; } = string.Empty;

    /// <summary>Optional explicit older version id for pairwise diff inspection.</summary>
    public int? OlderVersionId { get; set; }

    /// <summary>Optional explicit newer version id for pairwise diff inspection.</summary>
    public int? NewerVersionId { get; set; }

    /// <summary>
    /// The raw deck input the user provided — the pasted text or the public URL, whichever
    /// matches <see cref="DeckInputSource"/>.
    /// </summary>
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? DeckUrl : DeckText;
}
