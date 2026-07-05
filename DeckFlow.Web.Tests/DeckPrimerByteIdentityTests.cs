using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// PKTSVC-04 byte-identical regression harness for <see cref="DeckPrimerPacketService"/>, captured
/// against the CURRENT (unrefactored) service. Every assertion is <see cref="StringComparison.Ordinal"/>
/// against a golden captured from a real <see cref="DeckPrimerPacketService.BuildAsync"/> run.
///
/// Primer has ZERO Scryfall card-resolution code (confirmed by full-file read: no
/// <c>IScryfallCardResolver</c> reference anywhere in DeckPrimerPacketService.cs) — it satisfies
/// PKTSVC-02 by having no duplicate resolution path to remove, not by consuming the shared resolver
/// (see 83-RESEARCH.md Pitfall 3). Coverage:
/// 1. All 3 AI platforms via <c>PromptTextsByPlatform</c> (a single <c>BuildAsync</c> call yields all
///    enabled platforms at once — Gemini explicitly enabled here so all 3 keys are present).
/// 2. Whitespace-bearing DeckName (tab/newline/multi-space/bare CR) locking Primer's char-by-char
///    <c>CollapseWhitespace</c> (DeckPrimerPacketService.cs:554-576) — a DIFFERENT algorithm than
///    Analysis/Comparison/MetaGap's newline-only collapse, deliberately captured here.
/// 3. <c>tool.primer.stale-flag</c> ON vs OFF proves prompt bytes are unaffected — DeckPrimerPacketService
///    has NO <c>IFeatureFlagCache</c> dependency at all (confirmed by grep); the flag is read only by
///    DeckPrimerController to gate a UI banner (DeckPrimerController.cs:43), so this invariant holds by
///    construction, not by a runtime branch inside the service.
/// </summary>
public sealed class DeckPrimerByteIdentityTests
{
    // ---------------------------------------------------------------------------------------
    // 1. Baseline: all 3 enabled AI platforms via PromptTextsByPlatform.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Baseline_AllThreePlatforms_MatchGolden()
    {
        var service = PacketByteIdentityFixtures.CreatePrimerService();

        var result = await service.BuildAsync(BaselineRequest());

        Assert.Equal(["ChatGPT", "Claude", "Gemini"], result.PromptTextsByPlatform.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        foreach (var platform in PacketByteIdentityFixtures.AiPlatforms)
        {
            Assert.Equal(
                PrimerGoldens.BaselinePromptText(platform),
                PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.PromptTextsByPlatform[platform]),
                StringComparer.Ordinal);
        }

        Assert.Equal(PrimerGoldens.BaselineRequestContextText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.RequestContextText), StringComparer.Ordinal);
        Assert.Equal(PrimerGoldens.BaselineDecklistText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.DecklistText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 2. Whitespace-bearing DeckName — locks Primer's char-by-char CollapseWhitespace exactly.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task WhitespaceDeckName_MatchesGolden()
    {
        var service = PacketByteIdentityFixtures.CreatePrimerService();
        var request = BaselineRequest();
        request.DeckName = PacketByteIdentityFixtures.WhitespaceDeckName;

        var result = await service.BuildAsync(request);

        Assert.Equal(PrimerGoldens.WhitespaceRequestContextText, PacketByteIdentityFixtures.NormalizeForGoldenComparison(result.RequestContextText), StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // 3. tool.primer.stale-flag ON vs OFF: prompt bytes are byte-identical (it gates only a UI
    //    banner in DeckPrimerController, never reaches DeckPrimerPacketService.BuildAsync).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task StaleFlagOnVsOff_PromptBytesAreIdentical()
    {
        // DeckPrimerPacketService has no IFeatureFlagCache dependency at all (confirmed by grep of
        // DeckPrimerPacketService.cs) -- DeckPrimerController.cs:43 is the ONLY reader of
        // tool.primer.stale-flag, and it only decides whether to show a UI banner. Two independent
        // service instances (mirroring "flag off" and "flag on" at the controller layer, since the
        // service itself takes no flag input) must therefore produce byte-identical output.
        var serviceFlagOff = PacketByteIdentityFixtures.CreatePrimerService();
        var serviceFlagOn = PacketByteIdentityFixtures.CreatePrimerService();

        var resultFlagOff = await serviceFlagOff.BuildAsync(BaselineRequest());
        var resultFlagOn = await serviceFlagOn.BuildAsync(BaselineRequest());

        Assert.Equal(DeckPrimerPacketService.StaleFlag, "tool.primer.stale-flag", StringComparer.Ordinal);
        foreach (var platform in PacketByteIdentityFixtures.AiPlatforms)
        {
            Assert.Equal(resultFlagOff.PromptTextsByPlatform[platform], resultFlagOn.PromptTextsByPlatform[platform], StringComparer.Ordinal);
        }
    }

    private static DeckPrimerRequest BaselineRequest() => new()
    {
        DeckText = """
            Commander
            1 Kraum, Ludevic's Opus

            1 Sol Ring
            1 Arcane Signet
            1 Command Tower
            """,
        TargetCommanderBracket = "Upgraded",
        SelectedSectionIds =
        [
            "verified-combos",
            "near-combos",
            "role-count-grounding",
            "matchup-archetype-plan",
        ],
    };
}
