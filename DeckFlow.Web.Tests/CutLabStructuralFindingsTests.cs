using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the pure structural-finding detectors used by Cut Lab phase 102.</summary>
public sealed class CutLabStructuralFindingsTests
{
    [Fact]
    public void Compute_CurveCongestion_ReportsBucketLeadAndEvidence()
    {
        List<CutLabAnalyzedCard> pool = [];
        pool.AddRange(Enumerable.Range(1, 15).Select(index => Card($"Three Drop {index}", 3, false)));
        pool.AddRange(Enumerable.Range(1, 10).Select(index => Card($"Two Drop {index}", 2, false)));
        pool.AddRange(Enumerable.Range(1, 8).Select(index => Card($"Four Drop {index}", 4, false)));
        pool.AddRange(Enumerable.Range(1, 7).Select(index => Card($"Five Drop {index}", 5, false)));

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.CurveCongestion, finding.Kind);
        Assert.Equal("Curve congestion", finding.Heading);
        Assert.Equal("15 nonland cards sit at mana value 3 — 38% of your nonland pool.", finding.Lead);
        Assert.Equal(15, finding.Evidence.Count);
        Assert.All(finding.Evidence, evidence => Assert.Equal(3, evidence.ManaValue));
    }

    [Fact]
    public void Compute_CurveCongestion_DoesNotTriggerBelowMinimumCards()
    {
        List<CutLabAnalyzedCard> pool = [];
        pool.AddRange(Enumerable.Range(1, 11).Select(index => Card($"Three Drop {index}", 3, false)));
        pool.AddRange(Enumerable.Range(1, 10).Select(index => Card($"Two Drop {index}", 2, false)));
        pool.AddRange(Enumerable.Range(1, 9).Select(index => Card($"Four Drop {index}", 4, false)));

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Compute_CurveCongestion_WeightsQuantitiesForBucketCountAndShare()
    {
        List<CutLabAnalyzedCard> pool = [];
        pool.AddRange(Enumerable.Range(1, 4).Select(index => Card($"Three Drop {index}", 3, false, quantity: 3)));
        pool.AddRange(Enumerable.Range(1, 11).Select(index => Card($"Two Drop {index}", 2, false)));
        pool.AddRange(Enumerable.Range(1, 9).Select(index => Card($"Four Drop {index}", 4, false)));
        pool.AddRange(Enumerable.Range(1, 8).Select(index => Card($"Five Drop {index}", 5, false)));

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.CurveCongestion, finding.Kind);
        Assert.Equal("12 nonland cards sit at mana value 3 — 30% of your nonland pool.", finding.Lead);
        Assert.Equal(["Three Drop 1", "Three Drop 2", "Three Drop 3", "Three Drop 4"], finding.Evidence.Select(e => e.CardName));
    }

    [Fact]
    public void Compute_StrandedSubtheme_UsesSharedClassifierVocabularyExclusion()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Felidar Retreat", 4, false, categories: ["landfall"]),
            Card("Rampaging Baloths", 6, false, categories: ["landfall"]),
            Card("Scute Swarm", 3, false, categories: ["landfall"]),
            Card("Swords to Plowshares", 1, false, categories: ["Removal"]),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.StrandedSubtheme, finding.Kind);
        Assert.Equal("Stranded subthemes", finding.Heading);
        Assert.Equal("'landfall' appears on only 3 cards — likely too few to function as a theme.", finding.Lead);
        Assert.Equal(["Felidar Retreat", "Rampaging Baloths", "Scute Swarm"], finding.Evidence.Select(e => e.CardName));
    }

    [Fact]
    public void Compute_StrandedSubtheme_IgnoresRoleVocabularyAndOutOfBandCounts()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Swords to Plowshares", 1, false, categories: ["Removal"]),
            Card("Path to Exile", 1, false, categories: ["Removal"]),
            Card("Generous Gift", 3, false, categories: ["Removal"]),
            Card("Landfall One", 2, false, categories: ["landfall"]),
            Card("Token One", 2, false, categories: ["tokens"]),
            Card("Token Two", 3, false, categories: ["tokens"]),
            Card("Token Three", 4, false, categories: ["tokens"]),
            Card("Token Four", 5, false, categories: ["tokens"]),
            Card("Token Five", 6, false, categories: ["tokens"]),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Compute_RedundantFinishers_UsesWinconFloorMargin()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Closer 1", 6, false, roles: ["wincons"]),
            Card("Closer 2", 6, false, roles: ["wincons"]),
            Card("Closer 3", 6, false, roles: ["wincons"]),
            Card("Closer 4", 6, false, roles: ["wincons"]),
            Card("Closer 5", 6, false, roles: ["wincons"]),
            Card("Closer 6", 6, false, roles: ["wincons"]),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("wincons", 3)),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.RedundantFinishers, finding.Kind);
        Assert.Equal("6 win conditions against a floor of 3 — more than one game usually needs.", finding.Lead);
    }

    [Fact]
    public void Compute_WeakFloorCase_ReportsProtectedRoleMembers()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Wipe 1", 2, false, roles: ["interaction-mass"]),
            Card("Wipe 2", 2, false, roles: ["interaction-mass"]),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("interaction-mass", 3), ("interaction-targeted", 0)),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.WeakFloorCase, finding.Kind);
        Assert.Equal("Weak floor cases", finding.Heading);
        Assert.Equal("Mass removal is at 2 against a floor of 3 — every card in this role is effectively protected already.", finding.Lead);
        Assert.Equal(2, finding.Evidence.Count);
    }

    [Fact]
    public void Compute_WeakFloorCase_ReportsZeroCountAgainstPositiveFloor()
    {
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("interaction-targeted", 7)),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.WeakFloorCase, finding.Kind);
        Assert.Equal("You have no targeted removal cards yet; the suggested floor is 7.", finding.Lead);
        Assert.Empty(finding.Evidence);
    }

    [Fact]
    public void Compute_WeakFloorCase_SkipsZeroFloorEvenWhenRoleCountIsZero()
    {
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("interaction-targeted", 0)),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Compute_EnablerStarved_UsesInDeckCardsAsPluralSubjectAndMissingCardAsPartner()
    {
        IReadOnlyList<SpellbookAlmostCombo> nearCombos =
        [
            new("Thassa's Oracle", ["Demonic Consultation", "Tainted Pact"], ["Win the game"], "Cast both."),
            new("Heliod, Sun-Crowned", ["Walking Ballista"], ["Infinite damage"], "Assemble both."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            nearCombos,
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.EnablerStarved);
        Assert.Equal(CutLabFindingKind.EnablerStarved, finding.Kind);
        Assert.Equal("Enabler-starved cards", finding.Heading);
        Assert.Equal("Demonic Consultation and Tainted Pact are missing their combo partner: Thassa's Oracle.", finding.Lead);
    }

    [Fact]
    public void Compute_ComboProtected_ReportsCompleteComboPiecesWithBadgeStateAndRoundOneAdvisory()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Heliod, Sun-Crowned", 3, false),
            Card("Walking Ballista", 4, false),
        ];
        IReadOnlyList<SpellbookCombo> completeCombos =
        [
            new(["Heliod, Sun-Crowned", "Walking Ballista"], ["Infinite damage"], "Remove a counter to loop."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true,
            completeCombos: completeCombos);

        CutLabFinding finding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.ComboProtected);
        Assert.Equal("Combo-protected cards", finding.Heading);
        Assert.Contains("round 1", finding.Lead, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["Heliod, Sun-Crowned", "Walking Ballista"], finding.Evidence.Select(evidence => evidence.CardName));
        Assert.All(finding.Evidence, evidence => Assert.Equal(ComboBadgeState.CompletePiece, evidence.BadgeState));
    }

    [Fact]
    public void Compute_ComboProtected_ReportsNearComboMissingPartnerBadgeState()
    {
        IReadOnlyList<SpellbookAlmostCombo> nearCombos =
        [
            new("Thassa's Oracle", ["Demonic Consultation", "Tainted Pact"], ["Win the game"], "Cast both."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            nearCombos,
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.ComboProtected);
        Assert.Equal(
            [ComboBadgeState.NeedsPartner, ComboBadgeState.NeedsPartner],
            finding.Evidence.Select(evidence => evidence.BadgeState));
        Assert.Equal(["Demonic Consultation", "Tainted Pact"], finding.Evidence.Select(evidence => evidence.CardName));
        Assert.Contains("Needs Thassa's Oracle", finding.Lead, StringComparison.Ordinal);
    }

    [Fact]
    public void Compute_ComboProtected_GroupsNearComboVariantsByOrderInsensitiveCardsInDeck()
    {
        IReadOnlyList<SpellbookAlmostCombo> nearCombos =
        [
            new("Missing Piece A", ["Round 1 Card", "Helper Card"], ["Win"], "Assemble both."),
            new("Missing Piece B", ["Helper Card", "Round 1 Card"], ["Win"], "Assemble both."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            nearCombos,
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.ComboProtected);
        Assert.Equal(["Round 1 Card", "Helper Card"], finding.Evidence.Select(evidence => evidence.CardName));
        Assert.Contains("Missing Piece A", finding.Lead, StringComparison.Ordinal);
        Assert.Contains("Missing Piece B", finding.Lead, StringComparison.Ordinal);
    }

    [Fact]
    public void Compute_ComboProtected_CarriesComboContextWhenWeakFloorOverlapExists()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Walking Ballista", 4, false, roles: ["interaction-targeted"]),
            Card("Heliod, Sun-Crowned", 3, false),
        ];
        IReadOnlyList<SpellbookCombo> completeCombos =
        [
            new(["Heliod, Sun-Crowned", "Walking Ballista"], ["Infinite damage"], "Remove a counter to loop."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("interaction-targeted", 1)),
            comboDataAvailable: true,
            categoryDataAvailable: true,
            completeCombos: completeCombos);

        CutLabFinding comboFinding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.ComboProtected);
        CutLabFinding weakFloorFinding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.WeakFloorCase);
        Assert.Contains("Infinite damage", comboFinding.Lead, StringComparison.Ordinal);
        Assert.Equal(["Walking Ballista"], weakFloorFinding.Evidence.Select(evidence => evidence.CardName));
    }

    [Fact]
    public void BuildQueue_ComboProtectedOnlyEvidence_DoesNotIncreaseFindingTallies()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            new("Combo Card", 1, "Artifact", false, false, 2, false, [], []),
        ];
        CutLabStructuralFindingsResult findings = new(
            [
                new(
                    CutLabFindingKind.ComboProtected,
                    "Combo-protected cards",
                    "Combo Card is a combo piece.",
                    [new CutLabFindingEvidence("Combo Card", 2, ComboBadgeState.CompletePiece)]),
            ],
            ComboDataAvailable: true,
            CategoryDataAvailable: true);

        CutLabRoundPlan roundPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            findings,
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem proposal = Assert.Single(roundPlan.Queue);
        Assert.Equal("Combo Card", proposal.CardName);
        Assert.Equal(0, proposal.FindingCount);
        Assert.Empty(proposal.DiscriminatingFindingKinds);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, proposal.RoundKey);
    }

    [Fact]
    public void Compute_ComboProtected_AndEnablerStarved_CoexistForNearComboData()
    {
        IReadOnlyList<SpellbookAlmostCombo> nearCombos =
        [
            new("Thassa's Oracle", ["Demonic Consultation", "Tainted Pact"], ["Win the game"], "Cast both."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            nearCombos,
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.ComboProtected);
        Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.EnablerStarved);
    }

    [Fact]
    public void Compute_DegradedFlags_PreserveAvailabilityWithoutConfidentDependentFindings()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Closer 1", 6, false, roles: ["wincons"]),
            Card("Closer 2", 6, false, roles: ["wincons"]),
            Card("Closer 3", 6, false, roles: ["wincons"]),
            Card("Closer 4", 6, false, roles: ["wincons"]),
            Card("Closer 5", 6, false, roles: ["wincons"]),
            Card("Closer 6", 6, false, roles: ["wincons"]),
            Card("Landfall One", 2, false, categories: ["landfall"]),
            Card("Landfall Two", 3, false, categories: ["landfall"]),
            Card("Landfall Three", 4, false, categories: ["landfall"]),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            [new SpellbookAlmostCombo("Missing Piece", ["Present Piece", "Support Piece"], ["Win"], "Assemble both.")],
            Floors(("wincons", 3)),
            comboDataAvailable: false,
            categoryDataAvailable: false);

        Assert.False(result.ComboDataAvailable);
        Assert.False(result.CategoryDataAvailable);
        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.RedundantFinishers, finding.Kind);
    }

    [Fact]
    public void Compute_HealthyPoolWithAvailableData_ReturnsNoFindings()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool =
        [
            Card("Ramp Spell", 2, false, roles: ["ramp"]),
            Card("Draw Spell", 2, false, roles: ["draw"]),
            Card("Interaction Spell", 2, false, roles: ["interaction-targeted"]),
            Card("Engine", 4, false, roles: ["engines"]),
            Card("Payoff", 5, false, roles: ["payoffs"]),
            Card("Wincon", 6, false, roles: ["wincons"]),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("ramp", 0), ("draw", 0), ("interaction-targeted", 0), ("interaction-mass", 0), ("engines", 0), ("payoffs", 0), ("wincons", 0)),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        Assert.Empty(result.Findings);
        Assert.True(result.ComboDataAvailable);
        Assert.True(result.CategoryDataAvailable);
    }

    private static CutLabAnalyzedCard Card(
        string name,
        double manaValue,
        bool isLand,
        int quantity = 1,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? categories = null)
        => new(
            name,
            manaValue,
            isLand,
            roles ?? Array.Empty<string>(),
            categories ?? Array.Empty<string>())
        {
            Quantity = quantity,
        };

    private static IReadOnlyDictionary<string, int> Floors(params (string Role, int Count)[] overrides)
    {
        Dictionary<string, int> floors = new(StringComparer.Ordinal)
        {
            ["lands"] = 0,
            ["ramp"] = 0,
            ["draw"] = 0,
            ["interaction-targeted"] = 0,
            ["interaction-mass"] = 0,
            ["protection"] = 0,
            ["engines"] = 0,
            ["payoffs"] = 0,
            ["wincons"] = 0,
        };

        foreach ((string role, int count) in overrides)
        {
            floors[role] = count;
        }

        return floors;
    }
}
