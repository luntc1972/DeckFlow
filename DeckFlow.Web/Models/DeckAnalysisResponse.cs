using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the deck-analysis prompt; carries the structured deck assessment, weak slots, question answers, and optional deck-version suggestions.
/// </summary>
public sealed class DeckAnalysisResponse
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("commander")]
    public string Commander { get; init; } = string.Empty;

    [JsonPropertyName("game_plan")]
    public string GamePlan { get; init; } = string.Empty;

    [JsonPropertyName("primary_axes")]
    public IReadOnlyList<string> PrimaryAxes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("speed")]
    public string Speed { get; init; } = string.Empty;

    [JsonPropertyName("estimated_win_turn")]
    public int EstimatedWinTurn { get; init; }

    [JsonPropertyName("can_answer_win_turn")]
    public bool CanAnswerWinTurn { get; init; }

    [JsonPropertyName("assessed_bracket")]
    public string AssessedBracket { get; init; } = string.Empty;

    [JsonPropertyName("bracket_justification")]
    public string BracketJustification { get; init; } = string.Empty;

    [JsonPropertyName("strengths")]
    public IReadOnlyList<string> Strengths { get; init; } = Array.Empty<string>();

    [JsonPropertyName("weaknesses")]
    public IReadOnlyList<string> Weaknesses { get; init; } = Array.Empty<string>();

    [JsonPropertyName("deck_needs")]
    public IReadOnlyList<string> DeckNeeds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("weak_slots")]
    public IReadOnlyList<WeakSlot> WeakSlots { get; init; } = Array.Empty<WeakSlot>();

    [JsonPropertyName("synergy_tags")]
    public IReadOnlyList<string> SynergyTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("question_answers")]
    public IReadOnlyList<QuestionAnswer> QuestionAnswers { get; init; } = Array.Empty<QuestionAnswer>();

    [JsonPropertyName("deck_versions")]
    public IReadOnlyList<DeckVersion> DeckVersions { get; init; } = Array.Empty<DeckVersion>();
}

/// <summary>
/// A single weak-slot entry in the deck-analysis response: the card the AI flagged plus the reason it's considered weak.
/// </summary>
public sealed class WeakSlot
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// A single question/answer pair from the deck-analysis response, including the question number, original question text, the AI's answer, and the basis it cites.
/// </summary>
public sealed class QuestionAnswer
{
    [JsonPropertyName("question_number")]
    public int QuestionNumber { get; init; }

    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("answer")]
    public string Answer { get; init; } = string.Empty;

    [JsonPropertyName("basis")]
    public string Basis { get; init; } = string.Empty;
}

/// <summary>
/// One alternate deck version the AI proposed during analysis: its name, the full decklist, and the diff of cards added and cut versus the input deck.
/// </summary>
public sealed class DeckVersion
{
    [JsonPropertyName("version_name")]
    public string VersionName { get; init; } = string.Empty;

    [JsonPropertyName("decklist")]
    public string Decklist { get; init; } = string.Empty;

    [JsonPropertyName("cards_added")]
    public IReadOnlyList<string> CardsAdded { get; init; } = Array.Empty<string>();

    [JsonPropertyName("cards_cut")]
    public IReadOnlyList<string> CardsCut { get; init; } = Array.Empty<string>();
}
