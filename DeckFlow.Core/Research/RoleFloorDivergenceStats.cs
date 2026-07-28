namespace DeckFlow.Core.Research;

/// <summary>
/// The single, written statistical bar Cycle 21 Phase 2 applies uniformly to every commander/role
/// pair per decision D-B; no other file may re-derive or fork this threshold logic, and
/// <see cref="ClearsFloorBar"/> is the only verdict bar. The adopted floor statistic is the 25th
/// percentile rather than the mean. The adopted defaults are minDeckCount=40, ratioLow=0.667,
/// ratioHigh=1.5, zThreshold=2.0, and absoluteFloorGap=2.0: N=40 keeps the standard error of a
/// role-count mean to roughly stdev/sqrt(40) for a corpus of a few thousand decks, tight enough to
/// separate a real 50% mean shift from sampling noise; requiring both the ratio and z-score
/// conditions rejects both "big N, small effect" and "big ratio, tiny N" false positives; and
/// when the corpus 25th percentile is zero, a floor differing by one card is not worth acting on,
/// so the absolute-gap fallback requires at least two cards. At these exact defaults the z-gate is
/// largely redundant once N and ratio are both satisfied (for example, N=40, sd=3, mean=6,
/// ratio=1.5x implies z~6.3, well above 2.0), but the gate remains part of the documented bar.
/// Cohen's d is reporting-only here: it gives a scale-uniform effect size alongside fixed
/// percentage ratio thresholds, which are not scale-fair across roles with very different
/// corpus-wide means.
/// </summary>
public static class RoleFloorDivergenceStats
{
    private const double EqualityEpsilon = 1e-9;

    /// <summary>
    /// Computes the commander's mean relative to the corpus baseline; returns 0.0 when the corpus
    /// mean is exactly 0 because there is no baseline signal to compare against.
    /// </summary>
    public static double ComputeRatio(double commanderMean, double corpusMean)
        => corpusMean == 0.0 ? 0.0 : commanderMean / corpusMean;

    /// <summary>
    /// Computes the commander's z-score against the corpus mean. When the corpus standard deviation
    /// is exactly 0, equal means return 0.0 and unequal means return PositiveInfinity because any
    /// non-zero gap against a zero-spread baseline is maximally significant.
    /// </summary>
    public static double ComputeZScore(double commanderMean, double corpusMean, double corpusStdDev, int n)
    {
        if (corpusStdDev == 0.0)
        {
            return AreNearlyEqual(commanderMean, corpusMean) ? 0.0 : double.PositiveInfinity;
        }

        return (commanderMean - corpusMean) / (corpusStdDev / Math.Sqrt(n));
    }

    /// <summary>
    /// A commander-role row clears the bar only when its deduped deck count reaches
    /// <paramref name="minDeckCount"/>, its 25th-percentile role count diverges from the corpus
    /// 25th percentile by at least <paramref name="ratioHigh"/>x or at most
    /// <paramref name="ratioLow"/>x (or, where the corpus P25 is zero, by at least
    /// <paramref name="absoluteFloorGap"/> cards), and the commander's mean differs from the
    /// corpus mean with |z| &gt;= <paramref name="zThreshold"/>. Divergence is measured on the
    /// 25th percentile while significance stays on the mean because a sample percentile has no
    /// closed-form standard error.
    /// </summary>
    /// <param name="n">The number of deduped decks in the commander's sample.</param>
    /// <param name="commanderP25">The commander's 25th-percentile role count.</param>
    /// <param name="corpusP25">The corpus-wide 25th-percentile role count.</param>
    /// <param name="commanderMean">The commander's mean role count.</param>
    /// <param name="corpusMean">The corpus-wide mean role count.</param>
    /// <param name="corpusStdDev">The corpus-wide standard deviation for the role count.</param>
    /// <param name="minDeckCount">The minimum deduped deck count required before any verdict is possible.</param>
    /// <param name="ratioLow">The low-side multiplicative divergence threshold for non-zero corpus P25 values.</param>
    /// <param name="ratioHigh">The high-side multiplicative divergence threshold for non-zero corpus P25 values.</param>
    /// <param name="zThreshold">The minimum absolute mean z-score required to clear the significance gate.</param>
    /// <param name="absoluteFloorGap">The absolute-gap fallback, in cards, used when the corpus P25 is zero.</param>
    public static bool ClearsFloorBar(
        int n,
        double commanderP25,
        double corpusP25,
        double commanderMean,
        double corpusMean,
        double corpusStdDev,
        int minDeckCount,
        double ratioLow,
        double ratioHigh,
        double zThreshold,
        double absoluteFloorGap)
    {
        if (n < minDeckCount)
        {
            return false;
        }

        bool isDivergent;
        if (corpusP25 > 0.0)
        {
            double ratio = ComputeRatio(commanderP25, corpusP25);
            isDivergent = ratio >= ratioHigh || ratio <= ratioLow;
        }
        else
        {
            // Why: a corpus 25th percentile of zero is expected for thin roles such as wincons;
            // ComputeRatio returns 0.0 on a zero denominator, which would slide under ratioLow
            // and mark every commander divergent-low, so use an absolute-gap test instead.
            isDivergent = Math.Abs(commanderP25 - corpusP25) >= absoluteFloorGap;
        }

        if (!isDivergent)
        {
            return false;
        }

        return Math.Abs(ComputeZScore(commanderMean, corpusMean, corpusStdDev, n)) >= zThreshold;
    }

    /// <summary>
    /// Computes an inclusive linear-interpolation percentile (spreadsheet PERCENTILE.INC / numpy
    /// default) over a sorted copy of the input. A role-count sample with zero decks has nothing to
    /// report a percentile over.
    /// </summary>
    public static double ComputePercentile(IReadOnlyList<double> values, double percentile)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("A role-count sample with zero decks has nothing to report a percentile over.", nameof(values));
        }

        double[] sortedCopy = values.ToArray();
        Array.Sort(sortedCopy);

        if (sortedCopy.Length == 1)
        {
            return sortedCopy[0];
        }

        double rank = percentile * (sortedCopy.Length - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
        {
            return sortedCopy[lowerIndex];
        }

        double weight = rank - lowerIndex;
        return sortedCopy[lowerIndex] + ((sortedCopy[upperIndex] - sortedCopy[lowerIndex]) * weight);
    }

    /// <summary>
    /// Computes Cohen's d as a reporting-only, scale-uniform effect size. When the corpus standard
    /// deviation is exactly 0, equal means return 0.0; otherwise the sign is preserved and the
    /// method returns positive or negative infinity.
    /// </summary>
    public static double ComputeCohensD(double commanderMean, double corpusMean, double corpusStdDev)
    {
        if (corpusStdDev == 0.0)
        {
            if (AreNearlyEqual(commanderMean, corpusMean))
            {
                return 0.0;
            }

            return commanderMean > corpusMean ? double.PositiveInfinity : double.NegativeInfinity;
        }

        return (commanderMean - corpusMean) / corpusStdDev;
    }

    private static bool AreNearlyEqual(double left, double right)
        => Math.Abs(left - right) <= EqualityEpsilon;
}
