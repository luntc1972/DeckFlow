namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request DTO for the cEDH meta-gap page; captures the user deck plus the edhtop16 reference filters (time period, sort, minimum event size, max standing) and selected reference rows used to generate the meta-gap analysis prompt.
/// </summary>
public sealed class MetaGapRequest
{
    private string _commanderName = string.Empty;
    private string _deckSource = string.Empty;
    private string _deckUrl = string.Empty;
    private string _deckText = string.Empty;
    private string _metaGapResponseJson = string.Empty;
    private string _targetAiPlatform = "ChatGPT";
    private string _fetchedEntriesJson = string.Empty;
    private string _metaGapPromptText = string.Empty;

    /// <summary>
    /// Tracks the current step in the multi-step cEDH meta-gap workflow.
    /// </summary>
    public int WorkflowStep { get; set; } = 1;

    /// <summary>
    /// Commander name the user is analyzing; drives the edhtop16 reference lookup.
    /// </summary>
    public string CommanderName
    {
        get => _commanderName;
        set => _commanderName = value ?? string.Empty;
    }

    /// <summary>
    /// Raw deck input for the user's deck (public URL or pasted export text).
    /// </summary>
    public string DeckSource
    {
        get => _deckSource;
        set => _deckSource = value ?? string.Empty;
    }

    /// <summary>
    /// Selects whether the deck is supplied via a public URL or pasted export text.
    /// </summary>
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;

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
    /// Edhtop16 time-period filter (e.g., one-year window) applied when fetching reference decks.
    /// </summary>
    public CedhMetaTimePeriod TimePeriod { get; set; } = CedhMetaTimePeriod.ONE_YEAR;

    /// <summary>
    /// Edhtop16 sort order used when listing reference decks for selection.
    /// </summary>
    public CedhMetaSortBy SortBy { get; set; } = CedhMetaSortBy.TOP;

    /// <summary>
    /// Minimum event size to include in the edhtop16 reference query (filters out small events).
    /// </summary>
    public int MinEventSize { get; set; } = 50;

    /// <summary>
    /// Optional maximum tournament standing applied to the edhtop16 reference query.
    /// </summary>
    public int? MaxStanding { get; set; }

    /// <summary>
    /// Indexes (within <see cref="FetchedEntriesJson"/>) of the reference rows the user picked for the meta-gap analysis.
    /// </summary>
    public List<int> SelectedReferenceIndexes { get; set; } = new();

    /// <summary>
    /// Serialized meta-gap response JSON round-tripped between workflow steps and through the cEDH artifact zip.
    /// </summary>
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
        set => _targetAiPlatform = AiPlatform.Normalize(value).Key;
    }

    /// <summary>
    /// Hidden form field carrying the serialized List&lt;EdhTop16Entry&gt; between Step 2 submits.
    /// When non-empty and successfully deserialized, MetaGapService.BuildAsync uses
    /// these entries instead of re-fetching from edhtop16. Round-tripped through the cEDH zip
    /// via 20-edh-top16-references.json. Empty by default; safe to leave blank for fresh flows.
    /// </summary>
    public string FetchedEntriesJson
    {
        get => _fetchedEntriesJson;
        set => _fetchedEntriesJson = value ?? string.Empty;
    }

    /// <summary>
    /// Hidden form field carrying the generated Step-2 prompt between submits so a Step-3
    /// render (including a restored session) keeps the prompt available for display and zip
    /// download without rebuilding it — the rebuild needs a reference-deck selection that a
    /// restored request may not carry. Empty by default; safe to leave blank for fresh flows.
    /// </summary>
    public string MetaGapPromptText
    {
        get => _metaGapPromptText;
        set => _metaGapPromptText = value ?? string.Empty;
    }

    /// <summary>
    /// Reconciles the split deck-input fields with the canonical <see cref="DeckSource"/> value.
    /// </summary>
    public void NormalizeDeckSource()
    {
        (DeckInputSource, DeckUrl, DeckText, DeckSource) =
            DeckInputReconciler.Reconcile(DeckInputSource, DeckUrl, DeckText, DeckSource);
    }
}
