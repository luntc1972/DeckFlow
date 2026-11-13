using System.Linq;
using System.Text.RegularExpressions;

namespace DeckFlow.Core.Analysis;

/// <summary>
/// Pure static deck-stat classifiers used to tally role counts (ramp, draw, interaction,
/// board wipes, recursion, closing power) in a deck's mainboard.  These are pure CPU domain
/// logic; their inputs (typeLine, oracleText) come from Scryfall card data.
/// </summary>
public static class DeckStatClassifier
{
    // A card-draw effect that benefits YOU (efficacy R2 M7). Matches "draw(s) a/N card(s)" or
    // "draw(s) cards equal to ..." (Greater Good) —
    // imperative ("Draw a card"), activated ("<cost>: Draw a card"), "you (may) draw…", and
    // symmetric wheels ("each player draws seven cards", where you are a player too) — but EXCLUDES:
    //   * draws attributed to an opponent or an indeterminate other player ("target/that/another
    //     player draws", "opponent(s) draw"), which are not card advantage for the caster; and
    //   * draw-as-CONDITION, where the draw is a trigger/replacement rather than an effect
    //     ("whenever/when you draw a card, …" payoffs; "if you would draw a card, …" replacements) —
    //     those cards do not themselves draw. A real ETB draw ("When this enters, draw a card") has
    //     no "you" between the trigger word and "draw", so it is still matched.
    // Handling "draws?" (with the plural s) is the point: a "…draws two cards" card is now seen the
    // same by the v2 land-target credit (IsRepeatableRampOrDraw) and the budget draw count
    // (IsDrawPieceForBudget), instead of one subsystem crediting it while the other ignores it.
    private static readonly Regex YouCardDrawRegex = new(
        @"(?<!(?:opponent|opponents|target player|target opponent|that player|another player|whenever you|when you|would) )\bdraws?\s+(?:(?:a|one|two|three|four|five|six|seven|eight|nine|ten|x|\d+)\s+cards?|cards equal to)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ReminderTextRegex = new(@"\([^)]*\)", RegexOptions.Compiled);

    // Why: broad role-tally ramp signal — intentionally distinct from the tuned, flag-gated
    // Manabase ramp predicates (castability/land math). Do NOT unify the two. See ADR
    // docs/decisions/0003-ramp-classifier-divergence.md.
    /// <summary>
    /// Returns <see langword="true"/> when the card is a ramp source: a land, an explicit
    /// mana-add effect, a mana-symbol producer (mana rocks, dorks, rituals), a land-search,
    /// or a Treasure producer.
    /// </summary>
    /// <param name="typeLine">Card type line (e.g. "Artifact — Treasure").</param>
    /// <param name="oracleText">Raw oracle text; parenthesised reminder text is stripped before matching.</param>
    public static bool IsRampCard(string typeLine, string oracleText)
    {
        if (typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Why: gift-a-Treasure reminder text (for example, Bilbo's Gambit) is not a ramp effect.
        string rulesText = StripReminderText(oracleText);

        return rulesText.Contains("add one mana", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("add two mana", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("{T}: Add", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("Add {", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("search your library for a basic land", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("search your library for up to", StringComparison.OrdinalIgnoreCase) && rulesText.Contains("land", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("Treasure token", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("create a Treasure", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Shared you-anchored literal card-draw predicate: "draw(s) a/N card(s)" or
    /// "draw(s) cards equal to ..." (Greater Good) that benefits YOU, excluding opponent/trigger/replacement
    /// draws. Regex-only (no clue/connive union) so Manabase's Karsten draw term can reuse the exact same
    /// signal without inheriting role-tally extras.
    /// </summary>
    /// <param name="oracleText">Raw oracle text; parenthesised reminder text is stripped before matching.</param>
    internal static bool MatchesYouCardDraw(string oracleText)
    {
        // Strip reminder text so cycling's "(..., Discard this card: Draw a card.)" does not make
        // every cycling card look like literal draw (for example, Raugrin Triome).
        return YouCardDrawRegex.IsMatch(StripReminderText(oracleText));
    }

    /// <summary>
    /// Removes parenthesised reminder text from rules text.
    /// </summary>
    private static string StripReminderText(string text)
        // Most oracle text has no parentheses at all, so skip the regex pass (and its allocation)
        // when there's nothing to strip — this runs per card in the Cut Lab role-tally hot path.
        => text.IndexOf('(', StringComparison.Ordinal) < 0 ? text : ReminderTextRegex.Replace(text, string.Empty);

    /// <summary>
    /// Returns <see langword="true"/> when the card has you-anchored literal card draw (any count),
    /// or is clue/connive card-advantage (investigate/connive). Role-tally draw signal.
    /// </summary>
    /// <param name="oracleText">Raw oracle text; reminder text is stripped for the literal-draw check only.</param>
    public static bool IsDrawCard(string oracleText)
        => MatchesYouCardDraw(oracleText)
            || oracleText.Contains("investigate", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("connive", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is an interaction piece: an instant, or a
    /// spell that destroys, exiles, counters, bounces, or fights.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsInteractionCard(string typeLine, string oracleText)
        => typeLine.Contains("Instant", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("exile target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("counter target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return target spell", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("fight target", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card clears multiple permanents at once.
    /// </summary>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsBoardWipeCard(string oracleText)
        => oracleText.Contains("destroy all creatures", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy all artifacts", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy all enchantments", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("each creature", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("gets -", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("exile all", StringComparison.OrdinalIgnoreCase);

    // Reject temporary restrictions and self-cost wordings; tax needles require "more to cast".
    // Per-card corpus: docs/research/stax-vocabulary-corpus-2026-11.md.
    private static readonly string[] StaxRestrictionNeedles =
    [
        "your opponents control enter tapped",
        "your opponents control can't be activated",
        "your opponents can't cast",
        "your opponents can't activate",
        "each player can't cast more than",
        "each player can cast only",
    ];

    private static readonly string[] StaxTaxNeedles =
    [
        "spells cost {",
        "each spell costs",
        "spells your opponents cast cost",
    ];

    private static readonly string[] StaxTemporaryDurationMarkers =
    [
        "this turn",
        "until end of turn",
        "until your next turn",
    ];

    /// <summary>Returns whether a catalog staple or continuing opponent restriction supplies stax.</summary>
    public static bool IsStaxCard(string name, string oracleText)
    {
        if (StaxProtectionCatalog.IsStax(name))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(oracleText))
        {
            return false;
        }

        foreach (string sentence in oracleText.Split(['.', '\n', ';']))
        {
            if (ContainsAny(sentence, StaxTemporaryDurationMarkers))
            {
                continue;
            }

            if (ContainsAny(sentence, StaxRestrictionNeedles)
                || ContainsAny(sentence, StaxTaxNeedles)
                    && sentence.Contains("more to cast", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the card returns cards from the graveyard.
    /// </summary>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsRecursionCard(string oracleText)
    {
        // Why: every source needle and the Animate Dead pair contain "graveyard".
        if (!oracleText.Contains("graveyard", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (string clause in oracleText.Split(['.', '\n', ';']))
        {
            if (!clause.Contains("graveyard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string needle in RecursionActionNeedles)
            {
                int action = clause.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (action < 0)
                {
                    continue;
                }

                string effect = RecursionSourceEffect(clause[action..]);
                // Why: reject self references and bare names without limiting target counts.
                // A named subject has no card/type reference or selection determiner.
                int source = effect.IndexOf("from ", StringComparison.OrdinalIgnoreCase);
                string subject = source < 0 ? effect : effect[..source];
                if (subject.StartsWith(needle + "this ", StringComparison.OrdinalIgnoreCase)
                    || !ContainsAny(subject, RecursionSubjectMarkers))
                {
                    continue;
                }

                bool hasSource = ContainsAny(effect, RecursionSourceNeedles);
                bool hasDestination = ContainsAny(effect, RecursionDestinationNeedles);
                if (hasSource && hasDestination)
                {
                    return true;
                }
            }
        }

        // Why: Animate Dead establishes its graveyard source in the enchant ability, before
        // the separate return instruction. Require both rather than matching any Aura return.
        return oracleText.Contains("enchant creature card in a graveyard", StringComparison.OrdinalIgnoreCase)
            && oracleText.Contains("return enchanted creature card to the battlefield", StringComparison.OrdinalIgnoreCase);
    }

    private static string RecursionSourceEffect(string effect)
    {
        // Why: Accumulate Wisdom counts graveyard cards in a condition, not as the
        // moved objects. Keep comparison qualifiers such as Alesha's "less than or equal to"
        // while excluding condition/count tails from the action's source.
        foreach (string boundary in RecursionSourceBoundaries)
        {
            int condition = effect.IndexOf(boundary, StringComparison.OrdinalIgnoreCase);
            if (condition >= 0)
            {
                effect = effect[..condition];
            }
        }

        return effect;
    }

    private static readonly string[] RecursionSourceBoundaries =
    [
        " if ",
        " as long as ",
        " for each ",
        " equal to the number of ",
    ];

    // Why: match action verbs independently of quantities and target modifiers.
    // Rejected: "reanimate" (a word is not a zone change), "graveyard" (Bojuka Bog),
    // self-return subjects, and "put that card into your hand" without a graveyard source
    // (Enlightened Confidant, which selects a newly surveilled card).
    private static readonly string[] RecursionActionNeedles =
    [
        "return ", // The Soul Stone, Revival // Revenge, and Shadow of the Grave.
        "put ", // Reanimate and Necromancy.
    ];

    private static readonly string[] RecursionSubjectMarkers =
    [
        "card",
        "target ",
        "all ",
        "creature",
        "permanent",
        "artifact",
        "enchantment",
        "land",
        "planeswalker",
    ];

    private static readonly string[] RecursionSourceNeedles =
    [
        "from your graveyard",
        "from a graveyard",
        "from an opponent's graveyard",
        "in your graveyard",
    ];

    private static readonly string[] RecursionDestinationNeedles =
    [
        "to the battlefield",
        "to your hand",
        "to its owner's hand",
        "to their owners' hands",
    ];

    private static bool ContainsAny(string text, string[] needles)
    {
        foreach (string needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the card is a win condition, extra-turn effect,
    /// damage doubler, or combat-draw engine.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsClosingPowerCard(string typeLine, string oracleText)
        => oracleText.Contains("each opponent loses", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("you win the game", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("extra turn", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("double strike", StringComparison.OrdinalIgnoreCase)
            || typeLine.Contains("Craterhoof", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("combat damage to a player", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("draw", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("whenever this creature attacks", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("+X/+X", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card tutors a card from the library, excluding
    /// land-fetch ramp (basic-land search, generic land-card search, or land onto the battlefield).
    /// </summary>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsTutorCard(string oracleText)
        => oracleText.Contains("search your library for", StringComparison.OrdinalIgnoreCase)
            && !oracleText.Contains("basic land", StringComparison.OrdinalIgnoreCase)
            // Exclude land-fetch ramp ("a land card") but NOT nonland tutors: strip "nonland card"
            // first so its trailing "land card" substring does not trip the land-fetch exclusion.
            && !oracleText.Replace("nonland card", " ", StringComparison.OrdinalIgnoreCase)
                .Contains("land card", StringComparison.OrdinalIgnoreCase)
            && !oracleText.Contains("land onto the battlefield", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is fast mana: a zero-mana-value artifact that
    /// produces mana (e.g. Mana Crypt, Jeweled Lotus). Mana rocks with MV &gt;= 1 (e.g. Sol Ring) are excluded.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text; parenthesised reminder text is stripped before matching.</param>
    /// <param name="manaCost">Mana cost string (e.g. "{1}"); blank for zero-cost artifacts.</param>
    public static bool IsFastManaCard(string typeLine, string oracleText, string manaCost)
    {
        if (DeckStatAggregator.EstimateManaValue(manaCost) != 0
            || !typeLine.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rulesText = StripReminderText(oracleText);
        return rulesText.Contains("{T}: Add", StringComparison.OrdinalIgnoreCase)
            || rulesText.Contains("Add {", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the card is a ramp or draw piece with estimated mana value
    /// of 2 or less — the early acceleration/consistency signal the multi-axis scorer consumes.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text; the ramp and literal-draw checks strip reminder text.</param>
    /// <param name="manaCost">Mana cost string (e.g. "{1}{U}").</param>
    public static bool IsRampOrDrawUnderThreeMv(string typeLine, string oracleText, string manaCost)
        => DeckStatAggregator.EstimateManaValue(manaCost) <= 2
            && (IsRampCard(typeLine, oracleText) || IsDrawCard(oracleText));

    /// <summary>
    /// Returns <see langword="true"/> when the card counters a target spell. Ability counters
    /// (e.g. "counter target activated or triggered ability") are excluded.
    /// </summary>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsCounterspellCard(string oracleText)
        => oracleText.Contains("counter target spell", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is hard targeted removal, excluding board wipes and self-targeted effects.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsTargetedRemovalCard(string typeLine, string oracleText)
        => !IsBoardWipeCard(oracleText)
            && !oracleText.Contains("you control", StringComparison.OrdinalIgnoreCase)
            && (oracleText.Contains("destroy target ", StringComparison.OrdinalIgnoreCase)
                || oracleText.Contains("exile target ", StringComparison.OrdinalIgnoreCase)
                || oracleText.Contains("target creature", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("gets -", StringComparison.OrdinalIgnoreCase)
                || oracleText.Contains("deal", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("damage to target creature", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <see langword="true"/> when a targeted interaction is constrained to objects you control.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsSelfTargetedInteraction(string typeLine, string oracleText)
        => oracleText.Contains("you control", StringComparison.OrdinalIgnoreCase)
            && oracleText.Contains("target", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is temporary or weak targeted removal such as bounce, tuck, or blink.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsPseudoRemovalCard(string typeLine, string oracleText)
        => !oracleText.Contains("you control", StringComparison.OrdinalIgnoreCase)
            && (oracleText.Contains("return target", StringComparison.OrdinalIgnoreCase)
                && (oracleText.Contains("to its owner's hand", StringComparison.OrdinalIgnoreCase)
                    || oracleText.Contains("to their owner's hand", StringComparison.OrdinalIgnoreCase))
                || oracleText.Contains("target", StringComparison.OrdinalIgnoreCase)
                    && (oracleText.Contains("into its owner's library", StringComparison.OrdinalIgnoreCase)
                        || oracleText.Contains("on the bottom of", StringComparison.OrdinalIgnoreCase)
                        || oracleText.Contains("shuffles it into", StringComparison.OrdinalIgnoreCase))
                || oracleText.Contains("exile target", StringComparison.OrdinalIgnoreCase)
                    && oracleText.Contains("return", StringComparison.OrdinalIgnoreCase)
                    && (oracleText.Contains("end step", StringComparison.OrdinalIgnoreCase)
                        || oracleText.Contains("end of turn", StringComparison.OrdinalIgnoreCase)
                        || oracleText.Contains("next", StringComparison.OrdinalIgnoreCase)));

    // Why: a table (not an inline `||` chain) makes two properties checkable that an inline chain
    // cannot be asserted over — pairable subject forms (Success Criterion 2 of Phase 9.1), and a
    // future single source of truth for the research-report disclosure that
    // DeckFlow.CLI/RoleFloorResearchCommandRunner.cs currently hand-copies. That CLI array is NOT
    // retired by this table — it stays a stale hand-copied duplicate until plan 09.1-03 Task 3
    // deletes it and points the disclosure at this table.
    //
    // Why: every row below is corpus-derived, not guessed — corpus total and needle-exclusive match
    // counts for every ratified needle, plus the rejected candidates (bare `have hexproof`, bare
    // `shroud`, bare `regenerate`) and the real cards that caused each rejection, are recorded in
    // docs/research/protection-vocabulary-corpus-2026-09.md. A needle added here without a
    // corresponding row in that artifact does not belong. Rows are ordered by effect then subject
    // form so the singular/plural pairing is legible at a glance; `hexproof` and `shroud` both group
    // an Equipment/conditional "has/have" status form alongside their "gains/gain" grant form under
    // the same effect, so the pairing invariant is satisfied by the effect as a whole even though
    // "has hexproof" (Swiftfoot Boots) has no ratified "have hexproof" sibling — that plural was
    // measured and rejected (real hexproof-removal-enabler false positives), see the artifact's
    // "Rejected candidates" table. Regenerate and Ward have subject form "none": their oracle-text
    // phrasing is the ability text itself and does not inflect by subject number.
    /// <summary>
    /// The oracle vocabulary <see cref="IsProtectionCard"/> matches against, as data rather than an
    /// inline predicate chain.
    /// </summary>
    public static readonly IReadOnlyList<ProtectionNeedle> ProtectionOracleNeedles =
    [
        new ProtectionNeedle { Text = "gains hexproof", Effect = "hexproof", SubjectForm = "singular", IsPreWideningBaseline = true },
        new ProtectionNeedle { Text = "has hexproof", Effect = "hexproof", SubjectForm = "singular" },
        new ProtectionNeedle { Text = "gain hexproof", Effect = "hexproof", SubjectForm = "plural" },
        new ProtectionNeedle { Text = "gains indestructible", Effect = "indestructible", SubjectForm = "singular", IsPreWideningBaseline = true },
        new ProtectionNeedle { Text = "gain indestructible", Effect = "indestructible", SubjectForm = "plural" },
        new ProtectionNeedle { Text = "phases out", Effect = "phase-out", SubjectForm = "singular", IsPreWideningBaseline = true },
        new ProtectionNeedle { Text = "phase out", Effect = "phase-out", SubjectForm = "plural" },
        new ProtectionNeedle { Text = "gains protection from", Effect = "protection-from", SubjectForm = "singular" },
        new ProtectionNeedle { Text = "gain protection from", Effect = "protection-from", SubjectForm = "plural", IsPreWideningBaseline = true },
        new ProtectionNeedle { Text = "regenerate target", Effect = "regenerate", SubjectForm = "none" },
        new ProtectionNeedle { Text = "regenerate this creature", Effect = "regenerate", SubjectForm = "none" },
        new ProtectionNeedle { Text = "has shroud", Effect = "shroud", SubjectForm = "singular" },
        // Lightning Greaves's real oracle text ("Equipped creature has haste and shroud.") does NOT
        // match "has shroud" — shroud is the second item in the conjunction, not the word directly
        // after "has". A bare "and shroud" needle was measured and rejected (also matches Arcane
        // Lighthouse's anti-shroud removal-enabler clause); this needle is exhaustive at 1 corpus
        // match total. See docs/research/protection-vocabulary-corpus-2026-09.md.
        new ProtectionNeedle { Text = "haste and shroud", Effect = "shroud", SubjectForm = "singular" },
        new ProtectionNeedle { Text = "gains shroud", Effect = "shroud", SubjectForm = "singular" },
        new ProtectionNeedle { Text = "have shroud", Effect = "shroud", SubjectForm = "plural" },
        new ProtectionNeedle { Text = "gain shroud", Effect = "shroud", SubjectForm = "plural" },
        new ProtectionNeedle { Text = "ward—pay", Effect = "ward", SubjectForm = "none" },
    ];

    /// <summary>
    /// Returns <see langword="true"/> when the card is a curated protection staple, or its oracle text
    /// matches a corpus-derived protection needle — granting hexproof, indestructible, protection-from,
    /// or shroud (temporary grant or Equipment/conditional status), phasing out, regenerating, or
    /// carrying a Ward—Pay cost. See <see cref="ProtectionOracleNeedles"/> for the full vocabulary and
    /// docs/research/protection-vocabulary-corpus-2026-09.md for the corpus evidence behind it.
    /// </summary>
    /// <param name="name">Card name.</param>
    /// <param name="oracleText">Raw oracle text, including any reminder text.</param>
    public static bool IsProtectionCard(string name, string oracleText)
    {
        if (StaxProtectionCatalog.IsProtection(name))
        {
            return true;
        }

        // Plain loop, not ProtectionOracleNeedles.Any(needle => ...): this predicate runs once per
        // card on the Mana Base analysis and Deck Analysis request paths, and a LINQ lambda here
        // allocates a delegate per call for no benefit over a direct loop.
        foreach (ProtectionNeedle needle in ProtectionOracleNeedles)
        {
            if (oracleText.Contains(needle.Text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses a single mana symbol token (the text between <c>{</c> and <c>}</c>) into its
    /// converted mana cost contribution.  Numeric tokens return their integer value; X returns 0;
    /// hybrid symbols (containing '/') return 1; everything else returns 1.
    /// </summary>
    /// <param name="token">Token text without braces (e.g. "3", "X", "W/U").</param>
    public static int ParseManaToken(string token)
    {
        if (int.TryParse(token, out var numeric))
        {
            return numeric;
        }

        if (token.Contains('/', StringComparison.Ordinal))
        {
            return 1;
        }

        return token.Equals("X", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }
}
