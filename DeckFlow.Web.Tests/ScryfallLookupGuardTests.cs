using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Regression guard for the "scryfall lookup" hallucination trap. The four prompt families
/// that used to instruct the pasted-in AI to browse scryfall.com (Analysis, Comparison,
/// FollowUp, SetUpgrade) now tell it to treat unrecognized cards as unknown instead of
/// guessing. A pasted chat model cannot browse the web, so the old instruction invited
/// fabricated card text. Every platform variant (ChatGpt/Claude/Gemini) is checked because
/// ADR-0001 keeps the three variants' prose intentionally decoupled — the fix had to be
/// applied by hand to all of them and must not regress in any one.
/// </summary>
public sealed class ScryfallLookupGuardTests
{
    private static string[] AnalysisPrompts()
    {
        IAnalysisPromptVariant[] variants =
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant()
        };

        return variants.Select(v => v.Build(
            new DeckAnalysisRequest
            {
                Format = "Commander",
                DeckName = "Guard Fixture",
                TargetCommanderBracket = "cEDH"
            },
            DecklistText(),
            ReferenceText(),
            SchemaJson(),
            "Kraum, Ludevic's Opus",
            Array.Empty<string>(),
            Array.Empty<string>(),
            ComboResult(),
            includeCardVersions: false,
            enrichments: new AnalysisPromptEnrichments())).ToArray();
    }

    private static string[] ComparisonPrompts()
    {
        IComparisonPromptVariant[] variants =
        {
            new ChatGptComparisonPromptVariant(),
            new ClaudeComparisonPromptVariant(),
            new GeminiComparisonPromptVariant()
        };

        return variants.Select(v => v.Build(
            DeckSummary("Deck A", "Kraum, Ludevic's Opus"),
            DeckSummary("Deck B", "Kinnan, Bonder Prodigy"),
            DecklistText(),
            DecklistText(),
            "Complete combos: 0",
            "Complete combos: 0",
            ReferenceText(),
            SchemaJson())).ToArray();
    }

    private static string[] FollowUpPrompts()
    {
        IFollowUpPromptVariant[] variants =
        {
            new ChatGptFollowUpPromptVariant(),
            new ClaudeFollowUpPromptVariant(),
            new GeminiFollowUpPromptVariant()
        };

        return variants.Select(v => v.Build(SchemaJson())).ToArray();
    }

    private static string[] SetUpgradePrompts()
    {
        ISetUpgradePromptVariant[] variants =
        {
            new ChatGptSetUpgradePromptVariant(),
            new ClaudeSetUpgradePromptVariant(),
            new GeminiSetUpgradePromptVariant()
        };

        return variants.Select(v => v.Build(
            new DeckAnalysisRequest
            {
                Format = "Commander",
                DeckName = "Guard Fixture",
                TargetCommanderBracket = "cEDH"
            },
            DecklistText(),
            "{}",
            "Kraum, Ludevic's Opus",
            "Representative set packet line",
            Array.Empty<string>())).ToArray();
    }

    private static IEnumerable<string> AllCarryingPrompts()
        => AnalysisPrompts()
            .Concat(ComparisonPrompts())
            .Concat(FollowUpPrompts())
            .Concat(SetUpgradePrompts());

    [Fact]
    public void CarryingVariants_DoNotInstructScryfallBrowse()
    {
        foreach (var prompt in AllCarryingPrompts())
        {
            Assert.DoesNotContain("scryfall.com", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("look it up at", prompt, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CarryingVariants_InstructTreatUnknownInsteadOfGuessing()
    {
        // Assert every required clause of the guard, not just the opening phrase, so a variant
        // that dropped the anti-fabrication or exact-name wording still fails the guard.
        string[] requiredFragments =
        {
            "treat it as unknown instead of guessing",
            "Do not invent its rules text; flag it as unrecognized.",
            "match by exact name before concluding a card is unknown"
        };

        foreach (var prompt in AllCarryingPrompts())
        {
            foreach (var fragment in requiredFragments)
            {
                Assert.Contains(fragment, prompt, StringComparison.Ordinal);
            }
        }
    }

    // ---- Minimal representative fixtures (content-independent of the guarded prose) ----

    private static string DecklistText()
        => string.Join(Environment.NewLine, "1 Kraum, Ludevic's Opus", "1 Sol Ring", "1 Mana Crypt");

    private static string ReferenceText()
        => "Sol Ring: adds two colorless mana for a one-mana artifact.";

    private static string SchemaJson()
        => """
            {
              "type": "object",
              "properties": {
                "summary": { "type": "string" }
              }
            }
            """;

    private static CommanderSpellbookResult ComboResult()
        => new(
            [
                new SpellbookCombo(
                    ["Thassa's Oracle", "Demonic Consultation"],
                    ["Win the game"],
                    "Resolve Thassa's Oracle, then cast Demonic Consultation naming a card not in your deck.",
                    Popularity: 120000,
                    ManaValueNeeded: 3)
            ],
            []);

    private static DeckComparisonService.DeckComparisonDeckSummary DeckSummary(string name, string commander)
        => new(
            Name: name,
            CommanderName: commander,
            Bracket: CommanderBracketCatalog.Find("cEDH")!,
            MainboardCount: 99,
            Lands: 30,
            Creatures: 12,
            AverageManaValue: 1.8m,
            ManaCurve: new Dictionary<string, int> { ["1"] = 25, ["2"] = 30 },
            ColorIdentity: ["U", "B", "R", "W"],
            CategorySummaries: ["Ramp: 12"],
            Ramp: 12,
            Draw: 10,
            Interaction: 14,
            Wipes: 2,
            Recursion: 3,
            ClosingPower: 5,
            SharedThemes: ["Combo"],
            ComboSummaries: ["Thassa's Oracle + Demonic Consultation -> Win the game"],
            AlmostComboSummaries: [],
            IncludedComboCount: 1,
            AlmostIncludedComboCount: 0);
}
