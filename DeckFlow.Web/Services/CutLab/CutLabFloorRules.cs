using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Pure floor-clamping and floor-break evaluation rules for Cut Lab. Phase 103's cut engine MUST
/// route every proposed cut through Evaluate before presenting it — a proposed cut that breaks a
/// floor always carries an explicit warning, never a silent break (FLOOR-02).
/// </summary>
public static class CutLabFloorRules
{
    /// <summary>Stable serialized role keys in the fixed Cut Lab display and persistence order.</summary>
    public static readonly IReadOnlyList<string> RoleKeys =
    [
        "lands",
        "ramp",
        "draw",
        "interaction",
        "protection",
        "engines",
        "payoffs",
        "wincons",
    ];

    /// <summary>Maximum supported role floor after clamping untrusted persisted values.</summary>
    // Why: Cut Lab accepts up to a 150-card non-commander pool, and the commander is the plus one.
    public const int MaxFloor = CutLabPoolValidator.MaxPoolCards + 1;

    /// <summary>Clamps, canonicalizes, and de-duplicates untrusted persisted role-floor entries.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <returns>The original state when already valid; otherwise a copy with corrected role floors.</returns>
    public static CutLabState ClampFloors(CutLabState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        List<CutLabRoleFloor> normalized = [];
        HashSet<string> seenRoles = new(StringComparer.OrdinalIgnoreCase);
        bool changed = false;

        foreach (CutLabRoleFloor roleFloor in state.RoleFloors)
        {
            if (!TryGetCanonicalRole(roleFloor.Role, out string canonicalRole))
            {
                changed = true;
                continue;
            }

            if (!seenRoles.Add(canonicalRole))
            {
                changed = true;
                continue;
            }

            int clampedFloor = Math.Clamp(roleFloor.Floor, 0, MaxFloor);
            bool entryChanged = clampedFloor != roleFloor.Floor
                || !string.Equals(roleFloor.Role, canonicalRole, StringComparison.Ordinal);
            changed |= entryChanged;
            normalized.Add(entryChanged ? roleFloor with { Role = canonicalRole, Floor = clampedFloor } : roleFloor);
        }

        if (!changed)
        {
            return state;
        }

        return state with
        {
            RoleFloors = normalized,
        };
    }

    /// <summary>Warning emitted when a proposed cut would drop a role below its floor.</summary>
    public sealed record CutLabFloorWarning
    {
        /// <summary>Stable serialized role key whose floor would be broken.</summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>Role count after applying the proposed cut.</summary>
        public int NewCount { get; init; }

        /// <summary>User-visible floor that would be broken.</summary>
        public int Floor { get; init; }

        /// <summary>Fixed UI warning copy for the proposed floor break.</summary>
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>Evaluates whether cutting a card would break any currently configured role floors.</summary>
    /// <param name="roleCounts">Current role counts by stable role key.</param>
    /// <param name="floors">Active floors by stable role key.</param>
    /// <param name="candidateCutRoles">Every role the candidate cut card belongs to.</param>
    /// <param name="cardName">Display name of the card being considered for cutting.</param>
    /// <param name="quantity">Number of cards the proposed cut removes. Phase 103 must pass the real cut quantity.</param>
    /// <returns>One warning per broken floor, or an empty list when the cut stays above all floors.</returns>
    public static IReadOnlyList<CutLabFloorWarning> Evaluate(
        IReadOnlyDictionary<string, int> roleCounts,
        IReadOnlyDictionary<string, int> floors,
        IReadOnlyCollection<string> candidateCutRoles,
        string cardName,
        int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(roleCounts);
        ArgumentNullException.ThrowIfNull(floors);
        ArgumentNullException.ThrowIfNull(candidateCutRoles);
        ArgumentNullException.ThrowIfNull(cardName);

        Dictionary<string, int> normalizedRoleCounts = NormalizeCounts(roleCounts);
        Dictionary<string, int> normalizedFloors = NormalizeCounts(floors);
        List<CutLabFloorWarning>? warnings = null;
        HashSet<string> seenRoles = new(StringComparer.OrdinalIgnoreCase);

        foreach (string candidateRole in candidateCutRoles)
        {
            if (!TryGetCanonicalRole(candidateRole, out string canonicalRole) || !seenRoles.Add(canonicalRole))
            {
                continue;
            }

            if (!normalizedFloors.TryGetValue(canonicalRole, out int floor))
            {
                continue;
            }

            int currentCount = normalizedRoleCounts.TryGetValue(canonicalRole, out int count) ? count : 0;
            int newCount = Math.Max(0, currentCount - quantity);
            if (newCount >= floor)
            {
                continue;
            }

            warnings ??= [];
            warnings.Add(new CutLabFloorWarning
            {
                Role = canonicalRole,
                NewCount = newCount,
                Floor = floor,
                Message = $"Cutting {cardName} drops {canonicalRole} to {newCount}, below your floor of {floor}.",
            });
        }

        return warnings ?? [];
    }

    private static Dictionary<string, int> NormalizeCounts(IReadOnlyDictionary<string, int> values)
    {
        Dictionary<string, int> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string role, int value) in values)
        {
            if (TryGetCanonicalRole(role, out string canonicalRole))
            {
                normalized[canonicalRole] = value;
            }
        }

        return normalized;
    }

    private static bool TryGetCanonicalRole(string? role, out string canonicalRole)
    {
        foreach (string candidate in RoleKeys)
        {
            if (string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase))
            {
                canonicalRole = candidate;
                return true;
            }
        }

        canonicalRole = string.Empty;
        return false;
    }
}
