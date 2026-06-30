using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckPrimerStalenessTests
{
    [Fact]
    public void DeckMultisetHash_ReorderAndPrintingSwapAreEquivalent()
    {
        var original = Hash(
        [
            CreateDeckEntry("Sol Ring", "mainboard", setCode: "LCC", collectorNumber: "313"),
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander", setCode: "MUL", collectorNumber: "98"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 2, setCode: "CMM", collectorNumber: "690")
        ]);
        var reordered = Hash(
        [
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 2, setCode: "CMM", collectorNumber: "690"),
            CreateDeckEntry("Sol Ring", "mainboard", setCode: "LCC", collectorNumber: "313"),
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander", setCode: "MUL", collectorNumber: "98")
        ]);
        var printingSwap = Hash(
        [
            CreateDeckEntry("Sol Ring", "mainboard", setCode: "WHO", collectorNumber: "245"),
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander", setCode: "2X2", collectorNumber: "190"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 2, setCode: "CLB", collectorNumber: "293")
        ]);

        Assert.Equal(original, reordered);
        Assert.Equal(original, printingSwap);
    }

    [Fact]
    public void DeckMultisetHash_AddRemoveAndQuantityChangeAreDifferent()
    {
        var originalEntries = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Sol Ring", "mainboard"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 2)
        };
        var original = Hash(originalEntries);

        Assert.NotEqual(original, Hash([.. originalEntries, CreateDeckEntry("Swords to Plowshares", "mainboard")]));
        Assert.NotEqual(original, Hash(originalEntries.Where(entry => entry.Name != "Sol Ring").ToList()));
        Assert.NotEqual(original, Hash(
        [
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Sol Ring", "mainboard"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 1)
        ]));
    }

    [Fact]
    public void EvaluateStaleness_NullOrEmptyGeneratedHashIsFreshAndStillReportsCurrentHash()
    {
        var service = CreateService();
        var current = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Sol Ring", "mainboard")
        };

        var nullHash = service.EvaluateStaleness(null, current, current);
        var emptyHash = service.EvaluateStaleness("", current, current);

        Assert.False(nullHash.IsStale);
        Assert.Null(nullHash.ChangedCardCount);
        Assert.Equal(Hash(current), nullHash.CurrentDeckHash);
        Assert.False(emptyHash.IsStale);
        Assert.Null(emptyHash.ChangedCardCount);
        Assert.Equal(Hash(current), emptyHash.CurrentDeckHash);
    }

    [Fact]
    public void EvaluateStaleness_MissingCurrentDeckIsFreshWithNullHash()
    {
        var service = CreateService();

        var nullEntries = service.EvaluateStaleness("old-hash", null, null);
        var emptyEntries = service.EvaluateStaleness("old-hash", [], []);

        Assert.False(nullEntries.IsStale);
        Assert.Null(nullEntries.ChangedCardCount);
        Assert.Null(nullEntries.CurrentDeckHash);
        Assert.False(emptyEntries.IsStale);
        Assert.Null(emptyEntries.ChangedCardCount);
        Assert.Null(emptyEntries.CurrentDeckHash);
    }

    [Fact]
    public void EvaluateStaleness_MatchingCurrentHashIsFresh()
    {
        var service = CreateService();
        var current = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Sol Ring", "mainboard")
        };

        var result = service.EvaluateStaleness(Hash(current), current, current);

        Assert.False(result.IsStale);
        Assert.Null(result.ChangedCardCount);
        Assert.Equal(Hash(current), result.CurrentDeckHash);
    }

    [Fact]
    public void EvaluateStaleness_DifferentHashWithSavedDeckCountsAddRemoveAndQuantityChanges()
    {
        var service = CreateService();
        var saved = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 2),
            CreateDeckEntry("Swords to Plowshares", "mainboard")
        };
        var current = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 1),
            CreateDeckEntry("Cyclonic Rift", "mainboard")
        };

        var result = service.EvaluateStaleness(Hash(saved), current, saved);

        Assert.True(result.IsStale);
        Assert.Equal(3, result.ChangedCardCount);
        Assert.Equal(Hash(current), result.CurrentDeckHash);
    }

    [Fact]
    public void EvaluateStaleness_DifferentHashWithoutSavedDeckSuppressesChangedCount()
    {
        var service = CreateService();
        var current = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Sol Ring", "mainboard")
        };

        var nullSaved = service.EvaluateStaleness("old-hash", current, null);
        var emptySaved = service.EvaluateStaleness("old-hash", current, []);

        Assert.True(nullSaved.IsStale);
        Assert.Null(nullSaved.ChangedCardCount);
        Assert.Equal(Hash(current), nullSaved.CurrentDeckHash);
        Assert.True(emptySaved.IsStale);
        Assert.Null(emptySaved.ChangedCardCount);
        Assert.Equal(Hash(current), emptySaved.CurrentDeckHash);
    }

    [Fact]
    public void EvaluateStaleness_PrintingOnlyDiffDoesNotContributeToChangedCount()
    {
        var service = CreateService();
        var saved = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander", setCode: "MUL", collectorNumber: "98"),
            CreateDeckEntry("Sol Ring", "mainboard", setCode: "LCC", collectorNumber: "313")
        };
        var current = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander", setCode: "2X2", collectorNumber: "190"),
            CreateDeckEntry("Sol Ring", "mainboard", setCode: "WHO", collectorNumber: "245")
        };

        var fresh = service.EvaluateStaleness(Hash(saved), current, saved);
        var staleByExternalHash = service.EvaluateStaleness("different-generated-hash", current, saved);

        Assert.False(fresh.IsStale);
        Assert.Null(fresh.ChangedCardCount);
        Assert.True(staleByExternalHash.IsStale);
        Assert.Equal(0, staleByExternalHash.ChangedCardCount);
    }

    [Fact]
    public void TryParseDeckTextLocal_BlankUrlAndUnrecognizedReturnNull()
    {
        var service = CreateService();

        Assert.Null(service.TryParseDeckTextLocal("   "));
        Assert.Null(service.TryParseDeckTextLocal("https://www.moxfield.com/decks/abc123"));
        Assert.Null(service.TryParseDeckTextLocal("https://archidekt.com/decks/15918942/trashpanda"));
        Assert.Null(service.TryParseDeckTextLocal("*F*"));
    }

    [Fact]
    public void TryParseDeckTextLocal_ValidExportUsesRealLocalParser()
    {
        var service = CreateService();

        var entries = service.TryParseDeckTextLocal("""
            Commander
            1 Atraxa, Praetors' Voice (MUL) 98

            1 Sol Ring (LCC) 313
            """);

        Assert.NotNull(entries);
        Assert.Equal(2, entries.Count);
        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("Atraxa, Praetors' Voice", entries[0].Name);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("Sol Ring", entries[1].Name);
    }

    [Fact]
    public void TryParseDeckTextLocal_OverrideBypassesRealParser()
    {
        var service = CreateService(parseDeckTextLocalOverride: text =>
        [
            CreateDeckEntry(text, "mainboard")
        ]);

        var entries = service.TryParseDeckTextLocal("not real export text");

        Assert.NotNull(entries);
        var entry = Assert.Single(entries);
        Assert.Equal("not real export text", entry.Name);
    }

    [Fact]
    public async Task BuildAsync_PopulatesDeckMultisetHashFromAllLoadedEntries()
    {
        var loadedEntries = new[]
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Sol Ring", "mainboard"),
            CreateDeckEntry("Wishclaw Talisman", "maybeboard")
        };
        var service = CreateService(loadDeckEntriesAsyncOverride: (_, _) => Task.FromResult(loadedEntries.ToList()));

        var result = await service.BuildAsync(CreateRequest());

        Assert.Equal(Hash(loadedEntries), result.DeckMultisetHash);
    }

    private static DeckPrimerPacketService CreateService(
        Func<string, CancellationToken, Task<List<DeckEntry>>>? loadDeckEntriesAsyncOverride = null,
        Func<string, IReadOnlyList<DeckEntry>?>? parseDeckTextLocalOverride = null)
    {
        return new DeckPrimerPacketService(
            new PrimerPromptVariantRegistry([new TestPrimerPromptVariant(AiPlatform.ChatGpt)]),
            new PacketSessionCache(),
            loadDeckEntriesAsyncOverride: loadDeckEntriesAsyncOverride ?? ((_, _) => Task.FromResult<List<DeckEntry>>(
            [
                CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
                CreateDeckEntry("Sol Ring", "mainboard")
            ])),
            findCombosAsyncOverride: (_, _) => Task.FromResult<CommanderSpellbookResult?>(null),
            getTopArchetypesAsyncOverride: (_, _) => Task.FromResult<IReadOnlyList<EdhTop16Entry>>([]),
            getCategoryRowsForCommanderAsyncOverride: (_, _) => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>([]),
            parseDeckTextLocalOverride: parseDeckTextLocalOverride);
    }

    private static DeckPrimerRequest CreateRequest()
        => new()
        {
            DeckText = """
                Commander
                1 Atraxa, Praetors' Voice

                1 Sol Ring
                """,
            TargetCommanderBracket = "Upgraded",
            SelectedSectionIds =
            [
                "verified-combos"
            ]
        };

    private static string Hash(IReadOnlyList<DeckEntry> entries)
        => PacketSessionCache.ComputeKey(DeckPrimerPacketService.BuildCanonicalDeckSourceText(entries));

    private static DeckEntry CreateDeckEntry(
        string name,
        string board,
        int quantity = 1,
        string? setCode = null,
        string? collectorNumber = null)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = board,
            SetCode = setCode,
            CollectorNumber = collectorNumber
        };

    private sealed class TestPrimerPromptVariant : IPrimerPromptVariant
    {
        public TestPrimerPromptVariant(AiPlatform platform)
        {
            Platform = platform;
        }

        public AiPlatform Platform { get; }

        public string Build(
            DeckPrimerRequest request,
            string decklistText,
            IReadOnlyList<PrimerSectionEntry> selectedSections,
            CommanderSpellbookResult? comboResult,
            IReadOnlyList<EdhTop16Entry>? top16Entries,
            CategoryDistributionSummary? categoryDistribution,
            int bracketNumber,
            CancellationToken cancellationToken = default)
        {
            return decklistText;
        }
    }
}
