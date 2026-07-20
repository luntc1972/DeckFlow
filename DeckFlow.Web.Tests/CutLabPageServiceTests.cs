using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabPageService"/> covering orchestration, validation, and state round-trip behavior.</summary>
public sealed class CutLabPageServiceTests
{
    [Fact]
    public async Task ProcessAsync_HappyPath_ReturnsCountLegalityIntentAndLockedCommander()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            PrimaryPlan = "Counters",
            SecondaryPlan = "Blink",
            Bracket = 3,
            PlayExperience = "Resilient midrange",
        };

        var result = await service.ProcessAsync(request);
        var viewModel = CutLabViewModel.From(request, result);

        Assert.Null(result.ErrorMessage);
        Assert.True(result.HasResult);
        Assert.Equal(120, result.CardCount);
        Assert.True(result.IsLegal);
        Assert.Empty(result.BannedCardsPresent);
        Assert.Equal("Counters", result.State!.Intent.PrimaryPlan);
        Assert.Equal("Blink", result.State.Intent.SecondaryPlan);
        Assert.Equal(3, result.State.Intent.Bracket);
        Assert.Equal("Resilient midrange", result.State.Intent.PlayExperience);
        Assert.True(Assert.Single(result.State.Pool, card => card.IsCommander).IsLocked);
        Assert.False(result.CommanderSelectionRequired);
        Assert.NotNull(result.SerializedStateJson);
        Assert.Equal(DeckPageTab.CutLab, viewModel.ActiveTab);
        Assert.Equal(120, viewModel.CardCount);
        Assert.Equal(1, Assert.Single(viewModel.Pool, card => card.IsLocked).Quantity);
    }

    [Fact]
    public void From_GroupsWeakFloorFindingsIntoSingleBlock()
    {
        var request = new CutLabRequest();
        var result = new CutLabProcessResult
        {
            HasResult = true,
            Findings = new CutLabStructuralFindingsResult(
                [
                    new CutLabFinding(
                        CutLabFindingKind.WeakFloorCase,
                        "Weak floor cases",
                        "Interaction is at 8 against a floor of 7 — every card in this role is effectively protected already.",
                        [new CutLabFindingEvidence("Swords to Plowshares", null)]),
                    new CutLabFinding(
                        CutLabFindingKind.WeakFloorCase,
                        "Weak floor cases",
                        "Payoffs is at 0 against a floor of 6 — every card in this role is effectively protected already.",
                        []),
                    new CutLabFinding(
                        CutLabFindingKind.RedundantFinishers,
                        "Redundant finishers",
                        "6 win conditions against a floor of 3 — more than one game usually needs.",
                        [new CutLabFindingEvidence("Torment of Hailfire", null)]),
                ],
                true,
                true),
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.Equal(2, model.FindingGroups.Count);
        CutLabFindingGroupView weakFloorGroup = Assert.Single(model.FindingGroups, group => group.Kind == CutLabFindingKind.WeakFloorCase);
        Assert.Equal("Weak floor cases", weakFloorGroup.Heading);
        Assert.Equal(2, weakFloorGroup.Items.Count);
        Assert.Equal(
            [
                "Interaction is at 8 against a floor of 7 — every card in this role is effectively protected already.",
                "Payoffs is at 0 against a floor of 6 — every card in this role is effectively protected already.",
            ],
            weakFloorGroup.Items.Select(item => item.Lead));
        Assert.Equal(["Swords to Plowshares"], weakFloorGroup.Items[0].Evidence);
        Assert.Empty(weakFloorGroup.Items[1].Evidence);
    }

    [Fact]
    public async Task ProcessAsync_BannedCardsPresent_ReturnsIllegalSummary()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService(["Card 017", "Black Lotus"]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.False(result.IsLegal);
        Assert.Equal(["Card 017"], result.BannedCardsPresent);
    }

    [Fact]
    public async Task ProcessAsync_BanListFetchHttpFailure_FailsOpenPreservesStateAndAddsWarning()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var logger = new FakeLogger<CutLabPageService>();
        var priorState = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Atraxa, Praetors' Voice",
                    Quantity = 1,
                    TypeLine = "Legendary Creature — Phyrexian Angel Horror",
                    IsCommander = true,
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Card 010",
                    Quantity = 1,
                    TypeLine = "Artifact",
                    IsLocked = true,
                    PackageId = "ramp",
                },
            ],
            Packages =
            [
                new CutLabPackage
                {
                    Id = "ramp",
                    Name = "Ramp Core",
                    Locked = true,
                },
            ],
        };
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new ThrowingBanListService(new HttpRequestException("banlist down")),
            logger: logger);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.True(result.IsLegal);
        Assert.Empty(result.BannedCardsPresent);
        Assert.Contains(result.Warnings, warning => warning.Contains("legality was not verified", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, warning => warning.Contains("banlist fetch failed", StringComparison.Ordinal));
        var carriedCard = Assert.Single(result.State!.Pool, card => card.Name == "Card 010");
        Assert.True(carriedCard.IsLocked);
        Assert.Equal("ramp", carriedCard.PackageId);
        var roundTrippedState = CutLabStateSerializer.Deserialize(result.SerializedStateJson);
        Assert.Equal("Atraxa, Praetors' Voice", roundTrippedState.Commander);
        Assert.Contains(roundTrippedState.Pool, card => card.Name == "Card 010" && card.IsLocked && card.PackageId == "ramp");
        Assert.Contains(roundTrippedState.Packages, package => package.Id == "ramp" && package.Locked);
    }

    [Fact]
    public async Task ProcessAsync_SubmittedStateJsonCarriesForwardLocksAndPackages()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var priorState = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Atraxa, Praetors' Voice",
                    Quantity = 1,
                    TypeLine = "Legendary Creature — Phyrexian Angel Horror",
                    IsCommander = true,
                    IsLocked = false,
                },
                new CutLabPoolCard
                {
                    Name = "Card 010",
                    Quantity = 1,
                    TypeLine = "Artifact",
                    IsLocked = true,
                    PackageId = "ramp",
                },
            ],
            Packages =
            [
                new CutLabPackage
                {
                    Id = "ramp",
                    Name = "Ramp Core",
                    Locked = true,
                },
            ],
        };
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        };

        var result = await service.ProcessAsync(request);

        var carriedCard = Assert.Single(result.State!.Pool, card => card.Name == "Card 010");
        Assert.True(carriedCard.IsLocked);
        Assert.Equal("ramp", carriedCard.PackageId);
        Assert.True(Assert.Single(result.State.Packages).Locked);
        Assert.True(Assert.Single(result.State.Pool, card => card.IsCommander).IsLocked);
    }

    [Fact]
    public async Task ProcessAsync_AmbiguousCommanderInference_ReturnsSelectionRequired()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Academy Rector", "mainboard"),
            Entry("Arcane Signet", "mainboard"),
            Entry("Ancient Tomb", "mainboard"),
            Entry("Winota, Joiner of Forces", "mainboard"),
        };
        entries.AddRange(BuildBasicMainboard(start: 1, count: 101));
        var cards = new List<ScryfallCard>
        {
            Spell("Academy Rector", "Creature — Human Cleric"),
            Spell("Arcane Signet", "Artifact"),
            Spell("Ancient Tomb", "Land"),
            Spell("Winota, Joiner of Forces", "Legendary Creature — Human Warrior"),
        };
        cards.AddRange(BuildBasicResolvedCards(start: 1, count: 101));
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.True(result.CommanderSelectionRequired);
        Assert.Contains("Winota, Joiner of Forces", result.CommanderChoices);
        Assert.DoesNotContain(result.State!.Pool, card => card.IsCommander);
    }

    [Fact]
    public async Task ProcessAsync_NonEmptyPoolWithoutResolvedCommander_ReturnsFallbackSelectionRequired()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Forest", "mainboard") with { Quantity = 10 },
            Entry("Atraxa, Praetors' Voice", "mainboard"),
        };
        entries.AddRange(BuildBasicMainboard(start: 1, count: 100));
        var cards = new List<ScryfallCard>
        {
            Spell("Forest", "Basic Land — Forest"),
            Spell("Atraxa, Praetors' Voice", "Legendary Creature — Phyrexian Angel Horror"),
        };
        cards.AddRange(BuildBasicResolvedCards(start: 1, count: 100));
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.Null(result.ErrorMessage);
        Assert.True(result.HasResult);
        Assert.True(result.CommanderSelectionRequired);
        Assert.NotEmpty(result.CommanderChoices);
        Assert.Contains("Atraxa, Praetors' Voice", result.CommanderChoices);
    }

    [Theory]
    [InlineData(100, "This pool already has 100 cards or fewer — Cut Lab is for trimming an oversized pool down to 100. Try Deck Sync or Deck Analysis instead.")]
    [InlineData(151, "This pool has too many cards for Cut Lab (limit 150 plus commander). Trim it closer to 150 before importing.")]
    public async Task ProcessAsync_InvalidPoolCount_ReturnsValidatorMessage(int nonCommanderCount, string expectedMessage)
    {
        var entries = BuildPoolEntries(nonCommanderCount, "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.False(result.HasResult);
    }

    [Fact]
    public async Task ProcessAsync_DeckParseFailure_ReturnsErrorMessage()
    {
        var service = new CutLabPageService(
            new ThrowingLoader(new DeckParseException("Bad deck input.")),
            new FakeResolver([]),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.Equal("Bad deck input.", result.ErrorMessage);
        Assert.False(result.HasResult);
    }

    [Fact]
    public async Task ProcessAsync_OneHundredFiftyPoolPlusCommander_PassesWithPoolCount()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 150, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.Null(result.ErrorMessage);
        Assert.Equal(150, result.CardCount);
        Assert.True(result.HasResult);
    }

    [Fact]
    public async Task ProcessAsync_SelectedCommanderFromMainboard_IsExcludedFromPoolCountAndLocked()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Forest", "mainboard") with { Quantity = 2 },
            Entry("Atraxa, Praetors' Voice", "mainboard"),
        };
        entries.AddRange(BuildBasicMainboard(start: 1, count: 148));
        var cards = new List<ScryfallCard>
        {
            Spell("Forest", "Basic Land — Forest"),
            Spell("Atraxa, Praetors' Voice", "Legendary Creature — Phyrexian Angel Horror"),
        };
        cards.AddRange(BuildBasicResolvedCards(start: 1, count: 148));
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            SelectedCommander = "Atraxa, Praetors' Voice",
        };

        var result = await service.ProcessAsync(request);

        Assert.Null(result.ErrorMessage);
        Assert.True(result.HasResult);
        Assert.Equal(150, result.CardCount);
        var commander = Assert.Single(result.State!.Pool, card => card.IsCommander);
        Assert.Equal("Atraxa, Praetors' Voice", commander.Name);
        Assert.True(commander.IsLocked);
    }

    [Fact]
    public async Task ProcessAsync_PartnerCommanders_UseMaxCommanderManaValueForRampAndDrawFloors()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Kediss, Emberclaw Familiar", "commander"),
            Entry("Brinelin, the Moon Kraken", "commander"),
        };
        entries.AddRange(BuildBasicMainboard(start: 1, count: 120));
        var cards = new List<ScryfallCard>
        {
            Spell("Kediss, Emberclaw Familiar", "Legendary Creature — Elemental Lizard", manaCost: "{1}{R}", cmc: 2),
            Spell("Brinelin, the Moon Kraken", "Legendary Creature — Kraken", manaCost: "{4}{U}", cmc: 5),
        };
        cards.AddRange(BuildBasicResolvedCards(start: 1, count: 120));
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            Bracket = 4,
            PlayExperience = "Focused",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.Equal(13, Assert.Single(result.ResolvedFloors, floor => floor.Role == "ramp").DefaultValue);
        Assert.Equal(11, Assert.Single(result.ResolvedFloors, floor => floor.Role == "draw").DefaultValue);
    }

    [Fact]
    public async Task ProcessAsync_BatchedCategoryLookupRunsOnceForWholePool()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var categoryStore = new FakeCategoryKnowledgeStore();
        var spellbook = new FakeSpellbookService();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            categoryStore,
            spellbook);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.True(result.CategoryDataAvailable);
        Assert.True(result.ComboDataAvailable);
        Assert.Equal(1, categoryStore.GetCategoriesForNamesCalls);
    }

    [Fact]
    public async Task ProcessAsync_DelegatesStructuralAnalysisToSharedBuilder()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var analysisBuilder = new FakeAnalysisContextBuilder((workingList, _, commanderNames) => BuildAnalysisContext(workingList, commanderNames, comboDataAvailable: true));
        var simulationService = new FakeSimulationService();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.Equal(1, analysisBuilder.BuildCalls);
        Assert.Equal(121, analysisBuilder.LastWorkingListCount);
        Assert.Equal(["draw", "engines"], result.RoleAssignmentsByCardName["Card 001"]);
        Assert.True(result.ComboDataAvailable);
        Assert.False(result.CategoryDataAvailable);
    }

    [Fact]
    public async Task ProcessAsync_PopulatesResolvedCardCacheThroughBuilder()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var cache = new CutLabResolvedCardCache();
        var analysisBuilder = new CutLabAnalysisContextBuilder(new FakeResolver(cards), cache);
        var simulationService = new FakeSimulationService();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);
        string poolKey = CutLabResolvedCardCache.ComputePoolKey(
            result.State!.Pool.Select(card => (card.Name, card.Quantity)).ToArray());

        Assert.True(result.HasResult);
        Assert.True(cache.TryGet(poolKey, out IReadOnlyList<ScryfallCardData>? cachedCards));
        Assert.NotNull(cachedCards);
        Assert.Equal(result.State.Pool.Count, Assert.IsAssignableFrom<IReadOnlyList<ScryfallCardData>>(cachedCards).Count);
    }

    [Fact]
    public async Task ProcessAsync_DfcFrontFaceInput_AssignsRolesBuildsBaselineAndComputesDeltasWithoutWarnings()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        entries[1] = Entry("Malakir Rebirth", "mainboard");
        var cards = BuildResolvedCards(entries);
        cards.RemoveAll(card => string.Equals(card.Name, "Malakir Rebirth", StringComparison.OrdinalIgnoreCase));
        cards.Add(Spell(
            "Malakir Rebirth // Malakir Mire",
            "Instant",
            manaCost: "{B}",
            oracleText: "Choose target creature. You lose 2 life. Until end of turn, that creature gains \"When this creature dies, return it to the battlefield tapped under its owner's control.\"",
            cmc: 1));
        var resolver = new CountingNormalizerResolver(cards);
        var cache = new CutLabResolvedCardCache();
        var simulationService = new CutLabSimulationService(
            cache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            resolver,
            cache,
            new FakeSpellbookService(),
            new FakeCategoryKnowledgeStore());
        var service = new CutLabPageService(
            new FakeLoader(entries),
            resolver,
            new FakeBanListService([]),
            new FakeCategoryKnowledgeStore(),
            new FakeSpellbookService(),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            analysisBuilder,
            simulationService,
            new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            PlayExperience = "Focused",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.NotEmpty(result.RoleAssignmentsByCardName["Malakir Rebirth"]);
        Assert.NotNull(result.State!.BaselineSnapshot);
        Assert.NotEmpty(result.State.BaselineSnapshot!.Metrics);
        Assert.NotNull(result.CurrentSnapshot);
        Assert.NotNull(result.InitialProposalDeltas);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProcessAsync_ResolvesEachUniqueCardExactlyOnceAcrossAnalysisAndDeltas()
    {
        List<DeckEntry> entries =
        [
            Entry("Focused Commander", "commander"),
            Entry("Island", "mainboard") with { Quantity = 40 },
            Entry("Swamp", "mainboard") with { Quantity = 20 },
            Entry("Arcane Signet", "mainboard") with { Quantity = 10 },
            Entry("Sol Ring", "mainboard") with { Quantity = 10 },
            Entry("Value Engine", "mainboard") with { Quantity = 10 },
            Entry("Combo Tutor", "mainboard") with { Quantity = 10 },
            Entry("Fast Interaction", "mainboard") with { Quantity = 8 },
            Entry("Closing Threat", "mainboard") with { Quantity = 7 },
            Entry("Utility Land", "mainboard") with { Quantity = 5 },
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{U}{B}", oracleText: "Whenever you cast your second spell each turn, draw a card.", power: "3", cmc: 3),
            Spell("Island", "Basic Land — Island", oracleText: "{T}: Add {U}."),
            Spell("Swamp", "Basic Land — Swamp", oracleText: "{T}: Add {B}."),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", oracleText: "{T}: Add one mana of any color in your commander's color identity.", cmc: 2),
            Spell("Sol Ring", "Artifact", manaCost: "{1}", oracleText: "{T}: Add {C}{C}.", cmc: 1),
            Spell("Value Engine", "Enchantment", manaCost: "{1}{U}", oracleText: "At the beginning of your upkeep, draw a card.", cmc: 2),
            Spell("Combo Tutor", "Sorcery", manaCost: "{1}{B}", oracleText: "Search your library for a card, put that card into your hand, then shuffle.", cmc: 2),
            Spell("Fast Interaction", "Instant", manaCost: "{U}", oracleText: "Counter target spell.", cmc: 1),
            Spell("Closing Threat", "Creature — Leviathan", manaCost: "{5}{U}", oracleText: "Whenever this creature attacks, creatures you control get +6/+6 until end of turn.", power: "6", cmc: 6),
            Spell("Utility Land", "Land", oracleText: "{T}: Add {U} or {B}."),
        ];
        var resolver = new CountingNormalizerResolver(cards);
        var cache = new CutLabResolvedCardCache();
        var simulationService = new CutLabSimulationService(
            cache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            resolver,
            cache,
            new FakeSpellbookService(),
            new FakeCategoryKnowledgeStore());
        var service = new CutLabPageService(
            new FakeLoader(entries),
            resolver,
            new FakeBanListService([]),
            new FakeCategoryKnowledgeStore(),
            new FakeSpellbookService(),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            analysisBuilder,
            simulationService,
            new CutLabBaselineSnapshot(simulationService));

        var result = await service.ProcessAsync(new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            PlayExperience = "cEDH",
        });

        Assert.True(result.HasResult);
        Assert.NotNull(result.InitialProposalDeltas);
        Assert.Equal(10, resolver.ResolveSingleCallsByName.Count);
        Assert.All(resolver.ResolveSingleCallsByName.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task ProcessAsync_PersistsBaselineSnapshotAndReusesItAsCurrentSnapshotAtIntake()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var analysisBuilder = new FakeAnalysisContextBuilder((workingList, _, commanderNames) => BuildAnalysisContext(workingList, commanderNames));
        var simulationService = new FakeSimulationService
        {
            SnapshotFactory = (_, _, trialsOverride) => BuildSevenMetricSnapshot(trialsOverride is null ? 10 : 20),
            DeltasFactory = (_, candidateCardName, _) => BuildDeltas(candidateCardName),
        };
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);
        var roundTrippedState = CutLabStateSerializer.Deserialize(result.SerializedStateJson);

        Assert.True(result.HasResult);
        Assert.NotNull(result.State!.BaselineSnapshot);
        Assert.Equal(7, result.State.BaselineSnapshot!.Metrics.Count);
        Assert.Same(result.State.BaselineSnapshot, result.CurrentSnapshot);
        Assert.NotNull(roundTrippedState.BaselineSnapshot);
        Assert.Equal(7, roundTrippedState.BaselineSnapshot!.Metrics.Count);
        Assert.True(result.SerializedStateJson!.Length < 256_000);
    }

    [Fact]
    public async Task ProcessAsync_BaselineFailure_FailsOpenAndKeepsResult()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var analysisBuilder = new FakeAnalysisContextBuilder((workingList, _, commanderNames) => BuildAnalysisContext(workingList, commanderNames));
        var simulationService = new FakeSimulationService
        {
            BuildSnapshotException = new InvalidOperationException("baseline down"),
            ComputeProposalDeltasException = new InvalidOperationException("deltas down"),
        };
        var logger = new FakeLogger<CutLabPageService>();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService),
            logger: logger);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.Null(result.State!.BaselineSnapshot);
        Assert.Contains(result.Warnings, warning => warning.Contains("Baseline snapshot unavailable", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, warning => warning.Contains("baseline snapshot failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_ProvidesRoundPlanInitialDeltasAndCurrentSnapshotServerSide()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var analysisBuilder = new FakeAnalysisContextBuilder((workingList, _, commanderNames) => BuildAnalysisContext(workingList, commanderNames));
        var simulationService = new FakeSimulationService
        {
            SnapshotFactory = (_, _, trialsOverride) => BuildSevenMetricSnapshot(trialsOverride is null ? 10 : 20),
            DeltasFactory = (_, candidateCardName, _) => BuildDeltas(candidateCardName),
        };
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.NotNull(result.RoundPlan);
        Assert.NotNull(result.RoundPlan!.NextProposal);
        Assert.Equal(21, result.RoundPlan.CardsRemainingToTarget);
        Assert.NotNull(result.InitialProposalDeltas);
        Assert.Equal(result.RoundPlan.NextProposal!.CardName, result.InitialProposalDeltas!.CardName);
        Assert.NotNull(result.CurrentSnapshot);
    }

    [Fact]
    public async Task ProcessAsync_WithAcceptedCutsAtTarget_ReturnsNothingToCutPlan()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var priorState = new CutLabState
        {
            Decisions = Enumerable.Range(1, 21)
                .Select(index => new CutLabDecision
                {
                    CardName = $"Card {index:000}",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = index,
                })
                .ToArray(),
            BaselineSnapshot = BuildSevenMetricSnapshot(10),
        };
        var analysisBuilder = new FakeAnalysisContextBuilder((workingList, _, commanderNames) => BuildAnalysisContext(workingList, commanderNames));
        var simulationService = new FakeSimulationService
        {
            SnapshotFactory = (_, _, _) => BuildSevenMetricSnapshot(20),
            DeltasFactory = (_, candidateCardName, _) => BuildDeltas(candidateCardName),
        };
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.NotNull(result.RoundPlan);
        Assert.Null(result.RoundPlan!.NextProposal);
        Assert.Equal(0, result.RoundPlan.CardsRemainingToTarget);
        Assert.Null(result.InitialProposalDeltas);
    }

    [Fact]
    public async Task ProcessAsync_CurrentSnapshotAndDeltasFailure_FailsOpenOnDecisionRender()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var priorState = new CutLabState
        {
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Card 001",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ],
            BaselineSnapshot = BuildSevenMetricSnapshot(10),
        };
        var analysisBuilder = new FakeAnalysisContextBuilder((workingList, _, commanderNames) => BuildAnalysisContext(workingList, commanderNames));
        var simulationService = new FakeSimulationService
        {
            BuildSnapshotException = new InvalidOperationException("snapshot down"),
            ComputeProposalDeltasException = new InvalidOperationException("deltas down"),
        };
        var logger = new FakeLogger<CutLabPageService>();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService,
            baselineSnapshot: new CutLabBaselineSnapshot(simulationService),
            logger: logger);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.Null(result.CurrentSnapshot);
        Assert.Null(result.InitialProposalDeltas);
        Assert.Contains(result.Warnings, warning => warning.Contains("Current working snapshot unavailable", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("Proposal delta preview unavailable", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, warning => warning.Contains("current working snapshot failed", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, warning => warning.Contains("proposal deltas failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_DecisionRoundTripWithWarmCache_PerformsZeroAdditionalLiveResolves()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var resolver = new CountingNormalizerResolver(cards);
        var cache = new CutLabResolvedCardCache();
        var simulationService = new CutLabSimulationService(
            cache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            resolver,
            cache,
            new FakeSpellbookService(),
            new FakeCategoryKnowledgeStore());
        var service = new CutLabPageService(
            new FakeLoader(entries),
            resolver,
            new FakeBanListService([]),
            new FakeCategoryKnowledgeStore(),
            new FakeSpellbookService(),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            analysisBuilder,
            simulationService,
            new CutLabBaselineSnapshot(simulationService));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            PlayExperience = "Focused",
        };

        CutLabProcessResult intake = await service.ProcessAsync(request);
        int callsAfterIntake = resolver.ResolveSingleCallsByName.Values.Sum();
        CutLabState state = CutLabStateSerializer.Deserialize(intake.SerializedStateJson);
        state = CutLabDecisionApplier.Apply(state, "Card 001", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);
        request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

        CutLabProcessResult afterDecision = await service.ProcessAsync(request);

        Assert.True(afterDecision.HasResult);
        Assert.Equal(callsAfterIntake, resolver.ResolveSingleCallsByName.Values.Sum());
    }

    [Fact]
    public async Task ProcessAsync_IntakeWithOneUnresolvableCard_ResolvesEachUniqueCardOnlyOnce()
    {
        List<DeckEntry> entries =
        [
            Entry("Focused Commander", "commander"),
            Entry("Island", "mainboard") with { Quantity = 40 },
            Entry("Swamp", "mainboard") with { Quantity = 20 },
            Entry("Arcane Signet", "mainboard") with { Quantity = 10 },
            Entry("Sol Ring", "mainboard") with { Quantity = 10 },
            Entry("Value Engine", "mainboard") with { Quantity = 10 },
            Entry("Combo Tutor", "mainboard") with { Quantity = 10 },
            Entry("Fast Interaction", "mainboard") with { Quantity = 8 },
            Entry("Closing Threat", "mainboard") with { Quantity = 6 },
            Entry("Typo Card", "mainboard") with { Quantity = 7 },
        ];
        List<ScryfallCard> cards =
        [
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{U}{B}", oracleText: "Whenever you cast your second spell each turn, draw a card.", power: "3", cmc: 3),
            Spell("Island", "Basic Land — Island", oracleText: "{T}: Add {U}."),
            Spell("Swamp", "Basic Land — Swamp", oracleText: "{T}: Add {B}."),
            Spell("Arcane Signet", "Artifact", manaCost: "{2}", oracleText: "{T}: Add one mana of any color in your commander's color identity.", cmc: 2),
            Spell("Sol Ring", "Artifact", manaCost: "{1}", oracleText: "{T}: Add {C}{C}.", cmc: 1),
            Spell("Value Engine", "Enchantment", manaCost: "{1}{U}", oracleText: "At the beginning of your upkeep, draw a card.", cmc: 2),
            Spell("Combo Tutor", "Sorcery", manaCost: "{1}{B}", oracleText: "Search your library for a card, put that card into your hand, then shuffle.", cmc: 2),
            Spell("Fast Interaction", "Instant", manaCost: "{U}", oracleText: "Counter target spell.", cmc: 1),
            Spell("Closing Threat", "Creature — Leviathan", manaCost: "{5}{U}", oracleText: "Whenever this creature attacks, creatures you control get +6/+6 until end of turn.", power: "6", cmc: 6),
        ];
        var resolver = new CountingNormalizerResolver(cards);
        var cache = new CutLabResolvedCardCache();
        var simulationService = new CutLabSimulationService(
            cache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            resolver,
            cache,
            new FakeSpellbookService(),
            new FakeCategoryKnowledgeStore());
        var service = new CutLabPageService(
            new FakeLoader(entries),
            resolver,
            new FakeBanListService([]),
            new FakeCategoryKnowledgeStore(),
            new FakeSpellbookService(),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            analysisBuilder,
            simulationService,
            new CutLabBaselineSnapshot(simulationService));

        var result = await service.ProcessAsync(new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            PlayExperience = "cEDH",
        });

        Assert.True(result.HasResult);
        Assert.Equal(10, resolver.ResolveSingleCallsByName.Count);
        Assert.All(resolver.ResolveSingleCallsByName.Values, count => Assert.Equal(1, count));
        Assert.Equal(1, resolver.ResolveSingleCallsByName["typo card"]);
    }

    [Fact]
    public void From_WhenProposalDeltasUnavailable_StillBuildsProposalCardWithFallbackMessage()
    {
        var request = new CutLabRequest();
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
                Pool =
                [
                    new CutLabPoolCard
                    {
                        Name = "Arcane Signet",
                        Quantity = 1,
                    },
                ],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            Findings = new CutLabStructuralFindingsResult([], true, true),
            RoundPlan = new CutLabRoundPlan
            {
                Queue =
                [
                    new CutLabRoundQueueItem("Arcane Signet", CutLabCutRoundEngine.Round2Key, CutLabCutRoundEngine.Round2Label, 1, []),
                ],
                NextProposal = new CutLabRoundQueueItem("Arcane Signet", CutLabCutRoundEngine.Round2Key, CutLabCutRoundEngine.Round2Label, 1, []),
                CardsRemainingToTarget = 1,
            },
            InitialProposalDeltas = null,
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.True(model.Proposal.HasProposal);
        Assert.Equal("Arcane Signet", model.Proposal.CardName);
        Assert.Equal("Couldn't recalculate this cut — nothing changed. Try again.", model.Proposal.DeltaUnavailableMessage);
        Assert.Equal("Cards flagged by exactly one structural finding.", model.Proposal.RoundBannerBody);
    }

    [Fact]
    public void From_UsesAuthoritativeMetricUnitForProposalAndCompareDeltas()
    {
        var request = new CutLabRequest();
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
                Pool =
                [
                    new CutLabPoolCard
                    {
                        Name = "Arcane Signet",
                        Quantity = 1,
                    },
                ],
                BaselineSnapshot = new CutLabMetricSnapshot
                {
                    Metrics =
                    [
                        new CutLabMetricValue
                        {
                            Kind = CutLabMetricKind.Screw,
                            Family = CutLabMetricFamily.FloodScrewCurveRisk,
                            Label = "Screw",
                            Value = 10,
                            Unit = CutLabMetricUnit.Percent,
                        },
                    ],
                },
            },
            CurrentSnapshot = new CutLabMetricSnapshot
            {
                Metrics =
                [
                    new CutLabMetricValue
                    {
                        Kind = CutLabMetricKind.Screw,
                        Family = CutLabMetricFamily.FloodScrewCurveRisk,
                        Label = "Screw",
                        Value = 13,
                        Unit = CutLabMetricUnit.Percent,
                    },
                ],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            Findings = new CutLabStructuralFindingsResult([], true, true),
            RoundPlan = new CutLabRoundPlan
            {
                Queue =
                [
                    new CutLabRoundQueueItem("Arcane Signet", CutLabCutRoundEngine.Round2Key, CutLabCutRoundEngine.Round2Label, 0, []),
                ],
                NextProposal = new CutLabRoundQueueItem("Arcane Signet", CutLabCutRoundEngine.Round2Key, CutLabCutRoundEngine.Round2Label, 0, []),
                CardsRemainingToTarget = 1,
            },
            InitialProposalDeltas = new CutLabProposalDeltas
            {
                CardName = "Arcane Signet",
                ChangedFamilyCount = 1,
                Deltas =
                [
                    new CutLabMetricDelta
                    {
                        Kind = CutLabMetricKind.Screw,
                        Family = CutLabMetricFamily.FloodScrewCurveRisk,
                        Label = "Screw",
                        Before = 10,
                        After = 13,
                        Delta = 3,
                        Unit = CutLabMetricUnit.Percent,
                        Direction = CutLabMetricDirection.Up,
                        IsMeaningful = true,
                    },
                ],
            },
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.Equal("3.0%", Assert.Single(model.Proposal.ChangedDeltaLines).FormattedValueToken);
        Assert.Equal("3.0%", Assert.Single(model.CompareRows).DeltaValueToken);
    }

    [Fact]
    public async Task ProcessAsync_SpellbookFailure_FailsOpenAndLogsWarning()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var logger = new FakeLogger<CutLabAnalysisContextBuilder>();
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            new FakeResolver(cards),
            new CutLabResolvedCardCache(),
            new FakeSpellbookService { Exception = new InvalidOperationException("spellbook down") },
            new FakeCategoryKnowledgeStore(),
            logger);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.False(result.ComboDataAvailable);
        Assert.True(result.CategoryDataAvailable);
        Assert.Contains(logger.Warnings, warning => warning.Contains("Commander Spellbook fetch failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_CategoryLookupFailure_FailsOpenAndLogsWarning()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var logger = new FakeLogger<CutLabAnalysisContextBuilder>();
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            new FakeResolver(cards),
            new CutLabResolvedCardCache(),
            new FakeSpellbookService(),
            new ThrowingCategoryKnowledgeStore(new InvalidOperationException("db down")),
            logger);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            analysisContextBuilder: analysisBuilder);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.True(result.ComboDataAvailable);
        Assert.False(result.CategoryDataAvailable);
        Assert.Contains(logger.Warnings, warning => warning.Contains("batch category lookup failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_WithOptionalAnalysisDependenciesOmitted_BehavesAsUnavailable()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await service.ProcessAsync(request);

        Assert.True(result.HasResult);
        Assert.False(result.ComboDataAvailable);
        Assert.False(result.CategoryDataAvailable);
    }

    [Fact]
    public async Task ProcessAsync_SpellbookCancellation_Propagates()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            new FakeCategoryKnowledgeStore(),
            new FakeSpellbookService { Exception = new OperationCanceledException("cancel spellbook") });
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ProcessAsync(request));
    }

    [Fact]
    public async Task ProcessAsync_CategoryLookupCancellation_Propagates()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            new ThrowingCategoryKnowledgeStore(new OperationCanceledException("cancel categories")),
            new FakeSpellbookService());
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ProcessAsync(request));
    }

    [Fact]
    public void CutLabPageService_DiContainerMirrorsProgramRegistrationAndSuppliesOptionalAnalysisDependencies()
    {
        using ServiceProvider provider = BuildDiGuardProvider();
        using IServiceScope scope = provider.CreateScope();

        var service = Assert.IsType<CutLabPageService>(scope.ServiceProvider.GetRequiredService<ICutLabPageService>());

        Assert.True(service.HasStructuralAnalysisDependencies);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICutLabAnalysisContextBuilder>());
    }

    [Fact]
    public void CutLabPageService_DiGuardFailsWhenOptionalAnalysisRegistrationDrops()
    {
        using ServiceProvider provider = BuildDiGuardProvider(omitCategoryKnowledge: true);
        using IServiceScope scope = provider.CreateScope();

        var service = Assert.IsType<CutLabPageService>(scope.ServiceProvider.GetRequiredService<ICutLabPageService>());

        Assert.False(service.HasStructuralAnalysisDependencies);
    }

    [Fact]
    public void CutLabPageService_DiGuardFailsWhenSimulationRegistrationDrops()
    {
        using ServiceProvider provider = BuildDiGuardProvider(omitSimulationService: true);
        using IServiceScope scope = provider.CreateScope();

        var service = Assert.IsType<CutLabPageService>(scope.ServiceProvider.GetRequiredService<ICutLabPageService>());

        Assert.False(service.HasStructuralAnalysisDependencies);
    }

    [Fact]
    public async Task ProcessAsync_StructuralAnalysis_WiresRolesFloorsFindingsAndUserFloorPersistence()
    {
        var entries = BuildStructuralAnalysisPoolEntries(includeUnresolvedCard: true);
        var cards = BuildStructuralAnalysisResolvedCards();
        var categoryStore = new FakeCategoryKnowledgeStore();
        categoryStore.CategoriesByName["Value Engine"] = ["value engine"];
        categoryStore.CategoriesByName["Closer Beast"] = ["finisher"];
        var spellbook = new FakeSpellbookService
        {
            Result = new CommanderSpellbookResult(
                [new SpellbookCombo(["Combo Piece A", "Combo Piece B"], ["Win the game"], "Assemble the pair.")],
                [new SpellbookAlmostCombo("Combo Piece C", ["Combo Piece A", "Combo Piece B"], ["Win the game"], "Missing the third piece.")]),
        };
        var priorState = new CutLabState
        {
            Commander = "Focused Commander",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Answer Charm",
                    Quantity = 1,
                    TypeLine = "Instant",
                    IsLocked = true,
                },
            ],
            RoleFloors =
            [
                new CutLabRoleFloor
                {
                    Role = "interaction",
                    Floor = 15,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "draw",
                    Floor = 99,
                    IsUserSet = false,
                },
            ],
        };
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            categoryStore,
            spellbook,
            new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
            {
                Bracket = 4,
                AvgLands = 38.0,
                DeckCount = 100,
            }),
            new FakeCedhLandBaselineProvider());
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            Bracket = 4,
            PlayExperience = "Focused",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        };

        var result = await service.ProcessAsync(request);
        var model = CutLabViewModel.From(request, result);

        Assert.True(result.HasResult);
        Assert.Equal(CutLabFloorRules.RoleKeys, result.ResolvedFloors.Select(floor => floor.Role).ToArray());
        Assert.Equal(CutLabFloorRules.RoleKeys, model.RoleGroups.Select(group => group.RoleKey).ToArray());
        Assert.False(model.ComboDataUnavailable);
        Assert.False(model.CategoryDataUnavailable);
        Assert.NotEmpty(model.Findings);
        Assert.Equal(8, model.FloorRows.Count);
        Assert.Equal("Card draw · Engines", model.RoleListByCardName["Value Engine"]);
        Assert.Equal("draw engines", model.RoleKeysByCardName["Value Engine"]);
        Assert.Equal(string.Empty, model.RoleListByCardName["Unresolved Card"]);
        Assert.Empty(result.RoleAssignmentsByCardName["Unresolved Card"]);
        Assert.Contains(model.RoleGroups.Single(group => group.RoleKey == "draw").Members, member => member.Name == "Value Engine");
        Assert.Contains(model.RoleGroups.Single(group => group.RoleKey == "engines").Members, member => member.Name == "Value Engine");
        Assert.Equal(1, model.RoleGroups.Single(group => group.RoleKey == "interaction").LockedCount);

        CutLabFloorRowView interactionRow = Assert.Single(model.FloorRows, row => row.RoleKey == "interaction");
        Assert.Equal("Interaction", interactionRow.DisplayLabel);
        Assert.Equal(2, interactionRow.InPoolCount);
        Assert.Equal(15, interactionRow.Floor);
        Assert.Equal(10, interactionRow.DefaultValue);
        Assert.True(interactionRow.IsUserSet);
        Assert.True(interactionRow.AtFloor);
        Assert.Equal("Default for B4: 10", interactionRow.SourceLabel);

        CutLabRoleFloor persistedFloor = Assert.Single(result.State!.RoleFloors);
        Assert.Equal("interaction", persistedFloor.Role);
        Assert.Equal(15, persistedFloor.Floor);
        Assert.True(persistedFloor.IsUserSet);
        Assert.DoesNotContain(result.State.RoleFloors, floor => floor.Role == "draw");
    }

    [Fact]
    public async Task ProcessAsync_StackedBasics_WeightLandsCountsAcrossFindingsAndFloorViews()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Focused Commander", "commander"),
            Entry("Forest", "mainboard") with { Quantity = 38 },
        };
        entries.AddRange(BuildBasicMainboard(start: 1, count: 63));

        var cards = new List<ScryfallCard>
        {
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Forest", "Basic Land — Forest"),
        };
        cards.AddRange(BuildBasicResolvedCards(start: 1, count: 63));

        var priorState = new CutLabState
        {
            Commander = "Focused Commander",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Forest",
                    Quantity = 38,
                    TypeLine = "Basic Land — Forest",
                    IsLocked = true,
                },
            ],
        };

        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            manabaseBaseline: new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
            {
                Bracket = 4,
                AvgLands = 36.0,
                DeckCount = 100,
            }),
            cedhBaseline: new FakeCedhLandBaselineProvider());
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            Bracket = 4,
            PlayExperience = "Focused",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        };

        var result = await service.ProcessAsync(request);
        var model = CutLabViewModel.From(request, result);

        CutLabFloorRowView landsRow = Assert.Single(model.FloorRows, row => row.RoleKey == "lands");
        Assert.Equal(38, landsRow.InPoolCount);
        Assert.False(landsRow.AtFloor);
        Assert.Equal(38, model.RoleGroups.Single(group => group.RoleKey == "lands").LockedCount);
        Assert.DoesNotContain(
            result.Findings.Findings,
            finding => finding.Kind == CutLabFindingKind.WeakFloorCase
                && finding.Lead.StartsWith("Lands is at ", StringComparison.Ordinal));
    }

    private static List<DeckEntry> BuildPoolEntries(int nonCommanderCount, string commanderName)
    {
        var entries = new List<DeckEntry> { Entry(commanderName, "commander") };
        entries.AddRange(BuildBasicMainboard(start: 1, count: nonCommanderCount));
        return entries;
    }

    private static List<DeckEntry> BuildStructuralAnalysisPoolEntries(bool includeUnresolvedCard)
    {
        var entries = new List<DeckEntry>
        {
            Entry("Focused Commander", "commander"),
            Entry("Forest", "mainboard"),
            Entry("Rampant Growth", "mainboard"),
            Entry("Value Engine", "mainboard"),
            Entry("Answer Charm", "mainboard"),
            Entry("Protection Aura", "mainboard"),
            Entry("Closer Beast", "mainboard"),
            Entry("Combo Piece A", "mainboard"),
            Entry("Combo Piece B", "mainboard"),
        };

        if (includeUnresolvedCard)
        {
            entries.Add(Entry("Unresolved Card", "mainboard"));
        }

        entries.AddRange(BuildBasicMainboard(start: 1, count: 120 - entries.Count));
        return entries;
    }

    private static List<DeckEntry> BuildBasicMainboard(int start, int count)
        => Enumerable.Range(start, count)
            .Select(index => Entry($"Card {index:000}", "mainboard"))
            .ToList();

    private static List<ScryfallCard> BuildResolvedCards(IEnumerable<DeckEntry> entries)
        => entries.Select(entry => string.Equals(entry.Name, "Atraxa, Praetors' Voice", StringComparison.Ordinal)
            ? Spell(entry.Name, "Legendary Creature — Phyrexian Angel Horror")
            : Spell(entry.Name, entry.Name == "Ancient Tomb" ? "Land" : "Artifact", set: entry.SetCode, collectorNumber: entry.CollectorNumber))
            .ToList();

    private static List<ScryfallCard> BuildBasicResolvedCards(int start, int count)
        => Enumerable.Range(start, count)
            .Select(index => Spell($"Card {index:000}", "Artifact"))
            .ToList();

    private static List<ScryfallCard> BuildStructuralAnalysisResolvedCards()
    {
        var cards = new List<ScryfallCard>
        {
            Spell("Focused Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            Spell("Forest", "Basic Land — Forest"),
            Spell(
                "Rampant Growth",
                "Sorcery",
                manaCost: "{1}{G}",
                oracleText: "Search your library for a basic land card, put that card onto the battlefield tapped, then shuffle.",
                cmc: 2),
            Spell(
                "Value Engine",
                "Enchantment",
                manaCost: "{2}{U}",
                oracleText: "At the beginning of your upkeep, draw a card.",
                cmc: 3),
            Spell(
                "Answer Charm",
                "Instant",
                manaCost: "{1}{W}",
                oracleText: "Destroy target artifact.",
                cmc: 2),
            Spell(
                "Protection Aura",
                "Instant",
                manaCost: "{G}",
                oracleText: "Target creature gains hexproof until end of turn.",
                cmc: 1),
            Spell(
                "Closer Beast",
                "Creature — Beast",
                manaCost: "{5}{G}",
                oracleText: "Whenever this creature attacks, creatures you control get +X/+X until end of turn.",
                power: "6",
                cmc: 6),
            Spell("Combo Piece A", "Artifact", manaCost: "{2}", cmc: 2),
            Spell("Combo Piece B", "Artifact", manaCost: "{2}", cmc: 2),
        };
        cards.AddRange(BuildBasicResolvedCards(start: 1, count: 110));
        return cards;
    }

    private static DeckEntry Entry(string name, string board, string? set = null, string? collectorNumber = null)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = board,
            SetCode = set,
            CollectorNumber = collectorNumber,
        };

    private static ScryfallCard Spell(
        string name,
        string typeLine,
        string? set = null,
        string? collectorNumber = null,
        string? manaCost = null,
        string? oracleText = null,
        string? power = null,
        double cmc = 0)
        => new(
            name,
            manaCost,
            typeLine,
            oracleText,
            power,
            null,
            null,
            null,
            set,
            null,
            collectorNumber,
            Cmc: cmc);

    private static ServiceProvider BuildDiGuardProvider(bool omitCategoryKnowledge = false, bool omitSimulationService = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDeckEntryLoader>(new FakeLoader([]));
        services.AddSingleton<IScryfallCardResolver>(new FakeResolver([]));
        services.AddSingleton<ICommanderBanListService>(new FakeBanListService([]));
        if (!omitCategoryKnowledge)
        {
            services.AddSingleton<ICategoryKnowledgeStore>(new FakeCategoryKnowledgeStore());
        }

        services.AddSingleton<ICommanderSpellbookService>(new FakeSpellbookService());
        services.AddSingleton<IManabaseBaselineProvider>(new FakeManabaseBaselineProvider());
        services.AddSingleton<ICedhLandBaselineProvider>(new FakeCedhLandBaselineProvider());
        services.AddLogging();
        services.AddSingleton<CutLabResolvedCardCache>();
        services.AddSingleton<CutLabDeltaCache>();
        services.AddScoped<ICutLabAnalysisContextBuilder, CutLabAnalysisContextBuilder>();
        if (!omitSimulationService)
        {
            services.AddScoped<ICutLabSimulationService, CutLabSimulationService>();
            services.AddScoped<CutLabBaselineSnapshot>();
        }

        // Optional ctor params default to null when a registration is missing; this guard catches
        // a Program.cs regression by proving the plain AddScoped shape still resolves all four deps.
        services.AddScoped<ICutLabPageService, CutLabPageService>();
        return services.BuildServiceProvider();
    }

    private static CutLabAnalysisContext BuildAnalysisContext(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<string> commanderNames,
        bool comboDataAvailable = false,
        bool categoryDataAvailable = false)
    {
        HashSet<string> commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);
        List<CutLabAnalyzedCard> analyzedCards = new(workingList.Count);
        double commanderManaValue = 0;

        foreach (CutLabPoolCard card in workingList)
        {
            bool isCommander = commanderNameSet.Contains(card.Name);
            IReadOnlyList<string> roles = card.Name switch
            {
                "Card 001" => ["draw", "engines"],
                "Card 002" => ["interaction"],
                _ when card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase) => ["lands"],
                _ => [],
            };
            foreach (string role in roles)
            {
                roleCounts[role] = roleCounts.TryGetValue(role, out int count)
                    ? count + card.Quantity
                    : card.Quantity;
            }

            double manaValue = isCommander ? 4 : roles.Contains("lands", StringComparer.Ordinal) ? 0 : 2;
            if (isCommander)
            {
                commanderManaValue = Math.Max(commanderManaValue, manaValue);
            }

            rolesByCardName[card.Name] = roles;
            analyzedCards.Add(new CutLabAnalyzedCard(
                card.Name,
                manaValue,
                roles.Contains("lands", StringComparer.Ordinal),
                roles,
                [])
            {
                Quantity = card.Quantity,
            });
        }

        return new CutLabAnalysisContext(
            analyzedCards,
            rolesByCardName,
            roleCounts,
            commanderManaValue,
            ManabaseMode.Casual,
            new CutLabClassificationContext(
                [],
                comboDataAvailable,
                categoryDataAvailable,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            workingList
                .Select(card => new ScryfallCardData
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                    Cmc = card.Name == commanderNames.FirstOrDefault() ? 4 : 2,
                })
                .ToArray());
    }

    private static CutLabMetricSnapshot BuildSevenMetricSnapshot(double seed)
        => new()
        {
            Metrics =
            [
                Metric(CutLabMetricKind.CommanderOnTime, CutLabMetricFamily.CommanderOnTime, seed + 1),
                Metric(CutLabMetricKind.KeepableHand, CutLabMetricFamily.KeepableHand, seed + 2),
                Metric(CutLabMetricKind.ManaColorReliability, CutLabMetricFamily.ManaColorReliability, seed + 3),
                Metric(CutLabMetricKind.EarlyInteraction, CutLabMetricFamily.EarlyInteraction, seed + 4),
                Metric(CutLabMetricKind.PlanPresence, CutLabMetricFamily.PlanPresence, seed + 5),
                Metric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, seed + 6),
                Metric(CutLabMetricKind.Flood, CutLabMetricFamily.FloodScrewCurveRisk, seed + 7, CutLabMetricUnit.Cards),
            ],
        };

    private static CutLabMetricValue Metric(
        CutLabMetricKind kind,
        CutLabMetricFamily family,
        double value,
        CutLabMetricUnit unit = CutLabMetricUnit.Percent)
        => new()
        {
            Kind = kind,
            Family = family,
            Label = kind.ToString(),
            Value = value,
            Unit = unit,
        };

    private static CutLabProposalDeltas BuildDeltas(string candidateCardName)
        => new()
        {
            CardName = candidateCardName,
            ChangedFamilyCount = 1,
            Deltas =
            [
                new CutLabMetricDelta
                {
                    Kind = CutLabMetricKind.KeepableHand,
                    Family = CutLabMetricFamily.KeepableHand,
                    Label = "Keepable hand",
                    Before = 60,
                    After = 58,
                    Delta = -2,
                    Unit = CutLabMetricUnit.Percent,
                    Direction = CutLabMetricDirection.Down,
                    IsMeaningful = true,
                },
            ],
        };

    private sealed class FakeLoader(List<DeckEntry> entries) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(entries, null));

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingLoader(Exception exception) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromException<DeckSourceLoadResult>(exception);

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
            => throw new NotSupportedException();
    }

    private sealed class FakeResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
        {
            var matches = cards.ToList();
            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(matches, []),
            });
        }

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class CountingNormalizerResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public Dictionary<string, int> ResolveSingleCallsByName { get; } = new(StringComparer.Ordinal);

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cards.ToList(), []),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => ResolveSingleAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => ResolveSingleAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
        {
            string normalizedName = DeckFlow.Core.Normalization.CardNormalizer.Normalize(cardName);
            ResolveSingleCallsByName[normalizedName] = ResolveSingleCallsByName.TryGetValue(normalizedName, out int count)
                ? count + 1
                : 1;
            return Task.FromResult(cards.FirstOrDefault(card =>
                string.Equals(
                    DeckFlow.Core.Normalization.CardNormalizer.Normalize(card.Name),
                    normalizedName,
                    StringComparison.Ordinal)));
        }
    }

    private sealed class FakeAnalysisContextBuilder(Func<IReadOnlyList<CutLabPoolCard>, string, IReadOnlyList<string>, CutLabAnalysisContext> factory) : ICutLabAnalysisContextBuilder
    {
        public int BuildCalls { get; private set; }

        public int LastWorkingListCount { get; private set; }

        public Task<CutLabAnalysisContext> BuildAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            BuildCalls++;
            LastWorkingListCount = workingList.Sum(card => card.Quantity);
            return Task.FromResult(factory(workingList, playExperience, commanderNames));
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
        {
            cards = null;
            return false;
        }

        public bool TrySeedDerivedPool(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> sourceCards,
            out IReadOnlyList<ScryfallCardData>? seededCards)
        {
            seededCards = workingList
                .Select(card => sourceCards.FirstOrDefault(source => string.Equals(source.Name, card.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(card => card is not null)
                .Cast<ScryfallCardData>()
                .ToArray();
            return seededCards.Count == workingList.Count;
        }
    }

    private sealed class FakeSimulationService : ICutLabSimulationService
    {
        public Func<IReadOnlyList<CutLabPoolCard>, string?, int?, CutLabMetricSnapshot>? SnapshotFactory { get; set; }

        public Func<IReadOnlyList<CutLabPoolCard>, string, string?, CutLabProposalDeltas>? DeltasFactory { get; set; }

        public Exception? BuildSnapshotException { get; set; }

        public Exception? ComputeProposalDeltasException { get; set; }

        public Task<CutLabMetricSnapshot> BuildSnapshot(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            if (BuildSnapshotException is not null)
            {
                return Task.FromException<CutLabMetricSnapshot>(BuildSnapshotException);
            }

            return Task.FromResult(SnapshotFactory?.Invoke(workingList, playExperience, trialsOverride) ?? BuildSevenMetricSnapshot(10));
        }

        public Task<CutLabProposalDeltas> ComputeProposalDeltas(
            IReadOnlyList<CutLabPoolCard> currentWorkingList,
            string candidateCardName,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            if (ComputeProposalDeltasException is not null)
            {
                return Task.FromException<CutLabProposalDeltas>(ComputeProposalDeltasException);
            }

            return Task.FromResult(DeltasFactory?.Invoke(currentWorkingList, candidateCardName, playExperience) ?? BuildDeltas(candidateCardName));
        }
    }

    private sealed class FakeBanListService(IReadOnlyList<string> bannedCards) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(bannedCards);
    }

    private sealed class ThrowingBanListService(Exception exception) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<string>>(exception);
    }

    private sealed class FakeSpellbookService : ICommanderSpellbookService
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

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
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

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeManabaseBaselineProvider(ManabaseBracketBaseline? baseline = null) : IManabaseBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
            => baseline;

        public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames)
            => null;
    }

    private sealed class FakeCedhLandBaselineProvider : ICedhLandBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n, out double sd, out string? generated)
        {
            mean = default;
            n = default;
            sd = default;
            generated = default;
            return false;
        }
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
