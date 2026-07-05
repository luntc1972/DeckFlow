using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// PKTSVC-04 byte-identical regression harness for <see cref="MetaGapService"/>, captured against
/// the CURRENT (unrefactored) service. Every assertion is <see cref="StringComparison.Ordinal"/> against
/// a golden captured from a real <see cref="MetaGapService.BuildAsync"/> run.
///
/// MetaGap has NO prompt-mutating feature-flag registry (confirmed by grep: no
/// <c>IFeatureFlagCache</c> reference anywhere in MetaGapService.cs) and no free-form
/// DeckName/StrategyNotes/MetaNotes fields to whitespace-lock (confirmed against
/// <c>MetaGapRequest.cs</c> — its only string field is <c>CommanderName</c>, which is
/// <c>JsonTextFormatterService.NormalizeSingleLine</c>'d, the SAME shared helper Comparison uses, so
/// no separate whitespace fixture is needed here). Coverage:
/// 1. Forced collection-miss recovered via SearchFallbackCardAsync (ResolveOracleNameMapAsync,
///    MetaGapService.cs:562-621).
/// 2. No-explicit-Commander-section reflag (ReflagCommanderEntry, MetaGapService.cs:465-485).
/// </summary>
public sealed class MetaGapByteIdentityTests
{
    private const string UserDeckText = """
        Commander
        1 Kraum, Ludevic's Opus

        Deck
        1 Sol Ring
        1 Arcane Signet
        """;

    // ---------------------------------------------------------------------------------------
    // Baseline: 3 AI platforms (MetaGap has no prompt-mutating flags to sweep).
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(PacketByteIdentityFixtures.ChatGpt)]
    [InlineData(PacketByteIdentityFixtures.Claude)]
    [InlineData(PacketByteIdentityFixtures.Gemini)]
    public async Task Baseline_MatchesGolden(string platform)
    {
        var service = CreateService(new FixtureEdhTop16Client(ReferenceEntry()));

        var result = await service.BuildAsync(new MetaGapRequest
        {
            WorkflowStep = 2,
            CommanderName = "Kraum, Ludevic's Opus",
            DeckSource = UserDeckText,
            TargetAiPlatform = platform,
            SelectedReferenceIndexes = [0],
        });

        Assert.Equal(MetaGapGoldens.BaselinePromptText(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.PromptText), StringComparer.Ordinal);
        Assert.Equal(MetaGapGoldens.BaselineRequestContextText(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.RequestContextText), StringComparer.Ordinal);
        Assert.Equal(MetaGapGoldens.BaselineDecklistText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.DecklistText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // Forced collection-miss -> SearchFallbackCardAsync fallthrough (single-slash DFC submission).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ForcedCollectionMissFallback_MatchesGolden()
    {
        var service = CreateService(new FixtureEdhTop16Client(ReferenceEntry()));

        var result = await service.BuildAsync(new MetaGapRequest
        {
            WorkflowStep = 2,
            CommanderName = "Kraum, Ludevic's Opus",
            DeckSource = """
                Commander
                1 Kraum, Ludevic's Opus

                Deck
                1 Blex, Vexing Pest / Search for Blex
                1 Sol Ring
                """,
            TargetAiPlatform = PacketByteIdentityFixtures.ChatGpt,
            SelectedReferenceIndexes = [0],
        });

        Assert.Equal(MetaGapGoldens.CollectionMissFallbackPromptText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.PromptText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // No-explicit-Commander-section reflag (ReflagCommanderEntry runs inside LoadDeckAsync).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task NoCommanderSectionReflag_MatchesGolden()
    {
        var service = CreateService(new FixtureEdhTop16Client(ReferenceEntry()));

        var result = await service.BuildAsync(new MetaGapRequest
        {
            WorkflowStep = 2,
            CommanderName = string.Empty,
            DeckSource = """
                1 Kraum, Ludevic's Opus
                1 Sol Ring
                1 Arcane Signet
                """,
            TargetAiPlatform = PacketByteIdentityFixtures.ChatGpt,
            SelectedReferenceIndexes = [0],
        });

        Assert.Equal("Kraum, Ludevic's Opus", result.ResolvedCommanderName);
        Assert.Equal(MetaGapGoldens.NoCommanderSectionDecklistText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.DecklistText), StringComparer.Ordinal);
    }

    private static EdhTop16Entry ReferenceEntry() => new()
    {
        Standing = 1,
        Wins = 7,
        Losses = 1,
        Draws = 0,
        PlayerName = "Reference Pilot",
        DecklistUrl = "https://edhtop16.example/reference",
        TournamentName = "Fixture Championship",
        TournamentId = "fixture-champ",
        TournamentSize = 64,
        MainDeck =
        [
            new EdhTop16Card { Name = "Kraum, Ludevic's Opus", Type = "Commander" },
            new EdhTop16Card { Name = "Sol Ring", Type = "Artifact" },
            new EdhTop16Card { Name = "Counterspell", Type = "Instant" },
        ],
    };

    private static MetaGapService CreateService(IEdhTop16Client edhTop16Client)
        => TestServiceFactory.CreateMetaGapService(
            new FixtureMoxfieldDeckImporter(),
            new FixtureArchidektDeckImporter(),
            new MoxfieldParser(),
            new ArchidektParser(),
            edhTop16Client,
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

    private sealed class FixtureEdhTop16Client : IEdhTop16Client
    {
        private readonly IReadOnlyList<EdhTop16Entry> _entries;

        public FixtureEdhTop16Client(params EdhTop16Entry[] entries) => _entries = entries;

        public Task<IReadOnlyList<EdhTop16Entry>> SearchCommanderEntriesAsync(
            string commanderName,
            CedhMetaTimePeriod timePeriod,
            CedhMetaSortBy sortBy,
            int minEventSize,
            int? maxStanding,
            int count,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_entries);

        public Task<IReadOnlyList<EdhTop16Entry>> GetTopArchetypesAsync(int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
