using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers per-platform deck-primer prompt rendering behaviors.
/// </summary>
public sealed class PrimerPromptVariantTests
{
    private static readonly DeckPrimerRequest SampleRequest = new()
    {
        Format = "Commander",
        DeckName = "Atraxa Primer",
        TargetCommanderBracket = "cEDH",
        SelectedSectionIds = PrimerSectionCatalog.AllSections.Select(section => section.Id).ToList()
    };

    private static readonly CategoryDistributionSummary SampleCategoryDistribution = new(12, 10, 4, 11);
    private static readonly IReadOnlyList<PrimerSectionEntry> AllSections = PrimerSectionCatalog.AllSections;
    private static readonly IReadOnlyList<EdhTop16Entry> SampleTop16Entries =
    [
        new() { PlayerName = "Kraum / Tymna", TournamentName = "WUBR" },
        new() { PlayerName = "Kinnan", TournamentName = "UG" },
        new() { PlayerName = "Sisay", TournamentName = "WUBRG" }
    ];
    private static readonly CommanderSpellbookResult SampleComboResult = new(
        [
            new SpellbookCombo(
                ["Thassa's Oracle", "Demonic Consultation"],
                ["Win the game"],
                "Resolve Thassa's Oracle, then cast Demonic Consultation naming a card not in your deck."),
            new SpellbookCombo(
                ["Isochron Scepter", "Dramatic Reversal", "Sol Ring"],
                ["Infinite mana", "Infinite untaps"],
                "Imprint Dramatic Reversal, activate Isochron Scepter, and loop your mana rocks.")
        ],
        [
            new SpellbookAlmostCombo(
                "Tainted Pact",
                ["Thassa's Oracle"],
                ["Win the game"],
                "Cast Thassa's Oracle, then resolve Tainted Pact with no duplicates remaining.")
        ]);

    public static TheoryData<string> Platforms => new()
    {
        "ChatGPT",
        "Claude",
        "Gemini"
    };

    [Theory]
    [MemberData(nameof(Platforms))]
    public void AllVariants_EmitTwoComboBlocks(string platform)
    {
        var prompt = BuildPrompt(platform, SampleComboResult, SampleTop16Entries, bracketNumber: 5);

        Assert.Contains("Known Combos (ground truth — do not speculate)", prompt, StringComparison.Ordinal);
        Assert.Contains("Speculative Synergies", prompt, StringComparison.Ordinal);
        Assert.Contains("Near-Combos (one card away)", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void AllVariants_NullCombos_EmitDisclosure(string platform)
    {
        var prompt = BuildPrompt(platform, comboResult: null, SampleTop16Entries, bracketNumber: 5);

        Assert.Contains("No verified combos available — treat all synergies as speculative.", prompt, StringComparison.Ordinal);
        Assert.Contains("Commander Spellbook API was unreachable at generation time.", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void Bracket5_WithEntries_EmitsNamedArchetypes(string platform)
    {
        var prompt = BuildPrompt(platform, SampleComboResult, SampleTop16Entries, bracketNumber: 5);

        Assert.Contains("Kraum / Tymna", prompt, StringComparison.Ordinal);
        Assert.Contains("Kinnan", prompt, StringComparison.Ordinal);
        Assert.Contains("Sisay", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void Bracket5_NullEntries_EmitsGenericBuckets(string platform)
    {
        var prompt = BuildPrompt(platform, SampleComboResult, top16Entries: null, bracketNumber: 5);

        AssertGenericBuckets(prompt);
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void NonCedh_EmitsGenericBuckets(string platform)
    {
        var prompt = BuildPrompt(platform, SampleComboResult, SampleTop16Entries, bracketNumber: 3);

        AssertGenericBuckets(prompt);
        Assert.DoesNotContain("Kraum / Tymna", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Kinnan", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Sisay", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Gemini_OverCap_TrimsWithDisclosure()
    {
        var prompt = BuildPrompt(
            "Gemini",
            CreateOversizedComboResult(),
            SampleTop16Entries,
            bracketNumber: 5,
            decklistText: CreateOversizedDecklist());

        Assert.Contains("Sections omitted due to Gemini paste limit", prompt, StringComparison.Ordinal);
        Assert.True(prompt.Length <= 32000, $"Expected Gemini prompt <= 32000 chars but was {prompt.Length}.");
        Assert.Contains("Known Combos (ground truth — do not speculate)", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatGpt_And_Claude_NoCap_DoNotTrim()
    {
        var comboResult = CreateOversizedComboResult();
        var decklistText = CreateOversizedDecklist();

        var chatGptPrompt = BuildPrompt("ChatGPT", comboResult, SampleTop16Entries, bracketNumber: 5, decklistText);
        var claudePrompt = BuildPrompt("Claude", comboResult, SampleTop16Entries, bracketNumber: 5, decklistText);

        Assert.DoesNotContain("Sections omitted due to Gemini paste limit", chatGptPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Sections omitted due to Gemini paste limit", claudePrompt, StringComparison.Ordinal);
        Assert.True(chatGptPrompt.Length > 32000, $"Expected ChatGPT prompt > 32000 chars but was {chatGptPrompt.Length}.");
        Assert.True(claudePrompt.Length > 32000, $"Expected Claude prompt > 32000 chars but was {claudePrompt.Length}.");
    }

    [Fact]
    public void ChatGpt_UsesSequentialDirectiveNumbers_ForNonContiguousSelection()
    {
        var selectedSections = SelectSections(
            "archetype-and-table-role",
            "win-conditions-overview",
            "verified-combos");

        var prompt = BuildPrompt("ChatGPT", selectedSections, SampleComboResult, SampleTop16Entries, bracketNumber: 5);

        Assert.Contains("### 1. Archetype and Table Role", prompt, StringComparison.Ordinal);
        Assert.Contains("### 2. Win Conditions Overview", prompt, StringComparison.Ordinal);
        Assert.Contains("### 3. Verified Combos", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("### 8. Verified Combos", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Claude_UsesSequentialDirectiveNumbers_ForNonContiguousSelection()
    {
        var selectedSections = SelectSections(
            "archetype-and-table-role",
            "win-conditions-overview",
            "verified-combos");

        var prompt = BuildPrompt("Claude", selectedSections, SampleComboResult, SampleTop16Entries, bracketNumber: 5);

        Assert.Contains("1. Archetype and Table Role", prompt, StringComparison.Ordinal);
        Assert.Contains("2. Win Conditions Overview", prompt, StringComparison.Ordinal);
        Assert.Contains("3. Verified Combos", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("8. Verified Combos", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Gemini_UsesSequentialDirectiveNumbers_AcrossRenderedGroups()
    {
        var selectedSections = SelectSections(
            "archetype-and-table-role",
            "game-plan-by-phase",
            "budget-cut-ladder");

        var prompt = BuildPrompt("Gemini", selectedSections, SampleComboResult, SampleTop16Entries, bracketNumber: 5);

        Assert.Contains("1. Archetype and Table Role —", prompt, StringComparison.Ordinal);
        Assert.Contains("2. Game Plan by Phase —", prompt, StringComparison.Ordinal);
        Assert.Contains("3. Budget Cut Ladder —", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("14. Game Plan by Phase —", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("27. Budget Cut Ladder —", prompt, StringComparison.Ordinal);
    }

    private static string BuildPrompt(
        string platform,
        IReadOnlyList<PrimerSectionEntry> selectedSections,
        CommanderSpellbookResult? comboResult,
        IReadOnlyList<EdhTop16Entry>? top16Entries,
        int bracketNumber,
        string? decklistText = null)
    {
        IPrimerPromptVariant variant = platform switch
        {
            "ChatGPT" => new ChatGptPrimerPromptVariant(),
            "Claude" => new ClaudePrimerPromptVariant(),
            "Gemini" => new GeminiPrimerPromptVariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };

        var request = new DeckPrimerRequest
        {
            Format = SampleRequest.Format,
            DeckName = SampleRequest.DeckName,
            TargetCommanderBracket = SampleRequest.TargetCommanderBracket,
            SelectedSectionIds = selectedSections.Select(section => section.Id).ToList()
        };

        return variant.Build(
            request,
            decklistText ?? CreateDecklist(),
            selectedSections,
            comboResult,
            top16Entries,
            SampleCategoryDistribution,
            bracketNumber);
    }

    private static string BuildPrompt(
        string platform,
        CommanderSpellbookResult? comboResult,
        IReadOnlyList<EdhTop16Entry>? top16Entries,
        int bracketNumber,
        string? decklistText = null)
    {
        return BuildPrompt(platform, AllSections, comboResult, top16Entries, bracketNumber, decklistText);
    }

    private static void AssertGenericBuckets(string prompt)
    {
        Assert.Contains("Aggro", prompt, StringComparison.Ordinal);
        Assert.Contains("Control", prompt, StringComparison.Ordinal);
        Assert.Contains("Midrange", prompt, StringComparison.Ordinal);
        Assert.Contains("Combo", prompt, StringComparison.Ordinal);
        Assert.Contains("Stax/Hate", prompt, StringComparison.Ordinal);
    }

    private static CommanderSpellbookResult CreateOversizedComboResult()
    {
        var included = Enumerable.Range(1, 20)
            .Select(index => new SpellbookCombo(
                [$"Combo Card {index}A", $"Combo Card {index}B", $"Combo Card {index}C"],
                ["Win the game", $"Infinite mana line {index}"],
                string.Join(" ", Enumerable.Repeat($"Instruction block {index} describing sequencing, setup, protection, and contingency planning.", 8))))
            .ToList();

        var nearCombos = Enumerable.Range(1, 15)
            .Select(index => new SpellbookAlmostCombo(
                $"Missing Piece {index}",
                [$"Have Piece {index}A", $"Have Piece {index}B"],
                [$"Near result {index}"],
                string.Join(" ", Enumerable.Repeat($"Near-combo note {index} with tutor priorities and timing context.", 6))))
            .ToList();

        return new CommanderSpellbookResult(included, nearCombos);
    }

    private static string CreateDecklist()
        => """
            1 Atraxa, Praetors' Voice
            1 Sol Ring
            1 Arcane Signet
            1 Demonic Consultation
            1 Thassa's Oracle
            1 Force of Will
            """;

    private static string CreateOversizedDecklist()
        => string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 300).Select(index => $"1 Oversized Card {index} {new string('X', 80)}"));

    private static IReadOnlyList<PrimerSectionEntry> SelectSections(params string[] ids)
        => AllSections.Where(section => ids.Contains(section.Id, StringComparer.Ordinal)).ToList();
}
