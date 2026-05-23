namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request DTO for the deck-analysis page; carries the user's deck input, per-workflow-step toggles, AI-platform selection, analysis questions, set-upgrade options, and round-tripped artifact state used by the analysis prompt pipeline.
/// </summary>
public sealed class DeckAnalysisRequest
{
    private string _deckUrl = string.Empty;
    private string _deckText = string.Empty;
    private string _format = "Commander";
    private string _deckName = string.Empty;
    private string _strategyNotes = string.Empty;
    private string _metaNotes = string.Empty;
    private string _deckProfileJson = string.Empty;
    private string _targetCommanderBracket = string.Empty;
    private string _targetAiPlatform = "ChatGPT";
    private List<string> _selectedAnalysisQuestions = [];
    private List<string> _cardSpecificQuestionCardNames = new();
    private string _budgetUpgradeAmount = string.Empty;
    private List<string> _selectedSetCodes = [];
    private string _setPacketText = string.Empty;
    private string _protectedCards = string.Empty;
    private string _decklistExportFormat = string.Empty;
    private string _preferredCategories = string.Empty;

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
    /// Returns the raw deck input the user provided — either the pasted text or the public URL,
    /// whichever matches <see cref="DeckInputSource"/>. Setting this property routes the value to
    /// <see cref="DeckUrl"/> or <see cref="DeckText"/> based on the current mode so existing
    /// consumers and tests that treat a deck input as a single string keep working.
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
    /// Tracks the current step in the multi-step deck-analysis workflow (1 = deck input, later steps = analysis / set-upgrade / follow-up).
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
    /// Free-form strategy notes the user wants the AI to consider during analysis.
    /// </summary>
    public string StrategyNotes
    {
        get => _strategyNotes;
        set => _strategyNotes = value ?? string.Empty;
    }

    /// <summary>
    /// Free-form notes about the local meta the deck is being tuned against.
    /// </summary>
    public string MetaNotes
    {
        get => _metaNotes;
        set => _metaNotes = value ?? string.Empty;
    }

    /// <summary>
    /// Serialized deck-profile JSON round-tripped between workflow steps and through the analysis artifact zip.
    /// </summary>
    public string DeckProfileJson
    {
        get => _deckProfileJson;
        set => _deckProfileJson = value ?? string.Empty;
    }

    /// <summary>
    /// Target Commander bracket the user wants the AI to grade the deck against (e.g., casual / focused / cEDH).
    /// </summary>
    public string TargetCommanderBracket
    {
        get => _targetCommanderBracket;
        set => _targetCommanderBracket = value ?? string.Empty;
    }

    /// <summary>
    /// The AI platform the user intends to paste the generated artifact into.
    /// Defaults to "ChatGPT". Accepted values: "ChatGPT", "Claude", "Gemini".
    /// Anything else is normalized to "ChatGPT" so a crafted zip with an
    /// unrecognized <c>target_ai_platform</c> value cannot leave the request
    /// holding an out-of-set string (Phase 10 hardening).
    /// </summary>
    public string TargetAiPlatform
    {
        get => _targetAiPlatform;
        set => _targetAiPlatform = AiPlatform.Normalize(value).Key;
    }

    /// <summary>
    /// Identifiers of the analysis questions the user picked from the Step 2 checkbox list.
    /// </summary>
    public List<string> SelectedAnalysisQuestions
    {
        get => _selectedAnalysisQuestions;
        set => _selectedAnalysisQuestions = value ?? [];
    }

    /// <summary>
    /// Card names attached to card-specific analysis questions; normalized to distinct trimmed names on assignment.
    /// </summary>
    public List<string> CardSpecificQuestionCardNames
    {
        get => _cardSpecificQuestionCardNames;
        set => _cardSpecificQuestionCardNames = value is null
            ? new List<string>()
            : value
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>
    /// User-supplied budget cap used by the set-upgrade prompt to scope suggested adds.
    /// </summary>
    public string BudgetUpgradeAmount
    {
        get => _budgetUpgradeAmount;
        set => _budgetUpgradeAmount = value ?? string.Empty;
    }

    /// <summary>
    /// Scryfall set codes the user selected for the set-upgrade analysis step.
    /// </summary>
    public List<string> SelectedSetCodes
    {
        get => _selectedSetCodes;
        set => _selectedSetCodes = value ?? [];
    }

    /// <summary>
    /// Pre-built set-packet reference text round-tripped between the set-upgrade workflow steps.
    /// </summary>
    public string SetPacketText
    {
        get => _setPacketText;
        set => _setPacketText = value ?? string.Empty;
    }

    /// <summary>
    /// Comma-separated list of cards the user wants the AI to leave untouched when recommending cuts.
    /// </summary>
    public string ProtectedCards
    {
        get => _protectedCards;
        set => _protectedCards = value ?? string.Empty;
    }

    /// <summary>
    /// Deck export format used when emitting the decklist artifact (e.g., Moxfield, Archidekt).
    /// </summary>
    public string DecklistExportFormat
    {
        get => _decklistExportFormat;
        set => _decklistExportFormat = value ?? string.Empty;
    }

    /// <summary>
    /// When true, asks the AI to include specific printings/versions in its decklist output.
    /// </summary>
    public bool IncludeCardVersions { get; set; }

    /// <summary>
    /// When true, includes the deck's sideboard and maybeboard cards in the analysis prompt
    /// as authoritative candidate references (still labeled as candidates, not active deck cards).
    /// </summary>
    public bool IncludeCandidateReferencesInAnalysis { get; set; }

    /// <summary>
    /// User-supplied preferred categories to weight when the AI suggests deck organization.
    /// </summary>
    public string PreferredCategories
    {
        get => _preferredCategories;
        set => _preferredCategories = value ?? string.Empty;
    }

    private string _freeformQuestion = string.Empty;

    /// <summary>
    /// Free-form question the user wants the AI to answer alongside the structured analysis.
    /// </summary>
    public string FreeformQuestion
    {
        get => _freeformQuestion;
        set => _freeformQuestion = value ?? string.Empty;
    }

    private string _setUpgradeFocus = string.Empty;

    /// <summary>
    /// Controls the focus of the set-upgrade prompt: "lateral-moves", "strict-upgrades", or empty (default: best additions).
    /// </summary>
    public string SetUpgradeFocus
    {
        get => _setUpgradeFocus;
        set => _setUpgradeFocus = value ?? string.Empty;
    }

    private string _setUpgradeResponseJson = string.Empty;

    /// <summary>
    /// Serialized set-upgrade response JSON round-tripped between the set-upgrade workflow steps.
    /// </summary>
    public string SetUpgradeResponseJson
    {
        get => _setUpgradeResponseJson;
        set => _setUpgradeResponseJson = value ?? string.Empty;
    }

}
