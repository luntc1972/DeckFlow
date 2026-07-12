using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for folder-weight application and sample-size reporting.
/// </summary>
public sealed class FolderWeightingTests
{
    /// <summary>
    /// Verifies curated folder weights are applied by folder id.
    /// </summary>
    [Fact]
    public void ApplyWeights_UsesCuratedFolderMap()
    {
        var samples = new[]
        {
            Sample("deck-1", folderId: 10),
            Sample("deck-2", folderId: 20),
        };
        var folderWeights = new Dictionary<int, double>
        {
            [10] = 1.0,
            [20] = 0.5,
        };

        var weighted = FolderWeighting.ApplyWeights(samples, folderWeights, weightsUncurated: false);

        Assert.Equal(1.0, weighted[0].FolderWeight);
        Assert.Equal(0.5, weighted[1].FolderWeight);
    }

    /// <summary>
    /// Verifies samples without a curated folder mapping remain at full weight.
    /// </summary>
    [Fact]
    public void ApplyWeights_DefaultsMissingFolderIdToOne()
    {
        var samples = new[]
        {
            Sample("deck-1", folderId: null),
            Sample("deck-2", folderId: 30),
        };
        var folderWeights = new Dictionary<int, double>
        {
            [10] = 0.25,
        };

        var weighted = FolderWeighting.ApplyWeights(samples, folderWeights, weightsUncurated: false);

        Assert.All(weighted, sample => Assert.Equal(1.0, sample.FolderWeight));
    }

    /// <summary>
    /// Verifies uncurated creators keep every deck at full weight.
    /// </summary>
    [Fact]
    public void ApplyWeights_WhenUncurated_KeepsAllWeightsAtOne()
    {
        var samples = new[]
        {
            Sample("deck-1", folderId: 10),
            Sample("deck-2", folderId: 20),
        };
        var folderWeights = new Dictionary<int, double>
        {
            [10] = 0.25,
            [20] = 0.5,
        };

        var weighted = FolderWeighting.ApplyWeights(samples, folderWeights, weightsUncurated: true);

        Assert.All(weighted, sample => Assert.Equal(1.0, sample.FolderWeight));
    }

    /// <summary>
    /// Verifies the effective sample remains fractional and distinct from the raw deck count.
    /// </summary>
    [Fact]
    public void EffectiveSampleSize_ReturnsFractionalSumDistinctFromRawDeckCount()
    {
        var weighted = new[]
        {
            Sample("deck-1", folderWeight: 1.0),
            Sample("deck-2", folderWeight: 0.5),
            Sample("deck-3", folderWeight: 1.0),
        };

        var effectiveSampleSize = FolderWeighting.EffectiveSampleSize(weighted);
        var rawDeckCount = FolderWeighting.RawDeckCount(weighted);

        Assert.Equal(2.5, effectiveSampleSize);
        Assert.Equal(3, rawDeckCount);
    }

    private static CreatorDeckSample Sample(string deckId, int? folderId = null, double folderWeight = 1.0)
    {
        return new CreatorDeckSample
        {
            DeckId = deckId,
            Entries =
            [
                new DeckEntry
                {
                    Name = deckId,
                    NormalizedName = deckId.ToLowerInvariant(),
                    Quantity = 1,
                    Board = "mainboard",
                }
            ],
            CardCount = 1,
            FolderId = folderId,
            FolderWeight = folderWeight,
            ConfidenceMarker = "trusted",
        };
    }
}
