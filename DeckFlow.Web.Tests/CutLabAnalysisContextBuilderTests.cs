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
        var sharedCardResolver = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver, new ScryfallCollectionCardCache()),
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
    public async Task BuildAsync_CompleteComboCard_ResolvesMembershipWithCompleteCombos()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Value Engine", "Enchantment"),
            PoolCard("Combo Partner", "Artifact"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Value Engine", "Enchantment", manaCost: "{2}{U}", cmc: 3),
            Spell("Combo Partner", "Artifact", manaCost: "{2}", cmc: 2),
        ];
        SpellbookCombo combo = new(["Value Engine", "Combo Partner"], ["Infinite mana"], "Make infinite mana.");
        var sharedCardResolver2 = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver2,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver2, new ScryfallCollectionCardCache()),
            new LocalSpellbookService
            {
                Result = new CommanderSpellbookResult([combo], []),
            });

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.True(context.Classification.CardComboMembership.TryGetValue(
            CutLabCardNames.Normalize("Value Engine"),
            out CutLabCardComboMembership? membership));
        Assert.NotNull(membership);
        SpellbookCombo matchedCombo = Assert.Single(membership!.CompleteCombos);
        Assert.Equal(["Value Engine", "Combo Partner"], matchedCombo.CardNames);
        Assert.Equal(["Infinite mana"], matchedCombo.Results);
        Assert.Empty(membership.NearCombos);
    }

    [Fact]
    public async Task BuildAsync_NearComboCard_ResolvesMembershipWithMissingCard()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Near Piece", "Artifact"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Near Piece", "Artifact", manaCost: "{2}", cmc: 2),
        ];
        SpellbookAlmostCombo nearCombo = new("Missing Piece", ["Near Piece"], ["Draw your deck"], "Add the missing piece.");
        var sharedCardResolver3 = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver3,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver3, new ScryfallCollectionCardCache()),
            new LocalSpellbookService
            {
                Result = new CommanderSpellbookResult([], [nearCombo]),
            });

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.True(context.Classification.CardComboMembership.TryGetValue(
            CutLabCardNames.Normalize("Near Piece"),
            out CutLabCardComboMembership? membership));
        Assert.NotNull(membership);
        Assert.Empty(membership!.CompleteCombos);
        SpellbookAlmostCombo matchedCombo = Assert.Single(membership.NearCombos);
        Assert.Equal("Missing Piece", matchedCombo.MissingCard);
    }

    [Fact]
    public async Task BuildAsync_CardOutsideCombos_HasNoMembershipAndNoComboWinconRole()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Value Engine", "Enchantment"),
            PoolCard("Combo Partner", "Artifact"),
            PoolCard("Plain Value", "Artifact"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Value Engine", "Enchantment", manaCost: "{2}{U}", cmc: 3),
            Spell("Combo Partner", "Artifact", manaCost: "{2}", cmc: 2),
            Spell("Plain Value", "Artifact", manaCost: "{3}", cmc: 3),
        ];
        var sharedCardResolver4 = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver4,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver4, new ScryfallCollectionCardCache()),
            new LocalSpellbookService
            {
                Result = new CommanderSpellbookResult(
                    [new SpellbookCombo(["Value Engine", "Combo Partner"], ["Infinite mana"], "Make infinite mana.")],
                    []),
            });

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.False(context.Classification.CardComboMembership.ContainsKey(CutLabCardNames.Normalize("Plain Value")));
        Assert.DoesNotContain("wincons", context.RolesByCardName["Plain Value"]);
    }

    [Fact]
    public async Task BuildAsync_CompleteComboCard_PreservesComboWinconRoleSignal()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Value Engine", "Enchantment"),
            PoolCard("Combo Partner", "Artifact"),
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Value Engine", "Enchantment", manaCost: "{2}{U}", cmc: 3),
            Spell("Combo Partner", "Artifact", manaCost: "{2}", cmc: 2),
        ];
        var sharedCardResolver5 = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver5,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver5, new ScryfallCollectionCardCache()),
            new LocalSpellbookService
            {
                Result = new CommanderSpellbookResult(
                    [new SpellbookCombo(["Value Engine", "Combo Partner"], ["Infinite mana"], "Make infinite mana.")],
                    []),
            });

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"]);

        Assert.Contains("wincons", context.RolesByCardName["Value Engine"]);
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
        var builder = new CutLabAnalysisContextBuilder(resolver, cache, new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

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
            new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()),
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
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

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
        var sharedCardResolver6 = new CountingResolver(cards);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver6,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver6, new ScryfallCollectionCardCache()));

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
        var sharedCardResolver7 = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(
            sharedCardResolver7,
            new CutLabResolvedCardCache(),
            new ScryfallReferenceResolver(sharedCardResolver7, new ScryfallCollectionCardCache()),
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
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext beforeContext = await builder.BuildAsync(
            beforeWorkingList,
            "Focused",
            ["Focused Commander"]);
        CutLabAnalysisContext afterContext = await builder.BuildAsync(
            afterWorkingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: beforeContext.ResolvedCards);

        // Why: this count is satisfied by the shared ScryfallCollectionCardCache since 89723ba9, not
        // by preResolvedCards being honoured -- mutation-proved 2026-08-19. It still pins the warm-cache
        // no-POST property, but the reuse path itself is guarded by the cold-cache sibling below.
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(["Focused Commander", "Counterspell"], afterContext.ResolvedCards.Select(card => card.Name));
    }

    [Fact]
    public async Task BuildAsync_AfterDecisionWithPreResolvedCards_ColdCollectionCache_MakesNoAdditionalResolverCalls()
    {
        // Why: the warm-cache sibling above can no longer fail when preResolvedCards is ignored,
        // because the shared collection cache serves the second build's lookups. ICutLabAnalysisContextBuilder
        // is AddScoped, so the after-decision build really does run on a fresh builder; pairing that with a
        // cold ScryfallCollectionCardCache models the post-eviction / post-restart state (24h positive TTL,
        // bounded capacity) in which preResolvedCards is the only thing standing between the request and a POST.
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
        var beforeBuilder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));
        var afterBuilder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext beforeContext = await beforeBuilder.BuildAsync(
            beforeWorkingList,
            "Focused",
            ["Focused Commander"]);
        CutLabAnalysisContext afterContext = await afterBuilder.BuildAsync(
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
        var cardResolver = new ThrowingResolver();
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

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
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: preResolvedCards);

        // SC-1: the post-batch-miss fallback dispatches SearchFallbackCardAsync, not ResolveSingleAsync.
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(1, resolver.SearchFallbackCallsByName["Typo Card"]);
        Assert.DoesNotContain("Focused Commander", resolver.SearchFallbackCallsByName.Keys);
        Assert.DoesNotContain("Counterspell", resolver.SearchFallbackCallsByName.Keys);
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
        var builder = new CutLabAnalysisContextBuilder(resolver, cache, new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

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
        // SC-1: the post-batch-miss fallback dispatches SearchFallbackCardAsync, not ResolveSingleAsync.
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Single(resolver.SearchFallbackCallsByName);
        Assert.Equal(1, resolver.SearchFallbackCallsByName["Typo Card"]);
        Assert.True(cache.TryGetKnownMissingNames(poolKey, out IReadOnlySet<string>? missingNames));
        Assert.NotNull(missingNames);
        Assert.Contains(CutLabCardNames.Normalize("Typo Card"), missingNames!);
    }

    /// <summary>
    /// SC-1: a batch-collection miss must dispatch <see cref="IScryfallCardResolver.SearchFallbackCardAsync"/>,
    /// never the two-call <see cref="IScryfallCardResolver.ResolveSingleAsync"/> path, since the identifier
    /// already failed on cards/collection moments earlier in the same batch call. OBSERVED RED TODAY
    /// (round-1 review W-7): <c>ResolveSingleCalls == 3</c> and <c>SearchFallbackCalls == 3</c> BOTH, because
    /// <see cref="CountingResolver.ResolveSingleAsync"/> delegates straight to <c>SearchFallbackCardAsync</c>.
    /// The assertion that actually goes RED is <c>ResolveSingleCalls == 0</c>.
    /// </summary>
    [Fact]
    public async Task ResolvePoolCardsAsync_BatchMiss_DispatchesSearchFallbackNotResolveSingle()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Phase 111.1 Probe Alpha", "Creature"),
            PoolCard("Phase 111.1 Probe Beta", "Creature"),
            PoolCard("Phase 111.1 Probe Gamma", "Creature"),
        ];
        var resolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        await builder.ResolvePoolCardsAsync(workingList);

        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(3, resolver.SearchFallbackCalls);
        Assert.Contains("Phase 111.1 Probe Alpha", resolver.SearchFallbackCallsByName.Keys);
        Assert.Contains("Phase 111.1 Probe Beta", resolver.SearchFallbackCallsByName.Keys);
        Assert.Contains("Phase 111.1 Probe Gamma", resolver.SearchFallbackCallsByName.Keys);
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
    }

    /// <summary>
    /// SC-1 (round-1 review W-3): built against a REAL <see cref="ScryfallCardResolver"/> (not the
    /// <see cref="CountingResolver"/> double) with counting collection/search delegate overrides, so the
    /// reduced live-call count is observed end-to-end through <c>ScryfallReferenceResolver</c>, not
    /// merely inferred. OBSERVED RED TODAY: <c>collectionCalls == 4</c> (1 batch POST + 1 redundant
    /// per-miss POST via <c>ResolveSingleAsync</c>) and <c>searchCalls == 3</c>.
    /// </summary>
    [Fact]
    public async Task ResolvePoolCardsAsync_BatchMiss_IssuesExactlyOneLiveCallPerMiss()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Phase 111.1 Probe Alpha", "Creature"),
            PoolCard("Phase 111.1 Probe Beta", "Creature"),
            PoolCard("Phase 111.1 Probe Gamma", "Creature"),
        ];
        int collectionCalls = 0;
        int searchCalls = 0;
        var cardResolver = new ScryfallCardResolver(
            new FakeScryfallRestClientFactory(new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") }),
            new FakeResiliencePipelineProvider(),
            executeCollectionAsyncOverride: (request, _) =>
            {
                collectionCalls++;
                return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallCollectionResponse([], []),
                });
            },
            executeSearchAsyncOverride: (request, _) =>
            {
                searchCalls++;
                return Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse([]),
                });
            });
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

        await builder.ResolvePoolCardsAsync(workingList);

        Assert.Equal(1, collectionCalls);
        Assert.Equal(3, searchCalls);
    }

    /// <summary>
    /// SC-2: a transient HTTP 429 raised by the per-card fallback delegate during
    /// <c>failOpenOnLookupErrors: false</c> pool intake must NOT abort the resolution — the affected
    /// card is simply absent from the resolved set. RED today: throws.
    /// </summary>
    [Fact]
    public async Task ResolvePoolCardsAsync_FailClosed_RateLimitDuringFallback_DoesNotThrow()
    {
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Phase 111.1 Probe Delta", "Creature")];
        var resolver = new CountingResolver([])
        {
            FallbackException = new HttpRequestException("429", null, HttpStatusCode.TooManyRequests),
        };
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        IReadOnlyList<ScryfallCardData> result = await builder.ResolvePoolCardsAsync(
            workingList,
            failOpenOnLookupErrors: false);

        Assert.DoesNotContain(result, card => string.Equals(card.Name, "Phase 111.1 Probe Delta", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// SC-2 guard: a NON-429 failure during fallback dispatch must still fail closed. This must stay
    /// GREEN before and after the fix — it is the guard against over-widening the fail-open predicate.
    /// </summary>
    [Fact]
    public async Task ResolvePoolCardsAsync_FailClosed_NonRateLimitDuringFallback_StillThrows()
    {
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Phase 111.1 Probe Epsilon", "Creature")];
        var resolver = new CountingResolver([])
        {
            FallbackException = new HttpRequestException("503", null, HttpStatusCode.ServiceUnavailable),
        };
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            builder.ResolvePoolCardsAsync(workingList, failOpenOnLookupErrors: false));
    }

    /// <summary>
    /// SC-2: a 429 raised by the BATCH <c>cards/collection</c> call itself (surfaced as
    /// <c>ScryfallReferenceCollectionException</c>, which derives from <see cref="HttpRequestException"/>)
    /// must be treated identically to a 429 on the per-card fallback — fail-open, not fail-closed.
    /// RED today: throws.
    /// </summary>
    [Fact]
    public async Task ResolvePoolCardsAsync_FailClosed_BatchCollectionRateLimit_DoesNotThrow()
    {
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Phase 111.1 Probe Zeta", "Creature")];
        var resolver = new CountingResolver([]) { CollectionStatusCode = HttpStatusCode.TooManyRequests };
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        IReadOnlyList<ScryfallCardData> result = await builder.ResolvePoolCardsAsync(
            workingList,
            failOpenOnLookupErrors: false);

        Assert.DoesNotContain(result, card => string.Equals(card.Name, "Phase 111.1 Probe Zeta", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// B-1 (round-1 review): a transient failure must never be memoized as a permanent miss. A card
    /// left unattempted by a swallowed 429 on the first pass must be re-attempted (not silently
    /// short-circuited) by the next resolution of the same pool, and must appear once the upstream
    /// recovers. Uses a batch-collection 429 (rather than a fallback exception) to force a genuine
    /// collection miss on pass 1: the double's <see cref="CountingResolver.ExecuteCollectionAsync"/>
    /// always echoes its full <c>cards</c> list regardless of the request, so a card already present in
    /// that list would otherwise resolve via a direct collection hit on every pass and never reach
    /// the fallback delegate at all.
    /// </summary>
    [Fact]
    public async Task ResolvePoolCardsAsync_AfterSwallowedRateLimit_SecondResolveReattemptsTheCasualty()
    {
        ScryfallCard targetCard = Spell("Phase 111.1 Probe Eta", "Creature");
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Phase 111.1 Probe Eta", "Creature")];
        var resolver = new CountingResolver([targetCard]) { CollectionStatusCode = HttpStatusCode.TooManyRequests };
        var cache = new CutLabResolvedCardCache();
        var builder = new CutLabAnalysisContextBuilder(resolver, cache, new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        IReadOnlyList<ScryfallCardData> firstPass = await builder.ResolvePoolCardsAsync(
            workingList,
            failOpenOnLookupErrors: false);

        Assert.DoesNotContain(firstPass, card => string.Equals(card.Name, "Phase 111.1 Probe Eta", StringComparison.OrdinalIgnoreCase));

        resolver.CollectionStatusCode = HttpStatusCode.OK;
        int collectionCallsAfterFirstPass = resolver.ExecuteCollectionCalls;

        IReadOnlyList<ScryfallCardData> secondPass = await builder.ResolvePoolCardsAsync(
            workingList,
            failOpenOnLookupErrors: false);

        Assert.True(resolver.ExecuteCollectionCalls > collectionCallsAfterFirstPass);
        Assert.Contains(secondPass, card => string.Equals(card.Name, "Phase 111.1 Probe Eta", StringComparison.OrdinalIgnoreCase));
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
        var builder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

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
        // Why: as in the pre-resolved pair above, this count is now held up by the shared
        // ScryfallCollectionCardCache rather than by the seeded cards being consumed -- mutation-proved
        // 2026-08-19. The consumption itself is guarded by the cold-cache sibling below.
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(2, beforeRestoreContext.ResolvedCards.Count);
        Assert.Equal(3, restoredContext.ResolvedCards.Count);
    }

    [Fact]
    public async Task TrySeedDerivedPool_RestoreWithColdCollectionCache_AvoidsAdditionalResolverCalls()
    {
        // Why: the warm-cache sibling above cannot fail when the seeded cards go unconsumed, because the
        // shared collection cache answers the restore build's lookups. The restore arrives on a later
        // request and therefore a fresh AddScoped builder; a cold cache alongside it is the state in which
        // seeding is load-bearing, so this is the assertion that actually guards TrySeedDerivedPool reuse.
        IReadOnlyList<CutLabPoolCard> fullPool =
        [
            PoolCard("Focused Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Arcane Signet", "Artifact"),
            PoolCard("Counterspell", "Instant"),
        ];
        var resolver = new CountingResolver(
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
            Spell("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
        ]);
        var fullPoolBuilder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));
        var restoreBuilder = new CutLabAnalysisContextBuilder(resolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext fullPoolContext = await fullPoolBuilder.BuildAsync(
            fullPool,
            "Focused",
            ["Focused Commander"]);
        bool seeded = restoreBuilder.TrySeedDerivedPool(fullPool, fullPoolContext.ResolvedCards, out IReadOnlyList<ScryfallCardData>? restoredCards);
        CutLabAnalysisContext restoredContext = await restoreBuilder.BuildAsync(
            fullPool,
            "Focused",
            ["Focused Commander"],
            preResolvedCards: restoredCards);

        Assert.True(seeded);
        Assert.NotNull(restoredCards);
        Assert.Equal(1, resolver.ExecuteCollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(3, restoredContext.ResolvedCards.Count);
    }

    [Fact]
    public async Task BuildAsync_CopiesTypeLineLockAndCommanderOntoAnalyzedCards()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Locked Card", "Artifact", isLocked: true),
            PoolCard("Commander Card", "Legendary Creature - Human", isCommander: true),
            PoolCard("Plain Card", "Enchantment"),
        ];
        var cardResolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Commander Card"],
            preResolvedCards:
            [
                CardData("Locked Card", "Artifact"),
                CardData("Commander Card", "Legendary Creature - Human"),
                CardData("Plain Card", "Enchantment"),
            ]);

        foreach (CutLabPoolCard source in workingList)
        {
            CutLabAnalyzedCard analyzed = Assert.Single(context.AnalyzedCards, card => card.Name == source.Name);
            Assert.Equal(source.TypeLine, analyzed.TypeLine);
            Assert.Equal(source.IsLocked, analyzed.IsLocked);
            Assert.Equal(source.IsCommander, analyzed.IsCommander);
        }
    }

    [Fact]
    public async Task BuildAsync_CommanderFlaggedOnPoolCardButNotInCommanderNames_IsStillMarkedCommander()
    {
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Flagged Commander", "Creature", isCommander: true)];
        var cardResolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            [],
            preResolvedCards: [CardData("Flagged Commander", "Creature")]);

        Assert.True(Assert.Single(context.AnalyzedCards).IsCommander);
    }

    [Fact]
    public async Task BuildAsync_CommanderNamedInCommanderNamesButNotFlagged_IsStillMarkedCommander()
    {
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Named Commander", "Creature")];
        var cardResolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Named Commander"],
            preResolvedCards: [CardData("Named Commander", "Creature")]);

        Assert.True(Assert.Single(context.AnalyzedCards).IsCommander);
    }

    [Fact]
    public async Task BuildAsync_UnresolvedCard_StillProducesAnAnalyzedCardWithCommanderStateApplied()
    {
        IReadOnlyList<CutLabPoolCard> workingList = [PoolCard("Unresolved Commander", "Artifact")];
        var cardResolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Unresolved Commander"],
            preResolvedCards: []);

        CutLabAnalyzedCard analyzed = Assert.Single(context.AnalyzedCards);
        Assert.Empty(context.ResolvedCards);
        Assert.Equal("Unresolved Commander", analyzed.Name);
        Assert.True(analyzed.IsCommander);
    }

    [Fact]
    public async Task BuildAsync_EmitsOneAnalyzedCardPerWorkingListEntry()
    {
        IReadOnlyList<CutLabPoolCard> workingList =
        [
            PoolCard("Locked Card", "Artifact", isLocked: true),
            PoolCard("Commander Card", "Creature", isCommander: true),
            PoolCard("Plain Card", "Enchantment"),
        ];
        var cardResolver = new CountingResolver([]);
        var builder = new CutLabAnalysisContextBuilder(cardResolver, new CutLabResolvedCardCache(), new ScryfallReferenceResolver(cardResolver, new ScryfallCollectionCardCache()));

        CutLabAnalysisContext context = await builder.BuildAsync(
            workingList,
            "Focused",
            ["Commander Card"],
            preResolvedCards:
            [
                CardData("Locked Card", "Artifact"),
                CardData("Commander Card", "Creature"),
                CardData("Plain Card", "Enchantment"),
            ]);

        Assert.Equal(workingList.Count, context.AnalyzedCards.Count);
    }

    private static CutLabPoolCard PoolCard(string name, string typeLine, int quantity = 1, bool isCommander = false, bool isLocked = false)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine,
            IsCommander = isCommander,
            IsLocked = isLocked,
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

        public int SearchFallbackCalls { get; private set; }

        public Dictionary<string, int> SearchFallbackCallsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>When set, <see cref="SearchFallbackCardAsync"/> throws this instead of returning a match.</summary>
        public Exception? FallbackException { get; set; }

        /// <summary>When outside [200,300), <see cref="ExecuteCollectionAsync"/> returns this status with a null payload, simulating a batch-call failure (e.g. a 429 on cards/collection).</summary>
        public HttpStatusCode CollectionStatusCode { get; set; } = HttpStatusCode.OK;

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
        {
            ExecuteCollectionCalls++;
            if ((int)CollectionStatusCode is < 200 or >= 300)
            {
                return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
                {
                    StatusCode = CollectionStatusCode,
                    Data = null,
                });
            }

            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cards.ToList(), []),
            });
        }

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
        {
            SearchFallbackCalls++;
            SearchFallbackCallsByName[cardName] = SearchFallbackCallsByName.TryGetValue(cardName, out int count)
                ? count + 1
                : 1;
            if (FallbackException is not null)
            {
                throw FallbackException;
            }

            return Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));
        }

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

        public Task<IReadOnlyDictionary<string, int>> GetCategoryDeckCountsAsync(string cardName, CancellationToken cancellationToken = default)
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
