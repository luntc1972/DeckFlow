using System.Globalization;
using System.Text;

namespace DeckFlow.Web.Services.CommanderCategoryNorms;

internal static class CommanderCategoryNormsBlock
{
    internal const int MinimumDeckCount = 10;
    internal const int MediumConfidenceMinimumDeckCount = 50;
    internal const int HighConfidenceMinimumDeckCount = 250;
    internal const int MaxCategories = 15;
    internal const int MaxCategoryLabelLength = 60;
    internal const int MaxHarvestKeyLength = 200;
    internal const string Title = "HARVESTED COMMANDER CATEGORY NORMS";

    // Why: D-06 defines tiers once; rule lines and tests use these constants only.
    internal static string ConfidenceTier(int deckCount) => deckCount >= HighConfidenceMinimumDeckCount ? "HIGH" : deckCount >= MediumConfidenceMinimumDeckCount ? "MEDIUM" : "LOW";

    internal static string? Build(CommanderCategoryNormsResult norms, bool multiCommanderDeck)
    {
        ArgumentNullException.ThrowIfNull(norms);
        if (norms.DeckCount < MinimumDeckCount) return null;
        var key = SanitizeText(norms.HarvestKey, MaxHarvestKeyLength);
        if (key.Length == 0) return null;
        var count = norms.DeckCount.ToString(CultureInfo.InvariantCulture);
        var lines = new List<string>();
        foreach (var category in norms.Categories)
        {
            var label = SanitizeText(category.Category, MaxCategoryLabelLength);
            if (label.Length == 0 || !double.IsFinite(category.DeckShare)) continue;
            var percent = Math.Clamp(Math.Round(category.DeckShare * 100, MidpointRounding.AwayFromZero), 0, 100);
            lines.Add(percent == 0 ? $"- {label} - in under 1% of {count} decks" : $"- {label} - in {percent.ToString(CultureInfo.InvariantCulture)}% of {count} decks");
            if (lines.Count == MaxCategories) break;
        }
        if (lines.Count == 0) return null;
        var tier = ConfidenceTier(norms.DeckCount);
        var source = multiCommanderDeck
            ? $"Source: {count} harvested decks keyed under {key}, the alphabetically first commander in this deck; they include every partner or background pairing harvested under that name."
            : $"Source: {count} harvested decks with {key} as commander.";
        source += $" Confidence tiers: LOW {MinimumDeckCount}-{MediumConfidenceMinimumDeckCount - 1} decks, MEDIUM {MediumConfidenceMinimumDeckCount}-{HighConfidenceMinimumDeckCount - 1} decks, HIGH {HighConfidenceMinimumDeckCount}+ decks.";
        return string.Join(Environment.NewLine, new[] { $"{Title} - {count} decks ({tier} confidence)", source }.Concat(lines));
    }

    // Why: T-05-01 harvested labels are third-party text pasted into an LLM prompt.
    private static string SanitizeText(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (ch is '<' or '>') continue;
            if (char.IsControl(ch) || char.IsWhiteSpace(ch)) { pendingSpace = builder.Length > 0; continue; }
            if (pendingSpace) { builder.Append(' '); pendingSpace = false; }
            builder.Append(ch);
        }
        var text = builder.ToString().Trim();
        if (text.Length <= max) return text;
        text = text[..max];
        if (text.Length > 0 && char.IsHighSurrogate(text[^1])) text = text[..^1];
        return text.Trim();
    }
}
