using DeckFlow.Web.Models.CutLab;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds the plan-affinity map shared by every Cut Lab engine entry point.</summary>
public interface ICutLabPlanAffinityFactory
{
    /// <summary>
    /// Builds the plan-affinity map for the current pool, fetching bounded validated EDHREC theme
    /// memberships as needed. Returns <see langword="null"/> when there is nothing to resolve.
    /// </summary>
    /// <param name="planProfile">The user's checked plan profile, or <see langword="null"/>.</param>
    /// <param name="analyzedCards">The analyzed pool to resolve affinity for.</param>
    /// <param name="commanderNames">Resolved commander names for the current session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, CutLabPlanAffinity>?> BuildAsync(
        CutLabPlanProfile? planProfile,
        IReadOnlyList<CutLabAnalyzedCard> analyzedCards,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default plan-affinity factory. Fetches the checked and a bounded set of unchecked EDHREC theme
/// memberships, then delegates the actual per-card resolution to
/// <see cref="CutLabPlanAffinityResolver"/> so the page, the AJAX patch, and the decide API all
/// agree on the same affinity map for the same inputs.
/// </summary>
public sealed class CutLabPlanAffinityFactory : ICutLabPlanAffinityFactory
{
    // Why: bounds checked-theme requests independently from the unchecked-theme probe fill below,
    // so a single checked theme cannot trigger a dozen additional off-plan-probe fetches (CR-03/IN-05).
    internal const int MaxCheckedThemeFetches = 12;

    // Why: WR-07's off-plan-package detector only needs a representative sample of unchecked themes,
    // not the checked cap's full budget -- this stays deliberately small.
    internal const int MaxOffPlanProbeFetches = 3;

    // Why: theme fetches are bounded in count but not time; this prevents a cold EDHREC cache
    // from holding a Cut Lab request open for the cumulative duration of every sequential fetch.
    internal static readonly TimeSpan TotalThemeFetchBudget = TimeSpan.FromSeconds(20);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyThemeCardsBySlug =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<CutLabCommanderTheme> EmptyThemes = [];

    private readonly IEdhrecCommanderThemeService _themeService;
    private readonly ILogger<CutLabPlanAffinityFactory>? _logger;

    /// <summary>Creates the plan-affinity factory.</summary>
    /// <param name="themeService">EDHREC commander-theme source used to fetch checked/unchecked theme memberships.</param>
    /// <param name="logger">Optional logger for non-blocking diagnostics.</param>
    public CutLabPlanAffinityFactory(IEdhrecCommanderThemeService themeService, ILogger<CutLabPlanAffinityFactory>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(themeService);

        _themeService = themeService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, CutLabPlanAffinity>?> BuildAsync(
        CutLabPlanProfile? planProfile,
        IReadOnlyList<CutLabAnalyzedCard> analyzedCards,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analyzedCards);
        ArgumentNullException.ThrowIfNull(commanderNames);
        cancellationToken.ThrowIfCancellationRequested();

        if (planProfile is null || (planProfile.GenericStrategies.Count == 0 && planProfile.CommanderThemes.Count == 0))
        {
            return null;
        }

        if (planProfile.CommanderThemes.Count == 0)
        {
            // Strategies-only profile: no theme evidence needed, so no EDHREC request is issued.
            return CutLabPlanAffinityResolver.ResolveAll(analyzedCards, planProfile, EmptyThemeCardsBySlug, EmptyThemes);
        }

        string? commanderName = commanderNames.Count > 0 ? commanderNames[0] : null;
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            // No commander to query EDHREC against: degrade to the strategy layer alone.
            return CutLabPlanAffinityResolver.ResolveAll(analyzedCards, planProfile, EmptyThemeCardsBySlug, EmptyThemes);
        }

        EdhrecThemeResult themeResult = await FetchCommanderThemesAsync(commanderName, cancellationToken).ConfigureAwait(false);
        if (themeResult.IsUnavailable || themeResult.Themes.Count == 0)
        {
            // Fail-open: the strategy layer still resolves, and there is no known-theme list to
            // validate checked slugs against, so no card is labelled off-plan for a theme the user
            // was never shown.
            return CutLabPlanAffinityResolver.ResolveAll(analyzedCards, planProfile, EmptyThemeCardsBySlug, EmptyThemes);
        }

        IReadOnlyList<string> boundedSlugs = BuildBoundedSlugs(planProfile.CommanderThemes, themeResult.Themes);
        Dictionary<string, IReadOnlyList<string>> themeCardNamesBySlug = new(StringComparer.OrdinalIgnoreCase);
        using CancellationTokenSource themeFetchBudgetCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        themeFetchBudgetCancellation.CancelAfter(TotalThemeFetchBudget);
        foreach (string slug in boundedSlugs)
        {
            if (themeFetchBudgetCancellation.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }

            try
            {
                themeCardNamesBySlug[slug] = await FetchThemeCardNamesAsync(commanderName, slug, themeFetchBudgetCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (themeFetchBudgetCancellation.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }
        }

        return CutLabPlanAffinityResolver.ResolveAll(analyzedCards, planProfile, themeCardNamesBySlug, themeResult.Themes);
    }

    // Why: checked slugs are validated first (against the known-theme list) and prioritized so a
    // truncation always keeps the on-plan classification the user directly asked for; unchecked
    // known slugs fill any remaining budget so the off-plan detector (PLPR-06) still has membership
    // data to work with.
    private static IReadOnlyList<string> BuildBoundedSlugs(
        IReadOnlyList<CutLabCommanderTheme> checkedThemes,
        IReadOnlyList<CutLabCommanderTheme> allKnownThemes)
    {
        HashSet<string> knownSlugs = new(allKnownThemes.Select(theme => theme.Slug), StringComparer.OrdinalIgnoreCase);
        List<string> bounded = new(MaxCheckedThemeFetches + MaxOffPlanProbeFetches);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string slug in checkedThemes.Select(theme => theme.Slug))
        {
            if (bounded.Count >= MaxCheckedThemeFetches)
            {
                return bounded;
            }

            if (knownSlugs.Contains(slug) && seen.Add(slug))
            {
                bounded.Add(slug);
            }
        }

        if (bounded.Count == 0)
        {
            return bounded;
        }

        var offPlanProbeCount = 0;
        foreach (string slug in allKnownThemes.Select(theme => theme.Slug))
        {
            if (offPlanProbeCount >= MaxOffPlanProbeFetches)
            {
                return bounded;
            }

            if (seen.Add(slug))
            {
                bounded.Add(slug);
                offPlanProbeCount++;
            }
        }

        return bounded;
    }

    private async Task<EdhrecThemeResult> FetchCommanderThemesAsync(string commanderName, CancellationToken cancellationToken)
    {
        try
        {
            return await _themeService.GetCommanderThemesAsync(commanderName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Cut Lab: EDHREC commander theme list fetch failed for {CommanderName}", commanderName);
            return new EdhrecThemeResult([], true);
        }
    }

    private async Task<IReadOnlyList<string>> FetchThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken)
    {
        try
        {
            return await _themeService.GetThemeCardNamesAsync(commanderName, themeSlug, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Cut Lab: EDHREC theme card fetch failed for {ThemeSlug}", themeSlug);
            return [];
        }
    }
}

/// <summary>
/// Null-object fallback used by the page, patch and decide-API constructors when no
/// <see cref="ICutLabPlanAffinityFactory"/> is supplied (e.g. direct-construction tests). Always
/// resolves to <see langword="null"/>, matching the "no plan profile" no-op branch.
/// </summary>
internal sealed class NullCutLabPlanAffinityFactory : ICutLabPlanAffinityFactory
{
    public static NullCutLabPlanAffinityFactory Instance { get; } = new();

    public Task<IReadOnlyDictionary<string, CutLabPlanAffinity>?> BuildAsync(
        CutLabPlanProfile? planProfile,
        IReadOnlyList<CutLabAnalyzedCard> analyzedCards,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, CutLabPlanAffinity>?>(null);
}
