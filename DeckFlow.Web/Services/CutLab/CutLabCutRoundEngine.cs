using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Input card shape for the pure Cut Lab round engine.</summary>
/// <param name="Name">Display card name.</param>
/// <param name="Quantity">Quantity in the derived working list.</param>
/// <param name="TypeLine">Resolved type line for the card.</param>
/// <param name="IsCommander"><see langword="true"/> when this card is the deck's commander.</param>
/// <param name="IsLocked"><see langword="true"/> when this card is protected from cuts.</param>
/// <param name="ManaValue">Mana value used for deterministic fallback ordering.</param>
/// <param name="IsLand"><see langword="true"/> when the card is a land.</param>
/// <param name="Roles">Assigned structural role keys for the card.</param>
/// <param name="Categories">Assigned category tags for the card.</param>
public sealed record CutLabRoundInputCard(
    string Name,
    int Quantity,
    string TypeLine,
    bool IsCommander,
    bool IsLocked,
    double ManaValue,
    bool IsLand,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Categories);

/// <summary>One ordered proposal candidate emitted by the round engine.</summary>
/// <param name="CardName">Display card name to propose next.</param>
/// <param name="RoundKey">Stable round key for restore and decision logging.</param>
/// <param name="RoundLabel">Fixed UI banner copy for the round.</param>
/// <param name="FindingCount">Number of discriminating findings attached to this card.</param>
/// <param name="DiscriminatingFindingKinds">Distinct discriminating finding kinds used for evidence chips.</param>
public sealed record CutLabRoundQueueItem(
    string CardName,
    string RoundKey,
    string RoundLabel,
    int FindingCount,
    IReadOnlyList<CutLabFindingKind> DiscriminatingFindingKinds);

/// <summary>Deterministic ordered plan for the current cut rounds.</summary>
public sealed record CutLabRoundPlan
{
    /// <summary>Ordered proposal queue across the active rounds and second-pass loop.</summary>
    public required IReadOnlyList<CutLabRoundQueueItem> Queue { get; init; }

    /// <summary>The next single proposal to present, or <see langword="null"/> when already at target.</summary>
    public CutLabRoundQueueItem? NextProposal { get; init; }

    /// <summary>Cards still needing to be cut to reach 100, clamped at zero.</summary>
    public required int CardsRemainingToTarget { get; init; }

    /// <summary>Read-only advisory list for locked-card overshoot states, when applicable.</summary>
    public CutLabLockedOvershootAdvisory? LockedOvershootAdvisory { get; init; }
}

/// <summary>One grouped role bucket inside the locked-overshoot advisory.</summary>
public sealed record CutLabLockedOvershootGroup(
    string RoleKey,
    IReadOnlyList<string> CardNames);

/// <summary>Read-only suggestion list for locked-card overshoot states.</summary>
public sealed record CutLabLockedOvershootAdvisory(
    int CardsOverTarget,
    int HiddenCount,
    IReadOnlyList<CutLabLockedOvershootGroup> Groups);

/// <summary>Builds Cut Lab's fixed-order cut proposal sequence from the derived working list.</summary>
public static class CutLabCutRoundEngine
{
    private const int TargetDeckSize = 100;

    /// <summary>Stable round key for obvious-cut proposals.</summary>
    public const string Round1Key = "round-1";

    /// <summary>Stable round key for structural-choice proposals.</summary>
    public const string Round2Key = "round-2";

    /// <summary>Stable round key for preference-call proposals.</summary>
    public const string Round3Key = "round-3";

    /// <summary>Stable round key for revisiting deferred cards.</summary>
    public const string SecondPassDeferredKey = "second-pass-deferred";

    /// <summary>Stable round key for revisiting earlier rejected cards.</summary>
    public const string SecondPassRejectedKey = "second-pass-rejected";

    /// <summary>Stable round key for committed what-if swaps.</summary>
    public const string WhatifSwapKey = "whatif-swap";

    /// <summary>Fixed UI banner copy for round 1.</summary>
    public const string Round1Label = "Round 1 · Obvious cuts";

    /// <summary>Fixed UI banner copy for round 2.</summary>
    public const string Round2Label = "Round 2 · Structural choices";

    /// <summary>Fixed UI banner copy for round 3.</summary>
    public const string Round3Label = "Round 3 · Preference calls";

    /// <summary>Fixed UI banner copy for the deferred second pass.</summary>
    public const string SecondPassDeferredLabel = "Second pass · Revisiting deferred cards";

    /// <summary>Fixed UI banner copy for the rejected second pass.</summary>
    public const string SecondPassRejectedLabel = "Second pass · Revisiting earlier decisions";

    /// <summary>Fixed UI banner copy for committed what-if swaps.</summary>
    public const string WhatifSwapLabel = "What-if swap";

    // Why: "Obvious cuts" should reflect findings that discriminate among cards. Two separate
    // classes of finding fail that test and are excluded here.
    // WeakFloorCase and RedundantFinishers are role-wide warnings that attach to every member of
    // a protected or redundant role uniformly, so they rank nothing against anything.
    // ComboProtected and EnablerStarved are combo advisories, and both are emitted from the same
    // near-combo input (CutLabStructuralFindings.Compute feeds `nearCombos` to both detectors under
    // the same threshold). Counting only one of the pair meant a card was pushed *toward* being cut
    // for being a combo piece: the protective finding scored 0 while its punitive twin scored +1,
    // so combo-dense cards sorted to the top of round 1. Combo findings inform the user; they never
    // promote a card up the cut queue. Both still render in the UI.
    // FunctionalTwins is deliberately absent because its evidence is per-card selective: a specific
    // role at a specific exact mana value and a specific primary type, not a role-wide warning that
    // attaches uniformly to every member of a role.
    private static readonly IReadOnlySet<CutLabFindingKind> ExcludedFindingKindsFromTally =
        new HashSet<CutLabFindingKind>
        {
            CutLabFindingKind.WeakFloorCase,
            CutLabFindingKind.RedundantFinishers,
            CutLabFindingKind.ComboProtected,
            CutLabFindingKind.EnablerStarved,
            CutLabFindingKind.FunctionalTwins,
        };

    // Why: headroom (in-pool count minus effective floor) is now the primary locked-overshoot rank, because
    // the old fixed order put wincons first as least-structural even though wincons usually has the least
    // slack against its floor. This array survives as the deterministic tiebreak so the advisory still stays
    // stable between rounds even if the canonical floor order changes. Within the interaction split, mass
    // ranks before targeted because sweepers are more replaceable overshoot cuts than cheap point answers.
    private static readonly string[] LockedOvershootRoleOrder =
    [
        "wincons",
        "payoffs",
        "engines",
        "protection",
        "interaction-mass",
        "interaction-targeted",
        "draw",
        "ramp",
        "lands",
        "other",
    ];

    /// <summary>Returns the fixed round label for a stable round key.</summary>
    /// <param name="roundKey">Stable round key.</param>
    /// <returns>The fixed label for known rounds, or the original key when unknown.</returns>
    public static string LabelFor(string roundKey)
        => roundKey switch
        {
            Round1Key => Round1Label,
            Round2Key => Round2Label,
            Round3Key => Round3Label,
            SecondPassDeferredKey => SecondPassDeferredLabel,
            SecondPassRejectedKey => SecondPassRejectedLabel,
            WhatifSwapKey => WhatifSwapLabel,
            _ => roundKey,
        };

    /// <summary>Returns the fixed banner body copy for a stable round key.</summary>
    /// <param name="roundKey">Stable round key.</param>
    /// <returns>The supporting banner copy for known rounds, or empty when unknown.</returns>
    public static string RoundBannerBodyFor(string roundKey)
        => roundKey switch
        {
            Round1Key => "Cards flagged by 2 or more structural findings from the section above.",
            Round2Key => "Cards flagged by exactly one structural finding.",
            Round3Key => "Everything else, ordered by smallest measurable tradeoff first.",
            SecondPassDeferredKey or SecondPassRejectedKey => "Still over 100 cards. These were deferred or kept earlier; take another look.",
            WhatifSwapKey => "A hypothetical swap you kept.",
            _ => string.Empty,
        };

    /// <summary>Returns whether the provided value is one of Cut Lab's stable round keys.</summary>
    /// <param name="roundKey">Candidate round key.</param>
    /// <returns><see langword="true"/> when the key is a known round key.</returns>
    public static bool IsKnownRoundKey(string? roundKey)
        => roundKey is Round1Key
            or Round2Key
            or Round3Key
            or SecondPassDeferredKey
            or SecondPassRejectedKey
            or WhatifSwapKey;

    /// <summary>Builds the deterministic ordered proposal queue for the current working list.</summary>
    /// <param name="workingList">Derived working-list cards for the current session state.</param>
    /// <param name="findings">Structural findings already computed for the current working list.</param>
    /// <param name="decisions">Current decision history for second-pass routing and defensive accepted-card filtering.</param>
    /// <param name="cardsToCutTarget">Cards still needing to be cut to reach 100.</param>
    /// <param name="round3DeltaMagnitudes">Optional pure ordering hint for round 3 tradeoff magnitude.</param>
    /// <param name="floorByRole">Optional effective role floors used to rank the locked-overshoot advisory by headroom.</param>
    /// <param name="roleCounts">Optional in-pool role counts used to rank the locked-overshoot advisory by headroom.</param>
    /// <param name="planAffinities">Optional plan affinity keyed by normalized card name.</param>
    /// <returns>The ordered queue, top proposal, and cards still remaining to target.</returns>
    public static CutLabRoundPlan BuildQueue(
        IReadOnlyList<CutLabRoundInputCard> workingList,
        CutLabStructuralFindingsResult findings,
        IReadOnlyList<CutLabDecision> decisions,
        int cardsToCutTarget,
        IReadOnlyDictionary<string, double>? round3DeltaMagnitudes = null,
        IReadOnlyDictionary<string, int>? floorByRole = null,
        IReadOnlyDictionary<string, int>? roleCounts = null,
        IReadOnlyDictionary<string, CutLabPlanAffinity>? planAffinities = null)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(decisions);

        int cardsRemainingToTarget = Math.Max(cardsToCutTarget, 0);
        if (cardsRemainingToTarget == 0)
        {
            return new CutLabRoundPlan
            {
                Queue = [],
                NextProposal = null,
                CardsRemainingToTarget = 0,
                LockedOvershootAdvisory = null,
            };
        }

        IReadOnlyDictionary<string, CutLabDecision> latestDecisions = CutLabWorkingList.LatestDecisionsByCard(decisions);
        IReadOnlySet<string> acceptedCardNames = latestDecisions
            .Where(entry => entry.Value.Kind == CutLabDecisionKind.Accepted)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Why: ComboProtected is deliberately absent from the tally so it can never promote a card,
        // but that left it with no effect whatsoever — a card sitting in two complete combos could
        // still lead a round on one unrelated finding. Combo membership is therefore the FIRST
        // ordering key of rounds 1–3, so a combo piece is proposed only after equally-flagged
        // non-combo cards in that same round. In second-pass rounds, combo demotion is a
        // FIRST-APPEARANCE-ONLY rule: cards that have not yet been revisited sort before cards
        // re-decided in that pass, while revisited cards rotate purely by ordinal. This prevents
        // starvation where a re-deferred or re-rejected ordinary card permanently outranks an
        // older combo piece.
        // Why: only CompletePiece evidence counts toward demotion. A NeedsPartner combo piece is
        // missing its partner and is already a cut candidate (the same card EnablerStarved flags);
        // demoting it a second time for the same reason is backwards.
        // Why: only the combo demotion set is normalized through CutLabCardNames.Normalize/.Comparer,
        // so a DFC card whose deck-side full name ("Front // Back") differs from the Spellbook-side
        // front-face name still matches. General finding tallies deliberately retain their raw
        // OrdinalIgnoreCase keys; that is currently safe because ComboProtected is excluded from
        // those tallies, and changing their keying would alter round 1–3 assignment for every deck.
        IReadOnlySet<string> comboProtectedCardNames = findings.Findings
            .Where(finding => finding.Kind == CutLabFindingKind.ComboProtected)
            .SelectMany(finding => finding.Evidence)
            .Where(evidence => evidence.BadgeState == ComboBadgeState.CompletePiece)
            .Select(evidence => evidence.CardName)
            .Where(cardName => !string.IsNullOrWhiteSpace(cardName))
            .Select(CutLabCardNames.Normalize)
            .ToHashSet(CutLabCardNames.Comparer);
        IReadOnlyDictionary<string, CardFindingTally> findingTallies = BuildFindingTallies(findings.Findings, workingList, comboProtectedCardNames);

        IReadOnlyList<CutLabRoundInputCard> eligibleCards = workingList
            .Where(card =>
                !card.IsLocked
                && !card.IsCommander
                && !acceptedCardNames.Contains(card.Name)
                // Why: the current decision model cuts whole working-list entries, never partial quantities.
                && card.Quantity <= cardsRemainingToTarget)
            .ToArray();

        IReadOnlyList<(CutLabRoundInputCard Card, CardFindingTally Tally)> firstPassCards = eligibleCards
            .Where(card => !latestDecisions.TryGetValue(card.Name, out CutLabDecision? latestDecision) || latestDecision.Kind == CutLabDecisionKind.Accepted)
            .Select(card => (card, TallyFor(findingTallies, card.Name)))
            .ToArray();

        // Why: Plan affinity sits below combo protection and above finding tally deliberately: the user's declared plan is a stronger statement of intent than a structural finding count, but combo membership is a hard structural fact that outranks both.
        IReadOnlyList<CutLabRoundQueueItem> round1 = firstPassCards
            .Where(entry => entry.Tally.Count >= 2)
            .OrderBy(entry => ComboProtectionRank(comboProtectedCardNames, entry.Card.Name))
            .ThenBy(entry => PlanAffinityRank(planAffinities, entry.Card.Name))
            .ThenByDescending(entry => entry.Tally.Count)
            .ThenBy(entry => entry.Card.ManaValue)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, Round1Key, entry.Tally))
            .ToArray();

        IReadOnlyList<CutLabRoundQueueItem> round2 = firstPassCards
            .Where(entry => entry.Tally.Count == 1)
            .OrderBy(entry => ComboProtectionRank(comboProtectedCardNames, entry.Card.Name))
            .ThenBy(entry => PlanAffinityRank(planAffinities, entry.Card.Name))
            .ThenBy(entry => entry.Card.ManaValue)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, Round2Key, entry.Tally))
            .ToArray();

        IReadOnlyList<CutLabRoundQueueItem> round3 = firstPassCards
            .Where(entry => entry.Tally.Count == 0)
            .OrderBy(entry => ComboProtectionRank(comboProtectedCardNames, entry.Card.Name))
            .ThenBy(entry => PlanAffinityRank(planAffinities, entry.Card.Name))
            .ThenBy(entry => Round3DeltaMagnitudeFor(round3DeltaMagnitudes, entry.Card.Name))
            .ThenBy(entry => entry.Card.ManaValue)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, Round3Key, entry.Tally))
            .ToArray();

        IReadOnlyList<(CutLabRoundInputCard Card, CardFindingTally Tally, CutLabDecision Decision)> deferredCards = eligibleCards
            .Select(card => latestDecisions.TryGetValue(card.Name, out CutLabDecision? latestDecision)
                ? (Card: card, Tally: TallyFor(findingTallies, card.Name), Decision: latestDecision)
                : ((CutLabRoundInputCard Card, CardFindingTally Tally, CutLabDecision Decision)?)null)
            .Where(entry => entry is not null && entry.Value.Decision.Kind == CutLabDecisionKind.Deferred)
            .Select(entry => entry!.Value)
            .ToArray();
        IReadOnlyList<CutLabRoundQueueItem> deferredPass = deferredCards
            .OrderBy(entry => IsSecondPassRound(entry.Decision.Round) ? 1 : 0)
            .ThenBy(entry => IsSecondPassRound(entry.Decision.Round) ? 0 : ComboProtectionRank(comboProtectedCardNames, entry.Card.Name))
            .ThenBy(entry => entry.Decision.Ordinal)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, SecondPassDeferredKey, entry.Tally))
            .ToArray();

        IReadOnlyList<(CutLabRoundInputCard Card, CardFindingTally Tally, CutLabDecision Decision)> rejectedCards = eligibleCards
            .Select(card => latestDecisions.TryGetValue(card.Name, out CutLabDecision? latestDecision)
                ? (Card: card, Tally: TallyFor(findingTallies, card.Name), Decision: latestDecision)
                : ((CutLabRoundInputCard Card, CardFindingTally Tally, CutLabDecision Decision)?)null)
            .Where(entry => entry is not null && entry.Value.Decision.Kind == CutLabDecisionKind.Rejected)
            .Select(entry => entry!.Value)
            .ToArray();
        IReadOnlyList<CutLabRoundQueueItem> rejectedPass = rejectedCards
            .OrderBy(entry => IsSecondPassRound(entry.Decision.Round) ? 1 : 0)
            .ThenBy(entry => IsSecondPassRound(entry.Decision.Round) ? 0 : ComboProtectionRank(comboProtectedCardNames, entry.Card.Name))
            .ThenBy(entry => entry.Decision.Ordinal)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, SecondPassRejectedKey, entry.Tally))
            .ToArray();

        IReadOnlyList<CutLabRoundQueueItem> queue = round1
            .Concat(round2)
            .Concat(round3)
            .Concat(deferredPass)
            .Concat(rejectedPass)
            .ToArray();
        CutLabLockedOvershootAdvisory? lockedOvershootAdvisory = BuildLockedOvershootAdvisory(workingList, floorByRole, roleCounts);

        return new CutLabRoundPlan
        {
            Queue = queue,
            NextProposal = queue.FirstOrDefault(),
            CardsRemainingToTarget = cardsRemainingToTarget,
            LockedOvershootAdvisory = lockedOvershootAdvisory,
        };
    }

    internal static IReadOnlyList<CutLabRoundInputCard> BuildInputs(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<CutLabAnalyzedCard> analyzedCards)
    {
        IReadOnlyDictionary<string, CutLabAnalyzedCard> analyzedByName = CutLabCardNames.ToLastWinsDictionary(
            analyzedCards,
            card => card.Name,
            card => card);

        return workingList
            .Select(card =>
            {
                analyzedByName.TryGetValue(CutLabCardNames.Normalize(card.Name), out CutLabAnalyzedCard? analyzedCard);
                return new CutLabRoundInputCard(
                    card.Name,
                    card.Quantity,
                    card.TypeLine,
                    card.IsCommander,
                    card.IsLocked,
                    analyzedCard?.ManaValue ?? 0,
                    analyzedCard?.IsLand ?? false,
                    analyzedCard?.Roles ?? [],
                    analyzedCard?.Categories ?? []);
            })
            .ToArray();
    }

    /// <summary>Builds structural findings and the corresponding deterministic round plan.</summary>
    /// <param name="workingList">Derived working-list cards for the current session state.</param>
    /// <param name="context">Resolved analysis context for the working list.</param>
    /// <param name="floorByRole">Effective role floors for structural analysis.</param>
    /// <param name="decisions">Current decision history.</param>
    /// <param name="twinsEnabled"><c>true</c> when the <c>analysis.cut-lab.functional-twins</c> flag is on for this request.</param>
    /// <param name="round3DeltaMagnitudes">Optional pure ordering hint for round 3 tradeoff magnitude.</param>
    /// <param name="planAffinities">Optional plan affinity keyed by normalized card name.</param>
    /// <returns>The structural findings and the corresponding round plan.</returns>
    internal static (CutLabStructuralFindingsResult Findings, CutLabRoundPlan RoundPlan) BuildFindingsAndRoundPlan(
        IReadOnlyList<CutLabPoolCard> workingList,
        CutLabAnalysisContext context,
        IReadOnlyDictionary<string, int> floorByRole,
        IReadOnlyList<CutLabDecision> decisions,
        bool twinsEnabled,
        IReadOnlyDictionary<string, double>? round3DeltaMagnitudes = null,
        IReadOnlyDictionary<string, CutLabPlanAffinity>? planAffinities = null)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(floorByRole);
        ArgumentNullException.ThrowIfNull(decisions);

        CutLabStructuralFindingsResult findings = CutLabStructuralFindings.Compute(
            context.AnalyzedCards,
            context.Classification.AlmostIncludedCombos,
            floorByRole,
            context.Classification.ComboDataAvailable,
            context.Classification.CategoryDataAvailable,
            completeCombos: context.Classification.CardComboMembership.Values
                .SelectMany(membership => membership.CompleteCombos)
                .Distinct()
                .ToArray(),
            twinsEnabled: twinsEnabled,
            planAffinities: planAffinities);
        CutLabRoundPlan roundPlan = BuildQueue(
            BuildInputs(workingList, context.AnalyzedCards),
            findings,
            decisions,
            workingList.Sum(card => card.Quantity) - TargetDeckSize,
            round3DeltaMagnitudes,
            floorByRole,
            context.RoleCounts,
            planAffinities);
        return (findings, roundPlan);
    }

    private static IReadOnlyDictionary<string, CardFindingTally> BuildFindingTallies(
        IReadOnlyList<CutLabFinding> findings,
        IReadOnlyList<CutLabRoundInputCard> workingList,
        IReadOnlySet<string> comboProtectedCardNames)
    {
        Dictionary<string, CardFindingTallyBuilder> tallies = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabFinding finding in findings)
        {
            if (ExcludedFindingKindsFromTally.Contains(finding.Kind))
            {
                continue;
            }

            HashSet<string> cardNamesInFinding;
            if (finding.Kind == CutLabFindingKind.FunctionalTwins)
            {
                // Why: D-23 keeps general finding tallies raw (see the normalization warning in
                // BuildQueue) while allowing normalized functional-twins evidence to reach every
                // equivalent working-list entry.
                IReadOnlySet<string> normalizedEvidenceNames = finding.Evidence
                    .Select(evidence => evidence.CardName)
                    .Where(cardName => !string.IsNullOrWhiteSpace(cardName))
                    .Select(CutLabCardNames.Normalize)
                    .ToHashSet(CutLabCardNames.Comparer);
                cardNamesInFinding = workingList
                    .Select(card => card.Name)
                    .Where(name => normalizedEvidenceNames.Contains(CutLabCardNames.Normalize(name)))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                cardNamesInFinding = finding.Evidence
                    .Select(evidence => evidence.CardName)
                    .Where(cardName => !string.IsNullOrWhiteSpace(cardName))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            foreach (string cardName in cardNamesInFinding)
            {
                if (finding.Kind == CutLabFindingKind.FunctionalTwins
                    && comboProtectedCardNames.Contains(CutLabCardNames.Normalize(cardName)))
                {
                    // Why: a complete combo piece remains eligible for its non-twins evidence, but
                    // twins must not provide the extra tally that promotes it across round boundaries.
                    continue;
                }

                if (!tallies.TryGetValue(cardName, out CardFindingTallyBuilder? tally))
                {
                    tally = new CardFindingTallyBuilder();
                    tallies[cardName] = tally;
                }

                tally.Count++;
                tally.Kinds.Add(finding.Kind);
            }
        }

        return tallies.ToDictionary(
            entry => entry.Key,
            entry => new CardFindingTally(
                entry.Value.Count,
                entry.Value.Kinds.OrderBy(kind => kind.ToString(), StringComparer.Ordinal).ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    private static CutLabRoundQueueItem ToQueueItem(string cardName, string roundKey, CardFindingTally tally)
        => new(cardName, roundKey, LabelFor(roundKey), tally.Count, tally.Kinds);

    private static CardFindingTally TallyFor(IReadOnlyDictionary<string, CardFindingTally> tallies, string cardName)
        => tallies.TryGetValue(cardName, out CardFindingTally? tally)
            ? tally
            : CardFindingTally.Empty;

    // Why: returns a sort rank, not a bool, so it can lead an OrderBy chain without inverting the
    // remaining keys. 0 = not a combo piece (proposed first), 1 = combo piece (proposed last).
    private static int ComboProtectionRank(IReadOnlySet<string> comboProtectedCardNames, string cardName)
        => comboProtectedCardNames.Contains(CutLabCardNames.Normalize(cardName)) ? 1 : 0;

    // Why: This returns a sort rank rather than a bool so it can sit in an OrderBy chain without inverting later keys; 0 means off-plan and is proposed first. ComboProtectionRank remains the dominant tier because it is the primary OrderBy key and this rank is only a ThenBy key. OnPlanScoreCap separately keeps one, two, and three-or-more matching signals distinguishable without unbounded growth.
    private static int PlanAffinityRank(IReadOnlyDictionary<string, CutLabPlanAffinity>? planAffinities, string cardName)
        => planAffinities is null ? 0 : CutLabPlanAffinityResolver.For(planAffinities, cardName).Score;

    private static bool IsSecondPassRound(string roundKey)
        => roundKey is SecondPassDeferredKey or SecondPassRejectedKey;

    private static double Round3DeltaMagnitudeFor(IReadOnlyDictionary<string, double>? deltaMagnitudes, string cardName)
        => deltaMagnitudes is not null && deltaMagnitudes.TryGetValue(cardName, out double magnitude)
            ? magnitude
            : double.PositiveInfinity;

    private static CutLabLockedOvershootAdvisory? BuildLockedOvershootAdvisory(
        IReadOnlyList<CutLabRoundInputCard> workingList,
        IReadOnlyDictionary<string, int>? floorByRole,
        IReadOnlyDictionary<string, int>? roleCounts)
    {
        int lockedCardCount = workingList
            .Where(card => card.IsLocked)
            .Sum(card => card.Quantity);
        if (lockedCardCount <= TargetDeckSize)
        {
            return null;
        }

        IReadOnlyList<CutLabRoundInputCard> lockedCards = workingList
            .Where(card => card.IsLocked && !card.IsCommander)
            .ToArray();
        if (lockedCards.Count == 0)
        {
            return null;
        }

        IReadOnlyList<(string RoleKey, string Name)> rankedCards = lockedCards
            .Select(card => (RoleKey: AdvisoryRoleFor(card.Roles, floorByRole, roleCounts), Name: card.Name, Type: CardTypeLine.PrimaryType(card.TypeLine)))
            .OrderByDescending(entry => HeadroomFor(entry.RoleKey, floorByRole, roleCounts))
            .ThenBy(entry => RolePriority(entry.RoleKey))
            .ThenBy(entry => TypePriority(entry.Type))
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => (entry.RoleKey, entry.Name))
            .ToArray();

        IReadOnlyList<(string RoleKey, string Name)> visibleCards = rankedCards.Take(20).ToArray();
        IReadOnlyList<CutLabLockedOvershootGroup> groups = visibleCards
            .GroupBy(entry => entry.RoleKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CutLabLockedOvershootGroup(group.Key, group.Select(entry => entry.Name).ToArray()))
            .ToArray();

        return new CutLabLockedOvershootAdvisory(lockedCardCount - TargetDeckSize, Math.Max(rankedCards.Count - visibleCards.Count, 0), groups);
    }

    private static string AdvisoryRoleFor(
        IReadOnlyList<string> roles,
        IReadOnlyDictionary<string, int>? floorByRole,
        IReadOnlyDictionary<string, int>? roleCounts)
        // Why: a card's cuttability is bounded by its tightest role, so the conservative attribution is the
        // role with the least slack. Attributing the card to a roomier role would understate the cost of cutting it.
        => roles
            .Select(NormalizeRoleKey)
            .OrderBy(roleKey => HeadroomFor(roleKey, floorByRole, roleCounts))
            .ThenBy(RolePriority)
            .FirstOrDefault() ?? "other";

    private static int HeadroomFor(
        string roleKey,
        IReadOnlyDictionary<string, int>? floorByRole,
        IReadOnlyDictionary<string, int>? roleCounts)
    {
        string canonicalRoleKey = NormalizeRoleKey(roleKey);
        if (string.Equals(canonicalRoleKey, "other", StringComparison.Ordinal))
        {
            // Why: "other" has no floor to have headroom against, so it is left to the deterministic tiebreak,
            // where it already sits last. Ranking it by raw count would silently promote unclassified cards ahead
            // of every real role, which D-13 does not ask for.
            return 0;
        }

        int count = TryGetCaseInsensitiveValue(roleCounts, canonicalRoleKey, out int resolvedCount) ? resolvedCount : 0;
        int floor = TryGetCaseInsensitiveValue(floorByRole, canonicalRoleKey, out int resolvedFloor) ? resolvedFloor : 0;
        return count - floor;
    }

    private static string NormalizeRoleKey(string? roleKey)
    {
        foreach (string candidate in CutLabFloorRules.RoleKeys)
        {
            if (string.Equals(candidate, roleKey, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return string.Equals(roleKey, "other", StringComparison.OrdinalIgnoreCase)
            ? "other"
            : roleKey?.ToLowerInvariant() ?? "other";
    }

    private static bool TryGetCaseInsensitiveValue(
        IReadOnlyDictionary<string, int>? values,
        string roleKey,
        out int value)
    {
        if (values is not null)
        {
            foreach ((string candidateKey, int candidateValue) in values)
            {
                if (string.Equals(candidateKey, roleKey, StringComparison.OrdinalIgnoreCase))
                {
                    value = candidateValue;
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private static int RolePriority(string roleKey)
        => Array.IndexOf(LockedOvershootRoleOrder, NormalizeRoleKey(roleKey)) is int index && index >= 0
            ? index
            : LockedOvershootRoleOrder.Length;

    private static int TypePriority(string primaryType)
        => Array.IndexOf(CutLabRoleAssigner.TypeGroupOrder, primaryType) is int index && index >= 0
            ? index
            : CutLabRoleAssigner.TypeGroupOrder.Length;

    private sealed record CardFindingTally(int Count, IReadOnlyList<CutLabFindingKind> Kinds)
    {
        public static CardFindingTally Empty { get; } = new(0, []);
    }

    private sealed class CardFindingTallyBuilder
    {
        public int Count { get; set; }

        public HashSet<CutLabFindingKind> Kinds { get; } = [];
    }
}
