using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CommanderCategoryNorms;
using DeckFlow.Web.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CommanderCategoryNormsProviderTests
{
    [Fact]
    public async Task EndToEnd_FlagOn_RealServiceOverStore_ChatGptPromptCarriesBlock()
    {
        var store = CreateStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CommanderCategoryNormsProvider(new CommanderCategoryService(store), store, cache, NullLogger<CommanderCategoryNormsProvider>.Instance);
        var service = PacketByteIdentityFixtures.CreateAnalysisService(
            new StaticMoxfieldDeckImporter(PacketByteIdentityFixtures.BaselineEntries()),
            PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag),
            normsProvider: provider);

        var prompt = (await service.BuildAsync(Request())).AnalysisPromptText;

        Assert.Contains("HARVESTED COMMANDER CATEGORY NORMS - 12 decks (LOW confidence)", prompt);
        Assert.Contains("- Ramp - in 83% of 12 decks", prompt);
        Assert.Contains("- Draw - in 50% of 12 decks", prompt);
    }

    [Fact]
    public async Task RealService_AboveFloor_ReturnsNormsInServiceOrder()
    {
        var store = CreateStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CommanderCategoryNormsProvider(new CommanderCategoryService(store), store, cache, NullLogger<CommanderCategoryNormsProvider>.Instance);

        var result = await provider.GetNormsAsync("Kraum, Ludevic's Opus");

        Assert.NotNull(result);
        Assert.Equal("Kraum, Ludevic's Opus", result.HarvestKey);
        Assert.Equal(12, result.DeckCount);
        Assert.Equal(["Ramp", "Draw"], result.Categories.Select(category => category.Category));
    }

    private static FakeCategoryKnowledgeStore CreateStore()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CommanderDeckCount = 12,
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Sol Ring", 10),
                new CategoryKnowledgeRow("Card Draw", "Rhystic Study", 6),
            ],
        };
        store.Memberships.AddRange(Enumerable.Range(1, 10).Select(deckId => new CategoryDeckMembership("Ramp", "Sol Ring", deckId)));
        store.Memberships.AddRange(Enumerable.Range(1, 6).Select(deckId => new CategoryDeckMembership("Card Draw", "Rhystic Study", deckId)));
        return store;
    }

    private static DeckAnalysisRequest Request() => new()
    {
        DeckInputSource = DeckInputSource.PublicUrl,
        WorkflowStep = 2,
        DeckSource = "https://www.moxfield.com/decks/byte-identity-baseline",
        Format = "Commander",
        TargetCommanderBracket = "Upgraded",
        TargetAiPlatform = "ChatGPT",
        SelectedAnalysisQuestions = ["strengths-weaknesses"],
    };

    private sealed class StaticMoxfieldDeckImporter(List<DeckEntry> entries) : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default) => Task.FromResult(entries.Select(Clone).ToList());

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default) => Task.FromResult(new MoxfieldImportResult(ImportAsync(urlOrDeckId, cancellationToken).GetAwaiter().GetResult(), MoxfieldImportSource.Direct, null));

        private static DeckEntry Clone(DeckEntry entry) => PacketByteIdentityFixtures.CreateDeckEntry(entry.Name, entry.Quantity, entry.Board, entry.SetCode, entry.CollectorNumber, entry.Category);
    }
}
