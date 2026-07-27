using System.Reflection;

namespace DeckFlow.Core.Research;

/// <summary>
/// Pure guard predicates for the role-floor research harness.
/// </summary>
public static class RoleFloorGuards
{
    /// <summary>
    /// Tries to read the shipped role-key field from the supplied assigner type.
    /// </summary>
    /// <param name="assignerType">The type that declares the shipped role-key field.</param>
    /// <param name="fieldName">The field name to read from <paramref name="assignerType"/>.</param>
    /// <param name="shippedRoleKeys">The shipped role keys when the field was read successfully; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="null"/> when the field was read successfully; otherwise an error message naming
    /// the unreadable field.
    /// </returns>
    public static string? TryReadShippedRoleKeys(Type assignerType, string fieldName, out string[]? shippedRoleKeys)
    {
        ArgumentNullException.ThrowIfNull(assignerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        // Why: Core has no reference to the web assembly, so this reflection must stay
        // parameterized by Type rather than naming the concrete assigner here; lifting it into
        // Core is also what makes the field-unreadable branches unit-testable at all.
        string typeDisplayName = assignerType.FullName ?? assignerType.Name;
        FieldInfo? roleKeysField = assignerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (roleKeysField is null)
        {
            shippedRoleKeys = null;
            return $"Unable to read {typeDisplayName}.{fieldName}: expected a static string[] field named {fieldName}.";
        }

        object? rawValue = roleKeysField.GetValue(null);
        if (rawValue is null)
        {
            shippedRoleKeys = null;
            return $"Unable to read {typeDisplayName}.{fieldName}: expected the static field {fieldName} to hold a non-null string[].";
        }

        if (rawValue is not string[] typedRoleKeys)
        {
            shippedRoleKeys = null;
            return $"Unable to read {typeDisplayName}.{fieldName}: expected the static field {fieldName} to be a string[] but was {rawValue.GetType().FullName ?? rawValue.GetType().Name}.";
        }

        shippedRoleKeys = typedRoleKeys;
        return null;
    }

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
