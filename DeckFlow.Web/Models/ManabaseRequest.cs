using DeckFlow.Core.Manabase;

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
    private string _companionName = string.Empty;
    private string _costOverridesText = string.Empty;

    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>
    /// The analysis profile. <see cref="ManabaseMode.Casual"/> is the default (Karsten land
    /// target, castability table shown); <see cref="ManabaseMode.Cedh"/> lowers the land target.
    /// </summary>
    public ManabaseMode Mode { get; set; } = ManabaseMode.Casual;

    /// <summary>
    /// How heavily to weight the commander's colors. Defaults to
    /// <see cref="CommanderImportance.Standard"/>; <see cref="CommanderImportance.Central"/>
    /// holds the commander's colors to a stricter threshold.
    /// </summary>
    public CommanderImportance CommanderImportance { get; set; } = CommanderImportance.Standard;

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

    /// <summary>Optional manual companion designator; blank is fine.</summary>
    public string CompanionName
    {
        get => _companionName;
        set => _companionName = value ?? string.Empty;
    }

    /// <summary>
    /// Optional reduced / alternative cost overrides, one per line as <c>Card Name: cost</c>
    /// (e.g. <c>Force of Will: 0</c>, <c>Blasphemous Act: {R}</c>). Pre-populated from auto-detected
    /// suggestions; the user may edit. Parsed by <c>ManabaseCostOverrideParser</c>.
    /// </summary>
    public string CostOverridesText
    {
        get => _costOverridesText;
        set => _costOverridesText = value ?? string.Empty;
    }

    /// <summary>
    /// True once the user has edited the reduced-cost box (a client script sets it on the first
    /// input). It distinguishes "user deliberately cleared the box to reject the suggestions" from
    /// "user never touched the pre-filled suggestions" — without it, a cleared box silently refills
    /// with the auto-detected suggestions on the next render. Defaults false so an untouched box (and
    /// any caller that omits the field) keeps the historic pre-fill behavior.
    /// </summary>
    public bool OverridesTouched { get; set; }

    /// <summary>
    /// The raw deck input the user provided — the pasted text or the public URL, whichever
    /// matches <see cref="DeckInputSource"/>.
    /// </summary>
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? _deckUrl : _deckText;
}
