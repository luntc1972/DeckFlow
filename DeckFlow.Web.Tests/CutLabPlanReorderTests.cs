using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabPlanReorderTests
{
    [Fact]
    public void Round1_OffPlanBeforeOnPlan_AtEqualTally()
    {
        CutLabRoundPlan plan = Queue(["Zulu", "Alpha"], Findings(), Affinities(("Alpha", 1)), checkedThemes: true);

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round1Key, item.RoundKey));
        Assert.Equal(["Zulu", "Alpha"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round2_LowerAffinityScoreBeforeHigher()
    {
        CutLabRoundPlan plan = Queue(["Zulu", "Alpha"], ["Zulu", "Alpha"], Affinities(("Zulu", 1), ("Alpha", 3)));

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round2Key, item.RoundKey));
        // Higher mana value and alphabetical order both favor Alpha, so affinity must decide.
        Assert.Equal(["Zulu", "Alpha"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round1_ComboPieceStillLast_EvenWhenOffPlan()
    {
        CutLabStructuralFindingsResult findings = Findings(("Combo", CutLabFindingKind.ComboProtected), ("Combo", CutLabFindingKind.StrandedSubtheme), ("Plan", CutLabFindingKind.StrandedSubtheme));
        CutLabRoundPlan plan = Queue(["Combo", "Plan"], findings, Affinities(("Plan", 3)), checkedThemes: true);
        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round1Key, item.RoundKey));
        Assert.Equal(["Plan", "Combo"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round2_LowerAffinityScoreBeforeHigher_AtEqualTally()
    {
        CutLabRoundPlan plan = Queue(["Zulu", "Alpha"], Findings(("Zulu", CutLabFindingKind.StrandedSubtheme), ("Alpha", CutLabFindingKind.StrandedSubtheme)), Affinities(("Zulu", 1), ("Alpha", 2)));

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round2Key, item.RoundKey));
        Assert.Equal(["Zulu", "Alpha"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round3_LowerAffinityScoreBeforeHigher_AtEqualDelta()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [
                new CutLabRoundInputCard("Zulu", 1, "Artifact", false, false, 2, false, [], []),
                new CutLabRoundInputCard("Alpha", 1, "Artifact", false, false, 1, false, [], []),
            ],
            Findings(),
            [],
            cardsToCutTarget: 20,
            planAffinities: Affinities(("Zulu", 1), ("Alpha", 2)));

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round3Key, item.RoundKey));
        Assert.Equal(["Zulu", "Alpha"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void SecondPassDeferred_OrderUnchangedByAffinity()
    {
        CutLabDecision[] decisions = [Decision("Beta", CutLabDecisionKind.Deferred, 1), Decision("Alpha", CutLabDecisionKind.Deferred, 2)];
        Assert.Equal(Queue(["Beta", "Alpha"], [], null, decisions).Queue.Select(item => item with { PlanSignalAvailable = true }), Queue(["Beta", "Alpha"], [], Affinities(("Alpha", 3)), decisions).Queue.Select(item => item with { PlanSignalAvailable = true }));
    }

    [Fact]
    public void SecondPassRejected_OrderUnchangedByAffinity()
    {
        CutLabDecision[] decisions = [Decision("Beta", CutLabDecisionKind.Rejected, 1), Decision("Alpha", CutLabDecisionKind.Rejected, 2)];
        Assert.Equal(Queue(["Beta", "Alpha"], [], null, decisions).Queue.Select(item => item with { PlanSignalAvailable = true }), Queue(["Beta", "Alpha"], [], Affinities(("Alpha", 3)), decisions).Queue.Select(item => item with { PlanSignalAvailable = true }));
    }

    [Fact]
    public void BuildQueue_NoAffinityMap_QueueIdenticalToBaseline()
    {
        CutLabRoundPlan baseline = Queue(["A", "B", "C", "D", "E", "F", "G", "H"], ["A", "B", "C", "D"]);
        CutLabRoundPlan actual = Queue(["A", "B", "C", "D", "E", "F", "G", "H"], ["A", "B", "C", "D"], null);
        Assert.Equal(QueueShape(baseline), QueueShape(actual));
    }

    [Fact]
    public void BuildQueue_AllNeutralAffinities_QueueIdenticalToBaseline()
    {
        string[] cards = ["A", "B", "C", "D", "E", "F", "G", "H"];
        CutLabRoundPlan baseline = Queue(cards, ["A", "B", "C", "D"]);
        Assert.Equal(QueueShape(baseline), QueueShape(Queue(cards, ["A", "B", "C", "D"], cards.ToDictionary(CutLabCardNames.Normalize, _ => CutLabPlanAffinity.Neutral))));
    }

    [Fact]
    public void BuildQueue_CardMissingFromAffinityMap_RanksAsOffPlan()
    {
        CutLabStructuralFindingsResult findings = Findings(("Zulu Missing", CutLabFindingKind.StrandedSubtheme), ("Middle Zero", CutLabFindingKind.StrandedSubtheme), ("Alpha On", CutLabFindingKind.StrandedSubtheme));
        string[] cards = ["Zulu Missing", "Middle Zero", "Alpha On"];
        CutLabRoundPlan missing = Queue(cards, findings, Affinities(("Middle Zero", 0), ("Alpha On", 1)), checkedThemes: true);
        CutLabRoundPlan explicitZero = Queue(cards, findings, Affinities(("Zulu Missing", 0), ("Middle Zero", 0), ("Alpha On", 1)), checkedThemes: true);

        Assert.All(missing.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round1Key, item.RoundKey));
        AssertSameQueue(explicitZero.Queue, missing.Queue);
        // The two score-zero cards tie on affinity; mana value favors Middle Zero.
        // Alpha On wins both later keys, but its positive affinity must put it last.
        Assert.Equal(["Middle Zero", "Zulu Missing", "Alpha On"], missing.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round2_OrderingRemainsDeterministic_WhenAffinityScoresMatch()
    {
        CutLabRoundInputCard[] cards =
        [
            new("Zulu", 1, "Artifact", false, false, 2, false, [], []),
            new("Alpha", 1, "Artifact", false, false, 2, false, [], []),
        ];
        CutLabStructuralFindingsResult findings = Findings(("Zulu", CutLabFindingKind.StrandedSubtheme), ("Alpha", CutLabFindingKind.StrandedSubtheme));
        IReadOnlyDictionary<string, CutLabPlanAffinity> affinities = Affinities(("Zulu", 1), ("Alpha", 1));
        CutLabRoundPlan first = CutLabCutRoundEngine.BuildQueue(cards, findings, [], 20, planAffinities: affinities);
        CutLabRoundPlan second = CutLabCutRoundEngine.BuildQueue(cards.Reverse().ToArray(), findings, [], 20, planAffinities: affinities);

        Assert.All(first.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round2Key, item.RoundKey));
        AssertSameQueue(first.Queue, second.Queue);
        Assert.Equal(["Alpha", "Zulu"], first.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round3_MissingAffinityAndNeutralAffinityHaveSameRank()
    {
        CutLabRoundPlan missing = Queue(["Missing", "Neutral"], [], Affinities(("Neutral", 0)));
        CutLabRoundPlan neutral = Queue(["Missing", "Neutral"], [], Affinities(("Missing", 0), ("Neutral", 0)));

        Assert.Equal(QueueShape(missing), QueueShape(neutral));
    }

    [Fact]
    public void Round2_ComboProtectionDominatesPlanAffinity()
    {
        CutLabStructuralFindingsResult findings = Findings(("Combo", CutLabFindingKind.ComboProtected), ("Combo", CutLabFindingKind.StrandedSubtheme), ("Plan", CutLabFindingKind.StrandedSubtheme));
        CutLabRoundPlan plan = Queue(["Combo", "Plan"], findings, Affinities(("Combo", 1), ("Plan", 3)));

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round2Key, item.RoundKey));
        Assert.Equal(["Plan", "Combo"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round1_ScoreCapDoesNotPromoteComboPieces()
    {
        CutLabStructuralFindingsResult findings = Findings(
            ("Combo", CutLabFindingKind.ComboProtected),
            ("Combo", CutLabFindingKind.StrandedSubtheme),
            ("Plan", CutLabFindingKind.StrandedSubtheme),
            ("Plan", CutLabFindingKind.StrandedSubtheme));

        CutLabRoundPlan plan = Queue(["Combo", "Plan"], findings, Affinities(("Plan", CutLabPlanAffinityResolver.OnPlanScoreCap)), checkedThemes: true);

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round1Key, item.RoundKey));
        Assert.Equal("Plan", plan.Queue[0].CardName);
        Assert.Equal("Combo", plan.Queue[1].CardName);
    }

    private static CutLabRoundPlan Queue(string[] cards, string[] strandedCards, IReadOnlyDictionary<string, CutLabPlanAffinity>? affinities = null, IReadOnlyList<CutLabDecision>? decisions = null)
        => Queue(cards, Findings(strandedCards.Select(name => (name, CutLabFindingKind.StrandedSubtheme)).ToArray()), affinities, decisions);

    private static CutLabRoundPlan Queue(string[] cards, CutLabStructuralFindingsResult findings, IReadOnlyDictionary<string, CutLabPlanAffinity>? affinities = null, IReadOnlyList<CutLabDecision>? decisions = null, bool checkedThemes = false)
        => CutLabCutRoundEngine.BuildQueue(cards.Select((name, index) => new CutLabRoundInputCard(name, 1, "Artifact", false, false, index + 1, false, [], [])).ToArray(), findings, decisions ?? [], 20, planAffinities: affinities, checkedCommanderThemesAvailable: checkedThemes);

    private static CutLabStructuralFindingsResult Findings(params (string Name, CutLabFindingKind Kind)[] entries)
        => new(entries.GroupBy(entry => entry.Kind).Select(group => new CutLabFinding(group.Key, group.Key.ToString(), group.Key.ToString(), group.Select(entry => new CutLabFindingEvidence(entry.Name, 1, group.Key == CutLabFindingKind.ComboProtected ? ComboBadgeState.CompletePiece : default)).ToArray())).ToArray(), false, false);

    private static IReadOnlyDictionary<string, CutLabPlanAffinity> Affinities(params (string Name, int Score)[] entries)
        => entries.ToDictionary(entry => CutLabCardNames.Normalize(entry.Name), entry => new CutLabPlanAffinity([], entry.Score));

    private static CutLabDecision Decision(string cardName, CutLabDecisionKind kind, int ordinal)
        => new() { CardName = cardName, Kind = kind, Round = "r", Ordinal = ordinal };

    private static void AssertSameQueue(IReadOnlyList<CutLabRoundQueueItem> expected, IReadOnlyList<CutLabRoundQueueItem> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach ((CutLabRoundQueueItem left, CutLabRoundQueueItem right) in expected.Zip(actual))
        {
            Assert.Equal(left.DiscriminatingFindingKinds, right.DiscriminatingFindingKinds);
            Assert.Equal(left with { DiscriminatingFindingKinds = right.DiscriminatingFindingKinds }, right);
        }
    }

    private static IEnumerable<(string CardName, string RoundKey, int FindingCount)> QueueShape(CutLabRoundPlan plan)
        => plan.Queue.Select(item => (item.CardName, item.RoundKey, item.FindingCount));
}
