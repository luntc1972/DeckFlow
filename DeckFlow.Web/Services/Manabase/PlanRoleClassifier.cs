using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Classifies a spell into its win-directed <see cref="PlanRole"/>s for the "plan presence" opener
/// read. Pure: the caller (<see cref="ManabaseAnalysisService"/>) does the I/O — it fetches each
/// card's crowd-sourced categories and the Commander Spellbook combo-piece set — and passes them in.
/// Source precedence is FIRST-HIT-WINS per the locked plan-presence decisions:
/// <list type="number">
/// <item>crowd categories (a card's Archidekt category tags → role);</item>
/// <item>Commander Spellbook combo piece → <see cref="PlanRole.TutorCombo"/>;</item>
/// <item>an oracle-text heuristic fallback (<see cref="DeckStatClassifier"/>).</item>
/// </list>
/// Ramp, lands, and filler card draw deliberately never earn a role — that is resource/velocity, a
/// different axis already measured by keepable-% and on-curve castability.
/// </summary>
public static class PlanRoleClassifier
{
    /// <summary>
    /// Resolve a card's plan roles. Categories win when they map to any role; otherwise a known combo
    /// piece is <see cref="PlanRole.TutorCombo"/>; otherwise the oracle-text heuristic decides. Returns
    /// <see cref="PlanRole.None"/> for a pure resource/ramp/filler card.
    /// </summary>
    /// <param name="fact">The resolved card (type line + oracle text drive the heuristic fallback).</param>
    /// <param name="categories">The card's crowd-sourced category tags (free text; may be empty).</param>
    /// <param name="isComboPiece">True when Commander Spellbook lists the card in an included combo.</param>
    public static PlanRole Classify(CardFact fact, IReadOnlyList<string> categories, bool isComboPiece)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(categories);

        PlanRole fromCategories = FromCategories(categories);
        if (fromCategories != PlanRole.None)
        {
            return fromCategories;
        }

        if (isComboPiece)
        {
            return PlanRole.TutorCombo;
        }

        return FromHeuristic(fact);
    }

    /// <summary>
    /// Map a card's free-text category tags to roles by keyword. User-typed Archidekt tags are not a
    /// controlled vocabulary, so this is substring matching over the common role words, not an exact
    /// switch. A card tagged both "Win Condition" and "Card Draw" earns Payoff | Engine. Ramp / land /
    /// fixing tags contribute nothing.
    /// </summary>
    public static PlanRole FromCategories(IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        PlanRole roles = PlanRole.None;
        foreach (string category in categories)
        {
            string c = category.ToLowerInvariant();

            if (Has(c, "win", "finisher", "payoff", "wincon", "win con", "win-con", "closer", "beater"))
            {
                roles |= PlanRole.Payoff;
            }

            if (Has(c, "tutor", "combo"))
            {
                roles |= PlanRole.TutorCombo;
            }

            if (Has(c, "removal", "interaction", "counter", "protect", "wipe", "answer"))
            {
                roles |= PlanRole.Interaction;
            }

            // Draw/advantage/engine tags earn Engine — but only when the tag is NOT itself a ramp/mana
            // tag (e.g. "mana ramp / card draw" would already be split into separate tags upstream).
            if (Has(c, "engine", "advantage", "card draw", "value") || (Has(c, "draw") && !Has(c, "ramp", "mana")))
            {
                roles |= PlanRole.Engine;
            }
        }

        return roles;
    }

    /// <summary>
    /// Oracle-text heuristic fallback for a card with no useful category tags and not a known combo
    /// piece. Reuses the shared <see cref="DeckStatClassifier"/> signals. Engine requires a PERMANENT
    /// draw source (repeatable) — a one-shot instant/sorcery "draw two" is filler, not an engine, and
    /// stays None.
    /// </summary>
    public static PlanRole FromHeuristic(CardFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);

        string typeLine = fact.TypeLine;
        string oracle = fact.OracleText ?? fact.FrontFaceOracleText ?? string.Empty;

        PlanRole roles = PlanRole.None;

        if (DeckStatClassifier.IsClosingPowerCard(typeLine, oracle))
        {
            roles |= PlanRole.Payoff;
        }

        if (DeckStatClassifier.IsTutorCard(oracle))
        {
            roles |= PlanRole.TutorCombo;
        }

        if (DeckStatClassifier.IsInteractionCard(typeLine, oracle)
            || DeckStatClassifier.IsBoardWipeCard(oracle)
            || DeckStatClassifier.IsCounterspellCard(oracle)
            || DeckStatClassifier.IsTargetedRemovalCard(typeLine, oracle))
        {
            roles |= PlanRole.Interaction;
        }

        // Repeatable draw only: a permanent that draws is an engine; a one-shot instant/sorcery draw
        // is filler velocity (excluded, per the locked "filler draw never qualifies" decision).
        bool isSpellOnTheStack = typeLine.Contains("Instant", StringComparison.OrdinalIgnoreCase)
            || typeLine.Contains("Sorcery", StringComparison.OrdinalIgnoreCase);
        if (DeckStatClassifier.IsDrawCard(oracle) && !isSpellOnTheStack)
        {
            roles |= PlanRole.Engine;
        }

        return roles;
    }

    private static bool Has(string haystack, params string[] needles)
    {
        foreach (string needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
