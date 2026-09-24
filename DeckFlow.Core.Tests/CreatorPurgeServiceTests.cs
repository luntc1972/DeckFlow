using DeckFlow.Core.Content;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorPurgeServiceTests
{
    [Fact]
    public async Task PurgeAsync_AllStepsSucceed_ReturnsOrderedSuccessfulOutcomes()
    {
        var calls = new List<string>();
        var service = CreateService(calls, null);

        var result = await service.PurgeAsync(Identity);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "site-index", "videos", "stated-rules", "profile", "deck-cache", "creator-sources", "profile-source" }, result.Stores.Select(outcome => outcome.StoreName));
        Assert.All(result.Stores, outcome => Assert.True(outcome.Succeeded));
        Assert.Equal(new[] { "content-kb/old-folder", "content-kb/current-folder" }, result.ArtifactFolders);
        Assert.Equal(7, calls.Count);
    }

    [Fact]
    public async Task PurgeAsync_StepThrows_RecordsFailureAndContinues()
    {
        var calls = new List<string>();
        var service = CreateService(calls, "profile");

        var result = await service.PurgeAsync(Identity);

        Assert.False(result.Succeeded);
        var failed = Assert.Single(result.Stores, outcome => !outcome.Succeeded);
        Assert.Equal("profile", failed.StoreName);
        Assert.Equal("failure-profile", failed.Error);
        Assert.Equal(7, calls.Count);
    }

    [Fact]
    public async Task PurgeAsync_IdentityWithFolderSlugs_ListsOneArtifactFolderPerSlug()
    {
        var service = CreateService(new List<string>(), null);

        var result = await service.PurgeAsync(Identity);

        Assert.Equal(new[] { "content-kb/old-folder", "content-kb/current-folder" }, result.ArtifactFolders);
    }

    private static readonly CreatorIdentity Identity = new(
        "canonical-slug",
        new long[] { 17 },
        new[] { "Display Name" },
        new[] { "old-folder", "current-folder" });

    private static CreatorPurgeService CreateService(List<string> calls, string? failingStore)
    {
        var names = new[] { "site-index", "videos", "stated-rules", "profile", "deck-cache", "creator-sources", "profile-source" };
        var steps = names.Select(name => new CreatorPurgeStep(name, (_, _) =>
        {
            calls.Add(name);
            return name == failingStore ? Task.FromException<int>(new InvalidOperationException($"failure-{name}")) : Task.FromResult(1);
        })).ToArray();
        return new CreatorPurgeService(steps);
    }
}
