namespace DeckFlow.Core.Manabase;

/// <summary>
/// Applies density-graded manabase-only adjustments for commander-legal conditional Moxen after the
/// base classifier has already built source rows and the generic 0-cost artifact fast-mana count.
/// </summary>
public static class ConditionalMoxHeuristics
{
    /// <summary>The legend density where <c>Mox Amber</c> keeps full untapped fast-mana credit.</summary>
    public const int AmberReliableLegends = 12;

    /// <summary>The legend density where <c>Mox Amber</c> rises from weak to mid reliability.</summary>
    public const int AmberWeakLegends = 6;

    /// <summary>The effective artifact support where <c>Mox Opal</c> keeps full untapped fast-mana credit.</summary>
    public const int OpalReliableArtifacts = 15;

    /// <summary>The effective artifact support where <c>Mox Opal</c> rises from weak to mid reliability.</summary>
    public const int OpalWeakArtifacts = 8;

    private static readonly IReadOnlyList<ManaColor> AllColors = ManabaseColorMask.Wubrg;

    private static readonly Dictionary<string, Func<int, double, IReadOnlyList<ManaColor>, MoxAdjustment>> RuleTable =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mox Amber"] = (legendaryPermanentCount, _, commanderColors) =>
                GradedTier(legendaryPermanentCount, AmberReliableLegends, AmberWeakLegends, commanderColors),
            ["Mox Opal"] = (_, effectiveArtifactSupport, _) =>
                GradedTier(effectiveArtifactSupport, OpalReliableArtifacts, OpalWeakArtifacts, AllColors),
            ["Chrome Mox"] = (_, _, commanderColors) =>
                new MoxAdjustment(commanderColors, EntersUntapped: true, Weight: 0.50, KeepFastMana: false),
            ["Mox Tantalite"] = (_, _, _) =>
                new MoxAdjustment(AllColors, EntersUntapped: false, Weight: 0.50, KeepFastMana: false),
            ["Mox Diamond"] = (_, _, _) =>
                new MoxAdjustment(AllColors, EntersUntapped: true, Weight: 0.75, KeepFastMana: true),
        };

    // A density-graded Mox (Amber by legend count, Opal by artifact support): full untapped
    // fast-mana credit at/above the reliable threshold, a mid weight down to the weak threshold,
    // and a floor weight below it. Only the reliable tier stays untapped and keeps fast mana.
    private static MoxAdjustment GradedTier(
        double density, int reliableThreshold, int weakThreshold, IReadOnlyList<ManaColor> produces)
        => density >= reliableThreshold
            ? new MoxAdjustment(produces, EntersUntapped: true, Weight: 0.75, KeepFastMana: true)
            : new MoxAdjustment(produces, EntersUntapped: false, Weight: density >= weakThreshold ? 0.60 : 0.40, KeepFastMana: false);

    /// <summary>
    /// Rewrites the five commander-legal conditional Mox sources with density-aware color and
    /// reliability heuristics, and removes any fast-mana credit those heuristics no longer justify.
    /// </summary>
    /// <param name="sources">The already-classified manabase sources.</param>
    /// <param name="fastMana">The classifier's current generic 0-cost artifact fast-mana count.</param>
    /// <param name="commanderColorMask">
    /// Bitmask of the commander's colored identity derived from commander spell pips.
    /// </param>
    /// <param name="legendaryPermanentCount">
    /// Count of legendary creatures and legendary planeswalkers in the deck, by quantity.
    /// </param>
    /// <param name="effectiveArtifactSupport">
    /// Effective artifact support <c>Ae = A + 0.5 * Tk</c>, where <c>A</c> is artifact-card count and
    /// <c>Tk</c> is artifact-token-creator count.
    /// </param>
    /// <returns>The adjusted source list and adjusted fast-mana count.</returns>
    public static (IReadOnlyList<ManaSource> sources, int fastMana) Apply(
        IReadOnlyList<ManaSource> sources,
        int fastMana,
        int commanderColorMask,
        int legendaryPermanentCount,
        double effectiveArtifactSupport)
    {
        ArgumentNullException.ThrowIfNull(sources);

        IReadOnlyList<ManaColor> commanderColors = ResolveCommanderColors(commanderColorMask);
        var adjustedSources = new List<ManaSource>(sources.Count);
        int adjustedFastMana = fastMana;

        foreach (ManaSource source in sources)
        {
            if (!RuleTable.TryGetValue(source.Name, out Func<int, double, IReadOnlyList<ManaColor>, MoxAdjustment>? rule))
            {
                adjustedSources.Add(source);
                continue;
            }

            MoxAdjustment adjustment = rule(legendaryPermanentCount, effectiveArtifactSupport, commanderColors);
            adjustedSources.Add(source with
            {
                Produces = adjustment.Produces,
                EntersUntapped = adjustment.EntersUntapped,
                Weight = adjustment.Weight,
            });

            if (!adjustment.KeepFastMana)
            {
                // Invariant: each conditional Mox is a 0-cost artifact the base classifier already
                // counted as exactly +1 fast mana, so removing its credit subtracts one. Max(0, ...)
                // guards against underflow if that upstream accounting ever changes.
                adjustedFastMana = Math.Max(0, adjustedFastMana - 1);
            }
        }

        return (adjustedSources, adjustedFastMana);
    }

    private static IReadOnlyList<ManaColor> ResolveCommanderColors(int commanderColorMask)
    {
        IReadOnlyList<ManaColor> colors = ManabaseColorMask.ColorsFromMask(commanderColorMask);
        return colors.Count > 0 ? colors : AllColors;
    }

    private sealed record MoxAdjustment(
        IReadOnlyList<ManaColor> Produces,
        bool EntersUntapped,
        double Weight,
        bool KeepFastMana);
}
