namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request for the mana-base page: the user's deck input (public URL or pasted
/// text) plus an optional deck name used in the generated ChatGPT swap prompt.
/// </summary>
public sealed class ManabaseRequest
{
    private string _deckUrl = string.Empty;
    private string _deckText = string.Empty;
    private string _deckName = string.Empty;

    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>Public deck URL used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.</summary>
    public string DeckUrl
    {
        get => _deckUrl;
        set => _deckUrl = value ?? string.Empty;
    }

    /// <summary>Pasted deck export text used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PasteText"/>.</summary>
    public string DeckText
    {
        get => _deckText;
        set => _deckText = value ?? string.Empty;
    }

    /// <summary>Optional user-supplied deck name; blank is fine.</summary>
    public string DeckName
    {
        get => _deckName;
        set => _deckName = value ?? string.Empty;
    }

    /// <summary>
    /// The raw deck input the user provided — the pasted text or the public URL, whichever
    /// matches <see cref="DeckInputSource"/>.
    /// </summary>
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? _deckUrl : _deckText;
}
