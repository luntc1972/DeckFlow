using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Verifies Cut Lab commander state survives state-only round-trip processing.</summary>
public sealed class CutLabPartnerStateRoundTripTests
{
    [Fact]
    public async Task Process_StateOnlyRestore_PartnerCommandersBothStayCommanders()
    {
        var (service, request) = CreateService(partnerCommanders: true);
        var firstResult = await service.ProcessAsync(request);
        Assert.True(firstResult.HasResult);
        var firstState = CutLabStateSerializer.Deserialize(firstResult.SerializedStateJson);
        Assert.All(firstState.Pool.Where(card => card.Name is "Kediss, Emberclaw Familiar" or "Brinelin, the Moon Kraken"), card => Assert.True(card.IsCommander));

        var result = await CreateController(service).Process(new CutLabRequest { CutLabStateJson = firstResult.SerializedStateJson });
        var model = Assert.IsType<CutLabViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasResult);
        var state = CutLabStateSerializer.Deserialize(model.CutLabStateJson);
        var kediss = Assert.Single(state.Pool, card => card.Name == "Kediss, Emberclaw Familiar");
        var brinelin = Assert.Single(state.Pool, card => card.Name == "Brinelin, the Moon Kraken");
        Assert.True(kediss.IsCommander, "Kediss, Emberclaw Familiar should remain a commander after state-only restore.");
        Assert.True(kediss.IsLocked);
        Assert.True(brinelin.IsCommander, "Brinelin, the Moon Kraken should remain a commander after state-only restore.");
        Assert.True(brinelin.IsLocked);
        Assert.Equal(firstResult.CardCount, model.CardCount);
    }

    [Fact]
    public async Task Process_StateOnlyRestore_SingleCommanderStillResolvesOneCommander()
    {
        var (service, request) = CreateService(partnerCommanders: false);
        var firstResult = await service.ProcessAsync(request);
        Assert.True(firstResult.HasResult);

        var result = await CreateController(service).Process(new CutLabRequest { CutLabStateJson = firstResult.SerializedStateJson });
        var model = Assert.IsType<CutLabViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasResult);
        var state = CutLabStateSerializer.Deserialize(model.CutLabStateJson);
        var commanders = state.Pool.Where(card => card.IsCommander).ToList();
        var commander = Assert.Single(commanders);
        Assert.Equal("Kediss, Emberclaw Familiar", commander.Name);
    }

    private static (CutLabPageService Service, CutLabRequest Request) CreateService(bool partnerCommanders)
    {
        var entries = new List<DeckEntry> { Entry("Kediss, Emberclaw Familiar", "commander") };
        if (partnerCommanders) entries.Add(Entry("Brinelin, the Moon Kraken", "commander"));
        entries.AddRange(Enumerable.Range(1, 120).Select(i => Entry($"Card {i:000}", "mainboard")));
        var cards = new List<ScryfallCard> { Spell("Kediss, Emberclaw Familiar", "Legendary Creature — Elemental Lizard", "{1}{R}", 2) };
        if (partnerCommanders) cards.Add(Spell("Brinelin, the Moon Kraken", "Legendary Creature — Kraken", "{4}{U}", 5));
        cards.AddRange(Enumerable.Range(1, 120).Select(i => Spell($"Card {i:000}", "Artifact")));
        return (new CutLabPageService(new FakeLoader(entries), new FakeResolver(cards), new FakeBanListService([])), new CutLabRequest { DeckInputSource = DeckInputSource.PasteText, DeckText = "pool", Bracket = 4, PlayExperience = "Focused" });
    }

    private static CutLabController CreateController(ICutLabPageService service) => new(service, new FakeCutLabWhatifService(), new FakeExportService(), new FakeLogger<CutLabController>()) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    private static DeckEntry Entry(string name, string board) => new() { Name = name, NormalizedName = name.ToLowerInvariant(), Quantity = 1, Board = board };
    private static ScryfallCard Spell(string name, string typeLine, string? manaCost = null, double cmc = 0) => new(name, manaCost, typeLine, null, null, null, null, null, null, null, null, Cmc: cmc);

    private sealed class FakeLoader(List<DeckEntry> entries) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DeckSourceLoadResult> LoadFromSourceAsync(string deckSource, UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized, CancellationToken cancellationToken = default) => Task.FromResult(new DeckSourceLoadResult(entries, null));
        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100) { }
    }

    private sealed class FakeResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken) => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request) { StatusCode = HttpStatusCode.OK, Data = new ScryfallCollectionResponse(cards.ToList(), []) });
        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken) => Task.FromResult(cards.FirstOrDefault(card => card.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase)));
        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken) => SearchFallbackCardAsync(cardName, cancellationToken);
        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken) => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class FakeBanListService(IReadOnlyList<string> bannedCards) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(bannedCards);
    }
    private sealed class FakeExportService : ICutLabExportService
    {
        public Task<CutLabExportView> BuildExportAsync(CutLabState state, string playExperience, IReadOnlyList<string> commanderNames, CancellationToken cancellationToken) => Task.FromResult(new CutLabExportView());
    }
}
