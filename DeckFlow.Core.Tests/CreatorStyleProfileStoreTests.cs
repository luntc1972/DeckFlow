using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorStyleProfileStoreTests : IDisposable
{
    private static readonly DateTimeOffset FullProfileUpdatedUtc = DateTimeOffset.Parse("2026-07-11T12:34:56Z");
    private readonly string _dbPath;
    private readonly CreatorStyleProfileStore _store;

    public CreatorStyleProfileStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"creator-style-profile-test-{Guid.NewGuid():N}.db");
        _store = new CreatorStyleProfileStore(_dbPath);
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
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();
    }

    [Fact]
    public async Task GetBySlugAsync_UnknownSlug_ReturnsNull()
    {
        var profile = await _store.GetBySlugAsync("missing-slug");

        Assert.Null(profile);
    }

    [Fact]
    public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape()
    {
        var expected = CreateFullProfile("full-round-trip");

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        AssertProfilesEqual(expected, actual!);
    }

    [Fact]
    public async Task UpsertAsync_BelowFloor_InsufficientSampleSurvivesRoundTrip()
    {
        var expected = CreateFullProfile("below-floor") with
        {
            MinDecks = CreatorStyleProfile.MinDeckFloor - 1,
            InsufficientSample = true
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Equal(expected.MinDecks, actual!.MinDecks);
        Assert.True(actual.InsufficientSample);
    }

    [Fact]
    public async Task UpsertAsync_MeasuredOnly_EmptySectionsReadBackEmptyNotNull()
    {
        var expected = CreateFullProfile("measured-only") with
        {
            StatedRules = Array.Empty<StatedRule>(),
            FusedTargets = Array.Empty<FusedTarget>()
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Empty(actual!.StatedRules);
        Assert.NotNull(actual.StatedRules);
        Assert.Single(actual.MeasuredMetrics);
        Assert.Empty(actual.FusedTargets);
        Assert.NotNull(actual.FusedTargets);
    }

    [Fact]
    public async Task UpsertAsync_StatedOnly_EmptySectionsReadBackEmptyNotNull()
    {
        var expected = CreateFullProfile("stated-only") with
        {
            MeasuredMetrics = Array.Empty<MeasuredMetric>(),
            FusedTargets = Array.Empty<FusedTarget>()
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Single(actual!.StatedRules);
        Assert.Empty(actual.MeasuredMetrics);
        Assert.NotNull(actual.MeasuredMetrics);
        Assert.Empty(actual.FusedTargets);
        Assert.NotNull(actual.FusedTargets);
    }

    [Fact]
    public async Task UpsertAsync_FusedOnly_EmptySectionsReadBackEmptyNotNull()
    {
        var expected = CreateFullProfile("fused-only") with
        {
            StatedRules = Array.Empty<StatedRule>(),
            MeasuredMetrics = Array.Empty<MeasuredMetric>()
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Empty(actual!.StatedRules);
        Assert.NotNull(actual.StatedRules);
        Assert.Empty(actual.MeasuredMetrics);
        Assert.NotNull(actual.MeasuredMetrics);
        Assert.Single(actual.FusedTargets);
    }

    [Fact]
    public async Task UpsertAsync_ReupsertSameSlug_OverwritesSingleRow()
    {
        var original = CreateFullProfile("overwrite-same-slug");
        var updated = original with
        {
            Platform = "moxfield",
            MinDecks = original.MinDecks + 4,
            UpdatedUtc = original.UpdatedUtc.AddMinutes(5)
        };

        await _store.UpsertAsync(original);
        var firstRead = await _store.GetBySlugAsync(original.Slug);

        await _store.UpsertAsync(updated);
        var secondRead = await _store.GetBySlugAsync(original.Slug);
        var count = await CountRowsBySlugAsync(original.Slug);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.Equal(1, count);
        Assert.Equal(updated.Platform, secondRead!.Platform);
        Assert.Equal(updated.MinDecks, secondRead.MinDecks);
        Assert.True(secondRead.UpdatedUtc > firstRead!.UpdatedUtc);
        AssertCloseTo(updated.UpdatedUtc, secondRead.UpdatedUtc);
    }

    private async Task<int> CountRowsBySlugAsync(string slug)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM creator_style_profile
             WHERE slug = @slug;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@slug", slug);

        var count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count);
    }

    private static CreatorStyleProfile CreateFullProfile(string slug)
        => new()
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = CreatorStyleProfile.MinDeckFloor + 2,
            InsufficientSample = false,
            StatedRules = new[]
            {
                new StatedRule
                {
                    Category = "curve",
                    TargetMetric = "avg_cmc",
                    TargetValue = 2.3,
                    Comparator = "<=",
                    SourceClip = "Keep the curve low.",
                    Confidence = 0.87
                }
            },
            MeasuredMetrics = new[]
            {
                new MeasuredMetric
                {
                    Metric = "lands",
                    Value = 35.4,
                    NumDecks = 7,
                    Distribution = new MetricDistribution
                    {
                        Mean = 35.4,
                        Min = 33.0,
                        Max = 37.0,
                        StdDev = 1.2
                    }
                }
            },
            FusedTargets = new[]
            {
                new FusedTarget
                {
                    Metric = "interaction",
                    Value = 11.5,
                    Weight = 0.65,
                    Source = "fused",
                    Conflict = new FusedConflict
                    {
                        StatedValue = 12.0,
                        MeasuredValue = 11.0,
                        Delta = 1.0
                    }
                }
            },
            UpdatedUtc = FullProfileUpdatedUtc
        };

    private static void AssertProfilesEqual(CreatorStyleProfile expected, CreatorStyleProfile actual)
    {
        Assert.Equal(expected.Slug, actual.Slug);
        Assert.Equal(expected.Platform, actual.Platform);
        Assert.Equal(expected.MinDecks, actual.MinDecks);
        Assert.Equal(expected.InsufficientSample, actual.InsufficientSample);
        Assert.Single(actual.StatedRules);
        Assert.Equal(expected.StatedRules[0], actual.StatedRules[0]);
        Assert.Single(actual.MeasuredMetrics);
        Assert.Equal(expected.MeasuredMetrics[0].Metric, actual.MeasuredMetrics[0].Metric);
        Assert.Equal(expected.MeasuredMetrics[0].Value, actual.MeasuredMetrics[0].Value);
        Assert.Equal(expected.MeasuredMetrics[0].NumDecks, actual.MeasuredMetrics[0].NumDecks);
        Assert.Equal(expected.MeasuredMetrics[0].Distribution, actual.MeasuredMetrics[0].Distribution);
        Assert.Single(actual.FusedTargets);
        Assert.Equal(expected.FusedTargets[0].Metric, actual.FusedTargets[0].Metric);
        Assert.Equal(expected.FusedTargets[0].Value, actual.FusedTargets[0].Value);
        Assert.Equal(expected.FusedTargets[0].Weight, actual.FusedTargets[0].Weight);
        Assert.Equal(expected.FusedTargets[0].Source, actual.FusedTargets[0].Source);
        Assert.Equal(expected.FusedTargets[0].Conflict, actual.FusedTargets[0].Conflict);
        AssertCloseTo(expected.UpdatedUtc, actual.UpdatedUtc);
    }

    private static void AssertCloseTo(DateTimeOffset expected, DateTimeOffset actual)
    {
        var delta = (expected - actual).Duration();
        Assert.True(
            delta <= TimeSpan.FromSeconds(1),
            $"Expected UpdatedUtc within 1 second. Expected {expected:o}, actual {actual:o}.");
    }
}
