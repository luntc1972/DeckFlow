using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure immutable working-list derivation rules for the Cut Lab session pool.</summary>
public static class CutLabWorkingList
{
    // Why: HIGH-1 — Pool is immutable; this is the only place the working list is derived, so
    // every consumer agrees and restore is lossless.
    /// <summary>Derives the current working list by excluding cards whose latest decision was accepted.</summary>
    /// <param name="pool">Immutable session pool.</param>
    /// <param name="decisions">Persisted decision log.</param>
    /// <returns>The pool cards whose latest decision is not accepted.</returns>
    public static IReadOnlyList<CutLabPoolCard> Derive(IReadOnlyList<CutLabPoolCard> pool, IReadOnlyList<CutLabDecision> decisions)
        => Derive(pool, decisions, []);

    /// <summary>Derives the current working list by applying decisions first, then quantity adjustments.</summary>
    /// <param name="pool">Immutable session pool.</param>
    /// <param name="decisions">Persisted decision log.</param>
    /// <param name="adjustments">Persisted copy-delta adjustments keyed by card name.</param>
    /// <returns>The working-list cards after whole-entry decisions and quantity adjustments are applied.</returns>
    public static IReadOnlyList<CutLabPoolCard> Derive(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyList<CutLabDecision> decisions,
        IReadOnlyList<CutLabQuantityAdjustment> adjustments)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(adjustments);

        IReadOnlySet<string> acceptedCardNames = AcceptedCardNames(decisions);
        CutLabQuantityAdjustmentFold foldedAdjustments = FoldAdjustments(adjustments);
        List<CutLabPoolCard> workingList = new(pool.Count + foldedAdjustments.OrderedNames.Count);

        foreach (CutLabPoolCard card in pool.Where(card => !acceptedCardNames.Contains(card.Name)))
        {
            string normalizedCardName = CutLabCardNames.Normalize(card.Name);
            if (!foldedAdjustments.ByName.TryGetValue(normalizedCardName, out CutLabQuantityAdjustmentState? adjustment))
            {
                workingList.Add(card);
                continue;
            }

            int adjustedQuantity = Math.Clamp(card.Quantity + adjustment.NetDelta, 0, CutLabLegality.LegalMax(card.Name));
            foldedAdjustments.UnmatchedNames.Remove(normalizedCardName);
            if (adjustedQuantity <= 0)
            {
                continue;
            }

            workingList.Add(adjustedQuantity == card.Quantity ? card : card with { Quantity = adjustedQuantity });
        }

        foreach (string normalizedName in foldedAdjustments.OrderedNames)
        {
            if (!foldedAdjustments.UnmatchedNames.Contains(normalizedName))
            {
                continue;
            }

            CutLabQuantityAdjustmentState adjustment = foldedAdjustments.ByName[normalizedName];
            if (!adjustment.IsAddedBasic || adjustment.NetDelta <= 0)
            {
                continue;
            }

            if (!CutLabBasicLands.TryResolve(adjustment.Name, out CutLabBasicLands.Definition? definition) || definition is null)
            {
                continue;
            }

            workingList.Add(new CutLabPoolCard
            {
                Name = adjustment.Name,
                Quantity = Math.Clamp(adjustment.NetDelta, 0, CutLabLegality.LegalMax(adjustment.Name)),
                TypeLine = definition.TypeLine,
                IsCommander = false,
                IsLocked = false,
            });
        }

        return workingList;
    }

    /// <summary>Returns the case-insensitive set of card names whose latest decision is accepted.</summary>
    /// <param name="decisions">Persisted decision log.</param>
    /// <returns>The net-accepted card names keyed by latest ordinal.</returns>
    public static IReadOnlySet<string> AcceptedCardNames(IReadOnlyList<CutLabDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        return LatestDecisionsByCard(decisions)
            .Where(entry => entry.Value.Kind == CutLabDecisionKind.Accepted)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the latest decision for each card name keyed by highest ordinal.</summary>
    /// <param name="decisions">Persisted decision log.</param>
    /// <returns>The latest-decision fold keyed by card name.</returns>
    public static IReadOnlyDictionary<string, CutLabDecision> LatestDecisionsByCard(IReadOnlyList<CutLabDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        Dictionary<string, CutLabDecision> latestDecisions = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabDecision decision in decisions)
        {
            if (string.IsNullOrWhiteSpace(decision.CardName))
            {
                continue;
            }

            if (!latestDecisions.TryGetValue(decision.CardName, out CutLabDecision? current) || decision.Ordinal >= current.Ordinal)
            {
                latestDecisions[decision.CardName] = decision;
            }
        }

        return latestDecisions;
    }

    private static CutLabQuantityAdjustmentFold FoldAdjustments(IReadOnlyList<CutLabQuantityAdjustment> adjustments)
    {
        Dictionary<string, CutLabQuantityAdjustmentState> byName = new(CutLabCardNames.Comparer);
        List<string> orderedNames = [];

        foreach (CutLabQuantityAdjustment adjustment in adjustments)
        {
            if (string.IsNullOrWhiteSpace(adjustment.Name))
            {
                continue;
            }

            string normalizedName = CutLabCardNames.Normalize(adjustment.Name);
            if (byName.TryGetValue(normalizedName, out CutLabQuantityAdjustmentState? current))
            {
                byName[normalizedName] = current with
                {
                    NetDelta = current.NetDelta + adjustment.Delta,
                    IsAddedBasic = current.IsAddedBasic || adjustment.IsAddedBasic,
                };
                continue;
            }

            orderedNames.Add(normalizedName);
            byName[normalizedName] = new CutLabQuantityAdjustmentState(adjustment.Name, adjustment.Delta, adjustment.IsAddedBasic);
        }

        return new CutLabQuantityAdjustmentFold(byName, orderedNames, orderedNames.ToHashSet(CutLabCardNames.Comparer));
    }

    private sealed record CutLabQuantityAdjustmentFold(
        Dictionary<string, CutLabQuantityAdjustmentState> ByName,
        List<string> OrderedNames,
        HashSet<string> UnmatchedNames);

    private sealed record CutLabQuantityAdjustmentState(string Name, int NetDelta, bool IsAddedBasic);
}
