using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorIdentityResolverTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-identity-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task ResolveAsync_BySlugReturnsIdentity()
    {
        var resolver = await CreateResolverAsync();
        var identity = await resolver.ResolveAsync("salubrious-snail");
        Assert.NotNull(identity);
        Assert.Equal("salubrious-snail", identity!.CanonicalSlug);
    }

    [Fact]
    public async Task ResolveAsync_ByDisplayNameReturnsIdentity()
    {
        var resolver = await CreateResolverAsync();
        Assert.Equal("salubrious-snail", (await resolver.ResolveAsync("Salubrious Snail"))!.CanonicalSlug);
    }

    [Fact]
    public async Task ResolveAsync_ByFolderSlugReturnsIdentity()
    {
        var resolver = await CreateResolverAsync();
        Assert.Equal("salubrious-snail", (await resolver.ResolveAsync("salubrioussnail"))!.CanonicalSlug);
    }

    [Fact]
    public async Task ResolveAsync_BySourceIdReturnsIdentity()
    {
        var resolver = await CreateResolverAsync();
        Assert.Equal("salubrious-snail", (await resolver.ResolveAsync("2"))!.CanonicalSlug);
    }

    [Fact]
    public async Task ResolveAsync_UnknownReturnsNull()
    {
        var resolver = await CreateResolverAsync();
        Assert.Null(await resolver.ResolveAsync("unknown-creator"));
    }

    [Fact]
    public async Task ResolveAsync_AmbiguousAliasIsRefused()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_dbPath);
        var suppression = new CreatorSuppressionStore(connection);
        await suppression.SuppressAsync("one", new[] { "shared" }, "request", DateTimeOffset.UtcNow, null);
        await Assert.ThrowsAsync<CreatorAliasConflictException>(() => suppression.SuppressAsync("two", new[] { "shared" }, "request", DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsCompleteFieldsForTwoChannelCreator()
    {
        var resolver = await CreateResolverAsync();
        var first = await resolver.ResolveAsync("Sal2brious");
        var second = await resolver.ResolveAsync("sal2brious");
        Assert.Equal("salubrious-snail", first!.CanonicalSlug);
        Assert.Equal(first.CanonicalSlug, second!.CanonicalSlug);
        Assert.Equal(2, first.SourceIds.Count);
        Assert.Contains("Salubrious Snail", first.DisplayNames);
        Assert.Contains("Sal2brious", first.DisplayNames);
        Assert.Contains("salubrioussnail", first.FolderSlugs);
        Assert.Contains("sal2brious", first.FolderSlugs);
    }

    private async Task<CreatorIdentityResolver> CreateResolverAsync()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_dbPath);
        var sources = new ContentSourceStore(connection);
        var index = new ContentSiteIndexStore(connection);
        var suppressions = new CreatorSuppressionStore(connection);
        await sources.InsertSourceAsync("salubrious-snail", "Salubrious Snail", ContentSourceType.Youtube, "https://example.test/one");
        await sources.InsertSourceAsync("sal2brious", "Sal2brious", ContentSourceType.Youtube, "https://example.test/two");
        await suppressions.SuppressAsync("salubrious-snail", new[] { "Salubrious Snail", "Sal2brious", "salubrioussnail", "sal2brious" }, "request", DateTimeOffset.UtcNow, null);
        await index.UpsertRowAsync(CreateRow("Salubrious Snail", "content-kb/salubrioussnail/one.md", "one"));
        await index.UpsertRowAsync(CreateRow("Sal2brious", "content-kb/sal2brious/two.md", "two"));
        return new CreatorIdentityResolver(suppressions, sources, index);
    }

    private static ContentSiteIndexRow CreateRow(string source, string path, string videoId) => new()
    {
        Id = 0,
        Source = source,
        Title = videoId,
        VideoUrl = $"https://example.test/{videoId}",
        ArtifactPath = path,
        IndexedUtc = DateTimeOffset.UtcNow,
        ArchetypeTags = Array.Empty<string>(),
        BracketTags = Array.Empty<string>(),
        CardCategoryTags = Array.Empty<string>(),
        YoutubeVideoId = videoId
    };
}
