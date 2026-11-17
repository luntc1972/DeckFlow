using System.Text;
using DeckFlow.Core.Bracket;

namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

/// <summary>
/// Renders computed classification facts only, shared under the ADR-0001 addendum.
/// </summary>
internal static class BracketClassificationFactsRenderer
{
    /// <summary>
    /// Renders computed facts only; ADR-0001 permits sharing these data-derived lines.
    /// </summary>
    public static string Render(BracketClassification classification, IReadOnlyList<BracketTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(tiers);

        var builder = new StringBuilder();
        var gcCount = classification.DetectedGameChangers.Count;
        var classifiedTier = tiers.FirstOrDefault(t => t.Number == classification.BracketNumber);
        var gcCap = classifiedTier is { MaxGameChangers: >= 0 } ct ? $"B{ct.Number} allows up to {ct.MaxGameChangers}" : $"B{classification.BracketNumber} has no cap";

        builder.AppendLine($"- {gcCount} Game Changer{(gcCount != 1 ? "s" : "")} ({gcCap}).");

        if (classification.TwoCardCombos is { Count: > 0 } combos)
        {
            foreach (var combo in combos)
            {
                builder.AppendLine($"- {combo.CardNames.Count}-card win combo: {string.Join(" + ", combo.CardNames)}.");
            }
        }
        else if (classification.ComboDetectionAvailable)
        {
            builder.AppendLine("- No two-card win combos detected.");
        }

        if (classification.DetectedMassLandDenial.Count == 0)
        {
            builder.AppendLine("- 0 mass land denial.");
        }

        foreach (var mld in classification.DetectedMassLandDenial)
        {
            builder.AppendLine($"- 1 mass land denial: {mld}.");
        }

        var extraTurnCount = classification.DetectedExtraTurnCards.Count;
        builder.AppendLine($"- {extraTurnCount} extra-turn card{(extraTurnCount != 1 ? "s" : "")} (informational only; does not affect bracket number).");

        return builder.ToString();
    }
}
