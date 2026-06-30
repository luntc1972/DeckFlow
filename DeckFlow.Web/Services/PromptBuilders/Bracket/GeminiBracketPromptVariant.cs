// Why: ADR-0001 — bracket prompt variants are intentionally decoupled; do not extract shared text.
using System.Text;
using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

/// <summary>
/// Builds a bracket classification and optional balancer prompt body formatted for Gemini.
/// Uses markdown persona-scaffold framing per Gemini's prompt contract.
/// </summary>
internal sealed class GeminiBracketPromptVariant : IBracketPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Gemini;

    /// <inheritdoc/>
    public string Build(
        BracketClassification classification,
        int? targetBracketNumber,
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(catalog);

        var builder = new StringBuilder();
        var classifiedTier = tiers.FirstOrDefault(t => t.Number == classification.BracketNumber);
        var tierName = classifiedTier?.Name ?? $"Bracket {classification.BracketNumber}";
        var tierLabel = classifiedTier?.Label ?? $"Bracket {classification.BracketNumber}";

        // ── Classification block (always present) ───────────────────────────

        builder.AppendLine("You are an expert Commander deck analyst. Review the following bracket classification and provide guidance.");
        builder.AppendLine();
        builder.AppendLine("Think carefully through the evidence before responding.");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(deckName))
        {
            builder.AppendLine($"## Bracket Classification: {deckName}");
        }
        else
        {
            builder.AppendLine("## Bracket Classification");
        }

        builder.AppendLine();
        builder.AppendLine($"This deck classifies as **{tierLabel}** ({tierName}).");
        builder.AppendLine();

        builder.AppendLine("## WHY THIS BRACKET");
        builder.AppendLine();
        AppendClassificationReasons(builder, classification, tiers);
        builder.AppendLine();

        // Effective-date stamp (BRACKET-05) — hand-coded per ADR-0001
        builder.AppendLine($"Game Changers list effective {classification.EffectiveDate}. " +
            "Re-confirm Game Changers membership before suggesting swaps.");
        builder.AppendLine();

        // Combo-unavailable disclosure (BRACKET-03, T-76-07) — hand-coded per ADR-0001
        if (!classification.ComboDetectionAvailable)
        {
            builder.AppendLine("Note: combo detection was temporarily unavailable. " +
                "A two-card win combo could place this deck a bracket higher than shown — " +
                "please double-check for combos.");
            builder.AppendLine();
        }

        // ── Balancer block (only when target is below classified bracket) ───

        if (targetBracketNumber is int target && classification.BracketNumber > target)
        {
            var targetTier = tiers.FirstOrDefault(t => t.Number == target);
            var targetTierName = targetTier?.Name ?? $"Bracket {target}";
            var violations = targetTier is null ? null : classification.FloorViolations(targetTier);

            builder.AppendLine($"## FLOOR VIOLATIONS — cards that exceed B{target} {targetTierName}");
            builder.AppendLine();
            if (violations is not null) AppendFloorViolations(builder, targetTier!, violations);
            builder.AppendLine();

            builder.AppendLine($"## STARTER CUTS to reach B{target} {targetTierName}");
            builder.AppendLine();
            if (violations is not null) AppendStarterCuts(builder, targetTier!, violations);
            builder.AppendLine();
            builder.AppendLine("A starting point, not a verdict — use an AI to turn these into power-equivalent swaps.");
        }

        return builder.ToString();
    }

    private static void AppendClassificationReasons(
        StringBuilder builder,
        BracketClassification classification,
        IReadOnlyList<BracketTier> tiers)
    {
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

        var mldCount = classification.DetectedMassLandDenial.Count;
        if (mldCount > 0)
        {
            foreach (var mld in classification.DetectedMassLandDenial)
            {
                builder.AppendLine($"- 1 mass land denial: {mld}.");
            }
        }
        else
        {
            builder.AppendLine("- 0 mass land denial.");
        }

        var extraTurnCount = classification.DetectedExtraTurnCards.Count;
        builder.AppendLine($"- {extraTurnCount} extra-turn card{(extraTurnCount != 1 ? "s" : "")} (informational only; does not affect bracket number).");
        // Why: FIX A — no separate tutor-count gate; the October 2025 update dropped the old
        // "count your tutors" rule, but specific high-impact tutors remain on the Game Changers
        // list and DO count toward the GC total.
        builder.AppendLine("- No separate tutor-count gate: the October 2025 update dropped the old tutor-density rule, but specific powerful tutors (Demonic Tutor, Vampiric Tutor, Worldly Tutor, etc.) remain on the official Game Changers list and still count as Game Changers.");
    }

    // Why: FIX C — tier-aware violations via the FloorViolations domain method.
    // Extra-turn cards are never listed (FIX B) — FloorViolationSet excludes them by design.
    private static void AppendFloorViolations(
        StringBuilder builder,
        BracketTier targetTier,
        FloorViolationSet violations)
    {
        // B5→B4 via cEDH heuristic: emit a count advisory, not per-card GC violations.
        if (violations.IsCedhCountAdvisory)
        {
            builder.AppendLine($"- {violations.GameChangerCount} Game Changers [Trim below {BracketRubricThresholds.CedhGameChangerCount} to reach B4]");
        }

        foreach (var gc in violations.GameChangerViolations)
        {
            builder.AppendLine($"- {gc} [Game Changer]");
        }

        foreach (var combo in violations.ComboViolations)
        {
            foreach (var card in combo.CardNames)
            {
                builder.AppendLine($"- {card} [Combo half]");
            }
        }

        foreach (var mld in violations.MldViolations)
        {
            builder.AppendLine($"- {mld} [Mass land denial]");
        }
    }

    private static void AppendStarterCuts(
        StringBuilder builder,
        BracketTier targetTier,
        FloorViolationSet violations)
    {
        // FIX C: cEDH count advisory (B5→B4 via GC heuristic; B4 is uncapped so per-GC
        // violations are not listed — only trim the count below CedhGameChangerCount to exit
        // the B5 auto-classification).
        if (violations.IsCedhCountAdvisory)
        {
            builder.AppendLine(
                $"- Trim Game Changers below {BracketRubricThresholds.CedhGameChangerCount} (currently {violations.GameChangerCount}) to drop from B5 to B4.");
        }

        // Combo cuts (tier-aware: only populated when target is below B4).
        foreach (var combo in violations.ComboViolations)
        {
            var half1 = combo.CardNames.Count > 0 ? combo.CardNames[0] : "combo piece";
            var half2 = combo.CardNames.Count > 1 ? combo.CardNames[1] : "combo piece";
            builder.AppendLine($"- Cut {half1} or {half2} — breaks the two-card win combo.");
        }

        // MLD cuts (tier-aware: only populated when target is below B4).
        foreach (var mld in violations.MldViolations)
        {
            builder.AppendLine($"- Cut {mld} — no mass land denial at B{targetTier.Number}.");
        }

        // GC excess cuts (only when target has a cap and GCs exceed it).
        if (violations.GameChangerViolations.Count > 0 && targetTier.MaxGameChangers >= 0)
        {
            var excess = violations.GameChangerCount - targetTier.MaxGameChangers;
            var gcList = string.Join(", ", violations.GameChangerViolations);
            builder.AppendLine(
                $"- Trim {excess} of: {gcList} — B{targetTier.Number} allows up to {targetTier.MaxGameChangers} Game Changer{(targetTier.MaxGameChangers != 1 ? "s" : "")}; you run {violations.GameChangerCount}.");
        }
    }
}
