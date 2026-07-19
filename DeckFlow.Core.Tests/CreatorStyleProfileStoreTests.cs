using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorStyleProfileStoreTests : IDisposable
{
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
    public async Task GetAllAsync_EmptyStore_ReturnsEmptyNonNullList()
    {
        var profiles = await _store.GetAllAsync();

        Assert.NotNull(profiles);
        Assert.Empty(profiles);
    }

    [Fact]
    public async Task InterfaceDefaultGetAllAsync_WithoutOverride_ThrowsNotSupportedException()
    {
        ICreatorStyleProfileStore store = new MissingGetAllCreatorStyleProfileStore();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => store.GetAllAsync());

        Assert.Equal("GetAllAsync is not supported by this implementation.", exception.Message);
    }

    [Fact]
    public async Task GetAllAsync_AfterUpsertingProfiles_ReturnsMatchingSummaries()
    {
        var alpha = CreatorStyleProfileTestData.CreateFullProfile("alpha") with
        {
            MinDecks = 39,
            InsufficientSample = false,
            UpdatedUtc = DateTimeOffset.Parse("2026-07-18T10:00:00Z")
        };
        var beta = CreatorStyleProfileTestData.CreateFullProfile("beta") with
        {
            MinDecks = 12,
            InsufficientSample = true,
            UpdatedUtc = DateTimeOffset.Parse("2026-07-18T11:00:00Z")
        };
        var gamma = CreatorStyleProfileTestData.CreateFullProfile("gamma") with
        {
            MinDecks = 3,
            InsufficientSample = true,
            UpdatedUtc = DateTimeOffset.Parse("2026-07-18T12:00:00Z")
        };

        await _store.UpsertAsync(alpha);
        await _store.UpsertAsync(beta);
        await _store.UpsertAsync(gamma);

        var summaries = await _store.GetAllAsync();

        Assert.NotNull(summaries);
        Assert.Equal(3, summaries.Count);

        var bySlug = summaries.ToDictionary(summary => summary.Slug, StringComparer.Ordinal);
        Assert.Collection(
            new[] { alpha, beta, gamma },
            profile =>
            {
                var summary = Assert.Contains(profile.Slug, bySlug);
                Assert.Equal(profile.Platform, summary.Platform);
                Assert.Equal(profile.MinDecks, summary.MinDecks);
                Assert.Equal(profile.InsufficientSample, summary.InsufficientSample);
                CreatorStyleProfileTestData.AssertCloseTo(profile.UpdatedUtc, summary.UpdatedUtc);
            },
            profile =>
            {
                var summary = Assert.Contains(profile.Slug, bySlug);
                Assert.Equal(profile.Platform, summary.Platform);
                Assert.Equal(profile.MinDecks, summary.MinDecks);
                Assert.Equal(profile.InsufficientSample, summary.InsufficientSample);
                CreatorStyleProfileTestData.AssertCloseTo(profile.UpdatedUtc, summary.UpdatedUtc);
            },
            profile =>
            {
                var summary = Assert.Contains(profile.Slug, bySlug);
                Assert.Equal(profile.Platform, summary.Platform);
                Assert.Equal(profile.MinDecks, summary.MinDecks);
                Assert.Equal(profile.InsufficientSample, summary.InsufficientSample);
                CreatorStyleProfileTestData.AssertCloseTo(profile.UpdatedUtc, summary.UpdatedUtc);
            });
    }

    [Fact]
    public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape()
    {
        var expected = CreatorStyleProfileTestData.CreateFullProfile("full-round-trip");

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        CreatorStyleProfileTestData.AssertProfilesEqual(expected, actual!);
    }

    [Fact]
    public async Task UpsertAsync_NullEffectiveSampleSize_RoundTripsAsNull()
    {
        var metric = CreatorStyleProfileTestData.CreateFullProfile("fixture").MeasuredMetrics[0];
        var distribution = Assert.IsType<MetricDistribution>(metric.Distribution);
        var expected = CreatorStyleProfileTestData.CreateFullProfile("null-effective-sample") with
        {
            MeasuredMetrics = new[]
            {
                metric with
                {
                    Distribution = distribution with
                    {
                        EffectiveSampleSize = null
                    }
                }
            }
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Null(actual!.MeasuredMetrics[0].Distribution?.EffectiveSampleSize);
    }

    [Fact]
    public async Task UpsertAsync_BelowFloor_InsufficientSampleSurvivesRoundTrip()
    {
        var expected = CreatorStyleProfileTestData.CreateFullProfile("below-floor") with
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
        var expected = CreatorStyleProfileTestData.CreateFullProfile("measured-only") with
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
        var expected = CreatorStyleProfileTestData.CreateFullProfile("stated-only") with
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
        var expected = CreatorStyleProfileTestData.CreateFullProfile("fused-only") with
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
        var original = CreatorStyleProfileTestData.CreateFullProfile("overwrite-same-slug");
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
        CreatorStyleProfileTestData.AssertCloseTo(updated.UpdatedUtc, secondRead.UpdatedUtc);
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

    private sealed class MissingGetAllCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorStyleProfile?>(null);

        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

}
