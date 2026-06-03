using System.Text.Json;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Parses the analysis and set-upgrade JSON returns from the AI into the strongly-typed response shapes used by the deck-analysis page. Pure helpers — no side effects, no I/O, safe to call from anywhere.
/// </summary>
internal static class ResponseParsers
{
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal const string TruncatedResponseMessage =
        "The pasted response appears truncated — wait for the AI to finish generating before copying, then re-submit.";

    public static DeckAnalysisResponse ParseAnalysisResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the deck_profile JSON returned from your AI into Step 3.");
        }

        var json = JsonTextFormatterService.ExtractJsonPayload(input);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(TruncatedResponseMessage);
        }

        using (document)
        {
            var payload = document.RootElement;
            if (payload.ValueKind == JsonValueKind.Object
                && payload.TryGetProperty("deck_profile", out var profileElement))
            {
                payload = profileElement;
            }

            if (payload.ValueKind != JsonValueKind.Object || !LooksLikeDeckProfile(payload))
            {
                throw new InvalidOperationException("The submitted AI response did not contain a valid deck_profile payload.");
            }

            var result = JsonSerializer.Deserialize<DeckAnalysisResponse>(payload.GetRawText(), DeserializerOptions);
            if (result is null || !HasMeaningfulDeckProfileContent(result))
            {
                throw new InvalidOperationException("The submitted AI response did not contain a valid deck_profile payload.");
            }

            return result;
        }
    }

    public static SetUpgradeResponse ParseSetUpgradeResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the set_upgrade_report JSON returned from your AI into Step 5.");
        }

        var json = JsonTextFormatterService.ExtractJsonPayload(input);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(TruncatedResponseMessage);
        }

        using (document)
        {
            var payload = document.RootElement;
            if (payload.ValueKind == JsonValueKind.Object
                && payload.TryGetProperty("set_upgrade_report", out var reportElement))
            {
                payload = reportElement;
            }

            if (payload.ValueKind != JsonValueKind.Object || !LooksLikeSetUpgradeReport(payload))
            {
                throw new InvalidOperationException("The submitted AI response did not contain a valid set_upgrade_report payload.");
            }

            var result = JsonSerializer.Deserialize<SetUpgradeResponse>(payload.GetRawText(), DeserializerOptions);
            if (result is null || !HasMeaningfulSetUpgradeContent(result))
            {
                throw new InvalidOperationException("The submitted AI response did not contain a valid set_upgrade_report payload.");
            }

            return result;
        }
    }

    private static bool LooksLikeDeckProfile(JsonElement payload)
    {
        string[] knownProperties =
        [
            "format",
            "commander",
            "game_plan",
            "primary_axes",
            "speed",
            "estimated_win_turn",
            "can_answer_win_turn",
            "assessed_bracket",
            "bracket_justification",
            "strengths",
            "weaknesses",
            "deck_needs",
            "weak_slots",
            "synergy_tags",
            "question_answers",
            "deck_versions"
        ];

        return knownProperties.Any(propertyName => payload.TryGetProperty(propertyName, out _));
    }

    private static bool HasMeaningfulDeckProfileContent(DeckAnalysisResponse response)
        => !string.IsNullOrWhiteSpace(response.Format)
            || !string.IsNullOrWhiteSpace(response.Commander)
            || !string.IsNullOrWhiteSpace(response.GamePlan)
            || !string.IsNullOrWhiteSpace(response.Speed)
            || !string.IsNullOrWhiteSpace(response.AssessedBracket)
            || !string.IsNullOrWhiteSpace(response.BracketJustification)
            || response.PrimaryAxes.Count > 0
            || response.Strengths.Count > 0
            || response.Weaknesses.Count > 0
            || response.DeckNeeds.Count > 0
            || response.WeakSlots.Count > 0
            || response.SynergyTags.Count > 0
            || response.QuestionAnswers.Count > 0
            || response.DeckVersions.Count > 0;

    private static bool LooksLikeSetUpgradeReport(JsonElement payload)
    {
        string[] knownProperties = ["sets", "final_shortlist"];
        return knownProperties.Any(propertyName => payload.TryGetProperty(propertyName, out _));
    }

    private static bool HasMeaningfulSetUpgradeContent(SetUpgradeResponse response)
        => response.Sets.Count > 0
            || (response.FinalShortlist is not null
                && (response.FinalShortlist.MustTest.Any(HasMeaningfulTopAdd)
                    || response.FinalShortlist.Optional.Any(HasMeaningfulTopAdd)
                    || response.FinalShortlist.Skip.Any(card => !string.IsNullOrWhiteSpace(card))));

    private static bool HasMeaningfulTopAdd(SetUpgradeTopAdd add)
        => !string.IsNullOrWhiteSpace(add.Card)
            || !string.IsNullOrWhiteSpace(add.Reason)
            || !string.IsNullOrWhiteSpace(add.SuggestedCut)
            || !string.IsNullOrWhiteSpace(add.CutReason);
}
