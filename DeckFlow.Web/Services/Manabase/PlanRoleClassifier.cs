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
/// different axis already measured by keepable-% and on-curve castability. The <see cref="ManabaseMode"/>
/// tunes one role: a pure counterspell counts as <see cref="PlanRole.Interaction"/> only in
/// <see cref="ManabaseMode.Cedh"/> (it protects the combo turn); in Casual a counter is reactive
/// insurance, not a card that advances the win plan, so it earns nothing. The classifier also exposes
/// a pre-permanent-gate interaction signal for the cEDH early-interaction lens, while leaving the
/// returned plan-presence roles unchanged.
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
    /// <param name="mode">Analysis profile; gates whether a pure counterspell earns Interaction.</param>
    public static PlanRole Classify(CardFact fact, IReadOnlyList<string> categories, bool isComboPiece, ManabaseMode mode)
        => Classify(fact, categories, isComboPiece, mode, out _);

    /// <summary>
    /// Resolve a card's plan roles, while also reporting whether it earned
    /// <see cref="PlanRole.Interaction"/> before the permanent gate strips one-shot instants/sorceries.
    /// The returned value is byte-identical to <see cref="Classify(CardFact, IReadOnlyList{string}, bool, ManabaseMode)"/>.
    /// </summary>
    public static PlanRole Classify(
        CardFact fact,
        IReadOnlyList<string> categories,
        bool isComboPiece,
        ManabaseMode mode,
        out bool interactionMeritPreGate)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(categories);

        // Resolve roles first (categories → combo piece → heuristic, first-hit-wins), THEN apply the
        // permanent gate below to whatever won.
        PlanRole roles;
        PlanRole fromCategories = FromCategories(categories, mode);
        if (fromCategories != PlanRole.None)
        {
            roles = fromCategories;
        }
        else if (isComboPiece)
        {
            roles = PlanRole.TutorCombo;
        }
        else
        {
            roles = FromHeuristic(fact, mode);
        }

        interactionMeritPreGate = roles.HasFlag(PlanRole.Interaction);

        // PERMANENT gate (user decisions 2026-07-09): a hand "has a plan" when it holds a card that
        // advances the win castable on curve. PAYOFF and INTERACTION require a PERMANENT — a one-shot
        // burn/extra-turn finisher (Torment of Hailfire) or a one-shot removal/counter (Swords,
        // Counterspell) leaves nothing on the board, so it is not by itself a plan. TUTORS and CARD-DRAW
        // (TutorCombo / Engine) still count even as instants/sorceries: a sorcery tutor (Demonic Tutor)
        // points at the permanent win, and card advantage furthers the plan. So for a non-permanent front
        // face we strip only the permanent-only roles and keep the rest. (The lower-level
        // FromCategories/FromHeuristic detectors stay type-agnostic; the type rule lives here, at the
        // single service entry.)
        if (CardTypeLine.IsNonPermanentFront(fact.TypeLine))
        {
            roles &= ~PermanentOnlyRoles;
        }

        return roles;
    }

    // Roles that only "count" on a permanent: a board threat (Payoff) and reactive interaction that must
    // stick to matter. TutorCombo and Engine are deliberately absent — they advance the plan even as a
    // one-shot instant/sorcery.
    private const PlanRole PermanentOnlyRoles = PlanRole.Payoff | PlanRole.Interaction;

    /// <summary>
    /// Map a card's free-text category tags to roles by keyword. User-typed Archidekt tags are not a
    /// controlled vocabulary, so this is substring matching over the common role words, not an exact
    /// switch. A card tagged both "Win Condition" and "Card Draw" earns Payoff | Engine. Ramp / land /
    /// fixing tags contribute nothing. A "counter" tag earns Interaction only in cEDH.
    /// </summary>
    /// <param name="categories">The card's crowd-sourced category tags (free text; may be empty).</param>
    /// <param name="mode">Analysis profile; gates whether a "counter" tag earns Interaction.</param>
    public static PlanRole FromCategories(IReadOnlyList<string> categories, ManabaseMode mode)
    {
        ArgumentNullException.ThrowIfNull(categories);

        bool countsCounters = mode == ManabaseMode.Cedh;
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

            if (Has(c, "removal", "interaction", "protect", "wipe", "answer"))
            {
                roles |= PlanRole.Interaction;
            }

            // A counterspell tag advances the plan only in competitive play. In casual a counter is
            // reactive insurance, not a card that furthers the win plan, so it earns no role there.
            if (countsCounters && Has(c, "counter"))
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
    /// <param name="fact">The resolved card (type line + oracle text).</param>
    /// <param name="mode">Analysis profile; gates whether a pure counterspell earns Interaction.</param>
    public static PlanRole FromHeuristic(CardFact fact, ManabaseMode mode)
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

        if (GrantsInteraction(typeLine, oracle, mode))
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

    /// <summary>
    /// Whether a card earns <see cref="PlanRole.Interaction"/> from the oracle-text heuristic. Removal,
    /// board wipes, and non-counter instants always qualify. A pure counterspell — one that counters a
    /// spell and does nothing else — qualifies only in <see cref="ManabaseMode.Cedh"/>: a casual counter
    /// is reactive insurance, not a card that advances the win plan. A card that both counters and
    /// removes still counts in casual (it has removal merit beyond the counter).
    /// </summary>
    private static bool GrantsInteraction(string typeLine, string oracle, ManabaseMode mode)
    {
        // A pure counterspell hits IsInteractionCard (it's an instant / "counter target ...") but in
        // Casual earns nothing: it is reactive insurance, not a card that advances the win plan. cEDH
        // keeps it (it protects the combo turn). A card that ALSO removes still qualifies via the hard-
        // removal checks below (removal merit beyond the counter). Removal / board wipes are checked
        // second so IsInteractionCard short-circuits the extra oracle scans for the common instant case.
        bool interactionMerit = DeckStatClassifier.IsInteractionCard(typeLine, oracle)
            && (mode == ManabaseMode.Cedh || !CountersASpell(oracle));

        return interactionMerit
            || DeckStatClassifier.IsBoardWipeCard(oracle)
            || DeckStatClassifier.IsTargetedRemovalCard(typeLine, oracle);
    }

    // Broader than DeckStatClassifier.IsCounterspellCard (exact "counter target spell" only): also
    // catches narrow-target counters (Negate, Swan Song, Dovin's Veto) so the casual carve-out covers
    // them. Ability-only counters (Stifle) lack "spell" and stay generic interaction.
    private static bool CountersASpell(string oracle)
        => oracle.Contains("counter target", StringComparison.OrdinalIgnoreCase)
            && oracle.Contains("spell", StringComparison.OrdinalIgnoreCase);

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
