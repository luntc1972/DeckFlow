using System.Net;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabAnalysisContextBuilder"/> covering cache reuse, fail-open classification, and role analysis.</summary>
public sealed class CutLabAnalysisContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_ReturnsAnalyzedCardsAndRoleCounts()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Forest", "Basic Land — Forest", quantity: 2),
            PoolCard("Rampant Growth", "Sorcery"),
            PoolCard("Value Engine", "Enchantment"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Forest", "Basic Land — Forest"),
            Spell("Rampant Growth", "Sorcery", manaCost: "{1}{G}", oracleText: "Search your library for a basic land card, put that card onto the battlefield tapped, then shuffle.", cmc: 2),
            Spell("Value Engine", "Enchantment", manaCost: "{2}{U}", oracleText: "At the beginning of your upkeep, draw a card.", cmc: 3),
        ];
        var categoryStore = new FakeCategoryKnowledgeStore();
        categoryStore.CategoriesByName["Value Engine"] = ["value engine"];
        var spellbook = new LocalSpellbookService
        {
            Result = new CommanderSpellbookResult(
                [new SpellbookCombo(["Value Engine"], ["Advantage"], "Draw cards.")],
                [new SpellbookAlmostCombo("Combo Piece", ["Value Engine"], ["Advantage"], "Missing piece.")]),
        };
        var builder = new CutLabAnalysisContextBuilder(
            new CountingResolver(cards),
            new CutLabResolvedCardCache(),
            spellbook,
            categoryStore);

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.Equal(4, context.AnalyzedCards.Count);
        Assert.Equal(4, context.RolesByCardName.Count);
        Assert.Equal(2, context.RoleCounts["lands"]);
        Assert.Equal(1, context.RoleCounts["ramp"]);
        Assert.Equal(1, context.RoleCounts["draw"]);
        Assert.Equal(1, context.RoleCounts["engines"]);
        Assert.Equal(3, context.CommanderManaValue);
        Assert.Equal(ManabaseMode.Focused, context.Mode);
        Assert.True(context.Classification.ComboDataAvailable);
        Assert.True(context.Classification.CategoryDataAvailable);
        Assert.Equal(["value engine"], context.AnalyzedCards.Single(card => card.Name == "Value Engine").Categories);
    }

    [Fact]
    public async Task BuildAsync_CacheHit_SkipsResolver()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Arcane Signet", "Artifact"),
        ];
        List<ScryfallCardData> cachedCards =
        [
            CardData("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            CardData("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
        ];
        var cache = new CutLabResolvedCardCache();
        cache.Set(
            CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray()),
            cachedCards);
        var resolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(resolver, cache);

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(2, context.AnalyzedCards.Count);
        Assert.Equal(3, context.CommanderManaValue);
    }

    [Fact]
    public async Task BuildAsync_CacheMiss_ResolvesCachesAndFailsOpenWhenClassificationUnavailable()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Rampant Growth", "Sorcery"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Rampant Growth", "Sorcery", manaCost: "{1}{G}", oracleText: "Search your library for a basic land card, put that card onto the battlefield tapped, then shuffle.", cmc: 2),
        ];
        var cache = new CutLabResolvedCardCache();
        var resolver = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            resolver,
            cache,
            new LocalSpellbookService { Exception = new InvalidOperationException("spellbook down") },
            new ThrowingCategoryKnowledgeStore(new InvalidOperationException("db down")));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);
        bool hit = cache.TryGet(
            CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray()),
            out IReadOnlyList<ScryfallCardData>? cachedCards);

        Assert.True(hit);
        Assert.NotNull(cachedCards);
        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyList<ScryfallCardData>>(cachedCards).Count);
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.False(context.Classification.ComboDataAvailable);
        Assert.False(context.Classification.CategoryDataAvailable);
        Assert.Equal(1, context.RoleCounts["ramp"]);
    }

    [Fact]
    public async Task BuildAsync_ColdPoolWithOneHundredTwentyDistinctCards_UsesTwoCollectionCalls()
    {
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
        ];
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            .. Enumerable.Range(1, 119).Select(index => PoolCard($"Card {index:000}", "Artifact")),
        ];
        cards.AddRange(Enumerable.Range(1, 119).Select(index => Spell($"Card {index:000}", "Artifact", manaCost: "{2}", cmc: 2)));
        var resolver = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache());

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.Equal(120, context.ResolvedCards.Count);
        Assert.Equal(2, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
    }

    [Fact]
    public async Task BuildAsync_UsesMaxCommanderManaValueAcrossCommanders()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Kediss, Emberclaw Familiar", "Legendary Creature — Elemental Lizard", isCommander: true),
            PoolCard("Brinelin, the Moon Kraken", "Legendary Creature — Kraken", isCommander: true),
            PoolCard("Arcane Signet", "Artifact"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Kediss, Emberclaw Familiar", "Legendary Creature — Elemental Lizard", manaCost: "{1}{R}", cmc: 2),
            Spell("Brinelin, the Moon Kraken", "Legendary Creature — Kraken", manaCost: "{4}{U}", cmc: 5),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
        ];
        var builder = new CutLabAnalysisContextBuilder(
            new CountingResolver(cards),
            new CutLabResolvedCardCache());

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Kediss, Emberclaw Familiar", "Brinelin, the Moon Kraken"]);

        Assert.Equal(5, context.CommanderManaValue);
    }

    [Fact]
    public async Task BuildAsync_PreResolvedDfcFrontFaceMatchAndDuplicateResolvedNames_LastWinsWithoutThrow()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Malakir Rebirth", "Instant"),
            PoolCard("Value Engine", "Enchantment"),
        ];
        IReadOnlyList<ScryfallCardData> preResolvedCards =
        [
            CardData("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            CardData("Malakir Rebirth // Malakir Mire", "Instant", manaCost: "{B}", cmc: 1),
            CardData("Value Engine", "Creature — Wall", manaCost: "{3}", cmc: 3),
            CardData("Value Engine", "Enchantment", manaCost: "{2}{U}", cmc: 4),
        ];
        var categoryStore = new FakeCategoryKnowledgeStore();
        categoryStore.CategoriesByName["Malakir Rebirth"] = ["interaction"];
        var builder = new CutLabAnalysisContextBuilder(
            new CountingResolver([]),
            new CutLabResolvedCardCache(),
            categoryKnowledge: categoryStore);

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: preResolvedCards);

        CutLabAnalyzedCard malakir = Assert.Single(context.AnalyzedCards, card => card.Name == "Malakir Rebirth");
        CutLabAnalyzedCard valueEngine = Assert.Single(context.AnalyzedCards, card => card.Name == "Value Engine");
        Assert.NotEmpty(context.RolesByCardName["Malakir Rebirth"]);
        Assert.Equal(4, valueEngine.ManaValue);
    }

    [Fact]
    public async Task BuildAsync_AfterDecisionWithPreResolvedCards_UsesWarmCacheWithoutAdditionalResolverCalls()
    {
        IReadOnlyList<CutLabPoolCard> beforeWorkingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Arcane Signet", "Artifact"),
            PoolCard("Counterspell", "Instant"),
        ];
        IReadOnlyList<CutLabPoolCard> afterWorkingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Counterspell", "Instant"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
            Spell("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
        ];
        var resolver = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache());

        CutLabAnalysisContext beforeContext = await builder.BuildAsync(
            beforeWorkingList,
            "Focused",
            ["Focused Commander"]);
        CutLabAnalysisContext afterContext = await builder.BuildAsync(
            afterWorkingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: beforeContext.ResolvedCards);

        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(["Focused Commander", "Counterspell"], afterContext.ResolvedCards.Select(card => card.Name));
    }

    [Fact]
    public async Task BuildAsync_AddedBasicWithoutPreResolvedCard_UsesSyntheticCardDataWithoutResolverCall()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Wastes", "Basic Land", quantity: 2),
        ];
        IReadOnlyList<ScryfallCardData> preResolvedCards =
        [
            CardData("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
        ];
        var builder = new CutLabAnalysisContextBuilder(new ThrowingResolver(), new CutLabResolvedCardCache());

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: preResolvedCards);

        CutLabAnalyzedCard wastes = Assert.Single(context.AnalyzedCards, card => card.Name == "Wastes");
        Assert.True(wastes.IsLand);
        Assert.Contains("lands", context.RolesByCardName["Wastes"]);
        Assert.Equal(2, context.RoleCounts["lands"]);
        Assert.Contains(context.ResolvedCards, card => card.Name == "Wastes" && card.TypeLine == "Basic Land");
    }

    [Fact]
    public async Task BuildAsync_PreResolvedCardsMissingOneCard_OnlyResolvesTheMissingCard()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Counterspell", "Instant"),
            PoolCard("Typo Card", "Sorcery"),
        ];
        IReadOnlyList<ScryfallCardData> preResolvedCards =
        [
            CardData("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            CardData("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
        ];
        var resolver = new CountingResolver(
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
        ]);
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache());

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: preResolvedCards);

        Assert.Equal(1, resolver.ResolveSingleCalls);
        Assert.Equal(1, resolver.ResolveSingleCallsByName["Typo Card"]);
        Assert.DoesNotContain("Focused Commander", resolver.ResolveSingleCallsByName.Keys);
        Assert.DoesNotContain("Counterspell", resolver.ResolveSingleCallsByName.Keys);
        Assert.Equal(["Focused Commander", "Counterspell"], context.ResolvedCards.Select(card => card.Name));
    }

    [Fact]
    public async Task BuildAsync_SamePoolWithOneUnresolvableCard_FallsBackOnceAndCachesKnownMissing()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Arcane Signet", "Artifact"),
            PoolCard("Counterspell", "Instant"),
            PoolCard("Typo Card", "Sorcery"),
        ];
        var resolver = new CountingResolver(
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
            Spell("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
        ]);
        var cache = new CutLabResolvedCardCache();
        var builder = new CutLabAnalysisContextBuilder(resolver, cache);

        CutLabAnalysisContext first = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);
        CutLabAnalysisContext second = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);
        string poolKey = CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray());

        Assert.Equal(["Focused Commander", "Arcane Signet", "Counterspell"], first.ResolvedCards.Select(card => card.Name));
        Assert.Equal(["Focused Commander", "Arcane Signet", "Counterspell"], second.ResolvedCards.Select(card => card.Name));
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(1, resolver.ResolveSingleCalls);
        Assert.Single(resolver.ResolveSingleCallsByName);
        Assert.Equal(1, resolver.ResolveSingleCallsByName["Typo Card"]);
        Assert.True(cache.TryGetKnownMissingNames(poolKey, out IReadOnlySet<string>? missingNames));
        Assert.NotNull(missingNames);
        Assert.Contains(CutLabCardNames.Normalize("Typo Card"), missingNames!);
    }

    [Fact]
    public async Task TrySeedDerivedPool_RestoreWithWarmFullPoolCache_AvoidsAdditionalResolverCalls()
    {
        IReadOnlyList<CutLabPoolCard> fullPool =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Arcane Signet", "Artifact"),
            PoolCard("Counterspell", "Instant"),
        ];
        IReadOnlyList<CutLabPoolCard> beforeRestoreWorkingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Counterspell", "Instant"),
        ];
        var resolver = new CountingResolver(
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
            Spell("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
        ]);
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache());

        CutLabAnalysisContext fullPoolContext = await builder.BuildAsync(
            fullPool,
            "Focused",
            ["Focused Commander"]);
        bool seededBeforeRestore = builder.TrySeedDerivedPool(
            beforeRestoreWorkingList,
            fullPoolContext.ResolvedCards,
            out IReadOnlyList<ScryfallCardData>? beforeRestoreCards);
        CutLabAnalysisContext beforeRestoreContext = await builder.BuildAsync(
            beforeRestoreWorkingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: beforeRestoreCards);
        bool seeded = builder.TrySeedDerivedPool(fullPool, fullPoolContext.ResolvedCards, out IReadOnlyList<ScryfallCardData>? restoredCards);
        CutLabAnalysisContext restoredContext = await builder.BuildAsync(
            fullPool,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: restoredCards);

        Assert.True(seededBeforeRestore);
        Assert.True(seeded);
        Assert.NotNull(restoredCards);
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(2, beforeRestoreContext.ResolvedCards.Count);
        Assert.Equal(3, restoredContext.ResolvedCards.Count);
    }

    private static CutLabPoolCard PoolCard(string name, string typeLine, int quantity = 1, bool isCommander = false)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine,
            IsCommander = isCommander,
        };

    private static ScryfallCard Spell(
        string name,
        string typeLine,
        string? manaCost = null,
        string? oracleText = null,
        double cmc = 0)
        => new(
            name,
            manaCost,
            typeLine,
            oracleText,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Cmc: cmc);

    private static ScryfallCardData CardData(
        string name,
        string typeLine,
        string? manaCost = null,
        double cmc = 0)
        => new()
        {
            Name = name,
            TypeLine = typeLine,
            ManaCost = manaCost,
            Cmc = cmc,
        };

    private sealed class CountingResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public int ExecuteCollectionCalls { get; private set; }

        public int ResolveSingleCalls { get; private set; }

        public Dictionary<string, int> ResolveSingleCallsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
        {
            ExecuteCollectionCalls++;
            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cards.ToList(), []),
            });
        }

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
        {
            ResolveSingleCalls++;
            ResolveSingleCallsByName[cardName] = ResolveSingleCallsByName.TryGetValue(cardName, out int count)
                ? count + 1
                : 1;
            return SearchFallbackCardAsync(cardName, cancellationToken);
        }
    }

    private sealed class ThrowingResolver : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("ExecuteCollectionAsync should not be called for synthetic basics.");

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("SearchFallbackCardAsync should not be called for synthetic basics.");

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("SearchPrintingFallbackCardAsync should not be called for synthetic basics.");

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => throw new Xunit.Sdk.XunitException("ResolveSingleAsync should not be called for synthetic basics.");
    }

    private sealed class LocalSpellbookService : ICommanderSpellbookService
    {
        public CommanderSpellbookResult? Result { get; set; } = new([], []);

        public Exception? Exception { get; set; }

        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken = default)
            => Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<CommanderSpellbookResult?>(Exception);
    }

    private sealed class ThrowingCategoryKnowledgeStore(Exception exception) : ICategoryKnowledgeStore
    {
        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyDictionary<string, IReadOnlyList<string>>>(exception);

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
