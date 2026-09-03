using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Describes how strongly a pool card supports the checked plan profile.
/// </summary>
public sealed record CutLabPlanAffinity(
    IReadOnlyList<string> OnPlanThemes,
    IReadOnlyList<string> OffPlanThemes,
    IReadOnlyList<string> OnPlanStrategies,
    int Score)
{
    /// <summary>Gets whether this card matches at least one checked plan signal.</summary>
    public bool IsOnPlan => Score > 0;

    /// <summary>Gets the affinity used when no plan evidence applies.</summary>
    public static CutLabPlanAffinity Neutral { get; } = new([], [], [], 0);
}

/// <summary>
/// Resolves checked strategy and commander-theme membership for a Cut Lab pool.
/// </summary>
public static class CutLabPlanAffinityResolver
{
    // Why: The cap bounds how far a heavily-on-plan card can be pushed toward the back of the proposal queue, so a card matching every checked box cannot outrank the combo-protection demotion that must stay the dominant key (see CutLabCutRoundEngine.ComboProtectionRank). Three is the smallest value that still distinguishes "matches one box", "matches two", and "matches three or more".
    internal const int OnPlanScoreCap = 3;

    /// <summary>Resolves plan affinity for every card in <paramref name="pool"/>.</summary>
    public static IReadOnlyDictionary<string, CutLabPlanAffinity> ResolveAll(
        IReadOnlyList<CutLabAnalyzedCard> pool,
        CutLabPlanProfile? planProfile,
        IReadOnlyDictionary<string, IReadOnlyList<string>> themeCardNamesBySlug,
        IReadOnlyList<CutLabCommanderTheme> allKnownThemes)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(themeCardNamesBySlug);
        ArgumentNullException.ThrowIfNull(allKnownThemes);

        if (planProfile is null || (planProfile.GenericStrategies.Count == 0 && planProfile.CommanderThemes.Count == 0))
        {
            return CutLabCardNames.ToLastWinsDictionary(pool, card => card.Name, _ => CutLabPlanAffinity.Neutral);
        }

        Dictionary<string, HashSet<string>> normalizedThemeCards = BuildThemeCardIndex(themeCardNamesBySlug);
        HashSet<string> checkedThemeSlugs = new(planProfile.CommanderThemes.Select(theme => theme.Slug), StringComparer.OrdinalIgnoreCase);
        HashSet<string> checkedStrategySlugs = new(planProfile.GenericStrategies, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<CutLabCommanderTheme> offPlanThemes = allKnownThemes
            .Where(theme => !checkedThemeSlugs.Contains(theme.Slug))
            .ToArray();
        IReadOnlyList<DeckPlanStrategyEntry> checkedStrategies = DeckPlanStrategyCatalog.Entries
            .Where(strategy => checkedStrategySlugs.Contains(strategy.Slug))
            .ToArray();

        return CutLabCardNames.ToLastWinsDictionary(pool, card => card.Name, card => ResolveCard(
            card,
            planProfile.CommanderThemes,
            offPlanThemes,
            checkedStrategies,
            normalizedThemeCards));
    }

    /// <summary>Gets a card's affinity, returning neutral when it is absent from the resolved pool.</summary>
    public static CutLabPlanAffinity For(IReadOnlyDictionary<string, CutLabPlanAffinity> affinities, string cardName)
    {
        ArgumentNullException.ThrowIfNull(affinities);
        ArgumentNullException.ThrowIfNull(cardName);

        return affinities.TryGetValue(CutLabCardNames.Normalize(cardName), out CutLabPlanAffinity? affinity)
            ? affinity
            : CutLabPlanAffinity.Neutral;
    }

    private static Dictionary<string, HashSet<string>> BuildThemeCardIndex(IReadOnlyDictionary<string, IReadOnlyList<string>> themeCardNamesBySlug)
    {
        Dictionary<string, HashSet<string>> index = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string slug, IReadOnlyList<string> names) in themeCardNamesBySlug)
        {
            index[slug] = new HashSet<string>(names.Select(CutLabCardNames.Normalize), CutLabCardNames.Comparer);
        }

        return index;
    }

    private static CutLabPlanAffinity ResolveCard(
        CutLabAnalyzedCard card,
        IReadOnlyList<CutLabCommanderTheme> checkedThemes,
        IReadOnlyList<CutLabCommanderTheme> offPlanThemes,
        IReadOnlyList<DeckPlanStrategyEntry> checkedStrategies,
        IReadOnlyDictionary<string, HashSet<string>> normalizedThemeCards)
    {
        string normalizedName = CutLabCardNames.Normalize(card.Name);
        string[] onPlanThemes = checkedThemes
            .Where(theme => IsThemeMember(theme.Slug, normalizedName, normalizedThemeCards))
            .Select(theme => theme.DisplayName)
            .ToArray();
        string[] offPlanThemeNames = offPlanThemes
            .Where(theme => IsThemeMember(theme.Slug, normalizedName, normalizedThemeCards))
            .Select(theme => theme.DisplayName)
            .ToArray();
        string[] onPlanStrategies = checkedStrategies
            .Where(strategy => DeckPlanStrategyCatalog.MatchesCategories(strategy, card.Categories))
            .Select(strategy => strategy.DisplayName)
            .ToArray();
        int score = Math.Min(onPlanThemes.Length + onPlanStrategies.Length, OnPlanScoreCap);

        return score == 0 && offPlanThemeNames.Length == 0
            ? CutLabPlanAffinity.Neutral
            : new CutLabPlanAffinity(onPlanThemes, offPlanThemeNames, onPlanStrategies, score);
    }

    private static bool IsThemeMember(string slug, string normalizedName, IReadOnlyDictionary<string, HashSet<string>> normalizedThemeCards) =>
        normalizedThemeCards.TryGetValue(slug, out HashSet<string>? names) && names.Contains(normalizedName);
}
