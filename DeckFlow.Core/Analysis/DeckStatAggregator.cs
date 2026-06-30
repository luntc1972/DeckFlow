namespace DeckFlow.Core.Analysis;

/// <summary>
/// One card's contribution to a deck-stat tally: its quantity plus the Scryfall-derived
/// type line, normalized oracle text, and mana cost string used by the role classifiers.
/// </summary>
/// <param name="Quantity">Number of copies of this card in the deck slot being tallied.</param>
/// <param name="TypeLine">Card type line (e.g. "Artifact — Treasure").</param>
/// <param name="OracleText">Normalized oracle text.</param>
/// <param name="ManaCost">Mana cost string (e.g. "{3}{B}{B}"); blank for lands.</param>
public readonly record struct DeckStatCardInput(int Quantity, string TypeLine, string OracleText, string ManaCost);

/// <summary>
/// Pre-computed composition stats for a deck slot: counts the analysis prompt states as facts so the
/// AI never has to tally 100 cards by hand (LLMs miscount long lists).
/// </summary>
/// <param name="Cards">Total cards tallied (sum of quantities, including lands).</param>
/// <param name="Lands">Land count.</param>
/// <param name="Creatures">Non-land creature count.</param>
/// <param name="AverageManaValue">Average mana value across non-land cards, rounded to 2 dp.</param>
/// <param name="ManaCurve">Non-land curve buckets keyed "0-1","2","3","4","5+" (lands counted in "0-1").</param>
/// <param name="Ramp">Ramp source count.</param>
/// <param name="Draw">Card-draw count.</param>
/// <param name="Interaction">Interaction-piece count.</param>
/// <param name="Wipes">Board-wipe count.</param>
/// <param name="Recursion">Graveyard-recursion count.</param>
/// <param name="ClosingPower">Win-condition / closing-power count.</param>
public sealed record DeckStatSummary(
    int Cards,
    int Lands,
    int Creatures,
    decimal AverageManaValue,
    IReadOnlyDictionary<string, int> ManaCurve,
    int Ramp,
    int Draw,
    int Interaction,
    int Wipes,
    int Recursion,
    int ClosingPower)
{
    /// <summary>Count of tutor effects (search library for a non-land card).</summary>
    public int Tutors { get; init; }

    /// <summary>Count of fast-mana sources (zero-cost mana artifacts: Mana Crypt, Jeweled Lotus, etc.).</summary>
    public int FastMana { get; init; }

    /// <summary>Count of ramp or draw pieces with estimated mana value &lt;= 2.</summary>
    public int RampDrawUnderThreeMv { get; init; }

    /// <summary>Count of cards that counter target spells (subset of Interaction).</summary>
    public int Counters { get; init; }
}

/// <summary>
/// Aggregates a deck's cards into a <see cref="DeckStatSummary"/> using <see cref="DeckStatClassifier"/>
/// for role tallies. Pure domain logic: callers supply card type/oracle/mana plus quantities; this owns
/// the lands/creatures/curve/average-mana-value counting so the analysis and comparison prompts stay
/// consistent. Mirrors the long-standing comparison summary rules.
/// </summary>
public static class DeckStatAggregator
{
    /// <summary>
    /// Tallies composition stats for the supplied cards. Lands are counted toward the "0-1" curve
    /// bucket and excluded from the average-mana-value denominator, matching the comparison summary.
    /// </summary>
    /// <param name="cards">Cards to tally (already filtered to the slot of interest, e.g. mainboard).</param>
    public static DeckStatSummary Compute(IEnumerable<DeckStatCardInput> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var curveBuckets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["0-1"] = 0,
            ["2"] = 0,
            ["3"] = 0,
            ["4"] = 0,
            ["5+"] = 0
        };

        var totalCards = 0;
        var nonlandCardCount = 0;
        var manaValueTotal = 0m;
        var lands = 0;
        var creatures = 0;
        var ramp = 0;
        var draw = 0;
        var interaction = 0;
        var wipes = 0;
        var recursion = 0;
        var closingPower = 0;
        var tutors = 0;
        var fastMana = 0;
        var rampDrawUnderThreeMv = 0;
        var counters = 0;

        foreach (var card in cards)
        {
            var quantity = card.Quantity;
            if (quantity <= 0)
            {
                continue;
            }

            var typeLine = card.TypeLine ?? string.Empty;
            var oracleText = card.OracleText ?? string.Empty;
            totalCards += quantity;

            if (typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                lands += quantity;
                curveBuckets["0-1"] += quantity;
                continue;
            }

            var manaValue = EstimateManaValue(card.ManaCost);
            nonlandCardCount += quantity;
            manaValueTotal += manaValue * quantity;

            if (manaValue <= 1)
            {
                curveBuckets["0-1"] += quantity;
            }
            else if (manaValue == 2)
            {
                curveBuckets["2"] += quantity;
            }
            else if (manaValue == 3)
            {
                curveBuckets["3"] += quantity;
            }
            else if (manaValue == 4)
            {
                curveBuckets["4"] += quantity;
            }
            else
            {
                curveBuckets["5+"] += quantity;
            }

            if (typeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            {
                creatures += quantity;
            }

            if (DeckStatClassifier.IsRampCard(typeLine, oracleText))
            {
                ramp += quantity;
            }

            if (DeckStatClassifier.IsDrawCard(oracleText))
            {
                draw += quantity;
            }

            if (DeckStatClassifier.IsInteractionCard(typeLine, oracleText))
            {
                interaction += quantity;
            }

            if (DeckStatClassifier.IsBoardWipeCard(oracleText))
            {
                wipes += quantity;
            }

            if (DeckStatClassifier.IsRecursionCard(oracleText))
            {
                recursion += quantity;
            }

            if (DeckStatClassifier.IsClosingPowerCard(typeLine, oracleText))
            {
                closingPower += quantity;
            }

            if (DeckStatClassifier.IsTutorCard(oracleText))
            {
                tutors += quantity;
            }

            if (DeckStatClassifier.IsFastManaCard(typeLine, oracleText, card.ManaCost))
            {
                fastMana += quantity;
            }

            if (DeckStatClassifier.IsRampOrDrawUnderThreeMv(typeLine, oracleText, card.ManaCost))
            {
                rampDrawUnderThreeMv += quantity;
            }

            if (DeckStatClassifier.IsCounterspellCard(oracleText))
            {
                counters += quantity;
            }
        }

        var averageManaValue = nonlandCardCount == 0 ? 0m : Math.Round(manaValueTotal / nonlandCardCount, 2);

        return new DeckStatSummary(
            totalCards,
            lands,
            creatures,
            averageManaValue,
            curveBuckets,
            ramp,
            draw,
            interaction,
            wipes,
            recursion,
            closingPower)
        {
            Tutors = tutors,
            FastMana = fastMana,
            RampDrawUnderThreeMv = rampDrawUnderThreeMv,
            Counters = counters,
        };
    }

    /// <summary>
    /// Estimates a card's mana value by summing the <c>{...}</c> symbols in its mana cost via
    /// <see cref="DeckStatClassifier.ParseManaToken"/>. Returns 0 for a blank cost (e.g. lands).
    /// </summary>
    /// <param name="manaCost">Mana cost string such as "{3}{B}{B}".</param>
    public static int EstimateManaValue(string? manaCost)
    {
        if (string.IsNullOrWhiteSpace(manaCost))
        {
            return 0;
        }

        var total = 0;
        var tokenStart = -1; // -1 = not inside a {...} token
        for (var i = 0; i < manaCost.Length; i++)
        {
            var ch = manaCost[i];
            if (ch == '{')
            {
                tokenStart = i + 1;
            }
            else if (ch == '}' && tokenStart >= 0)
            {
                if (i > tokenStart)
                {
                    total += DeckStatClassifier.ParseManaToken(manaCost.Substring(tokenStart, i - tokenStart));
                }

                tokenStart = -1;
            }
        }

        return total;
    }
}
