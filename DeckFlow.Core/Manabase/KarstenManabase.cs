namespace DeckFlow.Core.Manabase;

/// <summary>
/// Frank Karsten's mana-base math: the land-count-vs-curve regression and the
/// colored-source requirement for a given pip pattern, reproduced from
/// "How Many Sources Do You Need to Consistently Cast Your Spells? A 2022 Update".
/// </summary>
/// <remarks>
/// The source requirement is computed directly from the conditional hypergeometric model
/// Karsten describes — P(at least N colored sources AND at least M lands by turn M) divided
/// by P(at least M lands by turn M) — rather than hard-coding his published tables. This
/// keeps the numbers self-consistent for arbitrary deck/land counts; it lands within ~1–2
/// sources of his tables, which additionally bake in a London-mulligan model not modeled here.
/// </remarks>
public static class KarstenManabase
{
    /// <summary>
    /// Recommended land count for a singleton / Commander deck (Karsten's regression fit).
    /// </summary>
    /// <param name="totalCards">Deck size including commanders (typically 100).</param>
    /// <param name="commanderCount">Number of commanders (1, or 2 for partners/backgrounds).</param>
    /// <param name="averageManaValue">Mean mana value of the non-land cards.</param>
    /// <param name="rampAndDrawUnderThree">Count of ramp/card-draw spells of mana value 2 or less.</param>
    /// <param name="fastMana">Count of 0-cost mana artifacts (Lotus, Moxen). Sol Ring ≈ 0.8.</param>
    /// <param name="mdfcCommon">Count of non-mythic land/spell MDFCs.</param>
    /// <param name="mdfcMythic">Count of mythic land/spell MDFCs.</param>
    public static double SingletonLandTarget(
        int totalCards,
        int commanderCount,
        double averageManaValue,
        double rampAndDrawUnderThree,
        double fastMana = 0,
        double mdfcCommon = 0,
        double mdfcMythic = 0)
    {
        double scale = (totalCards - commanderCount) / 60.0;
        double interior = 19.59 + (1.90 * averageManaValue) + (0.27 * commanderCount);
        return (scale * interior)
            - (0.28 * rampAndDrawUnderThree)
            - fastMana
            - (0.74 * mdfcCommon)
            - (0.38 * mdfcMythic)
            - 1.35;
    }

    /// <summary>
    /// Recommended land count for a 60-card constructed deck (Karsten's regression fit).
    /// </summary>
    public static double SixtyCardLandTarget(
        double averageManaValue,
        double rampAndDrawUnderThree,
        double fastMana = 0,
        double mdfcCommon = 0,
        double mdfcMythic = 0)
    {
        return 32.65
            + (3.16 * averageManaValue)
            - (0.28 * rampAndDrawUnderThree)
            - fastMana
            - (0.74 * mdfcCommon)
            - (0.38 * mdfcMythic);
    }

    /// <summary>
    /// Karsten's consistency target for a spell of mana value <paramref name="manaValue"/>:
    /// (89 + M)% — 90% for one-drops rising to 96% for seven-drops.
    /// </summary>
    public static double ConsistencyThreshold(int manaValue)
    {
        int pct = 89 + Math.Max(1, manaValue);
        return Math.Clamp(pct / 100.0, 0.0, 0.99);
    }

    /// <summary>
    /// Cards seen by turn <paramref name="turn"/> assuming a 7-card opener.
    /// On the play you skip the first draw step; on the draw you do not.
    /// </summary>
    public static int CardsSeenByTurn(int turn, bool onPlay)
        => 7 + (onPlay ? turn - 1 : turn);

    /// <summary>
    /// Conditional probability of having at least <paramref name="pips"/> sources of a color
    /// by turn <paramref name="manaValue"/>, given at least <paramref name="manaValue"/> lands
    /// were drawn — exactly the metric Karsten's tables report.
    /// </summary>
    /// <param name="deckSize">Cards in the library (exclude commanders in the command zone).</param>
    /// <param name="totalLands">Total lands in the library.</param>
    /// <param name="colorSources">Lands (or partial sources) producing the color in question.</param>
    /// <param name="pips">Colored pips of that color in the cost.</param>
    /// <param name="manaValue">The spell's mana value — also the turn it is cast on curve.</param>
    /// <param name="onPlay">True for on-the-play draw counts.</param>
    public static double CastConsistency(
        int deckSize,
        int totalLands,
        int colorSources,
        int pips,
        int manaValue,
        bool onPlay = true)
    {
        if (pips <= 0)
        {
            // No colored requirement. This is the conditional metric (already given the land
            // drop was hit), so the color condition is trivially satisfied → 1.0.
            return 1.0;
        }

        int draws = CardsSeenByTurn(manaValue, onPlay);
        int otherLands = totalLands - colorSources;
        int nonland = deckSize - totalLands;

        double pLandsEnough = Hypergeometric.AtLeast(deckSize, totalLands, draws, manaValue);
        if (pLandsEnough <= 0.0)
        {
            return 0.0;
        }

        // P(sources >= pips AND lands >= M): triple-category (sources, other lands, nonland).
        double logDenomDraw = Hypergeometric.LogChoose(deckSize, draws);
        double joint = 0.0;
        int maxS = Math.Min(colorSources, draws);
        for (int s = pips; s <= maxS; s++)
        {
            int maxO = Math.Min(otherLands, draws - s);
            for (int o = 0; o <= maxO; o++)
            {
                if (s + o < manaValue)
                {
                    continue;
                }

                int rest = draws - s - o;
                if (rest < 0 || rest > nonland)
                {
                    continue;
                }

                double logTerm = Hypergeometric.LogChoose(colorSources, s)
                    + Hypergeometric.LogChoose(otherLands, o)
                    + Hypergeometric.LogChoose(nonland, rest)
                    - logDenomDraw;
                joint += Math.Exp(logTerm);
            }
        }

        return Math.Clamp(joint / pLandsEnough, 0.0, 1.0);
    }

    /// <summary>
    /// Minimum colored sources required to cast a <paramref name="pips"/>-pip spell of mana
    /// value <paramref name="manaValue"/> on curve at Karsten's (89 + M)% threshold.
    /// Returns <paramref name="totalLands"/> if even an all-on-color base falls short.
    /// </summary>
    public static int SourcesNeeded(
        int deckSize,
        int totalLands,
        int pips,
        int manaValue,
        bool onPlay = true)
    {
        double threshold = ConsistencyThreshold(manaValue);
        for (int sources = pips; sources <= totalLands; sources++)
        {
            if (CastConsistency(deckSize, totalLands, sources, pips, manaValue, onPlay) >= threshold)
            {
                return sources;
            }
        }

        return totalLands;
    }
}
