using System.Text.Json;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests.Manabase;

/// <summary>
/// Regression guard for the health-band/castability coupling fix (debug session
/// manabase-health-band-coupling, Gate C). The Avatar (Sokka/Aang Jeskai) fixture is the
/// calibration deck: with the flag OFF the band is "Solid" (no color issue fires because
/// White's Karsten source count is generous); with the flag ON White counts as an issue
/// because Suki, Courageous Rescuer is color:White-limited below the 80% threshold AND
/// ColorLimitedUnderSupportedCount >= 1 (Gate C condition).
///
/// The test reads a committed Avatar facts fixture directly — no HTTP — so it runs in CI
/// without any network dependency.
/// </summary>
public sealed class ManabaseHealthBandRegressionTests
{
    // Path to the committed Avatar facts fixture (copied next to the test assembly via the
    // csproj Content item, so it is present in CI with no network dependency). Re-derives
    // ManaAmount from OracleText on load (same as the baseline harness) to survive older
    // cache formats.
    private static readonly string FactsCachePath =
        Path.Combine(AppContext.BaseDirectory, "Manabase", "avatar-facts.json");

    [Fact]
    public async Task Avatar_FlagOff_BandIsSolid()
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync();
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandCastability: false);

        string label = ManabaseDisplay.HealthLabel(report.Health);
        Assert.Equal("Solid", label);
    }

    [Fact]
    public async Task Avatar_FlagOn_BandIsWorkable_WeakestColorWhite()
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync();
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandCastability: true);

        string label = ManabaseDisplay.HealthLabel(report.Health);
        Assert.Equal("Workable", label);

        // Weakest color must be White (the sim-identified tight color).
        ColorSourceFinding? weakest = report.ColorFindings.Count > 0 ? report.ColorFindings[0] : null;
        Assert.NotNull(weakest);
        Assert.Equal(ManaColor.White, weakest!.Color);
    }

    private static async Task<IReadOnlyList<CardFact>> LoadFactsAsync()
    {
        Assert.True(File.Exists(FactsCachePath),
            $"Avatar facts cache not found at {FactsCachePath}. Run the baseline harness once to populate it.");

        List<CardFact> facts = JsonSerializer.Deserialize<List<CardFact>>(
            await File.ReadAllTextAsync(FactsCachePath))!;

        // Re-derive ManaAmount from oracle text: older caches may predate this field.
        return facts.Select(f => f with { ManaAmount = ManaProductionAmount.Parse(f.OracleText) }).ToList();
    }
}
