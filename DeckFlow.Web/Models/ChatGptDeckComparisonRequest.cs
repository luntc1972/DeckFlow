namespace DeckFlow.Web.Models;

public sealed class ChatGptDeckComparisonRequest
{
    private string _deckASource = string.Empty;
    private string _deckBSource = string.Empty;
    private string _deckAName = string.Empty;
    private string _deckBName = string.Empty;
    private string _deckABracket = string.Empty;
    private string _deckBBracket = string.Empty;
    private string _comparisonResponseJson = string.Empty;
    private string _targetAiPlatform = "ChatGPT";

    public int WorkflowStep { get; set; } = 1;

    public string DeckASource
    {
        get => _deckASource;
        set => _deckASource = value ?? string.Empty;
    }

    public string DeckBSource
    {
        get => _deckBSource;
        set => _deckBSource = value ?? string.Empty;
    }

    public string DeckAName
    {
        get => _deckAName;
        set => _deckAName = value ?? string.Empty;
    }

    public string DeckBName
    {
        get => _deckBName;
        set => _deckBName = value ?? string.Empty;
    }

    public string DeckABracket
    {
        get => _deckABracket;
        set => _deckABracket = value ?? string.Empty;
    }

    public string DeckBBracket
    {
        get => _deckBBracket;
        set => _deckBBracket = value ?? string.Empty;
    }

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
        set => _targetAiPlatform = value switch
        {
            "ChatGPT" or "Claude" or "Gemini" => value,
            _ => "ChatGPT"
        };
    }
}
