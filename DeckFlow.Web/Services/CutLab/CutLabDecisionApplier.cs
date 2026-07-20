using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure immutable decision-application rules shared by the JSON and no-JS Cut Lab flows.</summary>
public static class CutLabDecisionApplier
{
    /// <summary>Applies one Cut Lab decision while preserving pool immutability and commander locks.</summary>
    /// <param name="state">Current working-session state.</param>
    /// <param name="cardName">Card receiving the decision.</param>
    /// <param name="action">Decision action to apply.</param>
    /// <param name="roundKey">Stable round key to persist on appended decisions.</param>
    /// <returns>A new state reflecting the decision.</returns>
    public static CutLabState Apply(CutLabState state, string cardName, CutLabDecideAction action, string roundKey)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roundKey);

        if (action == CutLabDecideAction.Restore)
        {
            return Restore(state, cardName);
        }

        CutLabPoolCard? poolCard = state.Pool.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase));
        if (poolCard is not null && poolCard.IsLocked)
        {
            return CutLabLockRules.EnforceCommanderLock(state);
        }

        CutLabDecisionKind kind = action switch
        {
            CutLabDecideAction.Accept => CutLabDecisionKind.Accepted,
            CutLabDecideAction.Reject => CutLabDecisionKind.Rejected,
            CutLabDecideAction.Defer => CutLabDecisionKind.Deferred,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported Cut Lab decision action."),
        };

        CutLabDecision decision = new()
        {
            CardName = cardName,
            Kind = kind,
            Round = roundKey,
            Ordinal = NextOrdinal(state.Decisions),
        };

        return CutLabLockRules.EnforceCommanderLock(state with
        {
            Decisions = state.Decisions.Concat([decision]).ToArray(),
        });
    }

    private static CutLabState Restore(CutLabState state, string cardName)
    {
        IReadOnlyList<CutLabDecision> remainingDecisions = state.Decisions
            .Where(decision => !string.Equals(decision.CardName, cardName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return CutLabLockRules.EnforceCommanderLock(state with
        {
            Decisions = remainingDecisions,
        });
    }

    private static int NextOrdinal(IReadOnlyList<CutLabDecision> decisions)
        => decisions.Count == 0 ? 1 : decisions.Max(decision => decision.Ordinal) + 1;
}
