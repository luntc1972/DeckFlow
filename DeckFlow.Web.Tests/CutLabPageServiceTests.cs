using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public async Task ProcessAsync_SpellbookFailure_FailsOpenAndLogsWarning()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Atraxa, Praetors' Voice");
        var cards = BuildResolvedCards(entries);
        var logger = new FakeLogger<CutLabPageService>();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            new FakeCategoryKnowledgeStore(),
            new FakeSpellbookService { Exception = new InvalidOperationException("spellbook down") },
            logger: logger);
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
        var logger = new FakeLogger<CutLabPageService>();
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            new ThrowingCategoryKnowledgeStore(new InvalidOperationException("db down")),
            new FakeSpellbookService(),
            logger: logger);
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

    private static ServiceProvider BuildDiGuardProvider(bool omitCategoryKnowledge = false)
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
        // Optional ctor params default to null when a registration is missing; this guard catches
        // a Program.cs regression by proving the plain AddScoped shape still resolves all four deps.
        services.AddScoped<ICutLabPageService, CutLabPageService>();
        return services.BuildServiceProvider();
    }

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
