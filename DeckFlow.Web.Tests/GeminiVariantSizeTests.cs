using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using Xunit;
using Xunit.Abstractions;

namespace DeckFlow.Web.Tests;

/// <summary>
/// FEAT-01 (Phase 54) Gemini paste-limit verification. Measures the char count of the
/// Gemini prompt produced by each of the four workflows that emit a Gemini variant —
/// deck analysis, deck comparison, cEDH meta-gap, and Deck Primer — for a single
/// representative ~100-card cEDH fixture, and emits a WITHIN/OVER label against the
/// conservative 30,000-char Gemini paste ceiling.
///
/// DISPOSITION (LOCKED — recorded-measurement, not hard-fail): each fact asserts ONLY
/// that Build produced usable output; the size verdict is RECORDED in 54-VERIFICATION.md,
/// not enforced as a red assertion, so the suite gate stays deterministic-green while the
/// sizes are surfaced (CONTEXT.md "surface, don't trim"). No production code is changed and
/// no trimming is added. The char count uses string.Length (NOT UTF-8 byte count) — the
/// Gemini paste limit is specified in characters (Pitfall 3).
///
/// Routing of analysis/comparison/meta-gap to the Gemini variant under
/// TargetAiPlatform=Gemini is already proven by AiPlatformPhase10RoundTripTests; this test
/// measures the routed/built Gemini variant directly rather than duplicating that routing
/// coverage. The Primer fact measures the prompt through the flag-on DeckPrimerPacketService
/// fan-out (geminiEnabled: true) and asserts the enabled-platform set includes Gemini, which
/// is the only workflow whose Gemini emission is gated by the flag fan-out
/// (GetEnabledPlatforms, DeckPrimerPacketService.cs:512-518).
/// </summary>
public sealed class GeminiVariantSizeTests
{
    private const int GeminiPasteCeiling = 30000;

    private readonly ITestOutputHelper _output;

    /// <summary>Creates the test fixture with an xUnit output sink for emitting measurements.</summary>
    public GeminiVariantSizeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeminiAnalysis_RepresentativeDeck_MeasuresPromptSize()
    {
        var variant = new GeminiAnalysisPromptVariant();

        var prompt = variant.Build(
            new DeckAnalysisRequest
            {
                Format = "Commander",
                DeckName = "Kraum / Tymna cEDH Primer",
                TargetCommanderBracket = "cEDH"
            },
            RepresentativeDecklistText(),
            RepresentativeReferenceText(),
            RepresentativeSchemaJson(),
            "Kraum, Ludevic's Opus",
            Array.Empty<string>(),
            Array.Empty<string>(),
            RepresentativeComboResult(),
            includeCardVersions: false);

        EmitMeasurement("analysis", prompt.Length);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void GeminiComparison_RepresentativeDeck_MeasuresPromptSize()
    {
        var variant = new GeminiComparisonPromptVariant();

        var prompt = variant.Build(
            RepresentativeDeckSummary("Kraum / Tymna", "Kraum, Ludevic's Opus"),
            RepresentativeDeckSummary("Kinnan Combo", "Kinnan, Bonder Prodigy"),
            RepresentativeDecklistText(),
            RepresentativeDecklistText(),
            RepresentativeComboArtifactText(),
            RepresentativeComboArtifactText(),
            RepresentativeReferenceText(),
            RepresentativeSchemaJson());

        EmitMeasurement("comparison", prompt.Length);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void GeminiMetaGap_RepresentativeDeck_MeasuresPromptSize()
    {
        var variant = new GeminiMetaGapPromptVariant();

        var selectedEntries = new[]
        {
            RepresentativeTop16Entry("Alice", 1, "Spring Cup"),
            RepresentativeTop16Entry("Bob", 2, "Spring Cup"),
            RepresentativeTop16Entry("Carol", 3, "Spring Cup")
        };
        var referenceCombos = new CommanderSpellbookResult?[]
        {
            RepresentativeComboResult(),
            RepresentativeComboResult(),
            RepresentativeComboResult()
        };

        var prompt = variant.Build(
            "Kraum, Ludevic's Opus",
            RepresentativeDeckEntries(),
            RepresentativeComboResult(),
            selectedEntries,
            referenceCombos,
            new Dictionary<string, string>(),
            RepresentativeSchemaJson());

        EmitMeasurement("meta-gap", prompt.Length);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public async Task GeminiPrimer_FlagOnFanOut_MeasuresPromptSize()
    {
        var service = CreatePrimerService(geminiEnabled: true);

        var result = await service.BuildAsync(new DeckPrimerRequest
        {
            DeckText = RepresentativeDecklistText(),
            TargetCommanderBracket = "cEDH",
            SelectedSectionIds =
            [
                "verified-combos",
                "near-combos",
                "role-count-grounding",
                "matchup-archetype-plan"
            ]
        });

        // Prove the flag-flipped path: with geminiEnabled:true the fan-out must emit a
        // Gemini prompt (GetEnabledPlatforms includes Gemini). This is the only workflow
        // whose Gemini emission is gated by the flag fan-out.
        Assert.Contains("Gemini", result.PromptTextsByPlatform.Keys);

        var prompt = result.PromptTextsByPlatform["Gemini"];
        EmitMeasurement("primer", prompt.Length);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    // Char count, NOT UTF-8 byte count (Pitfall 3) — the Gemini paste limit is in characters.
    // Recorded-measurement disposition (LOCKED): emits a WITHIN/OVER label; the ≤30,000 verdict
    // is recorded in 54-VERIFICATION.md, never asserted as a red test.
    private void EmitMeasurement(string workflow, int promptLength)
    {
        _output.WriteLine($"{workflow}: {promptLength} chars — {(promptLength <= GeminiPasteCeiling ? "WITHIN" : "OVER")} {GeminiPasteCeiling}");
    }

    private static DeckPrimerPacketService CreatePrimerService(bool geminiEnabled)
    {
        var variants = new IPrimerPromptVariant[]
        {
            new ChatGptPrimerPromptVariant(),
            new ClaudePrimerPromptVariant(),
            new GeminiPrimerPromptVariant()
        };

        return new DeckPrimerPacketService(
            new PrimerPromptVariantRegistry(variants),
            new PacketSessionCache(),
            loadDeckEntriesAsyncOverride: (_, _) => Task.FromResult(RepresentativeDeckEntries().ToList()),
            findCombosAsyncOverride: (_, _) => Task.FromResult<CommanderSpellbookResult?>(RepresentativeComboResult()),
            getTopArchetypesAsyncOverride: (_, _) => Task.FromResult<IReadOnlyList<EdhTop16Entry>>(
            [
                RepresentativeTop16Entry("Alice", 1, "Spring Cup"),
                RepresentativeTop16Entry("Bob", 2, "Spring Cup")
            ]),
            getCategoryRowsForCommanderAsyncOverride: (_, _) => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(
            [
                new CategoryKnowledgeRow("Ramp", "Sol Ring", 1),
                new CategoryKnowledgeRow("Card Draw", "Mystic Remora", 1),
                new CategoryKnowledgeRow("Tutor", "Demonic Tutor", 1),
                new CategoryKnowledgeRow("Interaction", "Force of Will", 1)
            ]),
            geminiEnabled: geminiEnabled);
    }

    // ---- Representative ~100-card cEDH fixtures (shared across the four variants) ----

    private static IReadOnlyList<string> RepresentativeCardNames()
    {
        var cards = new List<string>
        {
            "Kraum, Ludevic's Opus",
            "Tymna the Weaver",
            "Sol Ring",
            "Mana Crypt",
            "Mana Vault",
            "Chrome Mox",
            "Mox Diamond",
            "Lotus Petal",
            "Arcane Signet",
            "Dramatic Reversal",
            "Isochron Scepter",
            "Thassa's Oracle",
            "Demonic Consultation",
            "Tainted Pact",
            "Force of Will",
            "Force of Negation",
            "Fierce Guardianship",
            "Mystic Remora",
            "Rhystic Study",
            "Cyclonic Rift"
        };

        // Pad to a realistic ~100-card mainboard with deterministic filler card names so the
        // generated prompt has representative bulk without depending on a live decklist.
        for (var i = cards.Count; i < 100; i++)
        {
            cards.Add($"Representative Filler Card {i}");
        }

        return cards;
    }

    private static IReadOnlyList<DeckEntry> RepresentativeDeckEntries()
    {
        var names = RepresentativeCardNames();
        var entries = new List<DeckEntry>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            entries.Add(new DeckEntry
            {
                Name = names[i],
                NormalizedName = names[i].ToLowerInvariant(),
                Quantity = 1,
                Board = i < 2 ? "commander" : "mainboard"
            });
        }

        return entries;
    }

    private static string RepresentativeDecklistText()
        => string.Join(Environment.NewLine, RepresentativeCardNames().Select(name => $"1 {name}"));

    private static CommanderSpellbookResult RepresentativeComboResult()
        => new(
            [
                new SpellbookCombo(
                    ["Isochron Scepter", "Dramatic Reversal", "Sol Ring"],
                    ["Infinite mana", "Infinite untaps"],
                    "Imprint Dramatic Reversal on Isochron Scepter, then loop your mana rocks for infinite mana.",
                    Popularity: 90000,
                    ManaValueNeeded: 4),
                new SpellbookCombo(
                    ["Thassa's Oracle", "Demonic Consultation"],
                    ["Win the game"],
                    "Resolve Thassa's Oracle, then cast Demonic Consultation naming a card not in your deck.",
                    Popularity: 120000,
                    ManaValueNeeded: 3)
            ],
            [
                new SpellbookAlmostCombo(
                    "Tainted Pact",
                    ["Thassa's Oracle"],
                    ["Win the game"],
                    "Cast Thassa's Oracle, then resolve Tainted Pact with no duplicates remaining.")
            ]);

    private static string RepresentativeComboArtifactText()
        => string.Join(
            Environment.NewLine,
            "Complete combos: 2",
            "- Isochron Scepter + Dramatic Reversal + Sol Ring -> Infinite mana",
            "- Thassa's Oracle + Demonic Consultation -> Win the game",
            "Near-combos: 1",
            "- Missing Tainted Pact -> Win the game");

    private static DeckComparisonService.DeckComparisonDeckSummary RepresentativeDeckSummary(string name, string commander)
        => new(
            Name: name,
            CommanderName: commander,
            Bracket: CommanderBracketCatalog.Find("cEDH")!,
            MainboardCount: 99,
            Lands: 30,
            Creatures: 12,
            AverageManaValue: 1.8m,
            ManaCurve: new Dictionary<string, int> { ["0"] = 10, ["1"] = 25, ["2"] = 30, ["3"] = 20 },
            ColorIdentity: ["U", "B", "R", "W"],
            CategorySummaries: ["Ramp: 12", "Draw: 10", "Interaction: 14"],
            Ramp: 12,
            Draw: 10,
            Interaction: 14,
            Wipes: 2,
            Recursion: 3,
            ClosingPower: 5,
            SharedThemes: ["Combo", "Control"],
            ComboSummaries:
            [
                "Isochron Scepter + Dramatic Reversal + Sol Ring -> Infinite mana",
                "Thassa's Oracle + Demonic Consultation -> Win the game"
            ],
            AlmostComboSummaries: ["Missing Tainted Pact -> Win the game"],
            IncludedComboCount: 2,
            AlmostIncludedComboCount: 1);

    private static EdhTop16Entry RepresentativeTop16Entry(string player, int standing, string tournament)
        => new()
        {
            Standing = standing,
            PlayerName = player,
            TournamentName = tournament,
            TournamentId = $"tc-{standing}",
            TournamentSize = 64,
            TournamentDate = new DateOnly(2026, 4, standing),
            DecklistUrl = $"https://example.com/{standing}",
            MainDeck = RepresentativeCardNames()
                .Select(name => new EdhTop16Card { Name = name, Type = "Spell" })
                .ToList()
        };

    private static string RepresentativeReferenceText()
        => string.Join(
            Environment.NewLine,
            RepresentativeCardNames().Select(name => $"{name}: an evidence reference line describing the card's role, cost, and interactions for grounding."));

    private static string RepresentativeSchemaJson()
        => """
            {
              "type": "object",
              "properties": {
                "game_plan": { "type": "string" },
                "speed": { "type": "string" },
                "estimated_win_turn": { "type": "integer" }
              }
            }
            """;
}
