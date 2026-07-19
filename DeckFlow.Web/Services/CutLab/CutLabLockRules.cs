using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure immutable lock-state mutation rules for Cut Lab pool cards, packages, and bulk land locks.</summary>
public static class CutLabLockRules
{
    /// <summary>Forces every commander card in the pool to remain locked, even if client state was tampered.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <returns>A new state whose commander cards are always locked.</returns>
    public static CutLabState EnforceCommanderLock(CutLabState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.Pool.Any(card => card.IsCommander && !card.IsLocked))
        {
            return state;
        }

        return state with
        {
            Pool = state.Pool
                .Select(card => card.IsCommander && !card.IsLocked ? card with { IsLocked = true } : card)
                .ToArray(),
        };
    }

    /// <summary>Locks the named card in the pool.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <param name="name">Case-insensitive card name to lock.</param>
    /// <returns>A new state with the matching card locked.</returns>
    public static CutLabState LockCard(CutLabState state, string name)
    {
        ArgumentNullException.ThrowIfNull(state);

        return EnforceCommanderLock(state with
        {
            Pool = state.Pool
                .Select(card => NamesMatch(card.Name, name) ? card with { IsLocked = true } : card)
                .ToArray(),
        });
    }

    /// <summary>Unlocks the named non-commander card while preserving the commander lock invariant.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <param name="name">Case-insensitive card name to unlock.</param>
    /// <returns>A new state with the matching non-commander card unlocked.</returns>
    public static CutLabState UnlockCard(CutLabState state, string name)
    {
        ArgumentNullException.ThrowIfNull(state);

        return EnforceCommanderLock(state with
        {
            Pool = state.Pool
                .Select(card => NamesMatch(card.Name, name) && !card.IsCommander ? card with { IsLocked = false } : card)
                .ToArray(),
        });
    }

    /// <summary>Locks the named package and every member card assigned to it.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <param name="packageId">Case-insensitive package identifier to lock.</param>
    /// <returns>A new state with the package and its member cards locked.</returns>
    public static CutLabState LockPackage(CutLabState state, string packageId)
    {
        ArgumentNullException.ThrowIfNull(state);

        return EnforceCommanderLock(state with
        {
            Packages = state.Packages
                .Select(package => PackageIdsMatch(package.Id, packageId) ? package with { Locked = true } : package)
                .ToArray(),
            Pool = state.Pool
                .Select(card => PackageIdsMatch(card.PackageId, packageId) ? card with { IsLocked = true } : card)
                .ToArray(),
        });
    }

    /// <summary>Unlocks the named package and its member cards, except the commander remains locked.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <param name="packageId">Case-insensitive package identifier to unlock.</param>
    /// <returns>A new state with the package unlocked and the commander lock re-applied.</returns>
    public static CutLabState UnlockPackage(CutLabState state, string packageId)
    {
        ArgumentNullException.ThrowIfNull(state);

        return EnforceCommanderLock(state with
        {
            Packages = state.Packages
                .Select(package => PackageIdsMatch(package.Id, packageId) ? package with { Locked = false } : package)
                .ToArray(),
            Pool = state.Pool
                .Select(card => PackageIdsMatch(card.PackageId, packageId) && !card.IsCommander ? card with { IsLocked = false } : card)
                .ToArray(),
        });
    }

    /// <summary>Locks every card in the supported role group for this phase.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <param name="roleGroup">Case-insensitive role-group name; only <c>lands</c> is supported in this phase.</param>
    /// <returns>A new state with the supported role-group cards locked, or the original state for unsupported groups.</returns>
    public static CutLabState BulkLockRoleGroup(CutLabState state, string roleGroup)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!string.Equals(roleGroup, "lands", StringComparison.OrdinalIgnoreCase))
        {
            return state;
        }

        return EnforceCommanderLock(state with
        {
            Pool = state.Pool
                .Select(card => IsLand(card.TypeLine) ? card with { IsLocked = true } : card)
                .ToArray(),
        });
    }

    /// <summary>True when the card's front-face type line is a land.</summary>
    /// <param name="typeLine">Raw type line, possibly including a second face after <c>//</c>.</param>
    /// <returns><see langword="true"/> when the front face contains <c>Land</c>; otherwise <see langword="false"/>.</returns>
    public static bool IsLand(string? typeLine)
        => CardTypeLine.FrontFace(typeLine).Contains("Land", StringComparison.OrdinalIgnoreCase);

    private static bool NamesMatch(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool PackageIdsMatch(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
