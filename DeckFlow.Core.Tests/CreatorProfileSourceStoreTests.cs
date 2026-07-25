using System.IO;
using DeckFlow.Core.Content;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CreatorProfileSourceStoreTests : IDisposable
{
    private static readonly DateTimeOffset SourceUpdatedUtc = DateTimeOffset.Parse("2026-07-11T15:22:33Z");
    private readonly string _dbPath;
    private readonly CreatorProfileSourceStore _store;

    public CreatorProfileSourceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"creator-profile-source-test-{Guid.NewGuid():N}.db");
        _store = new CreatorProfileSourceStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape()
    {
        var expected = CreateCuratedSource("full-round-trip");

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        AssertSourcesEqual(expected, actual!);
    }

    [Fact]
    public async Task UpsertAsync_UncuratedDefault_RoundTripsNullFreshnessAndEmptyWeights()
    {
        var expected = CreateCuratedSource("uncurated-default") with
        {
            FolderWeights = new Dictionary<int, double>(),
            WeightsUncurated = true,
            LastCrawledUtc = null
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.True(actual!.WeightsUncurated);
        Assert.Empty(actual.FolderWeights);
        Assert.Null(actual.LastCrawledUtc);
        Assert.Equal(expected.ProfileUsername, actual.ProfileUsername);
    }

    [Fact]
    public async Task UpsertAsync_ReupsertSameSlug_OverwritesFolderWeights()
    {
        var original = CreateCuratedSource("overwrite-same-slug");
        var updated = original with
        {
            FolderWeights = new Dictionary<int, double>
            {
                [9001] = 0.25,
                [9002] = 1.0
            },
            UpdatedUtc = original.UpdatedUtc.AddMinutes(7)
        };

        await _store.UpsertAsync(original);
        await _store.UpsertAsync(updated);

        var actual = await _store.GetBySlugAsync(original.Slug);

        Assert.NotNull(actual);
        AssertFolderWeightsEqual(updated.FolderWeights, actual!.FolderWeights);
        AssertCloseTo(updated.UpdatedUtc, actual.UpdatedUtc);
    }

    [Fact]
    public async Task SetLastCrawledAsync_UpdatesFreshnessWithoutChangingWeights()
    {
        var expected = CreateCuratedSource("set-last-crawled") with
        {
            LastCrawledUtc = null
        };
        var refreshedUtc = expected.UpdatedUtc.AddHours(6);

        await _store.UpsertAsync(expected);
        await _store.SetLastCrawledAsync(expected.Slug, refreshedUtc);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        AssertFolderWeightsEqual(expected.FolderWeights, actual!.FolderWeights);
        AssertCloseTo(refreshedUtc, Assert.IsType<DateTimeOffset>(actual.LastCrawledUtc));
        Assert.Equal(expected.WeightsUncurated, actual.WeightsUncurated);
    }

    private static CreatorProfileSource CreateCuratedSource(string slug)
        => new()
        {
            Slug = slug,
            Platform = "archidekt",
            ProfileUsername = "salubrioussnail",
            ProfileUrl = "https://archidekt.com/u/salubrioussnail",
            FolderWeights = new Dictionary<int, double>
            {
                [101] = 1.0,
                [202] = 0.5,
                [303] = 0.25
            },
            WeightsUncurated = false,
            LastCrawledUtc = DateTimeOffset.Parse("2026-07-11T14:00:00Z"),
            UpdatedUtc = SourceUpdatedUtc
        };

    private static void AssertSourcesEqual(CreatorProfileSource expected, CreatorProfileSource actual)
    {
        Assert.Equal(expected.Slug, actual.Slug);
        Assert.Equal(expected.Platform, actual.Platform);
        Assert.Equal(expected.ProfileUsername, actual.ProfileUsername);
        Assert.Equal(expected.ProfileUrl, actual.ProfileUrl);
        AssertFolderWeightsEqual(expected.FolderWeights, actual.FolderWeights);
        Assert.Equal(expected.WeightsUncurated, actual.WeightsUncurated);
        AssertCloseTo(
            Assert.IsType<DateTimeOffset>(expected.LastCrawledUtc),
            Assert.IsType<DateTimeOffset>(actual.LastCrawledUtc));
        AssertCloseTo(expected.UpdatedUtc, actual.UpdatedUtc);
    }

    private static void AssertFolderWeightsEqual(
        IReadOnlyDictionary<int, double> expected,
        IReadOnlyDictionary<int, double> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected)
        {
            Assert.True(actual.TryGetValue(pair.Key, out var actualWeight));
            Assert.Equal(pair.Value, actualWeight);
        }
    }

    private static void AssertCloseTo(DateTimeOffset expected, DateTimeOffset actual)
    {
        var delta = (expected - actual).Duration();
        Assert.True(
            delta <= TimeSpan.FromSeconds(1),
            $"Expected timestamp within 1 second. Expected {expected:o}, actual {actual:o}.");
    }
}
