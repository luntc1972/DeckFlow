using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds side-effect-free Cut Lab what-if swaps for preview and commit flows.</summary>
public interface ICutLabWhatifService
{
    /// <summary>Computes the metric deltas for swapping one working-list card with one cut-pile card.</summary>
    /// <param name="state">Current Cut Lab state.</param>
    /// <param name="cardOut">Working-list card to remove.</param>
    /// <param name="cardIn">Cut-pile card to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview deltas for the hypothetical swap.</returns>
    Task<CutLabWhatifPreview> PreviewSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken);

    /// <summary>Validates a what-if swap pair without mutating state.</summary>
    /// <param name="state">Current Cut Lab state.</param>
    /// <param name="cardOut">Working-list card to remove.</param>
    /// <param name="cardIn">Cut-pile card to restore.</param>
    /// <param name="error">Validation error when the pair is rejected.</param>
    /// <returns><see langword="true"/> when the swap pair is valid; otherwise <see langword="false"/>.</returns>
    bool TryValidateSwap(CutLabState state, string cardOut, string cardIn, out string? error);

    /// <summary>Validates and atomically applies a what-if swap.</summary>
    /// <param name="state">Current Cut Lab state.</param>
    /// <param name="cardOut">Working-list card to remove.</param>
    /// <param name="cardIn">Cut-pile card to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The swap result.</returns>
    Task<CutLabWhatifCommitResult> CommitSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken);
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

/// <summary>Result of attempting to commit a Cut Lab what-if swap.</summary>
public sealed record CutLabWhatifCommitResult
{
    /// <summary>Whether the swap was applied.</summary>
    public bool Applied { get; init; }

    /// <summary>The resulting Cut Lab state.</summary>
    public required CutLabState State { get; init; }

    /// <summary>The working-list card removed by the swap.</summary>
    public string? CardOut { get; init; }

    /// <summary>The cut-pile card restored by the swap.</summary>
    public string? CardIn { get; init; }

    /// <summary>The error message when the swap was rejected.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Default server-side implementation for Cut Lab what-if preview and commit flows.</summary>
public sealed class CutLabWhatifService : ICutLabWhatifService
{
    private readonly ICutLabSimulationService _simulationService;
    private readonly ICutLabAnalysisContextBuilder _contextBuilder;
    private readonly CutLabResolvedCardCache _resolvedCardCache;

    /// <summary>Creates a new <see cref="CutLabWhatifService"/>.</summary>
    /// <param name="simulationService">Simulation service used to build before/after snapshots.</param>
    /// <param name="contextBuilder">Context builder that exposes the full-pool resolved superset cache.</param>
    /// <param name="resolvedCardCache">Shared resolved-card cache used by the simulation service.</param>
    public CutLabWhatifService(
        ICutLabSimulationService simulationService,
        ICutLabAnalysisContextBuilder contextBuilder,
        CutLabResolvedCardCache resolvedCardCache)
    {
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _resolvedCardCache = resolvedCardCache ?? throw new ArgumentNullException(nameof(resolvedCardCache));
    }

    /// <inheritdoc />
    public async Task<CutLabWhatifPreview> PreviewSwapAsync(
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

        CutLabMetricDeltaSet metricDeltaSet = CutLabMetricDeltaSet.From(before.Metrics, after.Metrics);

        return new CutLabWhatifPreview
        {
            CardOut = cardOutPoolCard.Name,
            CardIn = cardInPoolCard.Name,
            Deltas = metricDeltaSet.Deltas,
            ChangedFamilyCount = metricDeltaSet.ChangedFamilyCount,
        };
    }

    /// <inheritdoc />
    public bool TryValidateSwap(CutLabState state, string cardOut, string cardIn, out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardOut);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardIn);

        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments);
        CutLabPoolCard? cardOutPoolCard = workingList.FirstOrDefault(card =>
            string.Equals(card.Name, cardOut, StringComparison.OrdinalIgnoreCase));
        if (cardOutPoolCard is null || cardOutPoolCard.IsLocked || cardOutPoolCard.IsCommander)
        {
            error = CutLabMessages.NoChangeMessage;
            return false;
        }

        IReadOnlySet<string> cutPile = CutLabWorkingList.AcceptedCardNames(state.Decisions);
        bool validCardIn = state.Pool.Any(card =>
            string.Equals(card.Name, cardIn, StringComparison.OrdinalIgnoreCase)
            && cutPile.Contains(card.Name));
        if (!validCardIn)
        {
            error = CutLabMessages.NoChangeMessage;
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public Task<CutLabWhatifCommitResult> CommitSwapAsync(
        CutLabState state,
        string cardOut,
        string cardIn,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardOut);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardIn);

        if (!TryValidateSwap(state, cardOut, cardIn, out string? error))
        {
            return Task.FromResult(new CutLabWhatifCommitResult
            {
                Applied = false,
                State = state,
                ErrorMessage = error ?? CutLabMessages.NoChangeMessage,
            });
        }

        CutLabState afterRestore = CutLabDecisionApplier.Apply(
            state,
            cardIn,
            CutLabDecideAction.Restore,
            CutLabCutRoundEngine.WhatifSwapKey);
        CutLabState afterSwap = CutLabDecisionApplier.Apply(
            afterRestore,
            cardOut,
            CutLabDecideAction.Accept,
            CutLabCutRoundEngine.WhatifSwapKey);
        if (afterSwap.Decisions.Count == afterRestore.Decisions.Count)
        {
            return Task.FromResult(new CutLabWhatifCommitResult
            {
                Applied = false,
                State = state,
                ErrorMessage = CutLabMessages.NoChangeMessage,
            });
        }

        return Task.FromResult(new CutLabWhatifCommitResult
        {
            Applied = true,
            State = afterSwap,
            CardOut = cardOut,
            CardIn = cardIn,
        });
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
