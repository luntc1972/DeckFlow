namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request DTO for the deck-primer page.
/// </summary>
public sealed class DeckPrimerRequest
{
    private string _deckText = string.Empty;
    private string _deckUrl = string.Empty;
    private string _format = "Commander";
    private string _deckName = string.Empty;
    private string _targetCommanderBracket = string.Empty;
    private string _targetAiPlatform = "ChatGPT";
    private List<string> _selectedSectionIds = [];

    /// <summary>
    /// Selects whether the deck is supplied via a public URL or pasted export text.
    /// </summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PasteText;

    /// <summary>
    /// Public deck URL used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.
    /// </summary>
    public string DeckUrl
    {
        get => _deckUrl;
        set => _deckUrl = value ?? string.Empty;
    }

    /// <summary>
    /// Pasted deck export text used when <see cref="DeckInputSource"/> is <see cref="DeckInputSource.PasteText"/>.
    /// </summary>
    public string DeckText
    {
        get => _deckText;
        set => _deckText = value ?? string.Empty;
    }

    /// <summary>
    /// Returns the raw deck input the user provided, routing through <see cref="DeckUrl"/> or <see cref="DeckText"/>
    /// based on the current <see cref="DeckInputSource"/> value.
    /// </summary>
    public string DeckSource
    {
        get => DeckInputSource == DeckInputSource.PublicUrl ? _deckUrl : _deckText;
        set
        {
            var normalized = value ?? string.Empty;
            if (DeckInputSource == DeckInputSource.PublicUrl)
            {
                _deckUrl = normalized;
            }
            else
            {
                _deckText = normalized;
            }
        }
    }

    /// <summary>
    /// Tracks the current step in the multi-step deck-primer workflow.
    /// </summary>
    public int WorkflowStep { get; set; } = 1;

    /// <summary>
    /// Magic: The Gathering format the deck targets; defaults to "Commander".
    /// </summary>
    public string Format
    {
        get => _format;
        set => _format = value ?? "Commander";
    }

    /// <summary>
    /// Optional user-supplied deck name; falls back to a derived name when blank.
    /// </summary>
    public string DeckName
    {
        get => _deckName;
        set => _deckName = value ?? string.Empty;
    }

    /// <summary>
    /// Target Commander bracket the primer should be written for.
    /// </summary>
    public string TargetCommanderBracket
    {
        get => _targetCommanderBracket;
        set => _targetCommanderBracket = value ?? string.Empty;
    }

    /// <summary>
    /// The AI platform the user intends to paste the generated primer into.
    /// Unknown values are normalized to the default platform.
    /// </summary>
    public string TargetAiPlatform
    {
        get => _targetAiPlatform;
        set => _targetAiPlatform = AiPlatform.Normalize(value).Key;
    }

    /// <summary>
    /// Controls the output style the primer prompt should request from the AI.
    /// </summary>
    public PrimerOutputStyle PrimerStyle { get; set; } = PrimerOutputStyle.Standard;

    /// <summary>
    /// Identifiers of the primer sections the user selected from the catalog.
    /// </summary>
    public List<string> SelectedSectionIds
    {
        get => _selectedSectionIds;
        set => _selectedSectionIds = value ?? [];
    }
}
