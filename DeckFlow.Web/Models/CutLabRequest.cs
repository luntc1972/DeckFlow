namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request for the Cut Lab page: deck input, declared deck intent, optional commander
/// override, and the hidden round-trip working-session JSON field.
/// </summary>
public sealed class CutLabRequest
{
    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>Public deck URL used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.</summary>
    public string DeckUrl { get; set; } = string.Empty;

    /// <summary>Pasted deck export text used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PasteText"/>.</summary>
    public string DeckText { get; set; } = string.Empty;

    /// <summary>Required primary plan for the intended finished 100-card deck.</summary>
    public string PrimaryPlan { get; set; } = string.Empty;

    /// <summary>Optional secondary plan supporting the primary plan.</summary>
    public string SecondaryPlan { get; set; } = string.Empty;

    /// <summary>Optional target Commander bracket for the finished deck.</summary>
    public int? Bracket { get; set; }

    /// <summary>Desired play experience for the finished deck.</summary>
    public string PlayExperience { get; set; } = string.Empty;

    /// <summary>When true, includes the deck's sideboard cards in the Cut Lab pool as trim candidates.</summary>
    public bool IncludeSideboard { get; set; }

    /// <summary>
    /// When true, includes the deck's considering or maybeboard cards in the Cut Lab pool as trim candidates.
    /// </summary>
    public bool IncludeMaybeboard { get; set; }

    /// <summary>Explicit commander selection when automatic inference is ambiguous.</summary>
    public string SelectedCommander { get; set; } = string.Empty;

    /// <summary>Hidden round-trip working-session JSON field for the Cut Lab state envelope.</summary>
    public string CutLabStateJson { get; set; } = string.Empty;

    /// <summary>
    /// The raw deck input the user provided — the pasted text or the public URL, whichever
    /// matches <see cref="DeckInputSource"/>.
    /// </summary>
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? DeckUrl : DeckText;
}
