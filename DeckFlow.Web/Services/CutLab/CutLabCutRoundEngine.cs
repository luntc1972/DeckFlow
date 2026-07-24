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
}

/// <summary>Builds Cut Lab's fixed-order cut proposal sequence from the derived working list.</summary>
public static class CutLabCutRoundEngine
{
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

    // Why: "Obvious cuts" should reflect findings that discriminate among cards, not role-wide
    // warnings that attach to every member of a protected or redundant role uniformly.
    private static readonly IReadOnlySet<CutLabFindingKind> ExcludedFindingKindsFromTally =
        new HashSet<CutLabFindingKind>
        {
            CutLabFindingKind.WeakFloorCase,
            CutLabFindingKind.RedundantFinishers,
            CutLabFindingKind.ComboProtected,
        };

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
    /// <returns>The ordered queue, top proposal, and cards still remaining to target.</returns>
    public static CutLabRoundPlan BuildQueue(
        IReadOnlyList<CutLabRoundInputCard> workingList,
        CutLabStructuralFindingsResult findings,
        IReadOnlyList<CutLabDecision> decisions,
        int cardsToCutTarget,
        IReadOnlyDictionary<string, double>? round3DeltaMagnitudes = null)
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
            };
        }

        IReadOnlyDictionary<string, CutLabDecision> latestDecisions = CutLabWorkingList.LatestDecisionsByCard(decisions);
        IReadOnlySet<string> acceptedCardNames = latestDecisions
            .Where(entry => entry.Value.Kind == CutLabDecisionKind.Accepted)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, CardFindingTally> findingTallies = BuildFindingTallies(findings.Findings);

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

        IReadOnlyList<CutLabRoundQueueItem> round1 = firstPassCards
            .Where(entry => entry.Tally.Count >= 2)
            .OrderByDescending(entry => entry.Tally.Count)
            .ThenBy(entry => entry.Card.ManaValue)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, Round1Key, entry.Tally))
            .ToArray();

        IReadOnlyList<CutLabRoundQueueItem> round2 = firstPassCards
            .Where(entry => entry.Tally.Count == 1)
            .OrderBy(entry => entry.Card.ManaValue)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, Round2Key, entry.Tally))
            .ToArray();

        IReadOnlyList<CutLabRoundQueueItem> round3 = firstPassCards
            .Where(entry => entry.Tally.Count == 0)
            .OrderBy(entry => Round3DeltaMagnitudeFor(round3DeltaMagnitudes, entry.Card.Name))
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
            .OrderBy(entry => entry.Decision.Ordinal)
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
            .OrderBy(entry => entry.Decision.Ordinal)
            .ThenBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToQueueItem(entry.Card.Name, SecondPassRejectedKey, entry.Tally))
            .ToArray();

        IReadOnlyList<CutLabRoundQueueItem> queue = round1
            .Concat(round2)
            .Concat(round3)
            .Concat(deferredPass)
            .Concat(rejectedPass)
            .ToArray();

        return new CutLabRoundPlan
        {
            Queue = queue,
            NextProposal = queue.FirstOrDefault(),
            CardsRemainingToTarget = cardsRemainingToTarget,
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

    internal static (CutLabStructuralFindingsResult Findings, CutLabRoundPlan RoundPlan) BuildFindingsAndRoundPlan(
        IReadOnlyList<CutLabPoolCard> workingList,
        CutLabAnalysisContext context,
        IReadOnlyDictionary<string, int> floorByRole,
        IReadOnlyList<CutLabDecision> decisions,
        IReadOnlyDictionary<string, double>? round3DeltaMagnitudes = null)
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
                .ToArray());
        CutLabRoundPlan roundPlan = BuildQueue(
            BuildInputs(workingList, context.AnalyzedCards),
            findings,
            decisions,
            workingList.Sum(card => card.Quantity) - 100,
            round3DeltaMagnitudes);
        return (findings, roundPlan);
    }

    private static IReadOnlyDictionary<string, CardFindingTally> BuildFindingTallies(IReadOnlyList<CutLabFinding> findings)
    {
        Dictionary<string, CardFindingTallyBuilder> tallies = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabFinding finding in findings)
        {
            if (ExcludedFindingKindsFromTally.Contains(finding.Kind))
            {
                continue;
            }

            HashSet<string> cardNamesInFinding = finding.Evidence
                .Select(evidence => evidence.CardName)
                .Where(cardName => !string.IsNullOrWhiteSpace(cardName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string cardName in cardNamesInFinding)
            {
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

    private static double Round3DeltaMagnitudeFor(IReadOnlyDictionary<string, double>? deltaMagnitudes, string cardName)
        => deltaMagnitudes is not null && deltaMagnitudes.TryGetValue(cardName, out double magnitude)
            ? magnitude
            : double.PositiveInfinity;

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
