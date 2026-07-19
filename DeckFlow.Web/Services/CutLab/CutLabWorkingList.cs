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
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(decisions);

        IReadOnlySet<string> acceptedCardNames = AcceptedCardNames(decisions);

        return pool
            .Where(card => !acceptedCardNames.Contains(card.Name))
            .ToArray();
    }

    /// <summary>Returns the case-insensitive set of card names whose latest decision is accepted.</summary>
    /// <param name="decisions">Persisted decision log.</param>
    /// <returns>The net-accepted card names keyed by latest ordinal.</returns>
    public static IReadOnlySet<string> AcceptedCardNames(IReadOnlyList<CutLabDecision> decisions)
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

        return latestDecisions
            .Where(entry => entry.Value.Kind == CutLabDecisionKind.Accepted)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
