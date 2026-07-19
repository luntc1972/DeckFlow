using DeckFlow.Web.Services.Manabase;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Kinds of structural findings Cut Lab can surface for an analyzed pool.</summary>
public enum CutLabFindingKind
{
    /// <summary>A mana-value bucket holds too much of the nonland pool.</summary>
    CurveCongestion,

    /// <summary>A non-role theme appears on too few cards to stand on its own.</summary>
    StrandedSubtheme,

    /// <summary>The pool carries materially more win conditions than the configured floor.</summary>
    RedundantFinishers,

    /// <summary>A role sits at or near its floor, so each member is already structurally protected.</summary>
    WeakFloorCase,

    /// <summary>A near-combo has enough in-deck pieces to surface the missing partner.</summary>
    EnablerStarved,
}

/// <summary>Per-card evidence attached to a structural finding.</summary>
/// <param name="CardName">Display card name.</param>
/// <param name="ManaValue">Card mana value when the finding needs it, otherwise null.</param>
public sealed record CutLabFindingEvidence(string CardName, double? ManaValue);

/// <summary>A single structural finding with its lead sentence and supporting evidence.</summary>
/// <param name="Kind">Finding type.</param>
/// <param name="Heading">UI heading, fixed by the Cut Lab UI specification.</param>
/// <param name="Lead">Lead sentence describing the measured issue.</param>
/// <param name="Evidence">Supporting card-level evidence.</param>
public sealed record CutLabFinding(
    CutLabFindingKind Kind,
    string Heading,
    string Lead,
    IReadOnlyList<CutLabFindingEvidence> Evidence);

/// <summary>The full structural-finding result plus source-availability flags.</summary>
/// <param name="Findings">Triggered findings in deterministic display order.</param>
/// <param name="ComboDataAvailable">Whether combo lookup succeeded for this run.</param>
/// <param name="CategoryDataAvailable">Whether category lookup succeeded for this run.</param>
public sealed record CutLabStructuralFindingsResult(
    IReadOnlyList<CutLabFinding> Findings,
    bool ComboDataAvailable,
    bool CategoryDataAvailable);

/// <summary>A Cut Lab pool card with the role and category data the detectors consume.</summary>
/// <param name="Name">Display card name.</param>
/// <param name="ManaValue">Front-face mana value used for curve bucketing.</param>
/// <param name="IsLand">Whether the front face is a land.</param>
/// <param name="Roles">Assigned structural role keys for the card.</param>
/// <param name="Categories">Crowd-sourced category tags for the card.</param>
public sealed record CutLabAnalyzedCard(
    string Name,
    double ManaValue,
    bool IsLand,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Categories);

/// <summary>
/// [ASSUMED] Computes Cut Lab's structural findings from the analyzed pool using fixed product
/// thresholds that still await explicit sign-off. The detectors are deterministic and fail open:
/// combo- and category-dependent reads surface source availability through the result flags instead
/// of fabricating a confident "no issue" when an upstream source was unavailable.
/// </summary>
public static class CutLabStructuralFindings
{
    // Why: A bucket needs a materially concentrated share before the curve read is worth surfacing.
    private const double CongestionShareThreshold = 0.30;

    // Why: Small pockets are normal variance; this minimum keeps congestion focused on real clumps.
    private const int CongestionMinimumCards = 12;

    // Why: A two-card tag can hint at a subtheme worth flagging before it disappears in the pool.
    private const int StrandedThemeMinCards = 2;

    // Why: At five cards the theme is no longer "stranded"; it has enough density to stand alone.
    private const int StrandedThemeMaxCards = 4;

    // Why: Three extra finishers above the floor is the point where closing redundancy becomes notable.
    private const int RedundantFinisherMargin = 3;

    // Why: The UI already treats floor + 1 as the caution band; findings should mirror that threshold.
    private const int WeakFloorMargin = 1;

    // Why: One in-deck combo card is noise; two pieces in hand makes the missing partner actionable.
    private const int NearComboMinPiecesInDeck = 2;

    private const string LandsRole = "lands";
    private const string RampRole = "ramp";
    private const string DrawRole = "draw";
    private const string InteractionRole = "interaction";
    private const string ProtectionRole = "protection";
    private const string EnginesRole = "engines";
    private const string PayoffsRole = "payoffs";
    private const string WinconsRole = "wincons";

    private static readonly string[] WeakFloorRoleOrder =
    [
        LandsRole,
        RampRole,
        DrawRole,
        InteractionRole,
        ProtectionRole,
        EnginesRole,
        PayoffsRole,
        WinconsRole,
    ];

    /// <summary>
    /// Computes the structural findings for the current analyzed pool.
    /// </summary>
    /// <param name="pool">Analyzed pool cards with their roles and categories.</param>
    /// <param name="nearCombos">One-card-away near-combos found for the pool.</param>
    /// <param name="floors">Role floors keyed by the eight fixed Cut Lab role keys.</param>
    /// <param name="comboDataAvailable"><see langword="true"/> when combo lookup ran (even if it found nothing); <see langword="false"/> when lookup failed/was unavailable.</param>
    /// <param name="categoryDataAvailable"><see langword="true"/> when category lookup ran (even if it found nothing); <see langword="false"/> when lookup failed/was unavailable.</param>
    public static CutLabStructuralFindingsResult Compute(
        IReadOnlyList<CutLabAnalyzedCard> pool,
        IReadOnlyList<SpellbookAlmostCombo> nearCombos,
        IReadOnlyDictionary<string, int> floors,
        bool comboDataAvailable,
        bool categoryDataAvailable)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(nearCombos);
        ArgumentNullException.ThrowIfNull(floors);

        List<CutLabFinding> findings = [];

        findings.AddRange(ComputeCurveCongestion(pool));

        if (categoryDataAvailable)
        {
            findings.AddRange(ComputeStrandedSubthemes(pool));
        }

        findings.AddRange(ComputeRedundantFinishers(pool, floors));
        findings.AddRange(ComputeWeakFloorCases(pool, floors));

        if (comboDataAvailable)
        {
            findings.AddRange(ComputeEnablerStarved(nearCombos));
        }

        return new CutLabStructuralFindingsResult(findings, comboDataAvailable, categoryDataAvailable);
    }

    private static IEnumerable<CutLabFinding> ComputeCurveCongestion(IReadOnlyList<CutLabAnalyzedCard> pool)
    {
        IReadOnlyList<CutLabAnalyzedCard> nonlands = pool.Where(card => !card.IsLand).ToArray();
        if (nonlands.Count == 0)
        {
            yield break;
        }

        foreach (IGrouping<string, CutLabAnalyzedCard> bucket in nonlands
            .GroupBy(card => ManaValueBucket(card.ManaValue))
            .OrderBy(group => BucketSortKey(group.Key)))
        {
            int count = bucket.Count();
            double share = (double)count / nonlands.Count;
            if (share < CongestionShareThreshold || count < CongestionMinimumCards)
            {
                continue;
            }

            yield return new CutLabFinding(
                CutLabFindingKind.CurveCongestion,
                "Curve congestion",
                $"{count} nonland cards sit at mana value {bucket.Key} — {FormatPercent(share)}% of your nonland pool.",
                bucket.Select(card => new CutLabFindingEvidence(card.Name, card.ManaValue)).ToArray());
        }
    }

    private static IEnumerable<CutLabFinding> ComputeStrandedSubthemes(IReadOnlyList<CutLabAnalyzedCard> pool)
    {
        var groupedCategories = pool
            .SelectMany(
                card => card.Categories.Select(category => (Category: category, Card: card)))
            .GroupBy(
                entry => entry.Category,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, (string Category, CutLabAnalyzedCard Card)> theme in groupedCategories)
        {
            int count = theme.Count();
            if (count < StrandedThemeMinCards || count > StrandedThemeMaxCards)
            {
                continue;
            }

            // Why: the exclusion must consume PlanRoleClassifier's own vocabulary so the two reads
            // cannot drift silently if the category keywords evolve later.
            if (PlanRoleClassifier.CategoryMapsToPlanRole(theme.Key))
            {
                continue;
            }

            yield return new CutLabFinding(
                CutLabFindingKind.StrandedSubtheme,
                "Stranded subthemes",
                $"'{theme.Key}' appears on only {count} cards — likely too few to function as a theme.",
                theme.Select(entry => new CutLabFindingEvidence(entry.Card.Name, null)).ToArray());
        }
    }

    private static IEnumerable<CutLabFinding> ComputeRedundantFinishers(
        IReadOnlyList<CutLabAnalyzedCard> pool,
        IReadOnlyDictionary<string, int> floors)
    {
        IReadOnlyList<CutLabAnalyzedCard> wincons = CardsInRole(pool, WinconsRole);
        int floor = FloorFor(floors, WinconsRole);
        if (wincons.Count < floor + RedundantFinisherMargin)
        {
            yield break;
        }

        yield return new CutLabFinding(
            CutLabFindingKind.RedundantFinishers,
            "Redundant finishers",
            $"{wincons.Count} win conditions against a floor of {floor} — more than one game usually needs.",
            wincons.Select(card => new CutLabFindingEvidence(card.Name, null)).ToArray());
    }

    private static IEnumerable<CutLabFinding> ComputeWeakFloorCases(
        IReadOnlyList<CutLabAnalyzedCard> pool,
        IReadOnlyDictionary<string, int> floors)
    {
        foreach (string roleKey in WeakFloorRoleOrder)
        {
            int floor = FloorFor(floors, roleKey);
            IReadOnlyList<CutLabAnalyzedCard> cards = CardsInRole(pool, roleKey);
            int count = cards.Count;

            if (floor <= 0 || count > floor + WeakFloorMargin)
            {
                continue;
            }

            yield return new CutLabFinding(
                CutLabFindingKind.WeakFloorCase,
                "Weak floor cases",
                $"{RoleDisplayName(roleKey)} is at {count} against a floor of {floor} — every card in this role is effectively protected already.",
                cards.Select(card => new CutLabFindingEvidence(card.Name, null)).ToArray());
        }
    }

    private static IEnumerable<CutLabFinding> ComputeEnablerStarved(IReadOnlyList<SpellbookAlmostCombo> nearCombos)
    {
        foreach (SpellbookAlmostCombo combo in nearCombos)
        {
            if (combo.CardsInDeck.Count < NearComboMinPiecesInDeck)
            {
                continue;
            }

            string cardsInDeck = JoinCardNames(combo.CardsInDeck);

            yield return new CutLabFinding(
                CutLabFindingKind.EnablerStarved,
                "Enabler-starved cards",
                $"{cardsInDeck} are missing their combo partner: {combo.MissingCard}.",
                combo.CardsInDeck.Select(cardName => new CutLabFindingEvidence(cardName, null)).ToArray());
        }
    }

    private static IReadOnlyList<CutLabAnalyzedCard> CardsInRole(IReadOnlyList<CutLabAnalyzedCard> pool, string roleKey)
        => pool.Where(card => card.Roles.Contains(roleKey, StringComparer.Ordinal)).ToArray();

    private static int FloorFor(IReadOnlyDictionary<string, int> floors, string roleKey)
        => floors.TryGetValue(roleKey, out int floor) ? floor : 0;

    private static string JoinCardNames(IReadOnlyList<string> cardNames)
        => cardNames.Count switch
        {
            0 => string.Empty,
            1 => cardNames[0],
            2 => $"{cardNames[0]} and {cardNames[1]}",
            _ => $"{string.Join(", ", cardNames.Take(cardNames.Count - 1))} and {cardNames[^1]}",
        };

    private static string ManaValueBucket(double manaValue)
    {
        if (manaValue <= 1)
        {
            return "0-1";
        }

        if (manaValue <= 2)
        {
            return "2";
        }

        if (manaValue <= 3)
        {
            return "3";
        }

        if (manaValue <= 4)
        {
            return "4";
        }

        return "5+";
    }

    private static int BucketSortKey(string bucket)
        => bucket switch
        {
            "0-1" => 0,
            "2" => 1,
            "3" => 2,
            "4" => 3,
            "5+" => 4,
            _ => int.MaxValue,
        };

    private static int FormatPercent(double share)
        => (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);

    private static string RoleDisplayName(string roleKey)
        => roleKey switch
        {
            LandsRole => "Lands",
            RampRole => "Ramp",
            DrawRole => "Card draw",
            InteractionRole => "Interaction",
            ProtectionRole => "Protection",
            EnginesRole => "Engines",
            PayoffsRole => "Payoffs",
            WinconsRole => "Win conditions",
            _ => roleKey,
        };
}
