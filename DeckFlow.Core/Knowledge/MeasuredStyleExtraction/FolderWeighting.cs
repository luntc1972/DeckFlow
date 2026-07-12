namespace DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

/// <summary>
/// Pure helper for applying curated folder weights and reporting effective sample sizes.
/// </summary>
public static class FolderWeighting
{
    /// <summary>
    /// Applies a curated folder-id weight map to the supplied samples, defaulting to full weight when uncurated.
    /// </summary>
    /// <param name="samples">Creator deck samples to rewrite.</param>
    /// <param name="folderWeights">Curated folder-id to weight map.</param>
    /// <param name="weightsUncurated">
    /// When <see langword="true"/>, every sample remains at weight <c>1.0</c> regardless of folder id.
    /// </param>
    /// <returns>Samples with populated <see cref="CreatorDeckSample.FolderWeight"/> values.</returns>
    public static IReadOnlyList<CreatorDeckSample> ApplyWeights(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlyDictionary<int, double> folderWeights,
        bool weightsUncurated)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(folderWeights);

        return samples
            .Select(sample => sample with
            {
                FolderWeight = ResolveWeight(sample, folderWeights, weightsUncurated)
            })
            .ToList();
    }

    /// <summary>
    /// Computes the fractional folder-weighted effective sample size.
    /// </summary>
    /// <param name="samples">Weighted creator deck samples.</param>
    /// <returns>The sum of per-sample folder weights.</returns>
    public static double EffectiveSampleSize(IReadOnlyList<CreatorDeckSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return samples.Sum(sample => sample.FolderWeight);
    }

    /// <summary>
    /// Returns the raw unweighted deck count.
    /// </summary>
    /// <param name="samples">Creator deck samples.</param>
    /// <returns>The number of supplied samples.</returns>
    public static int RawDeckCount(IReadOnlyList<CreatorDeckSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return samples.Count;
    }

    private static double ResolveWeight(
        CreatorDeckSample sample,
        IReadOnlyDictionary<int, double> folderWeights,
        bool weightsUncurated)
    {
        if (weightsUncurated || !sample.FolderId.HasValue)
        {
            return 1.0;
        }

        return folderWeights.TryGetValue(sample.FolderId.Value, out var weight) ? weight : 1.0;
    }
}
