namespace DeckFlow.Web.Models;

public sealed class ChatGptCedhMetaGapRequest
{
    private string _commanderName = string.Empty;
    private string _deckSource = string.Empty;
    private string _metaGapResponseJson = string.Empty;
    private string _targetAiPlatform = "ChatGPT";

    public int WorkflowStep { get; set; } = 1;

    public string CommanderName
    {
        get => _commanderName;
        set => _commanderName = value ?? string.Empty;
    }

    public string DeckSource
    {
        get => _deckSource;
        set => _deckSource = value ?? string.Empty;
    }

    public CedhMetaTimePeriod TimePeriod { get; set; } = CedhMetaTimePeriod.ONE_YEAR;

    public CedhMetaSortBy SortBy { get; set; } = CedhMetaSortBy.TOP;

    public int MinEventSize { get; set; } = 50;

    public int? MaxStanding { get; set; }

    public List<int> SelectedReferenceIndexes { get; set; } = new();

    public string MetaGapResponseJson
    {
        get => _metaGapResponseJson;
        set => _metaGapResponseJson = value ?? string.Empty;
    }

    /// <summary>
    /// The AI platform the user intends to paste the generated artifact into.
    /// Defaults to "ChatGPT". UI field only in Phase 9 — zip round-trip added in Phase 10.
    /// </summary>
    public string TargetAiPlatform
    {
        get => _targetAiPlatform;
        set => _targetAiPlatform = value ?? "ChatGPT";
    }
}
