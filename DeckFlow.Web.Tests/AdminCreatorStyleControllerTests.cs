using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class AdminCreatorStyleControllerTests
{
    [Fact]
    public async Task Index_SuppressedCreatorIsNotListed()
    {
        var store = new RecordingProfileStore();
        var suppressionStore = new FakeCreatorSuppressionStore();
        suppressionStore.Suppressed.Add("suppressed");
        store.Summaries.Add(NewSummary("suppressed"));
        store.Summaries.Add(NewSummary("control"));
        var result = await CreateController(store, new RecordingPacketService(), new FakeCreatorSuppressionGate(suppressionStore)).Index();

        var model = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Contains(model.AvailableCreators, summary => summary.Slug == "control");
        Assert.DoesNotContain(model.AvailableCreators, summary => summary.Slug == "suppressed");
    }

    [Fact]
    public async Task Index_LinkedSuppressedCreatorIsNotListedWhileUnrelatedControlIsListed()
    {
        var store = new RecordingProfileStore();
        store.Summaries.Add(NewSummary("requested-slug"));
        store.Summaries.Add(NewSummary("control"));
        var suppressionStore = new FakeCreatorSuppressionStore();
        suppressionStore.Rows.Add(new CreatorSuppression { Slug = "linked-display", Aliases = [], Reason = "test", RequestedUtc = DateTimeOffset.UtcNow });
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities["requested-slug"] = new CreatorIdentity("canonical", [], ["linked-display"], []);

        var result = await CreateController(store, new RecordingPacketService(), new CreatorSuppressionGate(resolver, suppressionStore)).Index();

        var model = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.DoesNotContain(model.AvailableCreators, summary => summary.Slug == "requested-slug");
        Assert.Contains(model.AvailableCreators, summary => summary.Slug == "control");
    }

    [Fact]
    public async Task Run_SuppressedCreator_RefusesWithoutBuildingPacket()
    {
        var store = new RecordingProfileStore();
        store.Summaries.Add(NewSummary("suppressed"));
        store.Summaries.Add(NewSummary("control"));
        var packets = new RecordingPacketService();
        var suppressionStore = new FakeCreatorSuppressionStore();
        suppressionStore.Suppressed.Add("suppressed");
        var controller = CreateController(store, packets, suppressionStore);

        var result = await controller.Run(new CreatorStyleRequest { CreatorSlug = "suppressed", DeckText = "1 Sol Ring" });

        var model = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("This creator is suppressed and cannot be used for critique.", model.Notice);
        Assert.Contains(model.AvailableCreators, summary => summary.Slug == "control");
        Assert.DoesNotContain(model.AvailableCreators, summary => summary.Slug == "suppressed");
        Assert.Empty(packets.Requests);
    }

    [Fact]
    public async Task Index_SuppressionReadFailure_PropagatesAfterReadableEmptyStoreSucceeds()
    {
        var packets = new RecordingPacketService();
        var readableStore = new RecordingProfileStore();
        readableStore.Summaries.Add(NewSummary("control"));
        var readable = CreateController(readableStore, packets, new FakeCreatorSuppressionStore());
        Assert.IsType<ViewResult>(await readable.Index());

        var throwingStore = new RecordingProfileStore();
        throwingStore.Summaries.Add(NewSummary("control"));
        var controller = CreateController(throwingStore, packets, new FakeCreatorSuppressionStore { ReadException = new InvalidOperationException("suppression unavailable") });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Index());

        Assert.Equal("suppression unavailable", exception.Message);
        Assert.Empty(packets.Requests);
    }

    [Fact]
    public async Task Index_NotSupportedSuppressionRead_PropagatesAfterReadableEmptyStoreSucceeds()
    {
        var packets = new RecordingPacketService();
        var readable = CreateController(new RecordingProfileStore(), packets, new FakeCreatorSuppressionStore());
        Assert.IsType<ViewResult>(await readable.Index());

        var throwingStore = new RecordingProfileStore();
        throwingStore.Summaries.Add(NewSummary("control"));
        var controller = CreateController(throwingStore, packets, new FakeCreatorSuppressionStore { ReadException = new NotSupportedException("suppression unavailable") });
        await Assert.ThrowsAsync<NotSupportedException>(() => controller.Index());
    }

    private static AdminCreatorStyleController CreateController(RecordingProfileStore store, RecordingPacketService packets, ICreatorSuppressionStore suppressionStore)
        => CreateController(store, packets, new FakeCreatorSuppressionGate(suppressionStore));

    private static AdminCreatorStyleController CreateController(RecordingProfileStore store, RecordingPacketService packets, ICreatorSuppressionGate suppressionGate)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = "https://deckflow.test";
        return new AdminCreatorStyleController(store, packets, suppressionGate, NullLogger<AdminCreatorStyleController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static CreatorStyleProfileSummary NewSummary(string slug) => new() { Slug = slug, Platform = "youtube", MinDecks = 5, UpdatedUtc = DateTimeOffset.UtcNow };

    private sealed class RecordingProfileStore : ICreatorStyleProfileStore
    {
        public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public List<CreatorStyleProfileSummary> Summaries { get; } = [];
        public Exception? ReadException { get; init; }
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult<CreatorStyleProfile?>(null);
        public Task<IReadOnlyList<CreatorStyleProfileSummary>> GetAllAsync(CancellationToken cancellationToken = default)
            => ReadException is null ? Task.FromResult<IReadOnlyList<CreatorStyleProfileSummary>>(Summaries) : Task.FromException<IReadOnlyList<CreatorStyleProfileSummary>>(ReadException);
    }

    private sealed class RecordingPacketService : ICreatorStylePacketService
    {
        public List<CreatorStyleRequest> Requests { get; } = [];
        public Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            throw new InvalidOperationException("Packet service should not be called.");
        }
    }
}
