using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabPlanReorderTests
{
    [Fact]
    public void Round1_OffPlanBeforeOnPlan_AtEqualTally()
    {
        Assert.Equal(["Off", "On"], Queue(["Off", "On"], ["Off", "On"], Affinities(("On", 1))).Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round1_LowerAffinityScoreBeforeHigher()
    {
        Assert.Equal(["One", "Three"], Queue(["One", "Three"], ["One", "Three"], Affinities(("One", 1), ("Three", 3))).Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round1_ComboPieceStillLast_EvenWhenOffPlan()
    {
        CutLabStructuralFindingsResult findings = Findings(("Combo", CutLabFindingKind.ComboProtected), ("Combo", CutLabFindingKind.CurveCongestion), ("Plan", CutLabFindingKind.CurveCongestion), ("Plan", CutLabFindingKind.StrandedSubtheme));
        Assert.Equal(["Plan", "Combo"], Queue(["Combo", "Plan"], findings, Affinities(("Plan", 3))).Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round2_OffPlanBeforeOnPlan()
    {
        CutLabStructuralFindingsResult findings = Findings(("Off", CutLabFindingKind.CurveCongestion), ("On", CutLabFindingKind.CurveCongestion));
        Assert.Equal(["Off", "On"], Queue(["Off", "On"], findings, Affinities(("On", 1))).Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round3_OffPlanBeforeOnPlan()
    {
        Assert.Equal(["Off", "On"], Queue(["Off", "On"], [], Affinities(("On", 1))).Queue.Select(item => item.CardName));
    }

    [Fact]
    public void SecondPassDeferred_OrderUnchangedByAffinity()
    {
        CutLabDecision[] decisions = [Decision("Beta", CutLabDecisionKind.Deferred, 1), Decision("Alpha", CutLabDecisionKind.Deferred, 2)];
        Assert.Equal(Queue(["Beta", "Alpha"], [], null, decisions).Queue, Queue(["Beta", "Alpha"], [], Affinities(("Alpha", 3)), decisions).Queue);
    }

    [Fact]
    public void SecondPassRejected_OrderUnchangedByAffinity()
    {
        CutLabDecision[] decisions = [Decision("Beta", CutLabDecisionKind.Rejected, 1), Decision("Alpha", CutLabDecisionKind.Rejected, 2)];
        Assert.Equal(Queue(["Beta", "Alpha"], [], null, decisions).Queue, Queue(["Beta", "Alpha"], [], Affinities(("Alpha", 3)), decisions).Queue);
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
        Assert.Equal("Missing", Queue(["Missing", "On"], ["Missing", "On"], Affinities(("On", 1))).Queue[0].CardName);
    }

    [Fact]
    public void Round1_OrderingRemainsDeterministic_WhenAffinityScoresMatch()
    {
        CutLabRoundPlan first = Queue(["Zulu", "Alpha"], ["Zulu", "Alpha"], Affinities(("Zulu", 1), ("Alpha", 1)));
        CutLabRoundPlan second = Queue(["Zulu", "Alpha"], ["Zulu", "Alpha"], Affinities(("Zulu", 1), ("Alpha", 1)));

        Assert.Equal(QueueShape(first), QueueShape(second));
        Assert.Equal(2, first.Queue.Count);
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
        CutLabStructuralFindingsResult findings = Findings(("Combo", CutLabFindingKind.ComboProtected), ("Plan", CutLabFindingKind.CurveCongestion));
        CutLabRoundPlan plan = Queue(["Combo", "Plan"], findings, Affinities(("Plan", 3)));

        Assert.Equal(["Plan", "Combo"], plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void Round1_ScoreCapDoesNotPromoteComboPieces()
    {
        CutLabStructuralFindingsResult findings = Findings(
            ("Combo", CutLabFindingKind.ComboProtected),
            ("Combo", CutLabFindingKind.CurveCongestion),
            ("Plan", CutLabFindingKind.CurveCongestion),
            ("Plan", CutLabFindingKind.StrandedSubtheme));

        CutLabRoundPlan plan = Queue(["Combo", "Plan"], findings, Affinities(("Plan", CutLabPlanAffinityResolver.OnPlanScoreCap)));

        Assert.Equal("Plan", plan.Queue[0].CardName);
        Assert.Equal("Combo", plan.Queue[1].CardName);
    }

    private static CutLabRoundPlan Queue(string[] cards, string[] twice, IReadOnlyDictionary<string, CutLabPlanAffinity>? affinities = null, IReadOnlyList<CutLabDecision>? decisions = null)
        => Queue(cards, Findings(twice.SelectMany(name => new[] { (name, CutLabFindingKind.CurveCongestion), (name, CutLabFindingKind.StrandedSubtheme) }).ToArray()), affinities, decisions);

    private static CutLabRoundPlan Queue(string[] cards, CutLabStructuralFindingsResult findings, IReadOnlyDictionary<string, CutLabPlanAffinity>? affinities = null, IReadOnlyList<CutLabDecision>? decisions = null)
        => CutLabCutRoundEngine.BuildQueue(cards.Select((name, index) => new CutLabRoundInputCard(name, 1, "Artifact", false, false, index + 1, false, [], [])).ToArray(), findings, decisions ?? [], 20, planAffinities: affinities);

    private static CutLabStructuralFindingsResult Findings(params (string Name, CutLabFindingKind Kind)[] entries)
        => new(entries.GroupBy(entry => entry.Kind).Select(group => new CutLabFinding(group.Key, group.Key.ToString(), group.Key.ToString(), group.Select(entry => new CutLabFindingEvidence(entry.Name, 1)).ToArray())).ToArray(), false, false);

    private static IReadOnlyDictionary<string, CutLabPlanAffinity> Affinities(params (string Name, int Score)[] entries)
        => entries.ToDictionary(entry => CutLabCardNames.Normalize(entry.Name), entry => new CutLabPlanAffinity([], entry.Score));

    private static CutLabDecision Decision(string cardName, CutLabDecisionKind kind, int ordinal)
        => new() { CardName = cardName, Kind = kind, Round = "r", Ordinal = ordinal };

    private static IEnumerable<(string CardName, string RoundKey, int FindingCount)> QueueShape(CutLabRoundPlan plan)
        => plan.Queue.Select(item => (item.CardName, item.RoundKey, item.FindingCount));
}
