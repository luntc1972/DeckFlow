using System.Text.Json.Serialization;

namespace DeckFlow.Core.Manabase;

/// <summary>One classified deck sample used to build the monthly cEDH land baseline.</summary>
/// <param name="CommanderKey">Commander name or partner-pair key.</param>
/// <param name="Tier">Tournament size tier label.</param>
/// <param name="Lands">Observed land count using the app's source classification.</param>
/// <param name="AvgManaValue">Average mana value of nonland, non-commander spells.</param>
/// <param name="CardCount">Resolved card-fact count used for gating.</param>
public sealed record CedhDeckSample(
    string CommanderKey,
    string Tier,
    int Lands,
    double AvgManaValue,
    int CardCount);

/// <summary>Pure helper that applies the cEDH gate and rolls up monthly land-baseline stats.</summary>
public static class CedhLandBaseline
{
    private const double MaxAverageManaValue = 2.7;
    private const int MinCardCount = 95;
    private const int MaxCardCount = 101;
    private const int MinCommanderSamples = 3;

    /// <summary>Returns true when a sample passes the cEDH completeness and low-curve gate.</summary>
    /// <param name="cardCount">Resolved card-fact count.</param>
    /// <param name="avgManaValue">Average mana value of the classified deck.</param>
    public static bool PassesCedhGate(int cardCount, double avgManaValue) =>
        cardCount is >= MinCardCount and <= MaxCardCount && avgManaValue <= MaxAverageManaValue;

    /// <summary>Build the gated monthly baseline from already-classified deck samples.</summary>
    /// <param name="samples">Deck samples to aggregate.</param>
    /// <param name="month">Month label in <c>YYYY-MM</c> form.</param>
    public static CedhLandBaselineResult Build(IEnumerable<CedhDeckSample> samples, string month)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentException.ThrowIfNullOrWhiteSpace(month);

        var materialized = samples.ToList();
        var kept = new List<CedhDeckSample>(materialized.Count);
        int droppedForCurve = 0;
        int droppedForIncomplete = 0;

        foreach (CedhDeckSample sample in materialized)
        {
            if (sample.CardCount < MinCardCount || sample.CardCount > MaxCardCount)
            {
                droppedForIncomplete++;
                continue;
            }

            if (sample.AvgManaValue > MaxAverageManaValue)
            {
                droppedForCurve++;
                continue;
            }

            kept.Add(sample);
        }

        CedhLandStats overall = BuildStats(kept.Select(s => s.Lands));

        IReadOnlyList<CedhLandTierStat> tiers = kept
            .GroupBy(sample => sample.Tier, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                CedhLandStats stats = BuildStats(group.Select(sample => sample.Lands));
                return new CedhLandTierStat
                {
                    Tier = group.Key,
                    SampleSize = stats.SampleSize,
                    MeanLands = stats.MeanLands,
                    StandardDeviation = stats.StandardDeviation,
                    MinLands = stats.MinLands,
                    MaxLands = stats.MaxLands,
                };
            })
            .ToList();

        IReadOnlyList<CedhLandHistogramEntry> histogram = kept
            .GroupBy(sample => sample.Lands)
            .OrderBy(group => group.Key)
            .Select(group => new CedhLandHistogramEntry
            {
                Lands = group.Key,
                Count = group.Count(),
            })
            .ToList();

        IReadOnlyDictionary<string, CedhLandStats> commanders = kept
            .GroupBy(sample => sample.CommanderKey, StringComparer.Ordinal)
            .Where(group => group.Count() >= MinCommanderSamples)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildStats(group.Select(sample => sample.Lands)),
                StringComparer.Ordinal);

        return new CedhLandBaselineResult
        {
            Month = month,
            RawSampleSize = materialized.Count,
            SampleSize = kept.Count,
            DroppedForCurve = droppedForCurve,
            DroppedForIncomplete = droppedForIncomplete,
            Overall = overall,
            Tiers = tiers,
            Histogram = histogram,
            Commanders = commanders,
        };
    }

    /// <summary>Project the monthly rollup to the JSON contract consumed by the web app.</summary>
    /// <param name="result">Aggregated monthly baseline.</param>
    public static CedhLandBaselineSnapshot ToSnapshot(CedhLandBaselineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new CedhLandBaselineSnapshot
        {
            Generated = result.Month,
            SampleSize = result.SampleSize,
            OverallMeanLands = Round1(result.Overall.MeanLands),
            Commanders = result.Commanders.ToDictionary(
                pair => pair.Key,
                pair => new CedhCommanderBaselineSnapshot
                {
                    N = pair.Value.SampleSize,
                    LandsMean = Round1(pair.Value.MeanLands),
                    LandsSd = Round1(pair.Value.StandardDeviation),
                },
                StringComparer.Ordinal),
        };
    }

    private static CedhLandStats BuildStats(IEnumerable<int> values)
    {
        var materialized = values.ToList();
        if (materialized.Count == 0)
        {
            return new CedhLandStats
            {
                SampleSize = 0,
                MeanLands = 0,
                StandardDeviation = 0,
                MinLands = 0,
                MaxLands = 0,
            };
        }

        double mean = materialized.Average();
        return new CedhLandStats
        {
            SampleSize = materialized.Count,
            MeanLands = mean,
            StandardDeviation = SampleStandardDeviation(materialized, mean),
            MinLands = materialized.Min(),
            MaxLands = materialized.Max(),
        };
    }

    private static double SampleStandardDeviation(IReadOnlyList<int> values, double mean)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        double variance = values.Sum(value => Math.Pow(value - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}

/// <summary>The full monthly cEDH land-baseline rollup used by the CLI report writer.</summary>
public sealed record CedhLandBaselineResult
{
    /// <summary>Month label supplied by the operator.</summary>
    public required string Month { get; init; }

    /// <summary>Raw sample count before the cEDH gate is applied.</summary>
    public int RawSampleSize { get; init; }

    /// <summary>Kept sample count after the cEDH gate is applied.</summary>
    public int SampleSize { get; init; }

    /// <summary>Samples dropped for failing the curve gate.</summary>
    public int DroppedForCurve { get; init; }

    /// <summary>Samples dropped for incomplete or oversized lists.</summary>
    public int DroppedForIncomplete { get; init; }

    /// <summary>Overall land-count stats across all kept cEDH samples.</summary>
    public required CedhLandStats Overall { get; init; }

    /// <summary>Per-tier land-count stats across kept cEDH samples.</summary>
    public required IReadOnlyList<CedhLandTierStat> Tiers { get; init; }

    /// <summary>Histogram of land counts across kept cEDH samples.</summary>
    public required IReadOnlyList<CedhLandHistogramEntry> Histogram { get; init; }

    /// <summary>Per-commander land stats for commanders with at least three kept samples.</summary>
    public required IReadOnlyDictionary<string, CedhLandStats> Commanders { get; init; }
}

/// <summary>Land-count summary stats.</summary>
public sealed record CedhLandStats
{
    /// <summary>Number of samples contributing to the stats.</summary>
    public int SampleSize { get; init; }

    /// <summary>Arithmetic mean land count.</summary>
    public double MeanLands { get; init; }

    /// <summary>Sample standard deviation of land counts.</summary>
    public double StandardDeviation { get; init; }

    /// <summary>Minimum observed land count.</summary>
    public int MinLands { get; init; }

    /// <summary>Maximum observed land count.</summary>
    public int MaxLands { get; init; }
}

/// <summary>Per-tier land-count summary.</summary>
public sealed record CedhLandTierStat
{
    /// <summary>Tier label.</summary>
    public required string Tier { get; init; }

    /// <summary>Number of kept samples in the tier.</summary>
    public int SampleSize { get; init; }

    /// <summary>Arithmetic mean land count.</summary>
    public double MeanLands { get; init; }

    /// <summary>Sample standard deviation of land counts.</summary>
    public double StandardDeviation { get; init; }

    /// <summary>Minimum observed land count.</summary>
    public int MinLands { get; init; }

    /// <summary>Maximum observed land count.</summary>
    public int MaxLands { get; init; }
}

/// <summary>One land-count histogram bucket.</summary>
public sealed record CedhLandHistogramEntry
{
    /// <summary>Land count for the bucket.</summary>
    public int Lands { get; init; }

    /// <summary>Number of kept decks with that land count.</summary>
    public int Count { get; init; }
}

/// <summary>Serialization model for the committed monthly/latest cEDH land baseline JSON.</summary>
public sealed record CedhLandBaselineSnapshot
{
    /// <summary>Month label the snapshot was generated for.</summary>
    [JsonPropertyName("generated")]
    public required string Generated { get; init; }

    /// <summary>Kept cEDH sample count.</summary>
    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; init; }

    /// <summary>Overall mean land count rounded to one decimal place.</summary>
    [JsonPropertyName("overallMeanLands")]
    public double OverallMeanLands { get; init; }

    /// <summary>Commander baselines keyed by commander name or partner-pair key.</summary>
    [JsonPropertyName("commanders")]
    public required IReadOnlyDictionary<string, CedhCommanderBaselineSnapshot> Commanders { get; init; }
}

/// <summary>Serialization model for one commander's cEDH land baseline.</summary>
public sealed record CedhCommanderBaselineSnapshot
{
    /// <summary>Number of kept samples for the commander.</summary>
    [JsonPropertyName("n")]
    public int N { get; init; }

    /// <summary>Mean land count rounded to one decimal place.</summary>
    [JsonPropertyName("landsMean")]
    public double LandsMean { get; init; }

    /// <summary>Sample standard deviation rounded to one decimal place.</summary>
    [JsonPropertyName("landsSd")]
    public double LandsSd { get; init; }
}
