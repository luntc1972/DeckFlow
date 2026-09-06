using DeckFlow.Web.Models.CutLab;
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

    // Why (WR-08): IsCompleteProfile previously required Power AND Toughness to be non-empty
    // unconditionally, which permanently excluded every non-creature (instant, sorcery, artifact,
    // enchantment, planeswalker) since Scryfall returns null P/T for them -- the class most likely to
    // contain true functional reprints in a Commander pool. Pins that a non-creature profile with
    // empty Power/Toughness is now treated as complete and can still surface a finding.
    [Fact]
    public void ComputeProvenEquivalence_NonCreatureWithNoPowerToughness_ProducesDisclosureOnlyFinding()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("Synthetic Ramp Rite", "sr-a", "{1}{G}", "Sorcery", "", "", [], ["G"], "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."),
            Card("Synthetic Verdant Rite", "sr-b", "{1}{G}", "Sorcery", "", "", [], ["G"], "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."));

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.ProvenEquivalence, finding.Kind);
        Assert.Equal(["Synthetic Ramp Rite", "Synthetic Verdant Rite"], finding.Evidence.Select(evidence => evidence.CardName));
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

    // Why: CR-01 -- the generic incomplete-profile Theory above pairs every abstain row against a
    // fixed partner whose mana cost/Oracle text already differ for an unrelated reason, so it never
    // actually exercises the {X}-cost or alternative-cost guards. These two Facts give both cards a
    // byte-identical profile apart from Oracle ID/name, so the guard is the only thing preventing a
    // positive; deleting either guard line in IsCompleteProfile makes the matching test go red.
    [Fact]
    public void ComputeProvenEquivalence_XCostManaCost_AbstainsEvenWithByteIdenticalProfile()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("Hangarback Walker", "hw-1", "{X}{X}", "Artifact Creature — Construct", "0", "0", [], [], "This creature enters with X +1/+1 counters on it."),
            Card("Hangarback Clone", "hw-2", "{X}{X}", "Artifact Creature — Construct", "0", "0", [], [], "This creature enters with X +1/+1 counters on it."));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ComputeProvenEquivalence_AlternativeCostOracleText_AbstainsEvenWithByteIdenticalProfile()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("Security Rhox", "sr-1", "{2}{R}{G}", "Creature — Rhino Warrior", "5", "4", [], ["G", "R"], "You may pay {R}{G} rather than pay this spell's mana cost."),
            Card("Rhox Clone", "sr-2", "{2}{R}{G}", "Creature — Rhino Warrior", "5", "4", [], ["G", "R"], "You may pay {R}{G} rather than pay this spell's mana cost."));

        Assert.Empty(result.Findings);
    }

    // Why: CR-02 -- names must not be substrings of their own Oracle text (unlike the old "A"/"B" vs
    // "A has haste."/"B has haste." pair), or a buggy self-name redaction could make the two profiles
    // diverge for the wrong reason and mask whether the same-OracleId guard below ever executes.
    // "Ramp Dork Prime" appears nowhere in the shared Oracle text, so with correct redaction (or no
    // redaction at all) both cards land on an identical SemanticKey, making this a non-vacuous pin of
    // the guard: deleting the OracleId-uniqueness filter in ComputeProvenEquivalence turns this red.
    [Fact]
    public void ComputeProvenEquivalence_SameOracleId_Abstains()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("Ramp Dork Prime", "same", "{1}{G}", "Creature — Elf Shaman", "2", "2", ["Reach"], ["G"], "When this creature enters, add {G}{G}."),
            Card("Ramp Dork Prime (Alt Name)", "same", "{1}{G}", "Creature — Elf Shaman", "2", "2", ["Reach"], ["G"], "When this creature enters, add {G}{G}."));

        Assert.Empty(result.Findings);
    }

    // Why (WR-09): a shared Oracle ID between two members of an otherwise-valid group previously
    // suppressed the WHOLE group; this pins the fix over a four-card group where exactly two share an
    // Oracle ID -- the ambiguous pair drops out, but the two genuinely distinct members that satisfy
    // D-02 still surface (rather than the whole four-card group vanishing).
    [Fact]
    public void ComputeProvenEquivalence_OneAmbiguousPairInLargerGroup_SurfacesOnlyTheDistinctMembers()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("Ramp Dork Alpha", "shared-alias", "{1}{G}", "Creature — Elf Shaman", "2", "2", ["Reach"], ["G"], "When this creature enters, add {G}{G}."),
            Card("Ramp Dork Alpha (Alt Name)", "shared-alias", "{1}{G}", "Creature — Elf Shaman", "2", "2", ["Reach"], ["G"], "When this creature enters, add {G}{G}."),
            Card("Ramp Dork Beta", "distinct-oracle-1", "{1}{G}", "Creature — Elf Shaman", "2", "2", ["Reach"], ["G"], "When this creature enters, add {G}{G}."),
            Card("Ramp Dork Gamma", "distinct-oracle-2", "{1}{G}", "Creature — Elf Shaman", "2", "2", ["Reach"], ["G"], "When this creature enters, add {G}{G}."));

        CutLabFinding finding = Assert.Single(result.Findings);
        Assert.Equal(CutLabFindingKind.ProvenEquivalence, finding.Kind);
        Assert.Equal(["Ramp Dork Beta", "Ramp Dork Gamma"], finding.Evidence.Select(evidence => evidence.CardName));
    }

    [Fact]
    public void ComputeProvenEquivalence_MatchingRoleManaValueAndTypeButDifferentFingerprint_Abstains()
    {
        CutLabStructuralFindingsResult result = Compute(
            Card("A", "a1", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "A has haste."),
            Card("B", "b2", "{R}", "Creature — Goblin", "1", "1", ["Haste"], ["R"], "B has menace."));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ComputeProvenEquivalence_DefaultParameterOmitted_StaysOff()
    {
        // Why: the call site omits provenEquivalenceEnabled entirely (no dark-launch gate wired to
        // this pool at all), proving the C# default parameter value itself -- not just a caller that
        // happens to pass false -- keeps the feature dark. Uses a pool that WOULD produce a finding
        // if the parameter defaulted true, so this is non-vacuous.
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(ElfFamilyPool(), [], Floors(), true, true);

        Assert.DoesNotContain(result.Findings, finding => finding.Kind == CutLabFindingKind.ProvenEquivalence);
    }

    [Fact]
    public void BuildQueue_ProvenEquivalenceEnabledOrDisabled_ProducesIdenticalTallyQueueAndNextProposal()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = ElfFamilyPool();
        CutLabStructuralFindingsResult enabledFindings = CutLabStructuralFindings.Compute(pool, [], Floors(), true, true, provenEquivalenceEnabled: true);
        CutLabStructuralFindingsResult disabledFindings = CutLabStructuralFindings.Compute(pool, [], Floors(), true, true, provenEquivalenceEnabled: false);

        // Why: prove the pair is non-vacuous -- ON must actually surface the disclosure finding that
        // OFF omits, otherwise an identical-output assertion below would pass for the wrong reason.
        Assert.Contains(enabledFindings.Findings, finding => finding.Kind == CutLabFindingKind.ProvenEquivalence);
        Assert.DoesNotContain(disabledFindings.Findings, finding => finding.Kind == CutLabFindingKind.ProvenEquivalence);

        IReadOnlyList<CutLabRoundInputCard> workingList = WorkingListFor(pool);
        CutLabRoundPlan enabledPlan = CutLabCutRoundEngine.BuildQueue(workingList, enabledFindings, [], cardsToCutTarget: 1);
        CutLabRoundPlan disabledPlan = CutLabCutRoundEngine.BuildQueue(workingList, disabledFindings, [], cardsToCutTarget: 1);

        // Why: CutLabRoundQueueItem carries a DiscriminatingFindingKinds array, whose default record
        // equality falls back to array reference equality (mirrors CutLabFunctionalTwinsFlagTests'
        // own Flatten rationale) -- flatten to strings before comparing so this asserts on VALUES.
        Assert.Equal(
            disabledPlan.Queue.Select(Flatten).ToArray(),
            enabledPlan.Queue.Select(Flatten).ToArray());
        Assert.Equal(
            disabledPlan.NextProposal is null ? null : Flatten(disabledPlan.NextProposal),
            enabledPlan.NextProposal is null ? null : Flatten(enabledPlan.NextProposal));
        Assert.Equal(disabledPlan.CardsRemainingToTarget, enabledPlan.CardsRemainingToTarget);
    }

    // Why: three real, independently printed one-mana Elf Druid mana dorks with distinct Oracle IDs
    // and a byte-identical canonical fingerprint -- the same corpus family committed in
    // proven-equivalence-cases.json, reused here so the ON/OFF comparison is non-vacuous.
    private static IReadOnlyList<CutLabAnalyzedCard> ElfFamilyPool() =>
    [
        Card("Llanowar Elves", "68954295-54e3-4303-a6bc-fc4547a4e3a3", "{G}", "Creature — Elf Druid", "1", "1", [], ["G"], "{T}: Add {G}."),
        Card("Elvish Mystic", "3f3b2c10-21f8-4e13-be83-4ef3fa36e123", "{G}", "Creature — Elf Druid", "1", "1", [], ["G"], "{T}: Add {G}."),
        Card("Fyndhorn Elves", "df317532-7d36-40fd-938f-e972749c8792", "{G}", "Creature — Elf Druid", "1", "1", [], ["G"], "{T}: Add {G}."),
    ];

    private static IReadOnlyList<CutLabRoundInputCard> WorkingListFor(IReadOnlyList<CutLabAnalyzedCard> pool) =>
        pool.Select(card => new CutLabRoundInputCard(
            card.Name,
            Quantity: 1,
            TypeLine: card.SemanticProfile?.TypeLine ?? string.Empty,
            IsCommander: false,
            IsLocked: false,
            ManaValue: card.ManaValue,
            IsLand: false,
            Roles: card.Roles,
            Categories: card.Categories)).ToArray();

    private static string Flatten(CutLabRoundQueueItem item) => string.Join(
        '|',
        item.CardName,
        item.RoundKey,
        item.RoundLabel,
        item.FindingCount,
        string.Join(',', item.DiscriminatingFindingKinds));

    private static CutLabStructuralFindingsResult Compute(params CutLabAnalyzedCard[] cards) =>
        CutLabStructuralFindings.Compute(cards, [], Floors(), true, true, provenEquivalenceEnabled: true);

    private static CutLabAnalyzedCard Card(string name, string oracleId, string manaCost, string typeLine, string power, string toughness, IReadOnlyList<string> keywords, IReadOnlyList<string> colorIdentity, string oracleText, string layout = "normal") =>
        new(name, 1, false, ["ramp"], [])
        {
            // Why: OracleName mirrors name here because these hand-built profiles have no separate
            // user-typed decklist string to diverge from; production wires it from the resolved
            // Scryfall Oracle name (see CutLabAnalysisContextBuilder), never the pool entry's raw text.
            SemanticProfile = new CutLabSemanticProfile(oracleId, manaCost, typeLine, power, toughness, keywords, colorIdentity, oracleText, layout, OracleName: name),
        };

    private static IReadOnlyDictionary<string, int> Floors() => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
