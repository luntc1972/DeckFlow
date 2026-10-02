using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CommanderCategoryNorms;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CommanderCategoryNormsKeyTests
{
    [Fact]
    public async Task FlagOn_ChatGpt_PromptCarriesNormsBlockAndRules_ProviderGetsHarvestKey()
    {
        var provider = new FakeCommanderCategoryNormsProvider(new("Kraum, Ludevic's Opus", 412,
        [new CommanderCategorySummary("Ramp", 0, 0, 338 / 412.0), new CommanderCategorySummary("Card Draw", 0, 0, 293 / 412.0), new CommanderCategorySummary("Counterspell", 0, 0, 210 / 412.0)]));
        var service = Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), provider);
        var prompt = (await service.BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest())).AnalysisPromptText;
        Assert.NotNull(prompt);
        Assert.Equal(["Kraum, Ludevic's Opus"], provider.RequestedKeys);
        Assert.Contains("HARVESTED COMMANDER CATEGORY NORMS - 412 decks (HIGH confidence)", prompt);
        Assert.Contains("- Ramp - in 82% of 412 decks", prompt);
        Assert.Contains("## HEURISTIC VALIDATION", prompt);
        Assert.True(prompt.IndexOf("HARVESTED COMMANDER CATEGORY NORMS", StringComparison.Ordinal) < prompt.IndexOf("## EVIDENCE RULES", StringComparison.Ordinal));
        Assert.StartsWith("EXECUTE NOW", prompt);
    }

    [Fact]
    public async Task FlagOff_ProviderNotCalled_PromptMatchesBaselineGolden()
    {
        var provider = new FakeCommanderCategoryNormsProvider(null);
        var result = await Create(PacketByteIdentityFixtures.AllAnalysisFlagsOff(), provider).BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest());
        Assert.Empty(provider.RequestedKeys);
        Assert.Equal(AnalysisGoldens.BaselineAnalysisPrompt("ChatGPT"), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
    }

    [Fact]
    public async Task ProviderThrows_FlagOn_PromptBuildsWithoutBlock()
    {
        var result = await Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), new ThrowingCommanderCategoryNormsProvider()).BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest());
        Assert.DoesNotContain("HARVESTED COMMANDER CATEGORY NORMS", result.AnalysisPromptText);
        Assert.Equal(AnalysisGoldens.BaselineAnalysisPrompt("ChatGPT"), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
    }

    [Fact]
    public async Task PartnerDeck_ReversedOrder_KeyedByAlphabeticallyFirst()
    {
        var entries = PacketByteIdentityFixtures.CompanionEntries();
        (entries[0], entries[1]) = (entries[1], entries[0]);
        var provider = new FakeCommanderCategoryNormsProvider(PacketByteIdentityFixtures.FixedCommanderCategoryNorms());

        var result = await Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), provider, entries).BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest());

        Assert.Equal(["Kraum, Ludevic's Opus"], provider.RequestedKeys);
        Assert.Contains("keyed under Kraum, Ludevic's Opus, the alphabetically first commander in this deck", result.AnalysisPromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandZoneFlagAlsoOn_KeyIsHarvestKey()
    {
        var provider = new FakeCommanderCategoryNormsProvider(PacketByteIdentityFixtures.FixedCommanderCategoryNorms());
        var flags = PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag);
        flags.Flags[DeckAnalysisPacketService.CommandZoneAwarenessFlag] = true;

        await Create(flags, provider, PacketByteIdentityFixtures.CompanionEntries()).BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest());

        Assert.Equal(["Kraum, Ludevic's Opus"], provider.RequestedKeys);
        Assert.DoesNotContain(provider.RequestedKeys, key => key.Contains(" & ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DfcCommander_SingleSlash_KeyedByOracleName()
    {
        var entries = PacketByteIdentityFixtures.VersionedDecklistWithSingleSlashMissEntries()
            .Where(entry => entry.Name != "Kraum, Ludevic's Opus")
            .Select(entry => entry.Name == "Blex, Vexing Pest / Search for Blex" ? entry with { Board = "commander" } : entry)
            .ToList();
        var provider = new FakeCommanderCategoryNormsProvider(PacketByteIdentityFixtures.FixedCommanderCategoryNorms());
        var request = new DeckAnalysisRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            WorkflowStep = 2,
            DeckSource = "https://www.moxfield.com/decks/byte-identity-versioned",
            Format = "Commander",
            TargetCommanderBracket = "Upgraded",
            TargetAiPlatform = "ChatGPT",
            IncludeCardVersions = true,
            IncludeCandidateReferencesInAnalysis = true,
            SelectedAnalysisQuestions = ["bracket-2-version"],
        };

        await Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), provider, entries).BuildAsync(request);

        Assert.Equal(["Blex, Vexing Pest // Search for Blex"], provider.RequestedKeys);
    }

    [Fact]
    public async Task NullProvider_FlagOn_NoBlock()
    {
        var result = await Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), null).BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest());

        Assert.DoesNotContain("HARVESTED COMMANDER CATEGORY NORMS", result.AnalysisPromptText);
        Assert.Equal(AnalysisGoldens.BaselineAnalysisPrompt("ChatGPT"), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
    }

    [Fact]
    public async Task ProviderCancelsCaller_BuildAsyncThrows()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), new CallerCancellingCommanderCategoryNormsProvider(cancellationTokenSource));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest(), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ProviderThrowsCanceledWithoutCaller_PromptBuildsWithoutBlock()
    {
        var result = await Create(PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag), new NonCallerCancellingCommanderCategoryNormsProvider()).BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest());

        Assert.DoesNotContain("HARVESTED COMMANDER CATEGORY NORMS", result.AnalysisPromptText);
    }

    private static DeckAnalysisPacketService Create(FakeFeatureFlagCache flags, ICommanderCategoryNormsProvider? provider, List<DeckEntry>? entries = null)
        => PacketByteIdentityFixtures.CreateAnalysisService(new PacketByteIdentityFixtures.StaticMoxfieldDeckImporter(entries ?? PacketByteIdentityFixtures.BaselineEntries()), flags, normsProvider: provider);

    private sealed class ThrowingCommanderCategoryNormsProvider : ICommanderCategoryNormsProvider
    {
        public Task<CommanderCategoryNormsResult?> GetNormsAsync(string harvestKey, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class CallerCancellingCommanderCategoryNormsProvider(CancellationTokenSource cancellationTokenSource) : ICommanderCategoryNormsProvider
    {
        public Task<CommanderCategoryNormsResult?> GetNormsAsync(string harvestKey, CancellationToken cancellationToken = default)
        {
            cancellationTokenSource.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class NonCallerCancellingCommanderCategoryNormsProvider : ICommanderCategoryNormsProvider
    {
        public Task<CommanderCategoryNormsResult?> GetNormsAsync(string harvestKey, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(new CancellationToken(canceled: true));
    }
}
