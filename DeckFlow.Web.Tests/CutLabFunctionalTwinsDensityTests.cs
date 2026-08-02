using DeckFlow.Core.Manabase;

using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// D-22 (04-04-PLAN.md): commits the automated half of the two-part density validation for
/// <c>FunctionalTwins</c> -- a regression-guarding numeric bound over a diverse, realistically-shaped
/// ~130-card fixture. It cannot prove "reviewable" on its own; the human check in Task 3 is the
/// authoritative Success Criterion 5 gate against a real decklist. This bound exists so a future
/// change that makes the detector fire materially more often fails loudly instead of silently.
/// </summary>
/// <remarks>
/// File-header fact disclosure (MUST DO 3 of the executing ticket). REAL Magic cards, whose mana
/// value and primary type are used here with confidence:
/// <list type="bullet">
/// <item>Nonland spells -- Swords to Plowshares (Instant, MV 1), Path to Exile (Instant, MV 1),
/// Night's Whisper (Sorcery, MV 2), Sign in Blood (Sorcery, MV 2), Smothering Tithe (Enchantment,
/// MV 4), Wrath of God (Sorcery, MV 4).</item>
/// <item>Nonbasic lands -- Command Tower, Reliquary Tower, Exotic Orchard and Ancient Tomb are all
/// real cards, and their stated facts here (Land, mana value 0) are correct.</item>
/// <item>Basic land entries use the five real basic land types ("Basic Land — Forest" and friends,
/// mana value 0) with a numeric suffix on the NAME so each entry is distinct; the type line and
/// mana value are the real facts, the suffixed name is a fixture artifact.</item>
/// </list>
/// Every OTHER card name in this file is clearly synthetic and type-consistent by construction, not
/// a guess at a real card's mana value or type line: the Signet Alpha/Beta/Gamma/Delta, Thicket
/// Golem Alpha, Grove Warden, Verdant Colossus Whelp, Kaelen, Solmir and Threnody cluster cards;
/// every near-miss card (Cataclysmic Surge, Swift Ward, Guardian's Vow, Absolute Shelter, Revenge
/// Engine, Vengeful Sentinel, Cursed Retribution); every "Filler ..." card; and every name in the
/// homogeneous control pool.
/// </remarks>
public sealed class CutLabFunctionalTwinsDensityTests
{
    // Why: CutLabEngineDeterminismTests.BuildTimingFacts (147 cards: 1 commander, 40 lands, 20
    // artifacts, 20 instants, 25 enchantments, 24 creatures, 17 sorceries) is unusable as this
    // fixture. It is homogeneous by construction -- 20 identical "Mana Rock NN" MV-2 artifacts, 20
    // identical "Interaction NN" MV-1 instants, 25 identical "Engine NN" MV-3 enchantments, and a
    // "Payoff NN" block -- so every one of its non-land blocks is already a single maximal twin
    // group. It measures the detector's worst case, not a realistic pool, and it is built at the
    // CardFact / CutLabRoleAssigner classifier level anyway, one layer above where
    // CutLabStructuralFindings.Compute reads. This file instead builds its own fixture directly at
    // the CutLabAnalyzedCard level, stating its own grouping keys (role, mana value, primary type),
    // so this test measures the detector alone -- an unrelated classifier tweak in another phase
    // cannot move these numbers.

    private const string LandsRole = "lands";
    private const string RampRole = "ramp";
    private const string DrawRole = "draw";
    private const string InteractionTargetedRole = "interaction-targeted";
    private const string InteractionMassRole = "interaction-mass";
    private const string ProtectionRole = "protection";
    private const string EnginesRole = "engines";
    private const string PayoffsRole = "payoffs";
    private const string WinconsRole = "wincons";

    private const int FillerCardCount = 64;

    private static readonly (string TypeLine, string Label)[] FillerTypeLines =
    [
        ("Artifact", "Artifact"),
        ("Creature — Golem", "Creature"),
        ("Instant", "Instant"),
        ("Sorcery", "Sorcery"),
        ("Enchantment", "Enchantment"),
        ("Legendary Planeswalker — Filler", "Planeswalker"),
    ];

    // Why: all eight twin-eligible role keys, in CutLabFloorRules.RoleKeys order minus "lands", so
    // the filler block carries every role the detector can group on rather than leaving the eight
    // role keys represented only by the two dozen designed cluster cards.
    private static readonly string[] FillerRoleCycle =
    [
        RampRole,
        DrawRole,
        InteractionTargetedRole,
        InteractionMassRole,
        ProtectionRole,
        EnginesRole,
        PayoffsRole,
        WinconsRole,
    ];

    // Why: weighted toward 1-3 (values 1, 2 and 3 occupy 12 of these 17 slots, roughly 71%) while
    // still covering 0 and 4-6+. Length 17 is prime, and therefore coprime with both the 8-entry
    // role cycle and the 6-entry type cycle, so the three grouping dimensions do not phase-lock and
    // the filler block spreads across (role, mana value, primary type) space instead of stacking.
    private static readonly double[] FillerManaValues = [0, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 6, 1, 2, 3, 1];

    [Fact]
    public void FunctionalTwins_OnDiverse130CardPool_StaysWithinReviewableBound()
    {
        IReadOnlyList<CutLabFinding> twins = ComputeTwins(BuildDiversePool());

        Assert.True(twins.Count > 0, "Expected at least one FunctionalTwins finding on a realistic diverse pool.");

        // Why: measured actual count on this fixture's six designed clusters is 6 (Ramp/Artifact/MV3,
        // Ramp/Creature/MV3, Targeted removal/Instant/MV1, Card draw/Sorcery/MV2, Engines/
        // Enchantment/MV4, Win conditions/Planeswalker/MV5). The population that bound is measured
        // over is 92 eligible cards -- every one of the fixture's 130 entries that is not one of its
        // 38 lands is role-bearing, so the detector really does have a ~130-card pool to act on here
        // and a firing-rate regression has somewhere to show up. 12 is double the measured 6 as
        // generous headroom,
        // deliberately loose so an unrelated fixture tweak elsewhere does not force an edit here. A
        // breach means the detector's firing rate changed materially and needs a human look -- it is
        // a regression signal, not a hard product limit.
        Assert.True(twins.Count <= 12, $"Expected at most 12 FunctionalTwins findings (measured baseline on this fixture: 6), but the detector produced {twins.Count}.");
    }

    [Fact]
    public void FunctionalTwins_OnDiverse130CardPool_ProducesNoLandGroups()
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = BuildDiversePool();
        HashSet<string> landNames = pool
            .Where(card => card.IsLand)
            .Select(card => card.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(landNames.Count >= 34, $"Expected at least 34 land entries in the fixture, found {landNames.Count}.");

        IReadOnlyList<CutLabFinding> twins = ComputeTwins(pool);

        foreach (CutLabFinding finding in twins)
        {
            foreach (CutLabFindingEvidence evidence in finding.Evidence)
            {
                Assert.DoesNotContain(evidence.CardName, landNames);
            }
        }
    }

    [Fact]
    public void FunctionalTwins_OnDiverse130CardPool_TotalEvidenceCardsStayBounded()
    {
        IReadOnlyList<CutLabFinding> twins = ComputeTwins(BuildDiversePool());

        int distinctEvidenceCount = twins
            .SelectMany(finding => finding.Evidence.Select(evidence => evidence.CardName))
            .Distinct(StringComparer.Ordinal)
            .Count();

        // Why: bounding distinct evidence NAMES rather than group count, because a single 40-member
        // group is worse for panel reviewability than four 3-member groups and only this assertion
        // catches that shape.
        Assert.True(distinctEvidenceCount <= 40, $"Expected at most 40 distinct evidence card names, got {distinctEvidenceCount}.");
    }

    [Fact]
    public void FunctionalTwins_OnDiverse130CardPool_IsOrderStableUnderInputPermutation()
    {
        IReadOnlyList<CutLabAnalyzedCard> original = BuildDiversePool();
        IReadOnlyList<CutLabAnalyzedCard> reversed = original.Reverse().ToArray();
        int rotateBy = original.Count / 3;
        IReadOnlyList<CutLabAnalyzedCard> rotated = original.Skip(rotateBy).Concat(original.Take(rotateBy)).ToArray();

        // Why: expected order is OrderByDescending(ManaValue).ThenBy(TypeGroupOrder index).
        // Ramp/Artifact/MV3 (Cluster A) and Ramp/Creature/MV3 (Cluster B) are the load-bearing
        // mana-value tie across two groups that share a role (ramp) but differ in primary type
        // (Artifact vs Creature): both sit at mana value 3, so without the TypeGroupOrder tiebreak
        // their relative order would be arbitrary. TypeGroupOrder lists Creature (index 0) before
        // Artifact (index 5), so Cluster B (creature) must lead Cluster A (artifact) in every run,
        // regardless of input order -- that is what this test actually proves.
        string[] expectedLeads =
        [
            "3 planeswalker cards fill your Win conditions slot at mana value 5 — they compete with each other, so the pool likely only needs some of them.",
            "3 enchantment cards fill your Engines slot at mana value 4 — they compete with each other, so the pool likely only needs some of them.",
            "3 creature cards fill your Ramp slot at mana value 3 — they compete with each other, so the pool likely only needs some of them.",
            "4 artifact cards fill your Ramp slot at mana value 3 — they compete with each other, so the pool likely only needs some of them.",
            "4 sorcery cards fill your Card draw slot at mana value 2 — they compete with each other, so the pool likely only needs some of them.",
            "3 instant cards fill your Targeted removal slot at mana value 1 — they compete with each other, so the pool likely only needs some of them.",
        ];

        string[] expectedEvidenceJoined =
        [
            string.Join("", "Kaelen, Herald of Dusk", "Solmir, Sunbound Warden", "Threnody Vex, Planeswalker of Ruin"),
            string.Join("", "Cascading Engine", "Perpetual Machine", "Smothering Tithe"),
            string.Join("", "Grove Warden", "Thicket Golem Alpha", "Verdant Colossus Whelp"),
            string.Join("", "Signet Alpha", "Signet Beta", "Signet Delta", "Signet Gamma"),
            string.Join("", "Cached Knowledge", "Night's Whisper", "Sign in Blood", "Whispered Secrets"),
            string.Join("", "Path to Exile", "Sudden Banishment", "Swords to Plowshares"),
        ];

        foreach (IReadOnlyList<CutLabAnalyzedCard> permutation in new[] { original, reversed, rotated })
        {
            IReadOnlyList<CutLabFinding> twins = ComputeTwins(permutation);

            Assert.Equal(expectedLeads, twins.Select(finding => finding.Lead));
            Assert.Equal(
                expectedEvidenceJoined,
                twins.Select(finding => string.Join("", finding.Evidence.Select(evidence => evidence.CardName))));
        }
    }

    [Fact]
    public void FunctionalTwins_OnDiverse130CardPool_WithGateOff_ProducesNothing()
    {
        IReadOnlyList<CutLabFinding> twins = ComputeTwins(BuildDiversePool(), twinsEnabled: false);

        Assert.Empty(twins);
    }

    [Fact]
    public void FunctionalTwins_OnHomogeneousPool_FiresFarMoreThanOnDiversePool()
    {
        IReadOnlyList<CutLabFinding> diverseTwins = ComputeTwins(BuildDiversePool());
        IReadOnlyList<CutLabFinding> homogeneousTwins = ComputeTwins(BuildHomogeneousControlPool());

        int diverseEvidenceCount = diverseTwins
            .SelectMany(finding => finding.Evidence.Select(evidence => evidence.CardName))
            .Distinct(StringComparer.Ordinal)
            .Count();
        int homogeneousEvidenceCount = homogeneousTwins
            .SelectMany(finding => finding.Evidence.Select(evidence => evidence.CardName))
            .Distinct(StringComparer.Ordinal)
            .Count();

        // Why: this is the control that makes test 1's bound meaningful. If the diverse and
        // homogeneous pools produced similar evidence totals, the diverse fixture would not
        // actually be diverse and test 1's upper bound would be measuring nothing.
        Assert.True(
            homogeneousEvidenceCount > diverseEvidenceCount,
            $"Expected the homogeneous control ({homogeneousEvidenceCount} evidence cards) to exceed the diverse fixture ({diverseEvidenceCount}).");
    }

    private static IReadOnlyList<CutLabFinding> ComputeTwins(IReadOnlyList<CutLabAnalyzedCard> pool, bool twinsEnabled = true)
        => CutLabStructuralFindings.Compute(
                pool,
                Array.Empty<SpellbookAlmostCombo>(),
                Floors(),
                comboDataAvailable: false,
                categoryDataAvailable: false,
                twinsEnabled: twinsEnabled)
            .Findings
            .Where(finding => finding.Kind == CutLabFindingKind.FunctionalTwins)
            .ToArray();

    private static IReadOnlyList<CutLabAnalyzedCard> BuildDiversePool()
    {
        List<CutLabAnalyzedCard> pool = [];
        pool.AddRange(BuildLands());
        pool.AddRange(BuildFiringClusters());
        pool.AddRange(BuildNearMissClusters());
        pool.AddRange(BuildFillerCards());
        return pool;
    }

    // Why: ~38 land entries -- the largest natural cluster in any real pool -- so the density number
    // measured by test 1 is meaningful. All are isLand: true and role "lands", which
    // ComputeFunctionalTwins excludes from eligibility entirely, so they must produce zero groups
    // (test 2).
    private static List<CutLabAnalyzedCard> BuildLands()
    {
        List<CutLabAnalyzedCard> lands = [];
        lands.AddRange(BasicLands("Forest", 10));
        lands.AddRange(BasicLands("Island", 8));
        lands.AddRange(BasicLands("Swamp", 8));
        lands.AddRange(BasicLands("Mountain", 4));
        lands.AddRange(BasicLands("Plains", 4));
        lands.Add(NonbasicLand("Command Tower"));
        lands.Add(NonbasicLand("Reliquary Tower"));
        lands.Add(NonbasicLand("Exotic Orchard"));
        lands.Add(NonbasicLand("Ancient Tomb"));
        return lands;
    }

    private static IEnumerable<CutLabAnalyzedCard> BasicLands(string basicName, int count)
        => Enumerable.Range(1, count)
            .Select(index => Card($"{basicName} {index}", 0, isLand: true, $"Basic Land — {basicName}", roles: [LandsRole]));

    private static CutLabAnalyzedCard NonbasicLand(string name)
        => Card(name, 0, isLand: true, "Land", roles: [LandsRole]);

    // Why: six genuine twin clusters, one per grouping shape, so the fixture measures a non-zero
    // number and would catch the detector silently ceasing to fire. Two cards here also carry a
    // second role (Signet Alpha: ramp+engines; Grove Warden: ramp+payoffs) so D-16's double-count
    // path -- a multi-role card evaluated under more than one role-loop iteration -- is exercised at
    // scale; neither forms a second firing group since no other card shares its second role's exact
    // (mana value, primary type).
    private static List<CutLabAnalyzedCard> BuildFiringClusters()
    {
        List<CutLabAnalyzedCard> cards = [];

        // Cluster A: Ramp / Artifact / mana value 3 -- fires (4 distinct cards).
        cards.Add(Card("Signet Alpha", 3, isLand: false, "Artifact", roles: [RampRole, EnginesRole]));
        cards.Add(Card("Signet Beta", 3, isLand: false, "Artifact", roles: [RampRole]));
        cards.Add(Card("Signet Gamma", 3, isLand: false, "Artifact", roles: [RampRole]));
        cards.Add(Card("Signet Delta", 3, isLand: false, "Artifact", roles: [RampRole]));

        // Cluster B: Ramp / Creature / mana value 3 -- fires (3 distinct cards; all synthetic). Same
        // role and mana value as Cluster A but a different primary type: this is the load-bearing
        // tie test 4 needs to exercise the TypeGroupOrder tiebreak (Creature sorts before Artifact).
        // Why "Thicket Golem Alpha": this entry used to be named "Bramble Elemental", which is a
        // real card at {3}{G}{G} -- mana value 5, not the 3 stated here. Correcting the mana value
        // would have destroyed the mana-value tie with Cluster A that test 4 depends on, so the
        // entry keeps its MV-3 Creature grouping key and takes a clearly synthetic name instead.
        cards.Add(Card("Thicket Golem Alpha", 3, isLand: false, "Creature — Golem", roles: [RampRole]));
        cards.Add(Card("Grove Warden", 3, isLand: false, "Creature — Treefolk", roles: [RampRole, PayoffsRole]));
        cards.Add(Card("Verdant Colossus Whelp", 3, isLand: false, "Creature — Elemental", roles: [RampRole]));

        // Cluster C: Targeted removal / Instant / mana value 1 -- fires (3 distinct cards; two real
        // staples plus one synthetic).
        cards.Add(Card("Swords to Plowshares", 1, isLand: false, "Instant", roles: [InteractionTargetedRole]));
        cards.Add(Card("Path to Exile", 1, isLand: false, "Instant", roles: [InteractionTargetedRole]));
        cards.Add(Card("Sudden Banishment", 1, isLand: false, "Instant", roles: [InteractionTargetedRole]));

        // Cluster D: Card draw / Sorcery / mana value 2 -- fires (4 distinct cards; two real staples
        // plus two synthetic).
        cards.Add(Card("Night's Whisper", 2, isLand: false, "Sorcery", roles: [DrawRole]));
        cards.Add(Card("Sign in Blood", 2, isLand: false, "Sorcery", roles: [DrawRole]));
        cards.Add(Card("Whispered Secrets", 2, isLand: false, "Sorcery", roles: [DrawRole]));
        cards.Add(Card("Cached Knowledge", 2, isLand: false, "Sorcery", roles: [DrawRole]));

        // Cluster E: Engines / Enchantment / mana value 4 -- fires (3 distinct cards; one real staple
        // plus two synthetic).
        cards.Add(Card("Smothering Tithe", 4, isLand: false, "Enchantment", roles: [EnginesRole]));
        cards.Add(Card("Perpetual Machine", 4, isLand: false, "Enchantment", roles: [EnginesRole]));
        cards.Add(Card("Cascading Engine", 4, isLand: false, "Enchantment", roles: [EnginesRole]));

        // Cluster F: Win conditions / Planeswalker / mana value 5 -- fires (3 synthetic cards).
        cards.Add(Card("Kaelen, Herald of Dusk", 5, isLand: false, "Legendary Planeswalker — Kaelen", roles: [WinconsRole]));
        cards.Add(Card("Solmir, Sunbound Warden", 5, isLand: false, "Legendary Planeswalker — Solmir", roles: [WinconsRole]));
        cards.Add(Card("Threnody Vex, Planeswalker of Ruin", 5, isLand: false, "Legendary Planeswalker — Threnody", roles: [WinconsRole]));

        return cards;
    }

    // Why: three near-miss clusters, each one dimension short of qualifying, so the density number
    // is a bound rather than a tautology. Named per MUST DO 1 of the executing ticket:
    // - Near-miss 1 (below threshold): Mass removal / Sorcery / mana value 4, only 2 distinct cards.
    // - Near-miss 2 (split across mana values): Protection / Instant, one card each at mana value 1,
    //   2 and 3, so no single (role, mana value, type) bucket ever reaches 3.
    // - Near-miss 3 (split across primary types): Payoffs / mana value 2, one card each of Artifact,
    //   Creature and Enchantment, so no single type bucket ever reaches 3.
    // These also give the "interaction-mass", "protection" and "payoffs" roles fixture presence
    // without adding another firing group.
    private static List<CutLabAnalyzedCard> BuildNearMissClusters()
    {
        List<CutLabAnalyzedCard> cards = [];

        cards.Add(Card("Wrath of God", 4, isLand: false, "Sorcery", roles: [InteractionMassRole]));
        cards.Add(Card("Cataclysmic Surge", 4, isLand: false, "Sorcery", roles: [InteractionMassRole]));

        cards.Add(Card("Swift Ward", 1, isLand: false, "Instant", roles: [ProtectionRole]));
        cards.Add(Card("Guardian's Vow", 2, isLand: false, "Instant", roles: [ProtectionRole]));
        cards.Add(Card("Absolute Shelter", 3, isLand: false, "Instant", roles: [ProtectionRole]));

        cards.Add(Card("Revenge Engine", 2, isLand: false, "Artifact", roles: [PayoffsRole]));
        cards.Add(Card("Vengeful Sentinel", 2, isLand: false, "Creature — Spirit", roles: [PayoffsRole]));
        cards.Add(Card("Cursed Retribution", 2, isLand: false, "Enchantment", roles: [PayoffsRole]));

        return cards;
    }

    // Why: 64 filler cards pad the fixture to ~130 entries, and every one of them is ROLE-BEARING.
    // An earlier revision gave them `roles: []`, which made them inert: ComputeFunctionalTwins
    // selects on `card.Card.Roles.Contains(roleKey)`, so a role-less card can never group, and with
    // the lands also excluded only 28 of the 130 entries were actually eligible. The bound in test 1
    // then claimed to measure a diverse ~130-card pool while really measuring a 28-card one, and a
    // regression that raised the detector's firing rate on ordinary role-bearing cards would have
    // had almost nothing here to act on -- exactly the regression D-22 says this file exists to
    // catch. Every non-land entry is now role-bearing; none is deliberately role-less.
    //
    // The three cycles are stepped independently -- role (period 8), primary type (period 6), mana
    // value (period 17, prime) -- so the block spreads across the detector's whole grouping space.
    // Every fifth card takes a second role so D-16's double-count path is exercised by ordinary
    // filler as well as by the two designed multi-role cluster cards.
    //
    // Grouping keys already claimed by the designed firing and near-miss clusters are SKIPPED. That
    // is not number-tuning: the six firing clusters have exact memberships that test 4 pins by name,
    // and the three near-miss clusters are only near-misses because they sit one card below the
    // threshold. A filler card landing in either bucket would silently rewrite the fixture's stated
    // design. Whatever groups the filler block forms outside those reserved keys are measured, not
    // suppressed.
    private static List<CutLabAnalyzedCard> BuildFillerCards()
    {
        HashSet<(string RoleKey, string PrimaryType, double ManaValue)> reservedKeys = DesignedGroupingKeys();
        List<CutLabAnalyzedCard> cards = [];

        for (int index = 0; cards.Count < FillerCardCount; index++)
        {
            string roleKey = FillerRoleCycle[index % FillerRoleCycle.Length];
            (string typeLine, string label) = FillerTypeLines[index % FillerTypeLines.Length];
            double manaValue = FillerManaValues[index % FillerManaValues.Length];
            string primaryType = CardTypeLine.PrimaryType(typeLine);
            if (reservedKeys.Contains((roleKey, primaryType, manaValue)))
            {
                continue;
            }

            List<string> roles = [roleKey];
            if (cards.Count % 5 == 0)
            {
                string secondRoleKey = FillerRoleCycle[(index + 3) % FillerRoleCycle.Length];
                if (!string.Equals(secondRoleKey, roleKey, StringComparison.Ordinal)
                    && !reservedKeys.Contains((secondRoleKey, primaryType, manaValue)))
                {
                    roles.Add(secondRoleKey);
                }
            }

            cards.Add(Card($"Filler {label} {cards.Count + 1:00}", manaValue, isLand: false, typeLine, roles));
        }

        return cards;
    }

    // Why: derived from the designed cards themselves rather than restated as a literal list, so a
    // later edit to a cluster cannot leave a stale copy of its grouping key behind here.
    private static HashSet<(string RoleKey, string PrimaryType, double ManaValue)> DesignedGroupingKeys()
    {
        HashSet<(string RoleKey, string PrimaryType, double ManaValue)> keys = [];
        foreach (CutLabAnalyzedCard card in BuildFiringClusters().Concat(BuildNearMissClusters()))
        {
            foreach (string roleKey in card.Roles)
            {
                keys.Add((roleKey, CardTypeLine.PrimaryType(card.TypeLine), card.ManaValue));
            }
        }

        return keys;
    }

    // Why: mirrors the documented shape of CutLabEngineDeterminismTests.BuildTimingFacts (1
    // commander, 40 lands, then homogeneous nonland blocks) at the CutLabAnalyzedCard level this
    // detector actually reads, so test 6 has a genuinely homogeneous control: every nonland block
    // below shares one (role, mana value, primary type) key by construction, so each block is a
    // single maximal twin group.
    private static IReadOnlyList<CutLabAnalyzedCard> BuildHomogeneousControlPool()
    {
        List<CutLabAnalyzedCard> pool = [];

        pool.Add(Card("Homogeneous Commander", 4, isLand: false, "Legendary Creature — Human", roles: [WinconsRole], isCommander: true));
        pool.AddRange(Enumerable.Range(1, 40)
            .Select(index => Card($"Homogeneous Land {index}", 0, isLand: true, "Land", roles: [LandsRole])));
        pool.AddRange(Enumerable.Range(1, 20)
            .Select(index => Card($"Mana Rock {index}", 2, isLand: false, "Artifact", roles: [RampRole])));
        pool.AddRange(Enumerable.Range(1, 20)
            .Select(index => Card($"Interaction Spell {index}", 1, isLand: false, "Instant", roles: [InteractionTargetedRole])));
        pool.AddRange(Enumerable.Range(1, 25)
            .Select(index => Card($"Engine Piece {index}", 3, isLand: false, "Enchantment", roles: [EnginesRole])));

        return pool;
    }

    private static CutLabAnalyzedCard Card(
        string name,
        double manaValue,
        bool isLand,
        string typeLine,
        IReadOnlyList<string>? roles = null,
        bool isCommander = false)
        => new(name, manaValue, isLand, roles ?? Array.Empty<string>(), Array.Empty<string>())
        {
            TypeLine = typeLine,
            IsCommander = isCommander,
        };

    private static IReadOnlyDictionary<string, int> Floors()
        => new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [LandsRole] = 0,
            [RampRole] = 0,
            [DrawRole] = 0,
            [InteractionTargetedRole] = 0,
            [InteractionMassRole] = 0,
            [ProtectionRole] = 0,
            [EnginesRole] = 0,
            [PayoffsRole] = 0,
            [WinconsRole] = 0,
        };
}
