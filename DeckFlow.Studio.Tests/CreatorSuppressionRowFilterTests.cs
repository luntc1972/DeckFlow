using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

public sealed class CreatorSuppressionRowFilterTests
{
    [Fact]
    public async Task GetAllowedAsync_MicroSignVariants_ExcludesOnlySuppressedValue()
    {
        var store = new FakeCreatorSuppressionStore();
        store.Suppressed.Add("\u00B5x");
        var filter = new CreatorSuppressionRowFilter(store, new NullCreatorIdentityResolver());
        var rows = new[] { MakeRow(1, "\u03BCx"), MakeRow(2, "\u00B5x") };

        var result = await filter.GetAllowedAsync(rows, CancellationToken.None);

        Assert.Equal(["\u03BCx"], result.Select(row => row.Source));
    }

    [Fact]
    public async Task GetAllowedAsync_SeveralCreators_ReadsSuppressionsOnceWithoutPerCandidateChecks()
    {
        var store = new FakeCreatorSuppressionStore();
        var filter = new CreatorSuppressionRowFilter(store, new FakeCreatorIdentityResolver());
        var rows = new[] { MakeRow(1, "one"), MakeRow(2, "two"), MakeRow(3, "three") };

        _ = await filter.GetAllowedAsync(rows, CancellationToken.None);

        Assert.Equal(1, store.ListCalls);
        Assert.Equal(0, store.IsSuppressedCalls);
    }

    private static ContentSiteIndexRow MakeRow(long id, string source)
        => new()
        {
            Id = id,
            Source = source,
            Title = source,
            VideoUrl = $"https://example.test/{id}",
            ArtifactPath = $"content-kb/{source}/{id}.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
        };

    private sealed class NullCreatorIdentityResolver : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorIdentity?>(null);
    }
}
