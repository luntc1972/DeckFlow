using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Scryfall;
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
        Assert.Equal("120 cards in pool · 1 locked", viewModel.PoolStatusText);
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

    private static List<DeckEntry> BuildPoolEntries(int nonCommanderCount, string commanderName)
    {
        var entries = new List<DeckEntry> { Entry(commanderName, "commander") };
        entries.AddRange(BuildBasicMainboard(start: 1, count: nonCommanderCount));
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

    private static ScryfallCard Spell(string name, string typeLine, string? set = null, string? collectorNumber = null)
        => new(
            name,
            null,
            typeLine,
            null,
            null,
            null,
            null,
            null,
            set,
            null,
            collectorNumber);

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
}
