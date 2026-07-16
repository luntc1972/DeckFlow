namespace DeckFlow.Web.Models;

/// <summary>
/// Form-bound request DTO for the deck-comparison page; captures two deck inputs (URL or pasted text), per-deck commander bracket, AI-platform selection, and the round-tripped comparison response state used across workflow steps.
/// </summary>
public sealed class DeckComparisonRequest
{
    private string _deckASource = string.Empty;
    private string _deckBSource = string.Empty;
    private string _deckAUrl = string.Empty;
    private string _deckAText = string.Empty;
    private string _deckBUrl = string.Empty;
    private string _deckBText = string.Empty;
    private string _deckAName = string.Empty;
    private string _deckBName = string.Empty;
    private string _deckABracket = string.Empty;
    private string _deckBBracket = string.Empty;
    private string _comparisonResponseJson = string.Empty;
    private string _targetAiPlatform = "ChatGPT";

    /// <summary>
    /// Tracks the current step in the multi-step deck-comparison workflow.
    /// </summary>
    public int WorkflowStep { get; set; } = 1;

    /// <summary>
    /// Raw input for deck A (public URL or pasted export text).
    /// </summary>
    public string DeckASource
    {
        get => _deckASource;
        set => _deckASource = value ?? string.Empty;
    }

    /// <summary>
    /// Selects whether deck A is supplied via a public URL or pasted export text.
    /// </summary>
    public DeckInputSource DeckAInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>
    /// Public deck URL used when <see cref="DeckAInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.
    /// </summary>
    public string DeckAUrl
    {
        get => _deckAUrl;
        set => _deckAUrl = value ?? string.Empty;
    }

    /// <summary>
    /// Pasted deck export text used when <see cref="DeckAInputSource"/> is <see cref="DeckInputSource.PasteText"/>.
    /// </summary>
    public string DeckAText
    {
        get => _deckAText;
        set => _deckAText = value ?? string.Empty;
    }

    /// <summary>
    /// Raw input for deck B (public URL or pasted export text).
    /// </summary>
    public string DeckBSource
    {
        get => _deckBSource;
        set => _deckBSource = value ?? string.Empty;
    }

    /// <summary>
    /// Selects whether deck B is supplied via a public URL or pasted export text.
    /// </summary>
    public DeckInputSource DeckBInputSource { get; set; } = DeckInputSource.PublicUrl;

    /// <summary>
    /// Public deck URL used when <see cref="DeckBInputSource"/> is <see cref="DeckInputSource.PublicUrl"/>.
    /// </summary>
    public string DeckBUrl
    {
        get => _deckBUrl;
        set => _deckBUrl = value ?? string.Empty;
    }

    /// <summary>
    /// Pasted deck export text used when <see cref="DeckBInputSource"/> is <see cref="DeckInputSource.PasteText"/>.
    /// </summary>
    public string DeckBText
    {
        get => _deckBText;
        set => _deckBText = value ?? string.Empty;
    }

    /// <summary>
    /// Optional user-supplied display name for deck A.
    /// </summary>
    public string DeckAName
    {
        get => _deckAName;
        set => _deckAName = value ?? string.Empty;
    }

    /// <summary>
    /// Optional user-supplied display name for deck B.
    /// </summary>
    public string DeckBName
    {
        get => _deckBName;
        set => _deckBName = value ?? string.Empty;
    }

    /// <summary>
    /// Target Commander bracket for deck A (e.g., casual / focused / cEDH).
    /// </summary>
    public string DeckABracket
    {
        get => _deckABracket;
        set => _deckABracket = value ?? string.Empty;
    }

    /// <summary>
    /// Target Commander bracket for deck B (e.g., casual / focused / cEDH).
    /// </summary>
    public string DeckBBracket
    {
        get => _deckBBracket;
        set => _deckBBracket = value ?? string.Empty;
    }

    /// <summary>
    /// Serialized comparison-response JSON round-tripped between workflow steps and through the comparison artifact zip.
    /// </summary>
    public string ComparisonResponseJson
    {
        get => _comparisonResponseJson;
        set => _comparisonResponseJson = value ?? string.Empty;
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
    /// Reconciles the split deck-input fields with the canonical deck-source values.
    /// </summary>
    public void NormalizeDeckSources()
    {
        (DeckAInputSource, DeckAUrl, DeckAText, DeckASource) =
            DeckInputReconciler.Reconcile(DeckAInputSource, DeckAUrl, DeckAText, DeckASource);
        (DeckBInputSource, DeckBUrl, DeckBText, DeckBSource) =
            DeckInputReconciler.Reconcile(DeckBInputSource, DeckBUrl, DeckBText, DeckBSource);
    }
}
