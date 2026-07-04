using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Surface contract tests for the Phase 80 win-condition/combo map round-trip: flag-OFF prompt/zip
/// byte-identity, conditional zip persistence, the fresh-download serialize-fallback, download/upload
/// restore, and hardened Step-3 deserialization of the untrusted <c>WinConMapJson</c> field.
/// </summary>
public sealed partial class DeckAnalysisPacketServiceTests
{
    private const string WinConMapFlagKey = "analysis.wincon-map";

    private static FakeFeatureFlagCache WinConMapFlag(bool enabled) =>
        new(new Dictionary<string, bool> { [WinConMapFlagKey] = enabled });

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public async Task WinConMapSurfaceContract_FlagOffPrompt_HasNoWinConMapBlock(string platformName)
    {
        var service = CreateService(
            flagCache: WinConMapFlag(false),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            TargetAiPlatform = platformName,
            WinConMapJson = FixedWinConMapJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.WinConMap);

        var prompt = BuildAnalysisPromptForWinConMap(platformName, winConMapText: null);
        Assert.DoesNotContain("WIN CONDITION", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void WinConMapSurfaceContract_FlagOffZip_ExcludesWinConMapEntryAndSentinel()
    {
        var request = new DeckAnalysisRequest
        {
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = string.Empty
        };

        var entries = ReadZipEntries(PacketArtifactStore.BuildZip(
            request,
            commanderName: "Atraxa, Praetors' Voice",
            inputSummary: "Input summary",
            requestContextText: "target_ai_platform: ChatGPT",
            referenceText: "Reference",
            analysisPromptText: BuildAnalysisPromptForWinConMap("ChatGPT", winConMapText: null),
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            canonicalDeckListText: "1 Sol Ring",
            originalDeckText: null,
            interactionAuditJson: null,
            winConMapJson: request.WinConMapJson));

        Assert.DoesNotContain("61-wincon-map.json", entries.Keys);
        Assert.DoesNotContain("WIN CONDITION", entries["31-analysis-prompt.txt"], StringComparison.Ordinal);
        Assert.DoesNotContain("WIN CONDITION", entries["all-responses.txt"], StringComparison.Ordinal);
    }

    [Fact]
    public void WinConMapSurfaceContract_FlagOnZip_WritesAndRestoresWinConMapJson()
    {
        var request = new DeckAnalysisRequest
        {
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = FixedWinConMapJson
        };

        var zipBytes = PacketArtifactStore.BuildZip(
            request,
            commanderName: "Atraxa, Praetors' Voice",
            inputSummary: "Input summary",
            requestContextText: "target_ai_platform: ChatGPT",
            referenceText: "Reference",
            analysisPromptText: BuildAnalysisPromptForWinConMap("ChatGPT", DeckAnalysisPacketService.BuildWinConMapText(BuildWinConMap())),
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            canonicalDeckListText: "1 Sol Ring",
            originalDeckText: null,
            interactionAuditJson: null,
            winConMapJson: request.WinConMapJson);

        var entries = ReadZipEntries(zipBytes);
        Assert.Contains("61-wincon-map.json", entries.Keys);
        Assert.Equal(FixedWinConMapJson, entries["61-wincon-map.json"].Trim());

        var restoredRequest = new DeckAnalysisRequest();
        using var stream = new MemoryStream(zipBytes);
        PacketArtifactStore.LoadFromZip(stream, restoredRequest);

        Assert.Equal(FixedWinConMapJson, restoredRequest.WinConMapJson);
    }

    [Fact]
    public async Task WinConMapSurfaceContract_DownloadUploadRoundTrip_RestoresJsonAndRematerializesMap()
    {
        var request = new DeckAnalysisRequest
        {
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = FixedWinConMapJson
        };

        var zipBytes = PacketArtifactStore.BuildZip(
            request,
            commanderName: "Atraxa, Praetors' Voice",
            inputSummary: "Input summary",
            requestContextText: "target_ai_platform: ChatGPT",
            referenceText: "Reference",
            analysisPromptText: BuildAnalysisPromptForWinConMap("ChatGPT", DeckAnalysisPacketService.BuildWinConMapText(BuildWinConMap())),
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            // Deliberately omit canonicalDeckListText/originalDeckText: LoadFromZip only backfills
            // DeckText when a deck-list entry is present, and the Step-3 saved-path short-circuit
            // (below) requires an EMPTY DeckSource -- mirroring a re-post that carries only the
            // parsed deck_profile JSON and round-tripped hidden fields, not a full deck re-import.
            interactionAuditJson: null,
            winConMapJson: request.WinConMapJson);

        var restoredRequest = new DeckAnalysisRequest();
        using var stream = new MemoryStream(zipBytes);
        PacketArtifactStore.LoadFromZip(stream, restoredRequest);
        Assert.Equal(FixedWinConMapJson, restoredRequest.WinConMapJson);
        Assert.True(string.IsNullOrWhiteSpace(restoredRequest.DeckSource), "Test setup: DeckSource must stay empty so BuildAsync takes the Step-3 saved-path short-circuit.");

        // Re-materialize via the Step-3 saved path (flag ON), proving the map survives the full
        // download -> upload -> re-render round trip, not just the raw JSON string.
        restoredRequest.WorkflowStep = 3;
        var service = CreateService(
            flagCache: WinConMapFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));
        var result = await service.BuildAsync(restoredRequest);

        Assert.NotNull(result.WinConMap);
        Assert.Equal(1, result.WinConMap!.AssemblyPathCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    // Blank card name in a combo.
    [InlineData("""{"Combos":[{"CardNames":[""],"Results":["Infinite damage"],"ManaValueNeeded":4,"Popularity":10,"Band":0}],"NearCombos":[],"AssemblyPathCount":1,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":0}""")]
    // Blank entry in a combo's Results list.
    [InlineData("""{"Combos":[{"CardNames":["Kiki-Jiki, Mirror Breaker"],"Results":[""],"ManaValueNeeded":4,"Popularity":10,"Band":0}],"NearCombos":[],"AssemblyPathCount":1,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":0}""")]
    // Blank entry in a near-combo's CardsInDeck list.
    [InlineData("""{"Combos":[],"NearCombos":[{"MissingCard":"Splinter Twin","CardsInDeck":[""],"Results":["Infinite damage"]}],"AssemblyPathCount":0,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":3}""")]
    // Blank entry in a near-combo's Results list.
    [InlineData("""{"Combos":[],"NearCombos":[{"MissingCard":"Splinter Twin","CardsInDeck":["Deceiver Exarch"],"Results":[""]}],"AssemblyPathCount":0,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":3}""")]
    // Null NearCombos list.
    [InlineData("""{"Combos":[],"NearCombos":null,"AssemblyPathCount":0,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":3}""")]
    // Null ClosingCards list.
    [InlineData("""{"Combos":[],"NearCombos":[],"AssemblyPathCount":0,"ClosingCards":null,"ComboDataAvailable":true,"OverallBand":3}""")]
    // Undefined Band enum value on a combo.
    [InlineData("""{"Combos":[{"CardNames":["Kiki-Jiki, Mirror Breaker"],"Results":["Infinite combat steps"],"ManaValueNeeded":4,"Popularity":10,"Band":99}],"NearCombos":[],"AssemblyPathCount":1,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":0}""")]
    // Undefined OverallBand enum value.
    [InlineData("""{"Combos":[],"NearCombos":[],"AssemblyPathCount":0,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":99}""")]
    // Out-of-range closing-card Quantity.
    [InlineData("""{"Combos":[],"NearCombos":[],"AssemblyPathCount":0,"ClosingCards":[{"Name":"Craterhoof Behemoth","Quantity":100}],"ComboDataAvailable":true,"OverallBand":3}""")]
    // Blank closing-card Name.
    [InlineData("""{"Combos":[],"NearCombos":[],"AssemblyPathCount":0,"ClosingCards":[{"Name":"","Quantity":1}],"ComboDataAvailable":true,"OverallBand":3}""")]
    // Tampered AssemblyPathCount (does not match Combos.Count).
    [InlineData("""{"Combos":[{"CardNames":["Kiki-Jiki, Mirror Breaker"],"Results":["Infinite combat steps"],"ManaValueNeeded":4,"Popularity":10,"Band":0}],"NearCombos":[],"AssemblyPathCount":5,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":0}""")]
    public async Task WinConMapSurfaceContract_Step3InvalidWinConMapJson_YieldsNullWithoutThrowing(string winConMapJson)
    {
        var service = CreateService(
            flagCache: WinConMapFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = winConMapJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.WinConMap);
    }

    /// <summary>
    /// Phase 80 code-review fix (HIGH): the BUILD path (<see cref="WinConMapAggregator.Compute"/>)
    /// passes Commander Spellbook's <c>manaValueNeeded</c> through UNBOUNDED, so a single combo with
    /// a value over 30 renders fine at Step 2 and in the downloaded zip. The RESTORE-path validation
    /// previously imposed an artificial &lt;= 30 upper bound that the build path never enforced, so
    /// restoring that exact map would fail the private structural validator and silently null out
    /// the ENTIRE map. Restore must accept anything the build path can emit.
    /// </summary>
    [Fact]
    public async Task WinConMapSurfaceContract_Step3ComboWithLargeManaValueNeeded_RestoresMapIntact()
    {
        var winConMap = new WinConMap(
            Combos: new[]
            {
                new WinConCombo(
                    CardNames: new[] { "Kiki-Jiki, Mirror Breaker", "Restoration Angel" },
                    Results: new[] { "Infinite combat steps" },
                    ManaValueNeeded: 42,
                    Popularity: 42,
                    Band: WinConBand.Mid)
            },
            NearCombos: Array.Empty<WinConNearCombo>(),
            AssemblyPathCount: 1,
            ClosingCards: Array.Empty<WinConClosingCard>(),
            ComboDataAvailable: true,
            OverallBand: WinConBand.Mid);
        var winConMapJson = JsonSerializer.Serialize(winConMap);

        var service = CreateService(
            flagCache: WinConMapFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = winConMapJson
        });

        Assert.NotNull(result.WinConMap);
        Assert.Equal(42, result.WinConMap!.Combos[0].ManaValueNeeded);
    }

    [Fact]
    public async Task WinConMapSurfaceContract_Step3OverCapComboList_YieldsNullWithoutThrowing()
    {
        // 21 CardNames entries exceeds the per-combo per-list entry cap (20).
        var oversizedCardNames = string.Join(",", Enumerable.Range(1, 21).Select(i => $"\"Card {i}\""));
        var winConMapJson = $$"""{"Combos":[{"CardNames":[{{oversizedCardNames}}],"Results":["Infinite damage"],"ManaValueNeeded":4,"Popularity":10,"Band":0}],"NearCombos":[],"AssemblyPathCount":1,"ClosingCards":[],"ComboDataAvailable":true,"OverallBand":0}""";

        var service = CreateService(
            flagCache: WinConMapFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = winConMapJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.WinConMap);
    }

    [Fact]
    public async Task WinConMapSurfaceContract_Step3OversizedWinConMapJson_YieldsNullWithoutThrowing()
    {
        var service = CreateService(
            flagCache: WinConMapFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            WinConMapJson = FixedWinConMapJson + new string('x', 33000)
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.WinConMap);
    }

    private static readonly string FixedWinConMapJson = JsonSerializer.Serialize(BuildWinConMap());

    private static WinConMap BuildWinConMap() => new(
        Combos: new[]
        {
            new WinConCombo(
                CardNames: new[] { "Kiki-Jiki, Mirror Breaker", "Restoration Angel" },
                Results: new[] { "Infinite combat steps" },
                ManaValueNeeded: 8,
                Popularity: 42,
                Band: WinConBand.Mid)
        },
        NearCombos: new[]
        {
            new WinConNearCombo(
                MissingCard: "Splinter Twin",
                CardsInDeck: new[] { "Deceiver Exarch" },
                Results: new[] { "Infinite hasty tokens" })
        },
        AssemblyPathCount: 1,
        ClosingCards: new[] { new WinConClosingCard("Craterhoof Behemoth", 1) },
        ComboDataAvailable: true,
        OverallBand: WinConBand.Mid);

    private static string BuildAnalysisPromptForWinConMap(string platformName, string? winConMapText)
    {
        var registry = new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });

        return registry.Build(
            AiPlatform.Normalize(platformName),
            new DeckAnalysisRequest
            {
                Format = "Commander",
                TargetCommanderBracket = "cEDH",
                TargetAiPlatform = platformName
            },
            decklistText: "1 Sol Ring",
            referenceText: "Reference text",
            deckProfileSchemaJson: "{}",
            commanderName: null,
            selectedQuestionIds: [],
            bannedCards: [],
            comboResult: null,
            includeCardVersions: false,
            enrichments: new AnalysisPromptEnrichments(WinConMapText: winConMapText));
    }
}
