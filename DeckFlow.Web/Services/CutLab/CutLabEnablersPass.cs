using DeckFlow.Core.Analysis;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Adds deck-aware roles after per-card structural assignment.</summary>
public static class CutLabEnablersPass
{
    /// <summary>Appends the enabler role when a card fires an active commander trigger family.</summary>
    public static IReadOnlyList<string> Apply(IReadOnlyList<string> activeFamilies, string? oracleText, IReadOnlyList<string> roles)
    {
        if (!TriggerFamilyCatalog.IsEnabler(activeFamilies, oracleText) || roles.Contains(CutLabRoleKeys.Enablers, StringComparer.Ordinal))
        {
            return roles;
        }

        return roles.Where(role => !string.Equals(role, CutLabRoleKeys.Residual, StringComparison.Ordinal))
            .Append(CutLabRoleKeys.Enablers).ToArray();
    }
}
