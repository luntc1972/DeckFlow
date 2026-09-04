using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabStrandedOffPlanPackageTests
{
    [Fact]
    public void Threshold_ThreeCards_NoFinding()
    {
        Assert.Empty(Find(3));
    }

    [Fact]
    public void Threshold_FourCards_OneFinding()
    {
        CutLabFinding finding = Assert.Single(Find(4));
        Assert.Equal(CutLabFindingKind.StrandedOffPlanPackage, finding.Kind);
        Assert.Equal(4, finding.Evidence.Count);
    }

    [Fact]
    public void Threshold_FiveCards_CountIsFive()
    {
        Assert.Equal(5, Assert.Single(Find(5)).Evidence.Count);
    }

    [Fact]
    public void OnPlanCard_ExcludedFromStrandedCount()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = Cards(5);
        Dictionary<string, CutLabPlanAffinity> affinities = Affinities(pool);
        affinities[CutLabCardNames.Normalize("Card 5")] = new(["Tokens"], [new("tokens", "Tokens")], [], 1);

        CutLabFinding finding = Assert.Single(Find(pool, affinities));

        Assert.Equal(4, finding.Evidence.Count);
        Assert.DoesNotContain(finding.Evidence, evidence => evidence.CardName == "Card 5");
    }

    [Fact]
    public void Lead_PhrasedAgainstSelection()
    {
        Assert.Equal("4 cards support Tokens — not in your plan.", Assert.Single(Find(4)).Lead);
    }

    [Fact]
    public void TwoUncheckedThemes_ProduceTwoFindings_OrderedDeterministically()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = Cards(4).Concat(Cards(4, "Zombie")).ToArray();
        Dictionary<string, CutLabPlanAffinity> affinities = Affinities(pool);
        foreach (string cardName in pool.Skip(4).Select(card => card.Name))
        {
            affinities[CutLabCardNames.Normalize(cardName)] = new([], [new("zombies", "Zombies")], [], 0);
        }

        CutLabFinding[] findings = Find(pool, affinities).ToArray();

        Assert.Equal(["Tokens", "Zombies"], findings.Select(finding => finding.Lead.Split(" support ")[1].Split(' ')[0]));
    }

    [Fact]
    public void OffPlanThemesWithSameDisplayNameAndDifferentSlugs_ProduceSeparateFindings()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = Cards(4, "First").Concat(Cards(4, "Second")).ToArray();
        Dictionary<string, CutLabPlanAffinity> affinities = pool
            .Take(4)
            .ToDictionary(card => CutLabCardNames.Normalize(card.Name), _ => new CutLabPlanAffinity([], [new("theme-one", "Shared Theme")], [], 0));
        foreach (CutLabAnalyzedCard card in pool.Skip(4))
        {
            affinities[CutLabCardNames.Normalize(card.Name)] = new([], [new("theme-two", "Shared Theme")], [], 0);
        }

        CutLabFinding[] findings = Find(pool, affinities).ToArray();

        Assert.Equal(2, findings.Length);
        Assert.All(findings, finding => Assert.Equal(4, finding.Evidence.Count));
    }

    [Fact]
    public void NullAffinities_NoFinding()
    {
        Assert.Empty(CutLabStructuralFindings.Compute(Cards(4), [], Floors(), false, false).Findings);
    }

    [Fact]
    public void AllNeutralAffinities_NoFinding()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = Cards(4);
        Assert.Empty(Find(pool, pool.ToDictionary(card => CutLabCardNames.Normalize(card.Name), _ => CutLabPlanAffinity.Neutral)));
    }

    [Fact]
    public void Threshold_MutationGuard_BoundaryMovesWithConstant()
    {
        Assert.Single(Find(CutLabStructuralFindings.StrandedOffPlanPackageThreshold));
        Assert.Empty(Find(CutLabStructuralFindings.StrandedOffPlanPackageThreshold - 1));
    }

    [Fact]
    public void Finding_ContributesToRoundOneTally()
    {
        CutLabFinding finding = Assert.Single(Find(4));
        CutLabStructuralFindingsResult findings = new([finding, new(CutLabFindingKind.CurveCongestion, "Curve", "Curve", [new("Card 1", 1)])], false, false);
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [new("Card 1", 1, "Artifact", false, false, 1, false, [], [])],
            findings,
            [],
            1);

        Assert.Equal("round-1", Assert.Single(plan.Queue).RoundKey);
        Assert.Equal(2, Assert.Single(plan.Queue).FindingCount);
    }

    [Fact]
    public void Evidence_IsOrderedByManaValueThenCardName()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            new("Zulu", 2, false, [], []),
            new("Alpha", 2, false, [], []),
            new("High", 4, false, [], []),
            new("Low", 1, false, [], []),
        ];

        CutLabFinding finding = Assert.Single(Find(pool, Affinities(pool)));

        Assert.Equal(["High", "Alpha", "Zulu", "Low"], finding.Evidence.Select(evidence => evidence.CardName));
    }

    private static IReadOnlyList<CutLabFinding> Find(int count)
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = Cards(count);
        return Find(pool, Affinities(pool));
    }

    private static IReadOnlyList<CutLabFinding> Find(IReadOnlyList<CutLabAnalyzedCard> pool, IReadOnlyDictionary<string, CutLabPlanAffinity> affinities)
        => CutLabStructuralFindings.Compute(pool, [], Floors(), false, false, planAffinities: affinities).Findings
            .Where(finding => finding.Kind == CutLabFindingKind.StrandedOffPlanPackage).ToArray();

    private static IReadOnlyList<CutLabAnalyzedCard> Cards(int count, string prefix = "Card")
        => Enumerable.Range(1, count).Select(index => new CutLabAnalyzedCard($"{prefix} {index}", index, false, [], [])).ToArray();

    private static Dictionary<string, CutLabPlanAffinity> Affinities(IReadOnlyList<CutLabAnalyzedCard> pool)
        => pool.ToDictionary(card => CutLabCardNames.Normalize(card.Name), _ => new CutLabPlanAffinity([], [new("tokens", "Tokens")], [], 0));

    private static IReadOnlyDictionary<string, int> Floors()
        => new Dictionary<string, int>();
}
