namespace DeckFlow.Core.Manabase;

/// <summary>
/// Hypergeometric probability helpers used by the mana-base analyzer. All draws are
/// without replacement, modeling cards seen from a shuffled library.
/// </summary>
/// <remarks>
/// Combinatorics are evaluated in log-space against a precomputed log-factorial table so
/// that 100-card decks with double-digit draw counts do not overflow a <see cref="double"/>.
/// </remarks>
public static class Hypergeometric
{
    // Why: a deck is at most ~100 cards, but pad the table so callers can model larger
    // singleton or stacked pools without re-allocating.
    private const int MaxN = 512;

    private static readonly double[] LogFactorial = BuildLogFactorialTable();

    private static double[] BuildLogFactorialTable()
    {
        var table = new double[MaxN + 1];
        table[0] = 0.0;
        for (int i = 1; i <= MaxN; i++)
        {
            table[i] = table[i - 1] + Math.Log(i);
        }

        return table;
    }

    /// <summary>Natural log of the binomial coefficient C(n, k).</summary>
    public static double LogChoose(int n, int k)
    {
        if (n > MaxN)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"Population exceeds the {MaxN}-entry log-factorial table.");
        }

        if (k < 0 || k > n || n < 0)
        {
            return double.NegativeInfinity;
        }

        return LogFactorial[n] - LogFactorial[k] - LogFactorial[n - k];
    }

    /// <summary>The binomial coefficient C(n, k) as a double.</summary>
    public static double Choose(int n, int k)
    {
        double log = LogChoose(n, k);
        return double.IsNegativeInfinity(log) ? 0.0 : Math.Exp(log);
    }

    /// <summary>
    /// P(X = hits) when drawing <paramref name="draws"/> cards from a population of
    /// <paramref name="population"/> containing <paramref name="successes"/> winners.
    /// </summary>
    public static double Exactly(int population, int successes, int draws, int hits)
    {
        if (hits < 0 || hits > successes || draws - hits > population - successes || draws > population || draws < 0)
        {
            return 0.0;
        }

        double log = LogChoose(successes, hits)
            + LogChoose(population - successes, draws - hits)
            - LogChoose(population, draws);
        return Math.Exp(log);
    }

    /// <summary>
    /// P(X &gt;= <paramref name="atLeast"/>) — the chance of drawing at least that many
    /// winners. Sums the shorter tail for numerical stability.
    /// </summary>
    public static double AtLeast(int population, int successes, int draws, int atLeast)
    {
        if (atLeast <= 0)
        {
            return 1.0;
        }

        int max = Math.Min(successes, draws);
        if (atLeast > max)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int hits = atLeast; hits <= max; hits++)
        {
            sum += Exactly(population, successes, draws, hits);
        }

        return Math.Clamp(sum, 0.0, 1.0);
    }
}
