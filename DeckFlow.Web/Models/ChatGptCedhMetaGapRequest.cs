namespace DeckFlow.Web.Models;

public sealed class ChatGptCedhMetaGapRequest
{
    private string _commanderName = string.Empty;
    private string _deckSource = string.Empty;
    private string _metaGapResponseJson = string.Empty;
    private string _targetAiPlatform = "ChatGPT";
    private string _fetchedEntriesJson = string.Empty;

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
    /// Defaults to "ChatGPT". Accepted values: "ChatGPT", "Claude", "Gemini".
    /// UI field only in Phase 9 — zip round-trip added in Phase 10. Anything
    /// outside the accepted set is normalized to "ChatGPT" so a crafted zip
    /// with an unrecognized <c>target_ai_platform</c> value cannot leave the
    /// request holding an out-of-set string (Phase 10 hardening).
    /// </summary>
    public string TargetAiPlatform
    {
        get => _targetAiPlatform;
        set => _targetAiPlatform = value switch
        {
            "ChatGPT" or "Claude" or "Gemini" => value,
            _ => "ChatGPT"
        };
    }

    /// <summary>
    /// Hidden form field carrying the serialized List&lt;EdhTop16Entry&gt; between Step 2 submits.
    /// When non-empty and successfully deserialized, ChatGptCedhMetaGapService.BuildAsync uses
    /// these entries instead of re-fetching from edhtop16. Round-tripped through the cEDH zip
    /// via 20-edh-top16-references.json. Empty by default; safe to leave blank for fresh flows.
    /// </summary>
    public string FetchedEntriesJson
    {
        get => _fetchedEntriesJson;
        set => _fetchedEntriesJson = value ?? string.Empty;
    }
}
