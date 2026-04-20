using System.Text.Json;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Parses ChatGPT JSON responses for the deck-analysis and set-upgrade workflows.
/// Pure helpers — no side effects, no I/O, safe to call from anywhere.
/// </summary>
internal static class ChatGptResponseParsers
{
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ChatGptDeckAnalysisResponse ParseAnalysisResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the deck_profile JSON returned from ChatGPT into Step 3.");
        }

        var json = ChatGptJsonTextFormatterService.ExtractJsonPayload(input);
        using var document = JsonDocument.Parse(json);

        var payload = document.RootElement;
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("deck_profile", out var profileElement))
        {
            payload = profileElement;
        }

        if (payload.ValueKind != JsonValueKind.Object || !LooksLikeDeckProfile(payload))
        {
            throw new InvalidOperationException("The submitted ChatGPT response did not contain a valid deck_profile payload.");
        }

        var result = JsonSerializer.Deserialize<ChatGptDeckAnalysisResponse>(payload.GetRawText(), DeserializerOptions);
        if (result is null || !HasMeaningfulDeckProfileContent(result))
        {
            throw new InvalidOperationException("The submitted ChatGPT response did not contain a valid deck_profile payload.");
        }

        return result;
    }

    public static ChatGptSetUpgradeResponse ParseSetUpgradeResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Paste the set_upgrade_report JSON returned from ChatGPT into Step 5.");
        }

        var json = ChatGptJsonTextFormatterService.ExtractJsonPayload(input);
        using var document = JsonDocument.Parse(json);

        var payload = document.RootElement;
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("set_upgrade_report", out var reportElement))
        {
            payload = reportElement;
        }

        if (payload.ValueKind != JsonValueKind.Object || !LooksLikeSetUpgradeReport(payload))
        {
            throw new InvalidOperationException("The submitted ChatGPT response did not contain a valid set_upgrade_report payload.");
        }

        var result = JsonSerializer.Deserialize<ChatGptSetUpgradeResponse>(payload.GetRawText(), DeserializerOptions);
        if (result is null || !HasMeaningfulSetUpgradeContent(result))
        {
            throw new InvalidOperationException("The submitted ChatGPT response did not contain a valid set_upgrade_report payload.");
        }

        return result;
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

    private static bool HasMeaningfulDeckProfileContent(ChatGptDeckAnalysisResponse response)
        => !string.IsNullOrWhiteSpace(response.Format)
            || !string.IsNullOrWhiteSpace(response.Commander)
            || !string.IsNullOrWhiteSpace(response.GamePlan)
            || !string.IsNullOrWhiteSpace(response.Speed)
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

    private static bool HasMeaningfulSetUpgradeContent(ChatGptSetUpgradeResponse response)
        => response.Sets.Count > 0
            || (response.FinalShortlist is not null
                && (response.FinalShortlist.MustTest.Any(HasMeaningfulTopAdd)
                    || response.FinalShortlist.Optional.Any(HasMeaningfulTopAdd)
                    || response.FinalShortlist.Skip.Any(card => !string.IsNullOrWhiteSpace(card))));

    private static bool HasMeaningfulTopAdd(ChatGptSetUpgradeTopAdd add)
        => !string.IsNullOrWhiteSpace(add.Card)
            || !string.IsNullOrWhiteSpace(add.Reason)
            || !string.IsNullOrWhiteSpace(add.SuggestedCut)
            || !string.IsNullOrWhiteSpace(add.CutReason);
}
