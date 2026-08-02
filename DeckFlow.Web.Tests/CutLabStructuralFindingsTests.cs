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
    public void Compute_EnablerStarved_GroupsVariantsSharingTheSameInDeckCardSet()
    {
        IReadOnlyList<SpellbookAlmostCombo> nearCombos =
        [
            new("Mikaeus, the Unhallowed", ["Ashnod's Altar", "Putrid Goblin"], ["Infinite mana"], "Assemble all three."),
            new("Melira, Sylvok Outcast", ["Ashnod's Altar", "Putrid Goblin"], ["Infinite mana"], "Assemble all three."),
            new("Vizier of Remedies", ["Ashnod's Altar", "Putrid Goblin"], ["Infinite mana"], "Assemble all three."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            nearCombos,
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        CutLabFinding finding = Assert.Single(result.Findings, candidate => candidate.Kind == CutLabFindingKind.EnablerStarved);
        Assert.Equal("Enabler-starved cards", finding.Heading);
        Assert.Equal(
            "Ashnod's Altar and Putrid Goblin are missing their combo partners: Melira, Sylvok Outcast, Mikaeus, the Unhallowed and Vizier of Remedies.",
            finding.Lead);
    }

    [Fact]
    public void Compute_EnablerStarved_KeepsDistinctInDeckCardSetsAsSeparateFindings()
    {
        IReadOnlyList<SpellbookAlmostCombo> nearCombos =
        [
            new("Thassa's Oracle", ["Demonic Consultation", "Tainted Pact"], ["Win the game"], "Cast both."),
            new("Mikaeus, the Unhallowed", ["Ashnod's Altar", "Putrid Goblin"], ["Infinite mana"], "Assemble all three."),
        ];

        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            Array.Empty<CutLabAnalyzedCard>(),
            nearCombos,
            Floors(),
            comboDataAvailable: true,
            categoryDataAvailable: true);

        Assert.Equal(2, result.Findings.Count(candidate => candidate.Kind == CutLabFindingKind.EnablerStarved));
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

    [Fact]
    public void Compute_NewAnalyzedCardMembers_DoNotAffectExistingDetectors()
    {
        // Why: This is the D-15 scope fence in executable form. If a future change makes any of the
        // five existing detectors read these members, this test fails; EnablerStarved is excluded
        // because it does not consume the analyzed pool.
        IReadOnlyList<CutLabAnalyzedCard> CreatePool(bool populateNewMembers) =>
        [
            Card("Wincon 1", 3, false, roles: ["wincons"], categories: ["landfall"], typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Wincon 2", 3, false, roles: ["wincons"], categories: ["landfall"], typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Wincon 3", 3, false, roles: ["wincons"], typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Interaction 1", 3, false, roles: ["interaction-targeted"], typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Interaction 2", 3, false, roles: ["interaction-targeted"], typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Combo One", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Combo Two", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Filler 1", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Filler 2", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Filler 3", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Filler 4", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
            Card("Filler 5", 3, false, typeLine: populateNewMembers ? "Artifact" : "", isLocked: populateNewMembers, isCommander: populateNewMembers),
        ];

        IReadOnlyList<SpellbookCombo> completeCombos =
        [
            new(["Combo One", "Combo Two"], ["Win"], "Assemble both."),
        ];
        CutLabStructuralFindingsResult baseline = CutLabStructuralFindings.Compute(
            CreatePool(populateNewMembers: false),
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("interaction-targeted", 2)),
            comboDataAvailable: true,
            categoryDataAvailable: true,
            completeCombos: completeCombos);
        CutLabStructuralFindingsResult populated = CutLabStructuralFindings.Compute(
            CreatePool(populateNewMembers: true),
            Array.Empty<SpellbookAlmostCombo>(),
            Floors(("interaction-targeted", 2)),
            comboDataAvailable: true,
            categoryDataAvailable: true,
            completeCombos: completeCombos);

        CutLabFindingKind[] expectedKinds =
        [
            CutLabFindingKind.CurveCongestion,
            CutLabFindingKind.StrandedSubtheme,
            CutLabFindingKind.RedundantFinishers,
            CutLabFindingKind.WeakFloorCase,
            CutLabFindingKind.ComboProtected,
        ];
        foreach (CutLabFindingKind kind in expectedKinds)
        {
            Assert.Contains(baseline.Findings, finding => finding.Kind == kind);
        }

        Assert.Equal(baseline.Findings.Select(finding => finding.Kind), populated.Findings.Select(finding => finding.Kind));
        Assert.Equal(baseline.Findings.Select(finding => finding.Heading), populated.Findings.Select(finding => finding.Heading));
        Assert.Equal(baseline.Findings.Select(finding => finding.Lead), populated.Findings.Select(finding => finding.Lead));
        Assert.Equal(
            baseline.Findings.Select(finding => finding.Evidence.Select(evidence => evidence.CardName)),
            populated.Findings.Select(finding => finding.Evidence.Select(evidence => evidence.CardName)));
    }

    [Fact]
    public void Compute_ThreeCardsShareRoleManaValueAndType_RaisesFunctionalTwins()
    {
        CutLabFinding finding = Assert.Single(Twins([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("C", 2, "Artifact", "ramp")]).Findings);
        Assert.Equal(CutLabFindingKind.FunctionalTwins, finding.Kind);
        Assert.Equal("Functional twins", finding.Heading);
        Assert.Equal(["A", "B", "C"], finding.Evidence.Select(evidence => evidence.CardName));
        Assert.Contains("Ramp", finding.Lead, StringComparison.Ordinal);
        Assert.Contains("artifact", finding.Lead, StringComparison.Ordinal);
        Assert.Contains("mana value 2", finding.Lead, StringComparison.Ordinal);
    }

    [Fact]
    public void Compute_TwoCardsOnly_DoesNotRaiseFunctionalTwins()
        => Assert.Empty(Twins([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp")]).Findings);

    [Fact]
    public void Compute_SameRoleAndTypeButDifferentManaValues_DoNotGroup()
    {
        // Why: Sol Ring at mana value 1 must never group with a Mox at mana value 0; CurveCongestion's ["0-1", ...] bucket would group all three.
        Assert.Empty(Twins([Twin("Mox", 0, "Artifact", "ramp"), Twin("Sol Ring", 1, "Artifact", "ramp"), Twin("Mana Vault", 1, "Artifact", "ramp")]).Findings);
    }

    [Fact]
    public void Compute_SameRoleAndManaValueButDifferentTypes_DoNotGroup()
        => Assert.Empty(Twins([Twin("Artifact", 2, "Artifact", "ramp"), Twin("Creature", 2, "Creature", "ramp"), Twin("Enchantment", 2, "Enchantment", "ramp")]).Findings);

    [Fact]
    public void Compute_SameManaValueAndTypeButDifferentRoles_DoNotGroup()
        => Assert.Empty(Twins([Twin("Ramp", 2, "Artifact", "ramp"), Twin("Draw", 2, "Artifact", "draw"), Twin("Engine", 2, "Artifact", "engines")]).Findings);

    [Fact]
    public void Compute_ArtifactCreatureGroupsAsCreature()
    {
        CutLabFinding finding = Assert.Single(Twins([Twin("Golem", 3, "Artifact Creature \u2014 Golem", "payoffs"), Twin("Elf", 3, "Creature \u2014 Elf", "payoffs"), Twin("Human", 3, "Legendary Creature \u2014 Human", "payoffs")]).Findings);
        Assert.Contains("creature", finding.Lead, StringComparison.Ordinal);
    }

    [Fact]
    public void Compute_LandsRole_NeverRaisesFunctionalTwins()
    {
        // Why: Success Criterion 5 requires a real 130-card pool's thirty-plus lands not to form one group larger than every other finding combined.
        Assert.Empty(Twins(Enumerable.Range(1, 5).Select(index => Twin($"Land {index}", 0, "Land", "lands", isLand: true)).ToArray()).Findings);
    }

    [Fact]
    public void Compute_LandsInAnEligibleNonLandRole_AreStillExcluded()
        => Assert.Empty(Twins(Enumerable.Range(1, 3).Select(index => Twin($"Draw Land {index}", 2, "Artifact", "draw", isLand: true)).ToArray()).Findings);

    [Fact]
    public void Compute_BlankTypeLine_IsIneligible()
        => Assert.Empty(Twins([Twin("A", 2, "", "ramp"), Twin("B", 2, "", "ramp"), Twin("C", 2, "", "ramp")]).Findings);

    [Fact]
    public void Compute_ThreeCopiesOfOneCard_DoesNotRaiseFunctionalTwins()
        => Assert.Empty(Twins([Twin("One Card", 2, "Artifact", "ramp", quantity: 3)]).Findings);

    [Fact]
    public void Compute_ThreeSeparateEntriesOfTheSameCard_DoesNotRaiseFunctionalTwins()
        => Assert.Empty(Twins([Twin("Malakir Rebirth // Malakir Mire", 2, "Artifact", "ramp"), Twin("malakir rebirth", 2, "Artifact", "ramp"), Twin("Malakir Rebirth", 2, "Artifact", "ramp")]).Findings);

    [Fact]
    public void Compute_DuplicateEntriesDoNotInflateEvidence()
    {
        CutLabFinding finding = Assert.Single(Twins([Twin("Alpha", 2, "Artifact", "ramp"), Twin("alpha", 2, "Artifact", "ramp"), Twin("Beta", 2, "Artifact", "ramp"), Twin("Gamma", 2, "Artifact", "ramp")]).Findings);
        Assert.Equal(["Alpha", "Beta", "Gamma"], finding.Evidence.Select(evidence => evidence.CardName));
    }

    [Fact]
    public void Compute_EvidenceWithinAGroup_IsOrderedByNameAscending()
    {
        CutLabFinding finding = Assert.Single(Twins([Twin("Zulu", 2, "Artifact", "ramp"), Twin("Alpha", 2, "Artifact", "ramp"), Twin("Mike", 2, "Artifact", "ramp")]).Findings);
        Assert.Equal(["Alpha", "Mike", "Zulu"], finding.Evidence.Select(evidence => evidence.CardName));
    }

    [Fact]
    public void Compute_MultipleTwinGroups_AreEmittedHighestManaValueFirst()
    {
        // Why: This is where TWIN-03's intent lives under D-14, because within-group descending mana value is degenerate.
        CutLabStructuralFindingsResult result = Twins([Twin("Low A", 2, "Artifact", "ramp"), Twin("Low B", 2, "Artifact", "ramp"), Twin("Low C", 2, "Artifact", "ramp"), Twin("High A", 5, "Creature", "draw"), Twin("High B", 5, "Creature", "draw"), Twin("High C", 5, "Creature", "draw")]);
        Assert.Equal(["3 creature cards fill your Card draw slot at mana value 5 \u2014 they compete with each other, so the pool likely only needs some of them.", "3 artifact cards fill your Ramp slot at mana value 2 \u2014 they compete with each other, so the pool likely only needs some of them."], result.Findings.Select(finding => finding.Lead));
    }

    [Fact]
    public void Compute_TwinFindings_AreOrderStableUnderInputPermutation()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = [Twin("Enchant Z", 4, "Enchantment", "ramp"), Twin("Enchant A", 4, "Enchantment", "ramp"), Twin("Enchant M", 4, "Enchantment", "ramp"), Twin("Artifact Z", 4, "Artifact", "ramp"), Twin("Artifact A", 4, "Artifact", "ramp"), Twin("Artifact M", 4, "Artifact", "ramp"), Twin("Creature Z", 4, "Creature", "ramp"), Twin("Creature A", 4, "Creature", "ramp"), Twin("Creature M", 4, "Creature", "ramp")];
        CutLabStructuralFindingsResult first = Twins(pool);
        CutLabStructuralFindingsResult second = Twins(pool.Reverse().ToArray());
        string[] expectedLeads = ["3 creature cards fill your Ramp slot at mana value 4 \u2014 they compete with each other, so the pool likely only needs some of them.", "3 artifact cards fill your Ramp slot at mana value 4 \u2014 they compete with each other, so the pool likely only needs some of them.", "3 enchantment cards fill your Ramp slot at mana value 4 \u2014 they compete with each other, so the pool likely only needs some of them."];
        Assert.Equal(expectedLeads, first.Findings.Select(finding => finding.Lead));
        Assert.Equal(expectedLeads, second.Findings.Select(finding => finding.Lead));
        Assert.Equal(first.Findings.Select(finding => finding.Evidence.Select(evidence => evidence.CardName)), second.Findings.Select(finding => finding.Evidence.Select(evidence => evidence.CardName)));
    }

    [Fact]
    public void Compute_LockedCards_AreExcludedFromTwinGroups()
    {
        Assert.Empty(Twins([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("Locked C", 2, "Artifact", "ramp", isLocked: true), Twin("Locked D", 2, "Artifact", "ramp", isLocked: true)]).Findings);
        CutLabFinding finding = Assert.Single(Twins([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("C", 2, "Artifact", "ramp"), Twin("D", 2, "Artifact", "ramp"), Twin("Locked", 2, "Artifact", "ramp", isLocked: true)]).Findings);
        Assert.DoesNotContain(finding.Evidence, evidence => evidence.CardName == "Locked");
    }

    [Fact]
    public void Compute_CommanderIsExcludedFromTwinGroups()
    {
        Assert.Empty(Twins([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("Commander C", 2, "Artifact", "ramp", isCommander: true), Twin("Commander D", 2, "Artifact", "ramp", isCommander: true)]).Findings);
        CutLabFinding finding = Assert.Single(Twins([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("C", 2, "Artifact", "ramp"), Twin("D", 2, "Artifact", "ramp"), Twin("Commander", 2, "Artifact", "ramp", isCommander: true)]).Findings);
        Assert.DoesNotContain(finding.Evidence, evidence => evidence.CardName == "Commander");
    }

    [Fact]
    public void Compute_ComboProtectedCardStillAppearsInTwinGroup()
    {
        // Why: This fails if someone adds a combo-membership filter to the twins detector.
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute([Twin("Combo Card", 2, "Artifact", "ramp"), Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp")], Array.Empty<SpellbookAlmostCombo>(), Floors(), comboDataAvailable: true, categoryDataAvailable: false, completeCombos: [new SpellbookCombo(["Combo Card"], ["Win"], "Do it.")], twinsEnabled: true);
        CutLabFinding twins = Assert.Single(result.Findings, finding => finding.Kind == CutLabFindingKind.FunctionalTwins);
        Assert.Contains(twins.Evidence, evidence => evidence.CardName == "Combo Card");
        Assert.Contains(result.Findings, finding => finding.Kind == CutLabFindingKind.ComboProtected && finding.Evidence.Any(evidence => evidence.CardName == "Combo Card"));
    }

    [Fact]
    public void Compute_TwinsDisabled_ProducesNoFunctionalTwinsFinding()
    {
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("C", 2, "Artifact", "ramp")], Array.Empty<SpellbookAlmostCombo>(), Floors(), comboDataAvailable: false, categoryDataAvailable: false, twinsEnabled: false);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Compute_TwinsDisabledByDefault_ProducesNoFunctionalTwinsFinding()
    {
        // Why: Omitting twinsEnabled proves an unwired call site cannot ship the detector.
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute([Twin("A", 2, "Artifact", "ramp"), Twin("B", 2, "Artifact", "ramp"), Twin("C", 2, "Artifact", "ramp")], Array.Empty<SpellbookAlmostCombo>(), Floors(), comboDataAvailable: false, categoryDataAvailable: false);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Compute_TwinsEnabled_LeavesExistingFindingsUnchanged()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = Enumerable.Range(1, 12).Select(index => Twin($"Twin {index}", 2, index <= 3 ? "Artifact" : "", "ramp")).ToArray();
        CutLabStructuralFindingsResult disabled = CutLabStructuralFindings.Compute(pool, Array.Empty<SpellbookAlmostCombo>(), Floors(), comboDataAvailable: false, categoryDataAvailable: false, twinsEnabled: false);
        CutLabStructuralFindingsResult enabled = CutLabStructuralFindings.Compute(pool, Array.Empty<SpellbookAlmostCombo>(), Floors(), comboDataAvailable: false, categoryDataAvailable: false, twinsEnabled: true);
        Assert.Equal(disabled.Findings.Select(finding => (finding.Kind, finding.Heading, finding.Lead, EvidenceNames: string.Join("\u001F", finding.Evidence.Select(evidence => evidence.CardName)))), enabled.Findings.Where(finding => finding.Kind != CutLabFindingKind.FunctionalTwins).Select(finding => (finding.Kind, finding.Heading, finding.Lead, EvidenceNames: string.Join("\u001F", finding.Evidence.Select(evidence => evidence.CardName)))));
    }

    private static CutLabStructuralFindingsResult Twins(IReadOnlyList<CutLabAnalyzedCard> pool)
        => CutLabStructuralFindings.Compute(pool, Array.Empty<SpellbookAlmostCombo>(), Floors(), comboDataAvailable: false, categoryDataAvailable: false, twinsEnabled: true);

    private static CutLabAnalyzedCard Twin(string name, double manaValue, string typeLine, string role, int quantity = 1, bool isLand = false, bool isLocked = false, bool isCommander = false)
        => Card(name, manaValue, isLand, quantity, [role], typeLine: typeLine, isLocked: isLocked, isCommander: isCommander);

    private static CutLabAnalyzedCard Card(
        string name,
        double manaValue,
        bool isLand,
        int quantity = 1,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? categories = null,
        string typeLine = "",
        bool isLocked = false,
        bool isCommander = false)
        => new(
            name,
            manaValue,
            isLand,
            roles ?? Array.Empty<string>(),
            categories ?? Array.Empty<string>())
        {
            Quantity = quantity,
            TypeLine = typeLine,
            IsLocked = isLocked,
            IsCommander = isCommander,
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
