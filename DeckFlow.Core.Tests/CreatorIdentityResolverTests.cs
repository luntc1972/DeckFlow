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

    [Fact]
    public async Task ResolveAsync_SourceDisplayNameMatchesIndexRow()
    {
        var identity = await ResolveDataAsync(
            new[] { ("snail-yt", "Salubrious Snail") },
            new[] { CreateRow("Salubrious Snail", "content-kb/salubrioussnail/one.md", "one") },
            Array.Empty<(string, IReadOnlyList<string>)>(), "Salubrious Snail");

        Assert.Equal("snail-yt", identity!.CanonicalSlug);
        Assert.Contains("salubrioussnail", identity.FolderSlugs);
    }

    [Fact]
    public async Task ResolveAsync_SuppressionSlugAndDisplayAliasResolveCompleteIdentity()
    {
        var sources = new[] { ("snail-yt", "Salubrious Snail") };
        var rows = new[] { CreateRow("Salubrious Snail", "content-kb/salubrioussnail/one.md", "one") };
        var suppressions = new[] { ("snail", (IReadOnlyList<string>)new[] { "Salubrious Snail" }) };

        var identities = new List<CreatorIdentity>();
        foreach (var representation in new[] { "snail", "Salubrious Snail" })
        {
            var identity = await ResolveDataAsync(sources, rows, suppressions, representation);

            Assert.Equal("snail", identity!.CanonicalSlug);
            Assert.Single(identity.SourceIds);
            Assert.Contains("salubrioussnail", identity.FolderSlugs);
            identities.Add(identity);
        }

        Assert.Equal(identities[0].SourceIds, identities[1].SourceIds);
        Assert.Equal(identities[0].FolderSlugs, identities[1].FolderSlugs);
    }

    [Fact]
    public async Task ResolveAsync_SuppressionSlugAliasIsIncludedAsFolder()
    {
        var sources = Array.Empty<(string, string)>();
        var rows = new[] { CreateRow("Salubrious Snail", "content-kb/salubrioussnail/one.md", "one") };
        var suppressions = new[] { ("snail", (IReadOnlyList<string>)new[] { "Salubrious Snail", "snail-archive" }) };

        var identities = new List<CreatorIdentity>();
        foreach (var representation in new[] { "Salubrious Snail", "snail-archive" })
        {
            var identity = await ResolveDataAsync(sources, rows, suppressions, representation);

            Assert.Equal("snail", identity!.CanonicalSlug);
            Assert.Contains("salubrioussnail", identity.FolderSlugs);
            Assert.Contains("snail-archive", identity.FolderSlugs);
            identities.Add(identity);
        }

        Assert.Equal(identities[0].FolderSlugs, identities[1].FolderSlugs);
    }

    [Fact]
    public async Task ResolveAsync_MultipleSourcesInOneGroupMergeInEitherOrder()
    {
        var rows = new[]
        {
            CreateRow("Snail", "content-kb/snail-yt/one.md", "one"),
            CreateRow("Snail", "content-kb/snail-pod/two.md", "two"),
        };

        foreach (var sources in new[]
        {
            new[] { ("snail-yt", "Snail"), ("snail-pod", "Snail") },
            new[] { ("snail-pod", "Snail"), ("snail-yt", "Snail") },
        })
        {
            var identity = await ResolveDataAsync(sources, rows, Array.Empty<(string, IReadOnlyList<string>)>(), "Snail");
            Assert.Equal("snail-pod", identity!.CanonicalSlug);
            Assert.Equal(2, identity.SourceIds.Count);
            Assert.Contains("snail-yt", identity.FolderSlugs);
            Assert.Contains("snail-pod", identity.FolderSlugs);
        }
    }

    [Fact]
    public async Task ResolveAsync_UnmatchedConflictingGroupDoesNotBlockQuery()
    {
        var rows = new[]
        {
            CreateRow("Alice", "content-kb/f1/one.md", "one"),
            CreateRow("Carol", "content-kb/f3/two.md", "two"),
        };
        var suppressions = new[] { ("s1", (IReadOnlyList<string>)new[] { "Alice" }), ("s2", (IReadOnlyList<string>)new[] { "f1" }) };

        var carol = await ResolveDataAsync(Array.Empty<(string, string)>(), rows, suppressions, "Carol");
        Assert.Equal("f3", carol!.CanonicalSlug);
        await Assert.ThrowsAsync<CreatorAliasConflictException>(() => ResolveDataAsync(Array.Empty<(string, string)>(), rows, suppressions, "Alice"));
    }

    [Fact]
    public async Task ResolveAsync_UnrelatedCarolResolvesAlongsideMergedSources()
    {
        var identity = await ResolveDataAsync(
            new[] { ("snail-yt", "Snail"), ("snail-pod", "Snail") },
            new[] { CreateRow("Snail", "content-kb/snail-yt/one.md", "one"), CreateRow("Snail", "content-kb/snail-pod/two.md", "two"), CreateRow("Carol", "content-kb/f3/three.md", "three") },
            Array.Empty<(string, IReadOnlyList<string>)>(), "Carol");

        Assert.Equal("f3", identity!.CanonicalSlug);
    }

    [Fact]
    public async Task ResolveAsync_RepointedNodesMergeInEveryOrder()
    {
        var rows = new[]
        {
            CreateRow("X", "content-kb/f5/one.md", "one"), CreateRow("Y", "content-kb/f2/two.md", "two"),
            CreateRow("Y", "content-kb/f5/three.md", "three"), CreateRow("Z", "content-kb/f3/four.md", "four"),
            CreateRow("X", "content-kb/f3/five.md", "five"),
        };

        foreach (var orderedRows in Permute(rows))
        {
            var identity = await ResolveRowsAsync(orderedRows, "X");
            Assert.Equal("f2", identity!.CanonicalSlug);
            Assert.Equal(new[] { "f2", "f3", "f5" }, identity.FolderSlugs);
        }
    }

    [Fact]
    public async Task ResolveAsync_CaseVariantFoldersUseDeterministicCanonical()
    {
        var rows = new[] { CreateRow("X", "content-kb/F1/one.md", "one"), CreateRow("X", "content-kb/f1/two.md", "two") };
        var first = await ResolveRowsAsync(rows, "X");
        var second = await ResolveRowsAsync(rows.Reverse().ToArray(), "X");
        Assert.Equal("F1", first!.CanonicalSlug);
        Assert.Equal(first.CanonicalSlug, second!.CanonicalSlug);
    }

    [Fact]
    public async Task ResolveAsync_BridgedIndexGroupsRetainSuppressedCanonical()
    {
        var identity = await ResolveDataAsync(
            new[] { ("g2-folder", "G1 Name") },
            new[]
            {
                CreateRow("G1 Name", "content-kb/g1-folder/one.md", "one"),
                CreateRow("G2 Name", "content-kb/g2-folder/two.md", "two"),
            },
            new[] { ("gsup", (IReadOnlyList<string>)new[] { "G2 Name" }) },
            "G1 Name");

        Assert.Equal("gsup", identity!.CanonicalSlug);
        Assert.Equal(new[] { "G1 Name", "G2 Name" }, identity.DisplayNames);
        Assert.Equal(new[] { "g1-folder", "g2-folder", "gsup" }, identity.FolderSlugs);
    }

    [Fact]
    public async Task ResolveAsync_SuppressionOnlyCreatorResolvesAllAliases()
    {
        foreach (var representation in new[] { "ghost", "Ghost Creator", "ghost-archive" })
        {
            var identity = await ResolveDataAsync(
                Array.Empty<(string, string)>(),
                Array.Empty<ContentSiteIndexRow>(),
                new[] { ("ghost", (IReadOnlyList<string>)new[] { "Ghost Creator", "ghost-archive" }) },
                representation);

            Assert.Equal("ghost", identity!.CanonicalSlug);
            Assert.Equal(new[] { "Ghost Creator" }, identity.DisplayNames);
            Assert.Equal(new[] { "ghost-archive" }, identity.FolderSlugs);
        }
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

    private static async Task<CreatorIdentity?> ResolveDataAsync(IReadOnlyList<(string Slug, string Name)> sources, IReadOnlyList<ContentSiteIndexRow> rows, IReadOnlyList<(string Slug, IReadOnlyList<string> Aliases)> suppressions, string representation)
    {
        var path = Path.Combine(Path.GetTempPath(), $"creator-identity-data-{Guid.NewGuid():N}.db");
        try
        {
            var connection = RelationalDatabaseConnection.FromSqlitePath(path);
            var sourceStore = new ContentSourceStore(connection);
            var index = new ContentSiteIndexStore(connection);
            var suppressionStore = new CreatorSuppressionStore(connection);
            foreach (var source in sources) await sourceStore.InsertSourceAsync(source.Slug, source.Name, ContentSourceType.Youtube, $"https://example.test/{source.Slug}");
            foreach (var row in rows) await index.UpsertRowAsync(row);
            foreach (var suppression in suppressions) await suppressionStore.SuppressAsync(suppression.Slug, suppression.Aliases, "request", DateTimeOffset.UtcNow, null);
            return await new CreatorIdentityResolver(suppressionStore, sourceStore, index).ResolveAsync(representation);
        }
        finally { SqliteConnection.ClearAllPools(); if (File.Exists(path)) File.Delete(path); }
    }

    private static IEnumerable<IReadOnlyList<ContentSiteIndexRow>> Permute(IReadOnlyList<ContentSiteIndexRow> rows)
    {
        if (rows.Count <= 1) { yield return rows; yield break; }
        for (var index = 0; index < rows.Count; index++)
        {
            foreach (var remainder in Permute(rows.Where((_, candidate) => candidate != index).ToArray()))
            {
                yield return new[] { rows[index] }.Concat(remainder).ToArray();
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
