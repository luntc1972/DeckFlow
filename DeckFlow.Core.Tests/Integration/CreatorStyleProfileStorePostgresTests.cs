using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests.Integration;

public sealed class CreatorStyleProfileStorePostgresTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly DateTimeOffset FullProfileUpdatedUtc = DateTimeOffset.Parse("2026-07-11T12:34:56Z");
    private readonly PostgresContainerFixture _fixture;

    public CreatorStyleProfileStorePostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreateFullProfile($"round-trip-{Guid.NewGuid():N}");

        await store.EnsureSchemaAsync();
        await store.UpsertAsync(expected);

        var actual = await store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        AssertProfilesEqual(expected, actual!);
    }

    [PostgresFact]
    public async Task UpsertAsync_BelowFloor_InsufficientSampleSurvives_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreateFullProfile($"below-floor-{Guid.NewGuid():N}") with
        {
            MinDecks = CreatorStyleProfile.MinDeckFloor - 1,
            InsufficientSample = true
        };

        await store.EnsureSchemaAsync();
        await store.UpsertAsync(expected);

        var actual = await store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Equal(expected.MinDecks, actual!.MinDecks);
        Assert.True(actual.InsufficientSample);
    }

    [PostgresFact]
    public async Task UpsertAsync_MeasuredOnly_EmptySectionsReadBackEmptyNotNull_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreateFullProfile($"measured-only-{Guid.NewGuid():N}") with
        {
            StatedRules = Array.Empty<StatedRule>(),
            FusedTargets = Array.Empty<FusedTarget>()
        };

        await store.EnsureSchemaAsync();
        await store.UpsertAsync(expected);

        var actual = await store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Empty(actual!.StatedRules);
        Assert.NotNull(actual.StatedRules);
        Assert.Single(actual.MeasuredMetrics);
        Assert.Empty(actual.FusedTargets);
        Assert.NotNull(actual.FusedTargets);
    }

    [PostgresFact]
    public async Task UpsertAsync_StatedOnly_EmptySectionsReadBackEmptyNotNull_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreateFullProfile($"stated-only-{Guid.NewGuid():N}") with
        {
            MeasuredMetrics = Array.Empty<MeasuredMetric>(),
            FusedTargets = Array.Empty<FusedTarget>()
        };

        await store.EnsureSchemaAsync();
        await store.UpsertAsync(expected);

        var actual = await store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Single(actual!.StatedRules);
        Assert.Empty(actual.MeasuredMetrics);
        Assert.NotNull(actual.MeasuredMetrics);
        Assert.Empty(actual.FusedTargets);
        Assert.NotNull(actual.FusedTargets);
    }

    [PostgresFact]
    public async Task UpsertAsync_FusedOnly_EmptySectionsReadBackEmptyNotNull_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreateFullProfile($"fused-only-{Guid.NewGuid():N}") with
        {
            StatedRules = Array.Empty<StatedRule>(),
            MeasuredMetrics = Array.Empty<MeasuredMetric>()
        };

        await store.EnsureSchemaAsync();
        await store.UpsertAsync(expected);

        var actual = await store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Empty(actual!.StatedRules);
        Assert.NotNull(actual.StatedRules);
        Assert.Empty(actual.MeasuredMetrics);
        Assert.NotNull(actual.MeasuredMetrics);
        Assert.Single(actual.FusedTargets);
    }

    private async Task<CreatorStyleProfileStore> CreateStoreAsync()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var descriptor = new RelationalDatabaseConnection(
            RelationalDatabaseProvider.Postgres,
            connectionString);
        return new CreatorStyleProfileStore(descriptor);
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
