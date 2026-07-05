using System.Threading.Tasks;
using DeckFlow.Studio.ViewModels;
using Xunit;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Tests for <see cref="CreatorManagementCoordinator.LinkCreatorToSourceAsync"/> (P87): links the
/// right creator by its normalized channel ref, or no-ops when the ref is unknown.
/// </summary>
public sealed class CreatorManagementCoordinatorTests
{
    private static CreatorManagementCoordinator Create(FakeCreatorSourceStore store)
        => new(store, new FakeContentKbOrchestrator());

    [Fact]
    public async Task LinkCreatorToSourceAsync_MatchingRef_LinksCreatorWithCanonicalSlug()
    {
        var store = new FakeCreatorSourceStore();
        store.Seed(("Creator A", "https://youtube.com/@A"));
        var creator = (await store.ListAsync()).Single();

        var linked = await Create(store).LinkCreatorToSourceAsync(
            "https://youtube.com/@A", contentSourceId: 55, canonicalSlug: "creator-a");

        Assert.True(linked);
        var call = Assert.Single(store.LinkCalls);
        Assert.Equal((creator.Id, 55L, "creator-a"), call);
    }

    [Fact]
    public async Task LinkCreatorToSourceAsync_RefDiffersOnlyByCaseAndWhitespace_StillLinks()
    {
        // A creator saved by handle links even when the harvest passes a differently-cased/spaced
        // form of the same ref — the link keys on the normalized ref, not an exact string.
        var store = new FakeCreatorSourceStore();
        store.Seed(("Creator A", "https://youtube.com/@A"));

        var linked = await Create(store).LinkCreatorToSourceAsync(
            "  https://YouTube.com/@A  ", contentSourceId: 7, canonicalSlug: "creator-a");

        Assert.True(linked);
        Assert.Single(store.LinkCalls);
    }

    [Fact]
    public async Task LinkCreatorToSourceAsync_UnknownRef_NoOpReturnsFalse()
    {
        var store = new FakeCreatorSourceStore();
        store.Seed(("Creator A", "https://youtube.com/@A"));

        var linked = await Create(store).LinkCreatorToSourceAsync(
            "https://youtube.com/@Unknown", contentSourceId: 9, canonicalSlug: "unknown");

        Assert.False(linked);
        Assert.Empty(store.LinkCalls);
    }
}
