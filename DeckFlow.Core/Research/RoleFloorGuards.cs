namespace DeckFlow.Core.Research;

/// <summary>
/// Pure guard predicates for the role-floor research harness.
/// </summary>
public static class RoleFloorGuards
{
    /// <summary>
    /// Returns a taxonomy-drift error when the shipped role keys, harness target roles, and probe
    /// emissions disagree; otherwise returns <see langword="null"/>.
    /// </summary>
    /// <param name="shippedRoleKeys">The authoritative shipped role keys.</param>
    /// <param name="targetRoles">The role keys the harness will measure.</param>
    /// <param name="emittedKeys">The union of role keys emitted by the startup probes.</param>
    /// <param name="residualRoleKey">The residual fallback role that is tolerated when emitted.</param>
    /// <returns>A drift message naming every failing category, or <see langword="null"/> when clean.</returns>
    public static string? FindTaxonomyDrift(
        IReadOnlyCollection<string> shippedRoleKeys,
        IReadOnlyCollection<string> targetRoles,
        IReadOnlyCollection<string> emittedKeys,
        string residualRoleKey)
    {
        ArgumentNullException.ThrowIfNull(shippedRoleKeys);
        ArgumentNullException.ThrowIfNull(targetRoles);
        ArgumentNullException.ThrowIfNull(emittedKeys);
        ArgumentException.ThrowIfNullOrWhiteSpace(residualRoleKey);

        var shippedSet = new HashSet<string>(shippedRoleKeys, StringComparer.Ordinal);
        var targetSet = new HashSet<string>(targetRoles, StringComparer.Ordinal);
        var emittedSet = new HashSet<string>(emittedKeys, StringComparer.Ordinal);
        var failures = new List<string>();

        string[] missingFromTarget = shippedSet.Where(role => !targetSet.Contains(role)).OrderBy(role => role, StringComparer.Ordinal).ToArray();
        if (missingFromTarget.Length > 0)
        {
            failures.Add($"shipped keys missing from TargetRoles: {string.Join(", ", missingFromTarget)}");
        }

        string[] staleTargets = targetSet.Where(role => !shippedSet.Contains(role)).OrderBy(role => role, StringComparer.Ordinal).ToArray();
        if (staleTargets.Length > 0)
        {
            failures.Add($"TargetRoles entries not shipped by Cut Lab: {string.Join(", ", staleTargets)}");
        }

        string[] unexpectedEmitted = emittedSet
            .Where(role => !targetSet.Contains(role) && !string.Equals(role, residualRoleKey, StringComparison.Ordinal))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        if (unexpectedEmitted.Length > 0)
        {
            failures.Add($"probe-emitted keys outside TargetRoles and residual '{residualRoleKey}': {string.Join(", ", unexpectedEmitted)}");
        }

        string[] unprobedShipped = shippedSet.Where(role => !emittedSet.Contains(role)).OrderBy(role => role, StringComparer.Ordinal).ToArray();
        if (unprobedShipped.Length > 0)
        {
            failures.Add($"shipped keys with no probe coverage: {string.Join(", ", unprobedShipped)}");
        }

        return failures.Count == 0
            ? null
            : $"Role-floor taxonomy drift: {string.Join("; ", failures)}.";
    }

    /// <summary>
    /// Returns whether the run produced zero qualifying commanders.
    /// </summary>
    /// <param name="qualifyingCommanderCount">The number of qualifying commanders.</param>
    /// <returns><see langword="true"/> when no commanders qualified; otherwise <see langword="false"/>.</returns>
    public static bool HasNoQualifyingCommanders(int qualifyingCommanderCount)
        => qualifyingCommanderCount == 0;
}
