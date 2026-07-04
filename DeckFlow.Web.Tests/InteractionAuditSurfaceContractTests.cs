using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Surface contract tests for the Phase 79 interaction-audit round-trip: flag-OFF prompt/zip
/// byte-identity, conditional zip persistence, download/upload restore, and hardened Step-3
/// deserialization of the untrusted <c>InteractionAuditJson</c> field.
/// </summary>
public sealed partial class DeckAnalysisPacketServiceTests
{
    private const string InteractionAuditFlagKey = "analysis.interaction-audit";

    private static FakeFeatureFlagCache InteractionAuditFlag(bool enabled) =>
        new(new Dictionary<string, bool> { [InteractionAuditFlagKey] = enabled });

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public async Task InteractionAuditSurfaceContract_FlagOffPrompt_HasNoInteractionAuditBlock(string platformName)
    {
        var service = CreateService(
            flagCache: InteractionAuditFlag(false),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            TargetAiPlatform = platformName,
            InteractionAuditJson = FixedInteractionAuditJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.InteractionAudit);

        var prompt = BuildAnalysisPrompt(platformName, interactionAuditText: null);
        Assert.DoesNotContain("INTERACTION AUDIT", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void InteractionAuditSurfaceContract_FlagOffZip_ExcludesInteractionAuditEntryAndSentinel()
    {
        var request = new DeckAnalysisRequest
        {
            DeckProfileJson = SavedDeckProfileJson,
            InteractionAuditJson = string.Empty
        };

        var entries = ReadZipEntries(PacketArtifactStore.BuildZip(
            request,
            commanderName: "Atraxa, Praetors' Voice",
            inputSummary: "Input summary",
            requestContextText: "target_ai_platform: ChatGPT",
            referenceText: "Reference",
            analysisPromptText: BuildAnalysisPrompt("ChatGPT", interactionAuditText: null),
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            canonicalDeckListText: "1 Sol Ring",
            originalDeckText: null,
            interactionAuditJson: request.InteractionAuditJson));

        Assert.DoesNotContain("60-interaction-audit.json", entries.Keys);
        Assert.DoesNotContain("INTERACTION AUDIT", entries["31-analysis-prompt.txt"], StringComparison.Ordinal);
        Assert.DoesNotContain("INTERACTION AUDIT", entries["all-responses.txt"], StringComparison.Ordinal);
    }

    [Fact]
    public void InteractionAuditSurfaceContract_FlagOnZip_WritesAndRestoresInteractionAuditJson()
    {
        var request = new DeckAnalysisRequest
        {
            DeckProfileJson = SavedDeckProfileJson,
            InteractionAuditJson = FixedInteractionAuditJson
        };

        var zipBytes = PacketArtifactStore.BuildZip(
            request,
            commanderName: "Atraxa, Praetors' Voice",
            inputSummary: "Input summary",
            requestContextText: "target_ai_platform: ChatGPT",
            referenceText: "Reference",
            analysisPromptText: BuildAnalysisPrompt("ChatGPT", DeckAnalysisPacketService.BuildInteractionAuditText(BuildInteractionAudit())),
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            canonicalDeckListText: "1 Sol Ring",
            originalDeckText: null,
            interactionAuditJson: request.InteractionAuditJson);

        var entries = ReadZipEntries(zipBytes);
        Assert.Contains("60-interaction-audit.json", entries.Keys);
        Assert.Equal(FixedInteractionAuditJson, entries["60-interaction-audit.json"].Trim());

        var restoredRequest = new DeckAnalysisRequest();
        using var stream = new MemoryStream(zipBytes);
        PacketArtifactStore.LoadFromZip(stream, restoredRequest);

        Assert.Equal(FixedInteractionAuditJson, restoredRequest.InteractionAuditJson);
    }

    [Fact]
    public async Task InteractionAuditSurfaceContract_Step3RestoresInteractionAudit_FromZipRestoredJson()
    {
        var service = CreateService(
            flagCache: InteractionAuditFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            InteractionAuditJson = FixedInteractionAuditJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.NotNull(result.InteractionAudit);
        Assert.Equal("Swords to Plowshares", result.InteractionAudit.TargetedRemoval.Confident[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    [InlineData("""{"TargetedRemoval":null,"BoardWipes":{"Confident":[],"Review":[]},"Counterspells":{"Confident":[],"Review":[]},"ProtectionRecursion":{"Confident":[],"Review":[]},"StaxTaxation":{"Confident":[],"Review":[]},"CoverageGaps":["gap"]}""")]
    [InlineData("""{"TargetedRemoval":{"Confident":null,"Review":[]},"BoardWipes":{"Confident":[],"Review":[]},"Counterspells":{"Confident":[],"Review":[]},"ProtectionRecursion":{"Confident":[],"Review":[]},"StaxTaxation":{"Confident":[],"Review":[]},"CoverageGaps":["gap"]}""")]
    [InlineData("""{"TargetedRemoval":{"Confident":[{"Name":null,"Quantity":1}],"Review":[]},"BoardWipes":{"Confident":[],"Review":[]},"Counterspells":{"Confident":[],"Review":[]},"ProtectionRecursion":{"Confident":[],"Review":[]},"StaxTaxation":{"Confident":[],"Review":[]},"CoverageGaps":["gap"]}""")]
    [InlineData("""{"TargetedRemoval":{"Confident":[{"Name":"Swords to Plowshares","Quantity":100}],"Review":[]},"BoardWipes":{"Confident":[],"Review":[]},"Counterspells":{"Confident":[],"Review":[]},"ProtectionRecursion":{"Confident":[],"Review":[]},"StaxTaxation":{"Confident":[],"Review":[]},"CoverageGaps":["gap"]}""")]
    [InlineData("""{"TargetedRemoval":{"Confident":[{"Name":"Swords to Plowshares","Quantity":1}],"Review":[]},"BoardWipes":{"Confident":[],"Review":[]},"Counterspells":{"Confident":[],"Review":[]},"ProtectionRecursion":{"Confident":[],"Review":[]},"StaxTaxation":{"Confident":[],"Review":[]},"CoverageGaps":[null]}""")]
    public async Task InteractionAuditSurfaceContract_Step3InvalidInteractionAuditJson_YieldsNullWithoutThrowing(string interactionAuditJson)
    {
        var service = CreateService(
            flagCache: InteractionAuditFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            InteractionAuditJson = interactionAuditJson
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.InteractionAudit);
    }

    [Fact]
    public async Task InteractionAuditSurfaceContract_Step3OversizedInteractionAuditJson_YieldsNullWithoutThrowing()
    {
        var service = CreateService(
            flagCache: InteractionAuditFlag(true),
            executeCollectionAsync: (_, _) => throw new InvalidOperationException("Scryfall lookup should not run for saved Step 3 JSON."));

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckProfileJson = SavedDeckProfileJson,
            InteractionAuditJson = FixedInteractionAuditJson + new string('x', 17000)
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Null(result.InteractionAudit);
    }

    private static readonly string FixedInteractionAuditJson = JsonSerializer.Serialize(BuildInteractionAudit());

    private static InteractionAudit BuildInteractionAudit() => new(
        TargetedRemoval: Bucket("Swords to Plowshares", "Beast Within"),
        BoardWipes: Bucket("Farewell", "Toxic Deluge"),
        Counterspells: Bucket("Counterspell", "Mana Drain"),
        ProtectionRecursion: Bucket("Teferi's Protection", "Eternal Witness"),
        StaxTaxation: Bucket("Drannith Magistrate", "Thalia, Guardian of Thraben"),
        CoverageGaps: ["Counterspell count is approximately low; verify against the list."]);

    private static InteractionBucketResult Bucket(string confident, string review) =>
        new(
            Confident: [new InteractionCard(confident, 1)],
            Review: [new InteractionCard(review, 1)]);

    private static string BuildAnalysisPrompt(string platformName, string? interactionAuditText)
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
            enrichments: new AnalysisPromptEnrichments(InteractionAuditText: interactionAuditText));
    }

    private static IReadOnlyDictionary<string, string> ReadZipEntries(byte[] zipBytes)
    {
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            },
            StringComparer.OrdinalIgnoreCase);
    }
}
