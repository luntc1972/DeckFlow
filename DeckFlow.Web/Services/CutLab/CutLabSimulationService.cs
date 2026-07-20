using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds Cut Lab metric snapshots and proposal deltas by reusing the existing manabase engine.</summary>
public interface ICutLabSimulationService
{
    /// <summary>Builds the metric snapshot for the current working list.</summary>
    /// <param name="workingList">Current working pool cards.</param>
    /// <param name="playExperience">Cut Lab play-experience label used to resolve the shared manabase mode.</param>
    /// <param name="trialsOverride">Optional simulation trial count override; null keeps the engine default.</param>
    /// <param name="poolKey">Optional precomputed pool key for the working list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projected seven-family metric snapshot.</returns>
    Task<CutLabMetricSnapshot> BuildSnapshot(
        IReadOnlyList<CutLabPoolCard> workingList,
        string? playExperience,
        int? trialsOverride = InLoopTrials,
        string? poolKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>Builds proposal deltas for removing a candidate card from the current working list.</summary>
    /// <param name="currentWorkingList">Current working pool cards.</param>
    /// <param name="candidateCardName">Candidate card to remove from the current working list.</param>
    /// <param name="playExperience">Cut Lab play-experience label used to resolve the shared manabase mode.</param>
    /// <param name="trialsOverride">Optional simulation trial count override; null keeps the engine default.</param>
    /// <param name="poolKey">Optional precomputed pool key for the current working list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The noise-floored proposal deltas keyed to the current working list.</returns>
    Task<CutLabProposalDeltas> ComputeProposalDeltas(
        IReadOnlyList<CutLabPoolCard> currentWorkingList,
        string candidateCardName,
        string? playExperience,
        int? trialsOverride = InLoopTrials,
        string? poolKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>Default in-loop trial count for Task 103-05 delta snapshots.</summary>
    public const int InLoopTrials = 4000;
}

/// <summary>Cut Lab simulation service that projects existing engine output into the shared metric contract.</summary>
public sealed class CutLabSimulationService : ICutLabSimulationService
{
    private static readonly IReadOnlyDictionary<string, int> EmptyFloors = new Dictionary<string, int>();

    private readonly CutLabResolvedCardCache _resolvedCardCache;
    private readonly CutLabDeltaCache _deltaCache;
    private readonly IScryfallCardResolver _resolver;
    private readonly ILogger<CutLabSimulationService> _logger;
    private readonly Func<IReadOnlyList<DeckCardEntry>, string?, int?, CutLabMetricSnapshot> _snapshotBuilder;

    /// <summary>Creates a new <see cref="CutLabSimulationService"/>.</summary>
    /// <param name="resolvedCardCache">Resolved-card cache for working pools.</param>
    /// <param name="deltaCache">Proposal-delta cache for repeated card renders.</param>
    /// <param name="resolver">Shared Scryfall resolver pipeline.</param>
    /// <param name="logger">Structured logger.</param>
    public CutLabSimulationService(
        CutLabResolvedCardCache resolvedCardCache,
        CutLabDeltaCache deltaCache,
        IScryfallCardResolver resolver,
        ILogger<CutLabSimulationService> logger)
        : this(resolvedCardCache, deltaCache, resolver, logger, BuildSnapshot)
    {
    }

    internal CutLabSimulationService(
        CutLabResolvedCardCache resolvedCardCache,
        CutLabDeltaCache deltaCache,
        IScryfallCardResolver resolver,
        ILogger<CutLabSimulationService> logger,
        Func<IReadOnlyList<DeckCardEntry>, string?, int?, CutLabMetricSnapshot> snapshotBuilder)
    {
        _resolvedCardCache = resolvedCardCache ?? throw new ArgumentNullException(nameof(resolvedCardCache));
        _deltaCache = deltaCache ?? throw new ArgumentNullException(nameof(deltaCache));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
    }

    /// <inheritdoc />
    public async Task<CutLabMetricSnapshot> BuildSnapshot(
        IReadOnlyList<CutLabPoolCard> workingList,
        string? playExperience,
        int? trialsOverride = ICutLabSimulationService.InLoopTrials,
        string? poolKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingList);

        return await GetOrBuildSnapshot(workingList, playExperience, trialsOverride, cancellationToken, poolKey).ConfigureAwait(false);
    }

    private static CutLabMetricSnapshot BuildSnapshot(
        IReadOnlyList<DeckCardEntry> deckEntries,
        string? playExperience,
        int? trialsOverride)
    {
        ManabaseMode mode = CutLabRoleAssigner.ResolveMode(playExperience);
        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
        ManabaseDeck deck = ManabaseClassifier.Classify(
            facts,
            isSingleton: true,
            rampCreditV2: true,
            landRampSim: true,
            payLifeUntapped: true,
            checkLandUntapped: true,
            restrictedLands: true);
        deck = TagPlanRoles(deck, facts, mode);
        CedhLandContext cedhContext = mode == ManabaseMode.Cedh
            ? new CedhLandContext(null, 0, Enabled: true)
            : CedhLandContext.Disabled;

        // Why: 103-01 measured 5,164 ms at DefaultTrials on a 147-card pool. In-loop Cut Lab deltas
        // therefore default to 4,000 trials here, while callers can pass null for full-fidelity runs.
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck,
            mode,
            useManaQuantity: true,
            colorAwareMulligan: true,
            gateRampOnCastable: true,
            ritualBurst: true,
            ritualLandCredit: true,
            scryCredit: true,
            colorlessSnow: true,
            keepShapes: true,
            interactionLens: mode == ManabaseMode.Cedh,
            useHealthBandCastability: true,
            useHealthBandHeadlineFloor: true,
            cedhContext: cedhContext,
            trialsOverride: trialsOverride);

        IReadOnlyList<CutLabMetricValue> metrics = BuildMetrics(report, deck, facts, mode);
        return new CutLabMetricSnapshot { Metrics = metrics };
    }

    /// <inheritdoc />
    public async Task<CutLabProposalDeltas> ComputeProposalDeltas(
        IReadOnlyList<CutLabPoolCard> currentWorkingList,
        string candidateCardName,
        string? playExperience,
        int? trialsOverride = ICutLabSimulationService.InLoopTrials,
        string? poolKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentWorkingList);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateCardName);

        string currentPoolKey = poolKey ?? CutLabResolvedCardCache.ComputePoolKey(currentWorkingList);
        if (_deltaCache.TryGet(currentPoolKey, candidateCardName, out CutLabProposalDeltas? cached, trialsOverride) && cached is not null)
        {
            return cached;
        }

        IReadOnlyList<DeckCardEntry> beforeEntries = await ResolveDeckEntries(currentWorkingList, currentPoolKey, cancellationToken).ConfigureAwait(false);
        CutLabMetricSnapshot before = GetOrBuildSnapshot(beforeEntries, currentPoolKey, playExperience, trialsOverride);
        IReadOnlyList<DeckCardEntry> afterEntries = RemoveCandidate(beforeEntries, candidateCardName);
        string afterPoolKey = CutLabResolvedCardCache.ComputePoolKey(afterEntries.Select(entry => (entry.Card.Name, entry.Quantity)).ToArray());
        CutLabMetricSnapshot after = GetOrBuildSnapshot(afterEntries, afterPoolKey, playExperience, trialsOverride);

        IReadOnlyDictionary<CutLabMetricKind, CutLabMetricValue> afterMetrics = after.Metrics
            .ToDictionary(metric => metric.Kind);
        IReadOnlyList<CutLabMetricDelta> deltas = before.Metrics
            .Select(metric => afterMetrics.TryGetValue(metric.Kind, out CutLabMetricValue? afterMetric)
                ? CutLabMetricDelta.Between(metric, afterMetric)
                : null)
            .Where(delta => delta is not null)
            .Cast<CutLabMetricDelta>()
            .ToArray();

        CutLabProposalDeltas computed = new()
        {
            CardName = candidateCardName,
            Deltas = deltas,
            ChangedFamilyCount = deltas.Where(delta => delta.IsMeaningful).Select(delta => delta.Family).Distinct().Count(),
        };

        _deltaCache.Set(currentPoolKey, candidateCardName, computed, trialsOverride);
        return computed;
    }

    private CutLabMetricSnapshot GetOrBuildSnapshot(
        IReadOnlyList<DeckCardEntry> deckEntries,
        string poolKey,
        string? playExperience,
        int? trialsOverride)
    {
        if (_deltaCache.TryGetSnapshot(poolKey, playExperience, trialsOverride, out CutLabMetricSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        CutLabMetricSnapshot snapshot = _snapshotBuilder(deckEntries, playExperience, trialsOverride);
        _deltaCache.SetSnapshot(poolKey, playExperience, trialsOverride, snapshot);
        return snapshot;
    }

    private async Task<CutLabMetricSnapshot> GetOrBuildSnapshot(
        IReadOnlyList<CutLabPoolCard> workingList,
        string? playExperience,
        int? trialsOverride,
        CancellationToken cancellationToken,
        string? poolKeyOverride = null)
    {
        string poolKey = poolKeyOverride ?? CutLabResolvedCardCache.ComputePoolKey(workingList);
        IReadOnlyList<DeckCardEntry> deckEntries = await ResolveDeckEntries(workingList, poolKey, cancellationToken).ConfigureAwait(false);
        return GetOrBuildSnapshot(deckEntries, poolKey, playExperience, trialsOverride);
    }

    private async Task<IReadOnlyList<DeckCardEntry>> ResolveDeckEntries(
        IReadOnlyList<CutLabPoolCard> workingList,
        string poolKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ScryfallCardData>? cachedCards;
        IReadOnlyList<ScryfallCardData> cards;
        if (!_resolvedCardCache.TryGet(poolKey, out cachedCards) || cachedCards is null)
        {
            var resolvedCards = new List<ScryfallCardData>(workingList.Count);
            foreach (CutLabPoolCard poolCard in workingList)
            {
                ScryfallCard? resolved = await _resolver.ResolveSingleAsync(poolCard.Name, cancellationToken).ConfigureAwait(false);
                if (resolved is null)
                {
                    throw new InvalidOperationException($"Cut Lab simulation could not resolve '{poolCard.Name}'.");
                }

                resolvedCards.Add(ScryfallCardDataMapper.ToCardData(resolved));
            }

            cards = resolvedCards;
            _resolvedCardCache.Set(poolKey, cards);
            _logger.LogInformation("Resolved {CardCount} Cut Lab cards for snapshot pool {PoolKey}.", cards.Count, poolKey);
        }
        else
        {
            IReadOnlyDictionary<string, ScryfallCardData> cardsByName = CutLabCardNames.ToLastWinsDictionary(
                cachedCards,
                card => card.Name,
                card => card);
            cards = workingList
                .Select(poolCard =>
                {
                    string normalizedName = CutLabCardNames.Normalize(poolCard.Name);
                    return cardsByName.TryGetValue(normalizedName, out ScryfallCardData? card)
                    ? card
                    : throw new InvalidOperationException($"Cut Lab resolved-card cache was missing '{poolCard.Name}' for pool {poolKey}.");
                })
                .ToArray();
        }

        return workingList
            .Zip(
                cards,
                (poolCard, card) => new DeckCardEntry
                {
                    Card = card,
                    Quantity = poolCard.Quantity,
                    IsCommander = poolCard.IsCommander,
                })
            .ToArray();
    }

    private static IReadOnlyList<CutLabMetricValue> BuildMetrics(
        ManabaseReport report,
        ManabaseDeck deck,
        IReadOnlyList<CardFact> facts,
        ManabaseMode mode)
    {
        CardCastability? commander = report.Castability.FirstOrDefault(row => row.IsCommander);
        CardCastability[] engineRows = PlanRows(deck, report, PlanRole.Engine);
        CardCastability[] lineRows = PlanRows(deck, report, PlanRole.Engine | PlanRole.Payoff | PlanRole.TutorCombo | PlanRole.Interaction);
        CutLabMetricValue[] metrics =
        [
            Metric(CutLabMetricKind.CommanderOnTime, CutLabMetricFamily.CommanderOnTime, "Commander on time", commander?.CastPercent ?? 0, CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.KeepableHand, CutLabMetricFamily.KeepableHand, "Keepable hand", report.MulliganEvaluation?.KeepableHandPercent ?? 0, CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.ManaColorReliability, CutLabMetricFamily.ManaColorReliability, "Mana and color reliability", report.ColorFindings.FirstOrDefault()?.AverageCastPercent ?? 0, CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.EarlyInteraction, CutLabMetricFamily.EarlyInteraction, "Early interaction", EarlyInteractionValue(report.InteractionLens), CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.PlanPresence, CutLabMetricFamily.PlanPresence, "Plan presence", report.MulliganEvaluation?.PlanPresence?.PlanPresencePercent ?? 0, CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, "Commander by turn 3", PercentByTurn(commander, CutLabCategoryByTurnDefaults.CommanderByTurn), CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.EngineByTurn, CutLabMetricFamily.CategoryByTurn, "Engine by turn 2", MaxPercentByTurn(engineRows, CutLabCategoryByTurnDefaults.EngineByTurn), CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.RepresentativeLineByTurn, CutLabMetricFamily.CategoryByTurn, "Representative line by turn 4", MaxPercentByTurn(lineRows, CutLabCategoryByTurnDefaults.RepresentativeLineByTurn), CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.Flood, CutLabMetricFamily.FloodScrewCurveRisk, "Flood", Math.Max(0, report.LandDelta), CutLabMetricUnit.Cards),
            Metric(CutLabMetricKind.Screw, CutLabMetricFamily.FloodScrewCurveRisk, "Screw", report.MulliganEvaluation?.MulliganTo5Percent ?? 0, CutLabMetricUnit.Percent),
            Metric(CutLabMetricKind.Curve, CutLabMetricFamily.FloodScrewCurveRisk, "Curve", CurveCongestionValue(facts, mode), CutLabMetricUnit.Cards),
        ];

        return metrics.Where(metric => double.IsFinite(metric.Value)).ToArray();
    }

    private static CutLabMetricValue Metric(
        CutLabMetricKind kind,
        CutLabMetricFamily family,
        string label,
        double value,
        CutLabMetricUnit unit)
        => new()
        {
            Kind = kind,
            Family = family,
            Label = label,
            Value = value,
            Unit = unit,
        };

    private static IReadOnlyList<DeckCardEntry> RemoveCandidate(
        IReadOnlyList<DeckCardEntry> currentEntries,
        string candidateCardName)
    {
        string normalizedCandidateName = CutLabCardNames.Normalize(candidateCardName);
        List<DeckCardEntry> remainingEntries = new(currentEntries.Count);
        foreach (DeckCardEntry entry in currentEntries)
        {
            if (string.Equals(CutLabCardNames.Normalize(entry.Card.Name), normalizedCandidateName, StringComparison.Ordinal))
            {
                remainingEntries.AddRange(currentEntries.Skip(remainingEntries.Count + 1));
                return remainingEntries;
            }

            remainingEntries.Add(entry);
        }

        return remainingEntries;
    }

    private static ManabaseDeck TagPlanRoles(
        ManabaseDeck deck,
        IReadOnlyList<CardFact> facts,
        ManabaseMode mode)
    {
        IReadOnlyDictionary<string, CardFact> factsByName = CutLabCardNames.ToLastWinsDictionary(
            facts,
            fact => fact.Name,
            fact => fact);
        return deck with
        {
            Spells = deck.Spells
                .Select(spell =>
                {
                    if (!factsByName.TryGetValue(CutLabCardNames.Normalize(spell.Name), out CardFact? fact))
                    {
                        return spell;
                    }

                    // Why: this service is intentionally scoped to resolver/cache/logger dependencies.
                    // With no category or Spellbook sources available, mirror ManabaseAnalysisService's
                    // heuristic fallback tier and leave the richer upstream sources fail-open.
                    PlanRole roles = PlanRoleClassifier.Classify(fact, [], false, mode, out bool interactionMeritPreGate);
                    return spell with { PlanRoles = roles, IsInteractionSpell = interactionMeritPreGate };
                })
                .ToArray(),
        };
    }

    private static CardCastability[] PlanRows(ManabaseDeck deck, ManabaseReport report, PlanRole roles)
    {
        var rowsByName = report.Castability.ToDictionary(row => row.Name, StringComparer.OrdinalIgnoreCase);
        return deck.Spells
            .Where(spell => (spell.PlanRoles & roles) != 0)
            .Select(spell => rowsByName.TryGetValue(spell.Name, out CardCastability? row) ? row : null)
            .Where(row => row is not null)
            .Cast<CardCastability>()
            .ToArray();
    }

    private static double EarlyInteractionValue(ManabaseInteractionLens? lens)
    {
        if (lens is null)
        {
            return double.NegativeInfinity;
        }

        return lens.QualifyingCount == 0
            ? 0
            : Math.Round((100.0 * lens.OnTargetCount) / lens.QualifyingCount, 1);
    }

    private static double MaxPercentByTurn(IReadOnlyList<CardCastability> rows, int turn)
        => rows.Count == 0 ? 0 : rows.Max(row => PercentByTurn(row, turn));

    private static double PercentByTurn(CardCastability? row, int turn)
    {
        if (row is null)
        {
            return 0;
        }

        if (row.EarlyCastPercents.Count == 0)
        {
            return turn >= row.OnCurveTurn ? row.CastPercent : 0;
        }

        int index = Math.Clamp(turn - 1, 0, row.EarlyCastPercents.Count - 1);
        return row.EarlyCastPercents[index];
    }

    private static double CurveCongestionValue(IReadOnlyList<CardFact> facts, ManabaseMode mode)
    {
        CutLabAnalyzedCard[] analyzedCards = facts
            .Select(fact => new CutLabAnalyzedCard(
                fact.Name,
                fact.ManaValue,
                CutLabLockRules.IsLand(fact.TypeLine),
                CutLabRoleAssigner.AssignRoles(fact, [], false, mode),
                [])
            {
                Quantity = fact.Quantity,
            })
            .ToArray();

        CutLabStructuralFindingsResult findings = CutLabStructuralFindings.Compute(
            analyzedCards,
            [],
            EmptyFloors,
            comboDataAvailable: false,
            categoryDataAvailable: false);

        return findings.Findings
            .Where(finding => finding.Kind == CutLabFindingKind.CurveCongestion)
            .Select(finding => (double)finding.Evidence.Count)
            .DefaultIfEmpty(0)
            .Max();
    }
}
