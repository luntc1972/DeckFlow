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
    public async Task ResolveAsync_IndexFolderVariantsShareOneIdentity()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_dbPath);
        var resolver = new CreatorIdentityResolver(new CreatorSuppressionStore(connection), new ContentSourceStore(connection), new ContentSiteIndexStore(connection));
        var index = new ContentSiteIndexStore(connection);
        await index.UpsertRowAsync(CreateRow("Salubrious Snail", "content-kb/salubrioussnail/one.md", "one"));
        await index.UpsertRowAsync(CreateRow("Salubrious Snail", "content-kb/salubrious-snail/two.md", "two"));

        foreach (var representation in new[] { "Salubrious Snail", "salubrioussnail", "salubrious-snail" })
        {
            var identity = await resolver.ResolveAsync(representation);
            Assert.NotNull(identity);
            Assert.Equal(new[] { "salubrious-snail", "salubrioussnail" }, identity!.FolderSlugs.OrderBy(value => value));
        }
    }

    [Fact]
    public async Task ResolveAsync_IndexRowsInEitherOrderUseOrdinalLowestFolderCanonical()
    {
        var rows = new[]
        {
            CreateRow("Salubrious Snail", "content-kb/salubrioussnail/one.md", "one"),
            CreateRow("Salubrious Snail", "content-kb/salubrious-snail/two.md", "two"),
        };

        var forward = await ResolveRowsAsync(rows, "Salubrious Snail");
        var reversed = await ResolveRowsAsync(rows.Reverse().ToArray(), "Salubrious Snail");

        Assert.Equal("salubrious-snail", forward!.CanonicalSlug);
        Assert.Equal(forward.CanonicalSlug, reversed!.CanonicalSlug);
        Assert.Equal(forward.FolderSlugs, reversed.FolderSlugs);
    }

    [Fact]
    public async Task ResolveAsync_ChainedIndexRowsMergeIdentitiesInEveryOrder()
    {
        var rows = new[]
        {
            CreateRow("Alice", "content-kb/f1/one.md", "one"),
            CreateRow("Alice", "content-kb/f2/two.md", "two"),
            CreateRow("Bob", "content-kb/f2/three.md", "three"),
            CreateRow("Carol", "content-kb/f3/four.md", "four"),
        };

        foreach (var orderedRows in Permute(rows))
        {
            var alice = await ResolveRowsAsync(orderedRows, "Alice");
            var bob = await ResolveRowsAsync(orderedRows, "Bob");
            var carol = await ResolveRowsAsync(orderedRows, "Carol");
            Assert.Equal("f1", alice!.CanonicalSlug);
            Assert.Equal(alice.CanonicalSlug, bob!.CanonicalSlug);
            Assert.Equal(new[] { "f1", "f2" }, alice.FolderSlugs.Order());
            Assert.Equal("f3", carol!.CanonicalSlug);
            Assert.Equal(new[] { "f3" }, carol.FolderSlugs);
        }
    }

    [Fact]
    public async Task ResolveAsync_RowMatchingTwoSuppressionsThrowsCreatorAliasConflictException()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_dbPath);
        var suppressions = new CreatorSuppressionStore(connection);
        await suppressions.SuppressAsync("s1", new[] { "Alice" }, "request", DateTimeOffset.UtcNow, null);
        await suppressions.SuppressAsync("s2", new[] { "f1" }, "request", DateTimeOffset.UtcNow, null);
        var index = new ContentSiteIndexStore(connection);
        await index.UpsertRowAsync(CreateRow("Alice", "content-kb/f1/one.md", "one"));
        var resolver = new CreatorIdentityResolver(suppressions, new ContentSourceStore(connection), index);

        await Assert.ThrowsAsync<CreatorAliasConflictException>(() => resolver.ResolveAsync("Alice"));
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

    private static async Task<CreatorIdentity?> ResolveRowsAsync(IReadOnlyList<ContentSiteIndexRow> rows, string representation)
    {
        var path = Path.Combine(Path.GetTempPath(), $"creator-identity-order-{Guid.NewGuid():N}.db");
        try
        {
            var connection = RelationalDatabaseConnection.FromSqlitePath(path);
            var index = new ContentSiteIndexStore(connection);
            foreach (var row in rows)
            {
                await index.UpsertRowAsync(row);
            }

            var resolver = new CreatorIdentityResolver(new CreatorSuppressionStore(connection), new ContentSourceStore(connection), index);
            return await resolver.ResolveAsync(representation);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static IEnumerable<IReadOnlyList<ContentSiteIndexRow>> Permute(IReadOnlyList<ContentSiteIndexRow> rows)
    {
        for (var first = 0; first < rows.Count; first++)
        {
            for (var second = 0; second < rows.Count; second++)
            {
                if (second == first) continue;
                for (var third = 0; third < rows.Count; third++)
                {
                    if (third == first || third == second) continue;
                    yield return new[] { rows[first], rows[second], rows[third], rows[6 - first - second - third] };
                }
            }
        }
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
