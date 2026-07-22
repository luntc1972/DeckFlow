using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds side-effect-free Cut Lab what-if swap previews.</summary>
public interface ICutLabWhatifPreviewService
{
    /// <summary>Computes the metric deltas for swapping one working-list card with one cut-pile card.</summary>
    /// <param name="state">Current Cut Lab state.</param>
    /// <param name="cardOut">Working-list card to remove.</param>
    /// <param name="cardIn">Cut-pile card to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview deltas for the hypothetical swap.</returns>
    Task<CutLabWhatifPreview> ComputeSwapPreviewAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken);
}

/// <summary>Computed preview payload for a hypothetical Cut Lab swap.</summary>
public sealed record CutLabWhatifPreview
{
    /// <summary>All granular metric deltas for the hypothetical swap.</summary>
    public IReadOnlyList<CutLabMetricDelta> Deltas { get; init; } = [];

    /// <summary>How many metric families changed meaningfully.</summary>
    public int ChangedFamilyCount { get; init; }

    /// <summary>The working-list card removed by the swap.</summary>
    public string CardOut { get; init; } = string.Empty;

    /// <summary>The cut-pile card restored by the swap.</summary>
    public string CardIn { get; init; } = string.Empty;
}

/// <summary>Default server-side implementation for Cut Lab what-if swap previews.</summary>
public sealed class CutLabWhatifPreviewService : ICutLabWhatifPreviewService
{
    private readonly ICutLabSimulationService _simulationService;
    private readonly ICutLabAnalysisContextBuilder _contextBuilder;
    private readonly CutLabResolvedCardCache _resolvedCardCache;

    /// <summary>Creates a new <see cref="CutLabWhatifPreviewService"/>.</summary>
    /// <param name="simulationService">Simulation service used to build before/after snapshots.</param>
    /// <param name="contextBuilder">Context builder that exposes the full-pool resolved superset cache.</param>
    /// <param name="resolvedCardCache">Shared resolved-card cache used by the simulation service.</param>
    public CutLabWhatifPreviewService(
        ICutLabSimulationService simulationService,
        ICutLabAnalysisContextBuilder contextBuilder,
        CutLabResolvedCardCache resolvedCardCache)
    {
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _resolvedCardCache = resolvedCardCache ?? throw new ArgumentNullException(nameof(resolvedCardCache));
    }

    /// <inheritdoc />
    public async Task<CutLabWhatifPreview> ComputeSwapPreviewAsync(
        CutLabState state,
        string cardOut,
        string cardIn,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardOut);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardIn);

        if (state.Pool.Count == 0)
        {
            throw new InvalidOperationException("Cut Lab what-if preview requires a non-empty pool.");
        }

        IReadOnlyList<CutLabPoolCard> beforeWorkingList = CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments);
        CutLabPoolCard cardOutPoolCard = beforeWorkingList.FirstOrDefault(card => string.Equals(card.Name, cardOut, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        if (cardOutPoolCard.IsLocked)
        {
            throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        }

        IReadOnlySet<string> cutPile = CutLabWorkingList.AcceptedCardNames(state.Decisions);
        if (!cutPile.Contains(cardIn))
        {
            throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        }

        CutLabPoolCard cardInPoolCard = state.Pool.FirstOrDefault(card =>
                string.Equals(card.Name, cardIn, StringComparison.OrdinalIgnoreCase)
                && cutPile.Contains(card.Name))
            ?? throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        IReadOnlyList<CutLabPoolCard> afterWorkingList = beforeWorkingList
            .Where(card => !string.Equals(card.Name, cardOut, StringComparison.OrdinalIgnoreCase))
            .Append(cardInPoolCard)
            .ToArray();

        string beforePoolKey = CutLabResolvedCardCache.ComputePoolKey(beforeWorkingList);
        string afterPoolKey = CutLabResolvedCardCache.ComputePoolKey(afterWorkingList);
        SeedResolvedSnapshotPool(state.Pool, beforeWorkingList, beforePoolKey);
        SeedResolvedSnapshotPool(state.Pool, afterWorkingList, afterPoolKey);

        CutLabMetricSnapshot before = await _simulationService.BuildSnapshot(
            beforeWorkingList,
            state.Intent.PlayExperience,
            ICutLabSimulationService.InLoopTrials,
            beforePoolKey,
            state.Goals,
            cancellationToken).ConfigureAwait(false);
        CutLabMetricSnapshot after = await _simulationService.BuildSnapshot(
            afterWorkingList,
            state.Intent.PlayExperience,
            ICutLabSimulationService.InLoopTrials,
            afterPoolKey,
            state.Goals,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<CutLabMetricKind, CutLabMetricValue> afterMetrics = after.Metrics
            .ToDictionary(metric => metric.Kind);
        IReadOnlyList<CutLabMetricDelta> deltas = before.Metrics
            .Select(metric => afterMetrics.TryGetValue(metric.Kind, out CutLabMetricValue? afterMetric)
                ? CutLabMetricDelta.Between(metric, afterMetric)
                : null)
            .Where(delta => delta is not null)
            .Cast<CutLabMetricDelta>()
            .ToArray();

        return new CutLabWhatifPreview
        {
            CardOut = cardOutPoolCard.Name,
            CardIn = cardInPoolCard.Name,
            Deltas = deltas,
            ChangedFamilyCount = deltas.Where(delta => delta.IsMeaningful).Select(delta => delta.Family).Distinct().Count(),
        };
    }

    private void SeedResolvedSnapshotPool(
        IReadOnlyList<CutLabPoolCard> fullPool,
        IReadOnlyList<CutLabPoolCard> targetPool,
        string targetPoolKey)
    {
        if (!_contextBuilder.TryGetCachedResolvedCards(fullPool, out IReadOnlyList<ScryfallCardData>? fullPoolCards)
            || fullPoolCards is null)
        {
            throw new InvalidOperationException("Cut Lab what-if preview requires the full-pool resolved-card cache.");
        }

        IReadOnlyList<ScryfallCardData> subset = BuildResolvedSubset(targetPool, fullPoolCards);
        int distinctTargetCount = targetPool
            .Select(card => CutLabCardNames.Normalize(card.Name))
            .Distinct(CutLabCardNames.Comparer)
            .Count();
        if (subset.Count != distinctTargetCount)
        {
            throw new InvalidOperationException("Cut Lab what-if preview could not pre-seed all resolved cards.");
        }

        _resolvedCardCache.Set(targetPoolKey, subset);
    }

    private static IReadOnlyList<ScryfallCardData> BuildResolvedSubset(
        IReadOnlyList<CutLabPoolCard> targetPool,
        IReadOnlyList<ScryfallCardData> sourceCards)
    {
        IReadOnlyDictionary<string, ScryfallCardData> sourceByName = CutLabCardNames.ToLastWinsDictionary(
            sourceCards,
            card => card.Name,
            card => card);
        IReadOnlyList<ScryfallCardData> subset = targetPool
            .Select(card => sourceByName.TryGetValue(CutLabCardNames.Normalize(card.Name), out ScryfallCardData? resolvedCard) ? resolvedCard : null)
            .Where(card => card is not null)
            .Cast<ScryfallCardData>()
            .DistinctBy(card => CutLabCardNames.Normalize(card.Name))
            .ToArray();
        return CutLabAnalysisContextBuilder
            .AugmentResolvedCardsWithSyntheticBasics(targetPool, subset)
            .DistinctBy(card => CutLabCardNames.Normalize(card.Name))
            .ToArray();
    }
}
