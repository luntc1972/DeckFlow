using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Resolves the effective commander-name list for Cut Lab page and AJAX flows.</summary>
public static class CutLabCommanderNames
{
    /// <summary>Returns all flagged commanders, distinct by name, or falls back to the stored commander string.</summary>
    public static IReadOnlyList<string> Resolve(CutLabState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        IReadOnlyList<string> flaggedCommanders = state.Pool
            .Where(card => card.IsCommander)
            .Select(card => card.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (flaggedCommanders.Count > 0)
        {
            return flaggedCommanders;
        }

        return string.IsNullOrWhiteSpace(state.Commander) ? [] : [state.Commander];
    }
}
