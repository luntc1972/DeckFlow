using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabExportServiceTests
{
    [Fact]
    public async Task BuildExportAsync_ReconstructsMissingOriginalEntryAsMainboardAndWarns()
    {
        var service = CreateService(
            contextBuilder: new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Arcane Signet", "Artifact", []),
                    CreateResolvedCard("Mystery Card", "Artifact", []))
            });
        var state = CreateState() with
        {
            Pool =
            [
                CreatePoolCard("Zur the Enchanter", "Legendary Creature", quantity: 1, isCommander: true, isLocked: true),
                CreatePoolCard("Arcane Signet", "Artifact"),
                CreatePoolCard("Mystery Card", "Artifact"),
            ],
            OriginalEntries =
            [
                CreateOriginalEntry("Zur the Enchanter", 1, "commander"),
                CreateOriginalEntry("Arcane Signet", 1, "mainboard"),
            ],
        };

        CutLabExportView result = await service.BuildExportAsync(state, "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.True(result.HasExport);
        Assert.Contains("Mystery Card", result.MoxfieldFullListText, StringComparison.Ordinal);
        Assert.Contains(result.ReconstructionWarnings, warning => warning.Contains("Mystery Card", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildExportAsync_SurfacesBanlistOffenderWithoutHardBlock()
    {
        var service = CreateService(
            contextBuilder: new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Arcane Signet", "Artifact", []),
                    CreateResolvedCard("Black Lotus", "Artifact", []))
            },
            banListService: new FakeBanListService(["Black Lotus"]));
        var state = CreateState() with
        {
            Pool =
            [
                CreatePoolCard("Zur the Enchanter", "Legendary Creature", quantity: 1, isCommander: true, isLocked: true),
                CreatePoolCard("Arcane Signet", "Artifact", quantity: 98),
                CreatePoolCard("Black Lotus", "Artifact"),
            ],
            OriginalEntries =
            [
                CreateOriginalEntry("Zur the Enchanter", 1, "commander"),
                CreateOriginalEntry("Arcane Signet", 98, "mainboard"),
                CreateOriginalEntry("Black Lotus", 1, "mainboard"),
            ],
        };

        CutLabExportView result = await service.BuildExportAsync(state, "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.Contains("Black Lotus", result.BanlistOffenders);
        Assert.False(result.HardBlock);
    }

    [Fact]
    public async Task BuildExportAsync_BanlistHttpRequestExceptionFailsOpen()
    {
        var service = CreateService(
            contextBuilder: new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Arcane Signet", "Artifact", []))
            },
            banListService: new ThrowingBanListService(new HttpRequestException("banlist unavailable")));

        CutLabExportView result = await service.BuildExportAsync(CreateState(), "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.False(result.HardBlock);
        Assert.Contains(result.Warnings, warning => warning.Contains("legality was not verified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildExportAsync_ReusesResolvedCardsWithoutSecondResolution()
    {
        var contextBuilder = new FakeAnalysisContextBuilder
        {
            Result = BuildAnalysisContext(
                CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                CreateResolvedCard("Arcane Signet", "Artifact", []))
        };
        var service = CreateService(contextBuilder: contextBuilder);

        CutLabExportView result = await service.BuildExportAsync(CreateState(), "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.True(result.HasExport);
        Assert.Equal(1, contextBuilder.BuildAsyncCallCount);
    }

    [Fact]
    public async Task BuildExportAsync_ExportsIncludedSideboardAndMaybeboardCardsInFinishedMainboardList()
    {
        var service = CreateService(
            contextBuilder: new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Arcane Signet", "Artifact", []),
                    CreateResolvedCard("Swords to Plowshares", "Instant", []),
                    CreateResolvedCard("Mystic Remora", "Enchantment", ["U"]))
            });
        var state = CreateState() with
        {
            Pool =
            [
                CreatePoolCard("Zur the Enchanter", "Legendary Creature", quantity: 1, isCommander: true, isLocked: true),
                CreatePoolCard("Arcane Signet", "Artifact", quantity: 97),
                CreatePoolCard("Swords to Plowshares", "Instant"),
                CreatePoolCard("Mystic Remora", "Enchantment"),
            ],
            OriginalEntries =
            [
                CreateOriginalEntry("Zur the Enchanter", 1, "commander"),
                CreateOriginalEntry("Arcane Signet", 97, "mainboard"),
                CreateOriginalEntry("Swords to Plowshares", 1, "sideboard"),
                CreateOriginalEntry("Mystic Remora", 1, "maybeboard"),
            ],
        };

        CutLabExportView result = await service.BuildExportAsync(state, "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.True(result.CountOk);
        Assert.Contains("1 Swords to Plowshares", result.MoxfieldFullListText, StringComparison.Ordinal);
        Assert.Contains("1 Mystic Remora", result.MoxfieldFullListText, StringComparison.Ordinal);
        Assert.Contains("1 Swords to Plowshares", result.ArchidektFullListText, StringComparison.Ordinal);
        Assert.Contains("1 Mystic Remora", result.ArchidektFullListText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildExportAsync_AddedBasicExportsAsAddWithoutMetadataWarningInBothDialects()
    {
        var service = CreateService(
            contextBuilder: new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Arcane Signet", "Artifact", []),
                    CreateResolvedCard("Wastes", "Basic Land", []))
            });
        var state = CreateState() with
        {
            Pool =
            [
                CreatePoolCard("Zur the Enchanter", "Legendary Creature", quantity: 1, isCommander: true, isLocked: true),
                CreatePoolCard("Arcane Signet", "Artifact", quantity: 96),
            ],
            OriginalEntries =
            [
                CreateOriginalEntry("Zur the Enchanter", 1, "commander"),
                CreateOriginalEntry("Arcane Signet", 96, "mainboard"),
            ],
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Wastes",
                    Delta = 3,
                    IsAddedBasic = true,
                },
            ],
        };

        CutLabExportView result = await service.BuildExportAsync(state, "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.DoesNotContain(result.ReconstructionWarnings, warning => warning.Contains("Wastes", StringComparison.Ordinal));
        Assert.Contains("ADD", result.MoxfieldPatchText, StringComparison.Ordinal);
        Assert.Contains("3 Wastes", result.MoxfieldPatchText, StringComparison.Ordinal);
        Assert.Contains("ADD", result.ArchidektPatchText, StringComparison.Ordinal);
        Assert.Contains("3 Wastes", result.ArchidektPatchText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildExportAsync_TrimmedBasicExportsAsCutInBothDialects()
    {
        var service = CreateService(
            contextBuilder: new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Island", "Basic Land — Island", ["U"]))
            });
        var state = CreateState() with
        {
            Pool =
            [
                CreatePoolCard("Zur the Enchanter", "Legendary Creature", quantity: 1, isCommander: true, isLocked: true),
                CreatePoolCard("Island", "Basic Land — Island", quantity: 102),
            ],
            OriginalEntries =
            [
                CreateOriginalEntry("Zur the Enchanter", 1, "commander"),
                CreateOriginalEntry("Island", 102, "mainboard"),
            ],
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Island",
                    Delta = -3,
                    IsAddedBasic = false,
                },
            ],
        };

        CutLabExportView result = await service.BuildExportAsync(state, "Focused", ["Zur the Enchanter"], CancellationToken.None);

        Assert.Contains("CUT", result.MoxfieldPatchText, StringComparison.Ordinal);
        Assert.Contains("3 Island", result.MoxfieldPatchText, StringComparison.Ordinal);
        Assert.Contains("CUT", result.ArchidektPatchText, StringComparison.Ordinal);
        Assert.Contains("3 Island", result.ArchidektPatchText, StringComparison.Ordinal);
    }

    private static CutLabExportService CreateService(
        FakeAnalysisContextBuilder? contextBuilder = null,
        ICommanderBanListService? banListService = null)
        => new(
            contextBuilder ?? new FakeAnalysisContextBuilder
            {
                Result = BuildAnalysisContext(
                    CreateResolvedCard("Zur the Enchanter", "Legendary Creature", ["W", "U", "B"]),
                    CreateResolvedCard("Arcane Signet", "Artifact", []))
            },
            new CutLabResolvedCardCache(),
            banListService ?? new FakeBanListService([]),
            new FakeLogger<CutLabExportService>());

    private static CutLabState CreateState()
        => new()
        {
            Commander = "Zur the Enchanter",
            Pool =
            [
                CreatePoolCard("Zur the Enchanter", "Legendary Creature", quantity: 1, isCommander: true, isLocked: true),
                CreatePoolCard("Arcane Signet", "Artifact", quantity: 99),
            ],
            OriginalEntries =
            [
                CreateOriginalEntry("Zur the Enchanter", 1, "commander"),
                CreateOriginalEntry("Arcane Signet", 99, "mainboard"),
            ],
            Intent = new CutLabIntent
            {
                PrimaryPlan = "Value enchantments",
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };

    private static CutLabPoolCard CreatePoolCard(string name, string typeLine, int quantity = 1, bool isCommander = false, bool isLocked = false)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine,
            IsCommander = isCommander,
            IsLocked = isLocked,
        };

    private static CutLabOriginalEntry CreateOriginalEntry(string name, int quantity, string board, string? setCode = null, string? collectorNumber = null, string? category = null)
        => new()
        {
            Name = name,
            Quantity = quantity,
            Board = board,
            SetCode = setCode,
            CollectorNumber = collectorNumber,
            Category = category,
        };

    private static ScryfallCardData CreateResolvedCard(string name, string typeLine, IReadOnlyList<string>? colorIdentity)
        => new()
        {
            Name = name,
            TypeLine = typeLine,
            ColorIdentity = colorIdentity,
        };

    private static CutLabAnalysisContext BuildAnalysisContext(params ScryfallCardData[] cards)
        => new(
            [],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            4,
            ManabaseMode.Focused,
            new CutLabClassificationContext([], true, true, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            cards);

    private sealed class FakeAnalysisContextBuilder : ICutLabAnalysisContextBuilder
    {
        public CutLabAnalysisContext Result { get; set; } = BuildAnalysisContext();

        public int BuildAsyncCallCount { get; private set; }

        public Task<CutLabAnalysisContext> BuildAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            BuildAsyncCallCount++;
            return Task.FromResult(Result);
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
        {
            cards = null;
            return false;
        }

        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            bool failOpenOnLookupErrors = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.ResolvedCards);

        public bool TrySeedDerivedPool(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> sourceCards,
            out IReadOnlyList<ScryfallCardData>? seededCards)
        {
            seededCards = null;
            return false;
        }
    }

    private sealed class FakeBanListService(IReadOnlyList<string> bannedCards) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(bannedCards);
    }

    private sealed class ThrowingBanListService(HttpRequestException exception) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<string>>(exception);
    }
}
