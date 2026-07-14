using System.IO;
using DeckFlow.Core.Content;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorStyleProfileAdditiveRoundTripTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CreatorStyleProfileStore _store;

    public CreatorStyleProfileAdditiveRoundTripTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"creator-style-profile-additive-test-{Guid.NewGuid():N}.db");
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
    public async Task UpsertAsync_FullyPopulatedFusedTarget_RoundTripsAllAdditiveFields()
    {
        var expected = CreatorStyleProfileTestData.CreateFullProfile("full-additive-round-trip") with
        {
            FusedTargets = new[]
            {
                CreatorStyleProfileTestData.CreateFullyPopulatedFusedTarget()
            }
        };

        await _store.UpsertAsync(expected);

        var actual = await _store.GetBySlugAsync(expected.Slug);

        Assert.NotNull(actual);
        Assert.Single(actual!.FusedTargets);
        Assert.Equal(expected.FusedTargets[0], actual.FusedTargets[0]);
    }
}
