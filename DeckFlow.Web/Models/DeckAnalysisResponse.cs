using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the deck-analysis prompt; carries the structured deck assessment, weak slots, question answers, and optional deck-version suggestions.
/// </summary>
public sealed class DeckAnalysisResponse
{
    /// <summary>
    /// Format context the prompt used when judging the submitted deck.
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    /// <summary>
    /// Commander identity for the analyzed deck.
    /// </summary>
    [JsonPropertyName("commander")]
    public string Commander { get; init; } = string.Empty;

    /// <summary>
    /// Strategy summary the prompt inferred from the submitted decklist.
    /// </summary>
    [JsonPropertyName("game_plan")]
    public string GamePlan { get; init; } = string.Empty;

    /// <summary>
    /// Primary strategic axes the analysis uses to frame the deck's plan.
    /// </summary>
    [JsonPropertyName("primary_axes")]
    public IReadOnlyList<string> PrimaryAxes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Speed label assigned to the deck after analysis.
    /// </summary>
    [JsonPropertyName("speed")]
    public string Speed { get; init; } = string.Empty;

    /// <summary>
    /// Estimated turn the deck can realistically convert into a win.
    /// </summary>
    [JsonPropertyName("estimated_win_turn")]
    public int EstimatedWinTurn { get; init; }

    /// <summary>
    /// Whether the prompt had enough evidence to answer the win-turn question directly.
    /// </summary>
    [JsonPropertyName("can_answer_win_turn")]
    public bool CanAnswerWinTurn { get; init; }

    /// <summary>
    /// Commander bracket the prompt assigned to the deck.
    /// </summary>
    [JsonPropertyName("assessed_bracket")]
    public string AssessedBracket { get; init; } = string.Empty;

    /// <summary>
    /// Explanation for the assessed bracket shown with the bracket label.
    /// </summary>
    [JsonPropertyName("bracket_justification")]
    public string BracketJustification { get; init; } = string.Empty;

    /// <summary>
    /// Strengths the prompt identified in the submitted deck.
    /// </summary>
    [JsonPropertyName("strengths")]
    public IReadOnlyList<string> Strengths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Weaknesses the prompt identified in the submitted deck.
    /// </summary>
    [JsonPropertyName("weaknesses")]
    public IReadOnlyList<string> Weaknesses { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Needs the prompt says the deck must address to improve.
    /// </summary>
    [JsonPropertyName("deck_needs")]
    public IReadOnlyList<string> DeckNeeds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Individual card slots the prompt considers weak enough to call out.
    /// </summary>
    [JsonPropertyName("weak_slots")]
    public IReadOnlyList<WeakSlot> WeakSlots { get; init; } = Array.Empty<WeakSlot>();

    /// <summary>
    /// Synergy tags the prompt inferred for downstream categorization and review.
    /// </summary>
    [JsonPropertyName("synergy_tags")]
    public IReadOnlyList<string> SynergyTags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Answers to the judge-question checklist included in the analysis prompt.
    /// </summary>
    [JsonPropertyName("question_answers")]
    public IReadOnlyList<QuestionAnswer> QuestionAnswers { get; init; } = Array.Empty<QuestionAnswer>();

    /// <summary>
    /// Alternate deck versions the prompt proposed as possible upgrade paths.
    /// </summary>
    [JsonPropertyName("deck_versions")]
    public IReadOnlyList<DeckVersion> DeckVersions { get; init; } = Array.Empty<DeckVersion>();
}

/// <summary>
/// A single weak-slot entry in the deck-analysis response: the card the AI flagged plus the reason it's considered weak.
/// </summary>
public sealed class WeakSlot
{
    /// <summary>
    /// Card name for the slot the prompt considers weak.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Reason the prompt flagged this card as a weak slot.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// A single question/answer pair from the deck-analysis response, including the question number, original question text, the AI's answer, and the basis it cites.
/// </summary>
public sealed class QuestionAnswer
{
    /// <summary>
    /// One-based question number from the judge-question checklist.
    /// </summary>
    [JsonPropertyName("question_number")]
    public int QuestionNumber { get; init; }

    /// <summary>
    /// Original question text answered by the prompt.
    /// </summary>
    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Prompt answer to the checklist question.
    /// </summary>
    [JsonPropertyName("answer")]
    public string Answer { get; init; } = string.Empty;

    /// <summary>
    /// Evidence or reasoning the prompt cites for the answer.
    /// </summary>
    [JsonPropertyName("basis")]
    public string Basis { get; init; } = string.Empty;
}

/// <summary>
/// One alternate deck version the AI proposed during analysis: its name, the full decklist, and the diff of cards added and cut versus the input deck.
/// </summary>
public sealed class DeckVersion
{
    /// <summary>
    /// Label for the alternate deck version proposed by the prompt.
    /// </summary>
    [JsonPropertyName("version_name")]
    public string VersionName { get; init; } = string.Empty;

    /// <summary>
    /// Full decklist text for the proposed version.
    /// </summary>
    [JsonPropertyName("decklist")]
    public string Decklist { get; init; } = string.Empty;

    /// <summary>
    /// Cards added to reach this proposed version.
    /// </summary>
    [JsonPropertyName("cards_added")]
    public IReadOnlyList<string> CardsAdded { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Cards cut to reach this proposed version.
    /// </summary>
    [JsonPropertyName("cards_cut")]
    public IReadOnlyList<string> CardsCut { get; init; } = Array.Empty<string>();
}
