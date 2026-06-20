namespace DeckFlow.Web.Models.Api;

/// <summary>
/// Request payload for the development-only analysis-prompt API. Lets a headless caller
/// (test harness, CLI, Codex) generate the same deck-analysis prompt the <c>/deck-analysis</c>
/// page produces, without driving the Razor UI. Supply either <see cref="DeckUrl"/> or
/// <see cref="DeckText"/>; the URL takes precedence when both are present.
/// </summary>
public sealed class AnalysisPromptApiRequest
{
    /// <summary>Public Moxfield/Archidekt deck URL. Takes precedence over <see cref="DeckText"/> when set.</summary>
    public string? DeckUrl { get; init; }

    /// <summary>Pasted deck export text, used when <see cref="DeckUrl"/> is blank.</summary>
    public string? DeckText { get; init; }

    /// <summary>Magic: The Gathering format; defaults to "Commander" when blank.</summary>
    public string? Format { get; init; }

    /// <summary>Optional deck name; derived when blank.</summary>
    public string? DeckName { get; init; }

    /// <summary>Optional target Commander bracket label the AI should grade against.</summary>
    public string? TargetCommanderBracket { get; init; }

    /// <summary>Target AI platform variant: "ChatGPT" (default), "Claude", or "Gemini".</summary>
    public string? TargetAiPlatform { get; init; }

    /// <summary>Optional analysis-question identifiers to include in the prompt.</summary>
    public IReadOnlyList<string>? SelectedAnalysisQuestions { get; init; }

    /// <summary>When true, includes sideboard/maybeboard cards as candidate references.</summary>
    public bool IncludeCandidateReferencesInAnalysis { get; init; }
}

/// <summary>
/// Response payload for the development-only analysis-prompt API. Carries the generated
/// prompt text and the supporting artifact strings the page would otherwise render.
/// </summary>
/// <param name="SuggestedChatTitle">Suggested chat title line.</param>
/// <param name="AnalysisPromptText">The generated deck-analysis prompt (primary output).</param>
/// <param name="ReferenceText">Card/mechanic/banned-list reference bundle embedded in the prompt.</param>
/// <param name="DeckProfileSchemaJson">JSON schema the prompt asks the AI to fill.</param>
/// <param name="SetUpgradePromptText">Companion set-upgrade prompt, when produced.</param>
/// <param name="InputSummary">Human-readable summary of the resolved deck input.</param>
/// <param name="ImportWarning">Non-fatal import warning, when present.</param>
/// <param name="PromptCharacterCount">Character count of <paramref name="AnalysisPromptText"/> for quick size checks.</param>
public sealed record AnalysisPromptApiResponse(
    string SuggestedChatTitle,
    string AnalysisPromptText,
    string ReferenceText,
    string DeckProfileSchemaJson,
    string SetUpgradePromptText,
    string InputSummary,
    string? ImportWarning,
    int PromptCharacterCount);
