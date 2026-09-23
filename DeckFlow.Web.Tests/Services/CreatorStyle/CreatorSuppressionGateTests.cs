using DeckFlow.Core.Content;
using DeckFlow.Web.Services.CreatorStyle;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

public sealed class CreatorSuppressionGateTests
{
    [Fact]
    public async Task IsSuppressedAsync_UnknownSlug_UsesRawSuppressionRow()
    {
        var store = StoreWith("raw-suppressed");
        var sut = new CreatorSuppressionGate(new FakeCreatorIdentityResolver(), store);

        Assert.True(await sut.IsSuppressedAsync("raw-suppressed"));
        Assert.False(await sut.IsSuppressedAsync("raw-control"));
    }

    [Theory]
    [InlineData("canonical")]
    [InlineData("display-name")]
    [InlineData("folder-slug")]
    public async Task IsSuppressedAsync_IdentityRepresentationMatchesSuppressionRow(string suppressedRepresentation)
    {
        var store = StoreWith(suppressedRepresentation);
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities["request-slug"] = new CreatorIdentity(
            suppressedRepresentation == "canonical" ? "canonical" : "other-canonical",
            [],
            suppressedRepresentation == "display-name" ? ["display-name"] : [],
            suppressedRepresentation == "folder-slug" ? ["folder-slug"] : []);
        var sut = new CreatorSuppressionGate(resolver, store);

        Assert.True(await sut.IsSuppressedAsync("request-slug"));
    }

    [Fact]
    public async Task IsSuppressedAsync_UnrelatedIdentity_IsNotSuppressed()
    {
        var store = StoreWith("suppressed-representation");
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities["control"] = new CreatorIdentity("control-canonical", [], ["control-name"], ["control-folder"]);
        var sut = new CreatorSuppressionGate(resolver, store);

        Assert.False(await sut.IsSuppressedAsync("control"));
    }

    [Fact]
    public async Task IsSuppressedAsync_ResolverThrows_Propagates()
    {
        var resolver = new FakeCreatorIdentityResolver { ThrowOn = "broken", ExceptionToThrow = new InvalidOperationException("resolver failed") };
        var sut = new CreatorSuppressionGate(resolver, StoreWith());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.IsSuppressedAsync("broken"));
        Assert.Equal("resolver failed", exception.Message);
    }

    [Fact]
    public async Task GetUnsuppressedAsync_ResolverThrows_Propagates()
    {
        var resolver = new FakeCreatorIdentityResolver { ThrowOn = "broken", ExceptionToThrow = new InvalidOperationException("resolver failed") };
        var sut = new CreatorSuppressionGate(resolver, StoreWith());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetUnsuppressedAsync(["broken"]));
        Assert.Equal("resolver failed", exception.Message);
    }

    [Fact]
    public async Task IsSuppressedAsync_ListThrows_Propagates()
    {
        var store = StoreWith();
        store.ReadException = new InvalidOperationException("store failed");
        var sut = new CreatorSuppressionGate(new FakeCreatorIdentityResolver(), store);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.IsSuppressedAsync("slug"));
        Assert.Equal("store failed", exception.Message);
    }

    [Fact]
    public async Task GetUnsuppressedAsync_ThreeSlugs_ReadsSuppressionSnapshotOnce()
    {
        var store = StoreWith("suppressed");
        var sut = new CreatorSuppressionGate(new FakeCreatorIdentityResolver(), store);

        IReadOnlySet<string> result = await sut.GetUnsuppressedAsync(["suppressed", "first", "second"]);

        Assert.Equal(["first", "second"], result.OrderBy(x => x));
        Assert.Equal(1, store.ListCallCount);
    }

    private static FakeCreatorSuppressionStore StoreWith(params string[] slugs)
    {
        var store = new FakeCreatorSuppressionStore();
        foreach (string slug in slugs)
        {
            store.Rows.Add(new CreatorSuppression
            {
                Slug = slug,
                Aliases = [],
                Reason = "test",
                RequestedUtc = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
            });
        }

        return store;
    }
}
