namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request for the bracket classification page: the user's deck input (public URL
/// or pasted text), an optional deck name, an optional target bracket, and the AI platform
/// choice for the generated balancer prompt.
/// </summary>
public sealed class BracketRequest
{
    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>Public deck URL used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.</summary>
    public string DeckUrl { get; set; } = string.Empty;

    /// <summary>Pasted deck export text used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PasteText"/>.</summary>
    public string DeckText { get; set; } = string.Empty;

    /// <summary>Optional user-supplied deck name; blank is fine.</summary>
    public string? DeckName { get; set; }

    /// <summary>Target bracket number (1–5), or null if the user chose classify-only.</summary>
    public int? TargetBracketNumber { get; set; }

    /// <summary>AI platform for the paste artifact (e.g. "ChatGPT", "Claude", "Gemini").</summary>
    public string TargetAiPlatform { get; set; } = "ChatGPT";

    /// <summary>
    /// The raw deck input the user provided — the pasted text or the public URL, whichever
    /// matches <see cref="DeckInputSource"/>.
    /// </summary>
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? DeckUrl : DeckText;
}
