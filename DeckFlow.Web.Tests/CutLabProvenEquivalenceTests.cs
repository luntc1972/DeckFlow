using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for fail-closed deterministic proven-equivalence evidence.</summary>
public sealed class CutLabProvenEquivalenceTests
{
    [Fact]
    public void ComputeProvenEquivalence_DistinctCompleteFunctionalReprints_ProducesDisclosureOnlyFinding()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("Llanowar Elves", "a1", "{G}", "Creature — Elf Druid", "1", "1", [], ["G"], "{T}: Add {G}."),
            Card("Elvish Mystic", "b2", "{G}", "Creature — Elf Druid", "1", "1", [], ["G"], "{T}: Add {G}."));

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.ProvenEquivalence, finding.Kind);
        Assert.Equal(["Elvish Mystic", "Llanowar Elves"], finding.Evidence.Select(evidence => evidence.CardName));
    }

    [Theory]
    [InlineData("", "a1", "normal", "{R}", "Creature — Goblin", "1", "1", "Haste")]
    [InlineData("A", "", "normal", "{R}", "Creature — Goblin", "1", "1", "Haste")]
    [InlineData("A", "a1", "transform", "{R}", "Creature — Goblin", "1", "1", "Haste")]
    [InlineData("A", "a1", "normal", "{X}{R}", "Creature — Goblin", "1", "1", "Haste")]
    [InlineData("A", "a1", "normal", "{R}", "", "1", "1", "Haste")]
    [InlineData("A", "a1", "normal", "{R}", "Creature — Goblin", "", "1", "Haste")]
    public void ComputeProvenEquivalence_IncompleteOrUncertainProfile_Abstains(
        string name, string oracleId, string layout, string manaCost, string typeLine, string power, string toughness, string oracleText)
    {
        CutLabStructuralFindingsResult result = Compute(
            Card(name, oracleId, manaCost, typeLine, power, toughness, ["Haste"], ["R"], oracleText, layout),
            Card("B", "b2", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "B has haste."));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ComputeProvenEquivalence_SameOracleId_Abstains()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("A", "same", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "A has haste."),
            Card("B", "same", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "B has haste."));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ComputeProvenEquivalence_MatchingRoleManaValueAndTypeButDifferentFingerprint_Abstains()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("A", "a1", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "A has haste."),
            Card("B", "b2", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "B has menace."));

        Assert.Empty(result.Findings);
    }

    private static CutLabStructuralFindingsResult Compute(params CutLabAnalyzedCard[] cards) =>
        CutLabStructuralFindings.Compute(cards, [], Floors(), true, true, provenEquivalenceEnabled: true);

    private static CutLabAnalyzedCard Card(string name, string oracleId, string manaCost, string typeLine, string power, string toughness, IReadOnlyList<string> keywords, IReadOnlyList<string> colorIdentity, string oracleText, string layout = "normal") =>
        new(name, 1, false, ["ramp"], [])
        {
            SemanticProfile = new CutLabSemanticProfile(oracleId, manaCost, typeLine, power, toughness, keywords, colorIdentity, oracleText, layout),
        };

    private static IReadOnlyDictionary<string, int> Floors() => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
