using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// PKTSVC-04 byte-identical regression harness for <see cref="DeckAnalysisPacketService"/>, captured
/// against the CURRENT (unrefactored) service. Every assertion is <see cref="StringComparison.Ordinal"/>
/// against a golden captured from a real <see cref="DeckAnalysisPacketService.BuildAsync"/> run (no
/// hand-typed goldens — see 83-01-PLAN.md's golden-capture-integrity instruction). This suite is the
/// baseline every Wave-2 migration in Phase 83 is gated against: a re-run after each collaborator
/// extraction MUST still pass, unchanged, or the migration introduced a regression.
///
/// Coverage (see 83-01-PLAN.md path_coverage_requirements):
/// 1. Versioned decklist (IncludeCardVersions=true) with a Possible-Includes section that stays PLAIN.
/// 2. Single-slash (Archidekt-style) collection-miss -> SearchPrintingFallbackCardAsync fallthrough.
/// 3. (No-commander-section reflag is Comparison/MetaGap-specific; Analysis's commander inference is
///    covered by its own existing DeckAnalysisPacketServiceTests suite and is NOT re-duplicated here.)
/// 4. All 6 prompt-mutating/reference flags swept ON and OFF, plus one ALL-4-mutating-flags-ON case.
/// 5. Whitespace-bearing DeckName/StrategyNotes/MetaNotes fields (tab/newline/multi-space/bare CR).
/// </summary>
public sealed class DeckAnalysisByteIdentityTests
{
    private const string DeckUrl = "https://www.moxfield.com/decks/byte-identity-baseline";
    private const string VersionedDeckUrl = "https://www.moxfield.com/decks/byte-identity-versioned";
    private const string CompanionDeckUrl = "https://www.moxfield.com/decks/byte-identity-companion";

    // ---------------------------------------------------------------------------------------
    // 1. Baseline: all 6 flags OFF, swept across all 3 AI platforms.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(PacketByteIdentityFixtures.ChatGpt)]
    [InlineData(PacketByteIdentityFixtures.Claude)]
    [InlineData(PacketByteIdentityFixtures.Gemini)]
    public async Task BaselineAllFlagsOff_MatchesGolden(string platform)
    {
        var result = await BuildBaselineAsync(platform, PacketByteIdentityFixtures.AllAnalysisFlagsOff());

        Assert.Equal(AnalysisGoldens.BaselineAnalysisPrompt(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
        Assert.Equal(AnalysisGoldens.BaselineReferenceText(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.ReferenceText), StringComparer.Ordinal);
        Assert.Equal(AnalysisGoldens.BaselineRequestContextText(platform), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.RequestContextText), StringComparer.Ordinal);
        Assert.Equal(AnalysisGoldens.BaselineDecklistText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.DecklistText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 2. Each of the 6 flag keys individually ON (baseline otherwise OFF), single platform.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(DeckAnalysisPacketService.CommandZoneAwarenessFlag)]
    [InlineData(DeckAnalysisPacketService.MultiAxisScoreFlag)]
    [InlineData(DeckAnalysisPacketService.InteractionAuditFlag)]
    [InlineData(DeckAnalysisPacketService.WinConMapFlag)]
    [InlineData(DeckAnalysisPacketService.ReferenceFullOracleFlag)]
    [InlineData(DeckAnalysisPacketService.ReferenceDeckStatsFlag)]
    public async Task SingleFlagOn_MatchesGolden(string flagKey)
    {
        var result = await BuildBaselineAsync(PacketByteIdentityFixtures.ChatGpt, PacketByteIdentityFixtures.WithSingleFlagOn(flagKey));

        Assert.Equal(AnalysisGoldens.SingleFlagOnAnalysisPrompt(flagKey), PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 3. ALL 4 PromptMutatingAnalysisFlags ON simultaneously (M1), companion/partner fixture.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AllFourMutatingFlagsOn_MatchesGolden()
    {
        var service = PacketByteIdentityFixtures.CreateAnalysisService(
            moxfieldDeckImporter: new StaticMoxfieldDeckImporter(PacketByteIdentityFixtures.CompanionEntries()),
            flagCache: PacketByteIdentityFixtures.AllFourMutatingFlagsOn());

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            WorkflowStep = 2,
            DeckSource = CompanionDeckUrl,
            Format = "Commander",
            TargetCommanderBracket = "Upgraded",
            TargetAiPlatform = PacketByteIdentityFixtures.ChatGpt,
            SelectedAnalysisQuestions = ["strengths-weaknesses"],
        });

        Assert.Equal(AnalysisGoldens.AllFourMutatingFlagsOnAnalysisPrompt, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 4. Versioned decklist (item 1) + single-slash collection-miss fallback (item 2), combined.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task VersionedDecklistWithPossibleIncludesAndSingleSlashFallback_MatchesGolden()
    {
        var service = PacketByteIdentityFixtures.CreateAnalysisService(
            moxfieldDeckImporter: new StaticMoxfieldDeckImporter(PacketByteIdentityFixtures.VersionedDecklistWithSingleSlashMissEntries()),
            flagCache: PacketByteIdentityFixtures.AllAnalysisFlagsOff());

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            WorkflowStep = 2,
            DeckSource = VersionedDeckUrl,
            Format = "Commander",
            TargetCommanderBracket = "Upgraded",
            TargetAiPlatform = PacketByteIdentityFixtures.ChatGpt,
            IncludeCardVersions = true,
            IncludeCandidateReferencesInAnalysis = true,
            SelectedAnalysisQuestions = ["bracket-2-version"],
        });

        // H1: commander/mainboard lines get the " (SET) COLLECTOR" suffix and DFC-slash truncation;
        // the DFC card's resolved oracle name (double-slash) differs from the submitted (single-slash)
        // name, so the "[printed as: ...]" annotation fires. Possible-Includes (Swords to Plowshares)
        // stays PLAIN even though IncludeCardVersions=true (no suffix, no slash truncation).
        Assert.Contains("1 Blex, Vexing Pest (TSR) 96 [printed as: Blex, Vexing Pest / Search for Blex]", result.AnalysisPromptText, StringComparison.Ordinal);
        Assert.Contains("1 Swords to Plowshares", result.AnalysisPromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("Swords to Plowshares (", result.AnalysisPromptText, StringComparison.Ordinal);
        Assert.Equal(AnalysisGoldens.VersionedDecklistAnalysisPrompt, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.AnalysisPromptText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 5. Whitespace-bearing request fields (H3) — locks the EXACT current collapse behavior.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task WhitespaceRequestFields_MatchesGolden()
    {
        var service = PacketByteIdentityFixtures.CreateAnalysisService(
            moxfieldDeckImporter: new StaticMoxfieldDeckImporter(PacketByteIdentityFixtures.BaselineEntries()),
            flagCache: PacketByteIdentityFixtures.AllAnalysisFlagsOff());

        var result = await service.BuildAsync(new DeckAnalysisRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            WorkflowStep = 2,
            DeckSource = DeckUrl,
            Format = "Commander",
            TargetCommanderBracket = "Upgraded",
            TargetAiPlatform = PacketByteIdentityFixtures.ChatGpt,
            SelectedAnalysisQuestions = ["strengths-weaknesses"],
            DeckName = PacketByteIdentityFixtures.WhitespaceDeckName,
            StrategyNotes = PacketByteIdentityFixtures.WhitespaceStrategyNotes,
            MetaNotes = PacketByteIdentityFixtures.WhitespaceMetaNotes,
        });

        Assert.Equal(AnalysisGoldens.WhitespaceRequestContextText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.RequestContextText), StringComparer.Ordinal);
    }

    private static async Task<DeckAnalysisPacketResult> BuildBaselineAsync(string platform, IFeatureFlagCache flagCache)
    {
        var service = PacketByteIdentityFixtures.CreateAnalysisService(
            moxfieldDeckImporter: new StaticMoxfieldDeckImporter(PacketByteIdentityFixtures.BaselineEntries()),
            flagCache: flagCache);

        return await service.BuildAsync(new DeckAnalysisRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            WorkflowStep = 2,
            DeckSource = DeckUrl,
            Format = "Commander",
            TargetCommanderBracket = "Upgraded",
            TargetAiPlatform = platform,
            SelectedAnalysisQuestions = ["strengths-weaknesses"],
        });
    }

    private sealed class StaticMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        private readonly List<DeckEntry> _entries;

        public StaticMoxfieldDeckImporter(List<DeckEntry> entries) => _entries = entries;

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.Select(Clone).ToList());

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MoxfieldImportResult(
                ImportAsync(urlOrDeckId, cancellationToken).GetAwaiter().GetResult(),
                MoxfieldImportSource.Direct,
                DetectedCompanionName: null));

        private static DeckEntry Clone(DeckEntry entry)
            => PacketByteIdentityFixtures.CreateDeckEntry(entry.Name, entry.Quantity, entry.Board, entry.SetCode, entry.CollectorNumber, entry.Category);
    }
}
