namespace DeckFlow.Core.Research;

/// <summary>
/// The single, written statistical bar Cycle 21 Phase 1 applies uniformly to every commander/role
/// pair; no other file may re-derive or fork this threshold logic. The adopted defaults are
/// minDeckCount=40, ratioLow=0.667, ratioHigh=1.5, and zThreshold=2.0: N=40 keeps the standard
/// error of a role-count mean to roughly stdev/sqrt(40) for a corpus of a few thousand decks,
/// tight enough to separate a real 50% mean shift from sampling noise; requiring both the ratio
/// and z-score conditions rejects both "big N, small effect" and "big ratio, tiny N" false
/// positives. At these exact defaults the z-gate is largely redundant once N and ratio are both
/// satisfied (for example, N=40, sd=3, mean=6, ratio=1.5x implies z~6.3, well above 2.0), but the
/// gate remains part of the documented bar. Cohen's d is reporting-only here: it gives a
/// scale-uniform effect size alongside fixed percentage ratio thresholds, which are not scale-fair
/// across roles with very different corpus-wide means.
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
    /// Returns true only when the commander clears the full written bar: enough deduped decks, a
    /// non-zero corpus baseline with a ratio outside the neutral band, and an absolute z-score at
    /// or above the threshold.
    /// </summary>
    public static bool ClearsBar(
        int n,
        double commanderMean,
        double corpusMean,
        double corpusStdDev,
        int minDeckCount,
        double ratioLow,
        double ratioHigh,
        double zThreshold)
    {
        if (n < minDeckCount || corpusMean <= 0.0)
        {
            return false;
        }

        double ratio = ComputeRatio(commanderMean, corpusMean);
        if (ratio < ratioHigh && ratio > ratioLow)
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
