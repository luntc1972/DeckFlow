using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using DeckFlow.Core.Tests;

namespace DeckFlow.Core.Tests.Integration;

public sealed class CreatorStyleProfileStorePostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public CreatorStyleProfileStorePostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreatorStyleProfileTestData.CreateFullProfile($"round-trip-{Guid.NewGuid():N}");

        await store.EnsureSchemaAsync();
        await store.UpsertAsync(expected);

        var actual = await store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        CreatorStyleProfileTestData.AssertProfilesEqual(expected, actual!);
    }

    [PostgresFact]
    public async Task UpsertAsync_BelowFloor_InsufficientSampleSurvives_OnPostgres()
    {
        var store = await CreateStoreAsync();
        var expected = CreatorStyleProfileTestData.CreateFullProfile($"below-floor-{Guid.NewGuid():N}") with
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
        var expected = CreatorStyleProfileTestData.CreateFullProfile($"measured-only-{Guid.NewGuid():N}") with
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
        var expected = CreatorStyleProfileTestData.CreateFullProfile($"stated-only-{Guid.NewGuid():N}") with
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
        var expected = CreatorStyleProfileTestData.CreateFullProfile($"fused-only-{Guid.NewGuid():N}") with
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

}
