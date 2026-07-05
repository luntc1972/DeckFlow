using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// PKTSVC-04 byte-identical regression harness for <see cref="DeckComparisonService"/>, captured
/// against the CURRENT (unrefactored) service. Every assertion is <see cref="StringComparison.Ordinal"/>
/// against a golden captured from a real <see cref="DeckComparisonService.BuildAsync"/> run.
///
/// Comparison has NO prompt-mutating feature-flag registry (confirmed by grep: no
/// <c>IFeatureFlagCache</c> reference anywhere in DeckComparisonService.cs), so the flag axis from the
/// Analysis suite does not apply here — only the 3-AI-platform axis plus the two mandated path-coverage
/// fixtures apply:
/// 1. Printed-name SearchFallback (a collection-miss recovered via SearchFallbackCardAsync).
/// 2. No-explicit-Commander-section reflag (ReflagCommanderEntry, DeckComparisonService.cs:363-383).
/// </summary>
public sealed class DeckComparisonByteIdentityTests
{
    private const string DeckAText = """
        Commander
        1 Kraum, Ludevic's Opus

        Deck
        1 Sol Ring
        1 Arcane Signet
        """;

    private const string DeckBText = """
        Commander
        1 Kraum, Ludevic's Opus

        Deck
        1 Sol Ring
        1 Counterspell
        """;

    // ---------------------------------------------------------------------------------------
    // Baseline: 3 AI platforms (Comparison has no prompt-mutating flags to sweep).
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(PacketByteIdentityFixtures.ChatGpt)]
    [InlineData(PacketByteIdentityFixtures.Claude)]
    [InlineData(PacketByteIdentityFixtures.Gemini)]
    public async Task Baseline_MatchesGolden(string platform)
    {
        var service = CreateService();

        var result = await service.BuildAsync(new DeckComparisonRequest
        {
            WorkflowStep = 2,
            DeckAName = "Kraum Value",
            DeckABracket = "Upgraded",
            DeckASource = DeckAText,
            DeckBName = "Atraxa Superfriends",
            DeckBBracket = "Upgraded",
            DeckBSource = DeckBText,
            TargetAiPlatform = platform,
        });

        Assert.Equal(ComparisonGoldens.BaselineComparisonPrompt(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.ComparisonPromptText), StringComparer.Ordinal);
        Assert.Equal(ComparisonGoldens.BaselineFollowUpPrompt(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.FollowUpPromptText), StringComparer.Ordinal);
        Assert.Equal(ComparisonGoldens.BaselineRequestContextText(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.RequestContextText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // Printed-name SearchFallback fixture: "Ya viene el coco" is a collection miss that resolves
    // via SearchFallbackCardAsync to "Perfect Defense // Denting Blows".
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task PrintedNameSearchFallback_MatchesGolden()
    {
        var service = CreateService();

        var result = await service.BuildAsync(new DeckComparisonRequest
        {
            WorkflowStep = 2,
            DeckAName = "Renamed Cards",
            DeckABracket = "Upgraded",
            DeckASource = """
                Commander
                1 Kraum, Ludevic's Opus

                Deck
                1 Ya viene el coco
                1 Sol Ring
                """,
            DeckBName = "Atraxa Superfriends",
            DeckBBracket = "Upgraded",
            DeckBSource = DeckBText,
        });

        Assert.Contains("Perfect Defense // Denting Blows [printed as: Ya viene el coco]", result.DeckAListText, StringComparison.Ordinal);
        Assert.Equal(ComparisonGoldens.PrintedNameFallbackDeckAListText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.DeckAListText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // No-explicit-Commander-section reflag fixture: both decks lead with a single-copy card and
    // no "Commander" header, forcing DeckComparisonService.ReflagCommanderEntry to run.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task NoCommanderSectionReflag_MatchesGolden()
    {
        var service = CreateService();
        var deckText = """
            1 Kraum, Ludevic's Opus
            1 Sol Ring
            1 Arcane Signet
            1 Counterspell
            """;

        var result = await service.BuildAsync(new DeckComparisonRequest
        {
            WorkflowStep = 2,
            DeckABracket = "Upgraded",
            DeckASource = deckText,
            DeckBBracket = "Upgraded",
            DeckBSource = deckText,
        });

        Assert.Equal("Kraum, Ludevic's Opus", result.ResolvedDeckACommander);
        Assert.Equal(ComparisonGoldens.NoCommanderSectionDeckAListText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.DeckAListText), StringComparer.Ordinal);
    }

    private static DeckComparisonService CreateService()
        => TestServiceFactory.CreateDeckComparisonService(
            new FixtureMoxfieldDeckImporter(),
            new FixtureArchidektDeckImporter(),
            new MoxfieldParser(),
            new ArchidektParser(),
            new FixtureCommanderSpellbookService(),
            executeCollectionAsync: (request, _) => Task.FromResult(PacketByteIdentityFixtures.CreateCollectionResponse(request)),
            executeSearchAsync: (request, _) => Task.FromResult(PacketByteIdentityFixtures.CreateSearchResponse(request)));

    private sealed class FixtureMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FixtureArchidektDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FixtureCommanderSpellbookService : ICommanderSpellbookService
    {
        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken = default)
            => Task.FromResult<CommanderSpellbookResult?>(null);
    }
}
