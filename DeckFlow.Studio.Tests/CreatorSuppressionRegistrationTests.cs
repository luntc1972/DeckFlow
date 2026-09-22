using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Extensions;
using DeckFlow.Studio.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DeckFlow.Studio.Tests;

public sealed class CreatorSuppressionRegistrationTests
{
    [Fact]
    public void StudioLocalComposition_ResolvesContentKbStores()
    {
        using var provider = new ServiceCollection()
            .AddStudioContentKbStores(Path.GetTempFileName())
            .BuildServiceProvider();

        Assert.IsAssignableFrom<ICreatorSuppressionStore>(provider.GetRequiredService<ICreatorSuppressionStore>());
        Assert.IsAssignableFrom<ICreatorIdentityResolver>(provider.GetRequiredService<ICreatorIdentityResolver>());
        Assert.IsAssignableFrom<IContentVideoStore>(provider.GetRequiredService<IContentVideoStore>());
    }

    [Fact]
    public void StudioProdFactory_CreatesStoreWithSuppressionDependency()
    {
        var store = new ProdStoreFactory().Create("Host=localhost;Database=deckflow;Username=test;Password=test");

        Assert.NotNull(typeof(ContentSiteIndexStore)
            .GetField("_suppressionStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store));
    }

    [Fact]
    public async Task StudioLocalComposition_SuppressedCreatorDisplayNameAndFolder_ExcludesBothApprovedRows()
    {
        var databasePath = Path.GetTempFileName();
        ServiceProvider? provider = null;
        try
        {
            provider = new ServiceCollection()
                .AddStudioContentKbStores(databasePath)
                .BuildServiceProvider();
            var suppressions = provider.GetRequiredService<ICreatorSuppressionStore>();
            var index = provider.GetRequiredService<IContentSiteIndexStore>();
            await index.UpsertContentColumnsOnlyAsync(Row(1, "vid-a", "Alice Display", "content-kb/alice-new/x.md"));
            await index.UpsertContentColumnsOnlyAsync(Row(2, "vid-b", "Alice Display", "content-kb/alice-old/y.md"));
            await index.UpsertContentColumnsOnlyAsync(Row(3, "vid-c", "Carol", "content-kb/carol/z.md"));

            Assert.Equal(3, (await index.GetApprovedRowsAsync()).Count);
            await suppressions.SuppressAsync("alice-new", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);

            var rows = await index.GetApprovedRowsAsync();

            Assert.Collection(rows, row => Assert.Equal("vid-c", row.YoutubeVideoId));
        }
        finally
        {
            provider?.Dispose();
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static ContentSiteIndexRow Row(long id, string videoId, string source, string artifactPath) => new()
    {
        Id = id,
        Source = source,
        Title = videoId,
        VideoUrl = $"https://youtu.be/{videoId}",
        ArtifactPath = artifactPath,
        IndexedUtc = DateTimeOffset.UtcNow,
        ApprovalStatus = "approved",
        ArchetypeTags = Array.Empty<string>(),
        BracketTags = Array.Empty<string>(),
        CardCategoryTags = Array.Empty<string>(),
        YoutubeVideoId = videoId,
    };
}
