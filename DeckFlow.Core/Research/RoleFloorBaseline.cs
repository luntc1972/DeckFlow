using System.Text.Json.Serialization;

namespace DeckFlow.Core.Research;

/// <summary>
/// Builds the shipped commander role-floor snapshot from the committed research findings artifact.
/// </summary>
public static class RoleFloorBaseline
{
    /// <summary>The role keys that may ship in the commander-aware floor snapshot.</summary>
    // Why: this list is hardcoded deliberately because RESEARCH-FINDINGS.json still includes lands
    // in rolesInScopeForPhase3, and 02-08-SUMMARY.md is the authority that overrides it.
    public static readonly string[] AdoptedRoleKeys =
    [
        "ramp",
        "draw",
        "interaction-targeted",
        "engines",
        "payoffs",
        "wincons",
    ];

    /// <summary>
    /// Builds the shipped snapshot from a deserialized research findings document.
    /// </summary>
    /// <param name="document">Committed research findings input.</param>
    /// <param name="generated">Generation label to stamp onto the snapshot.</param>
    /// <returns>The byte-stable snapshot contract consumed by downstream loaders.</returns>
    public static RoleFloorBaselineSnapshot Build(RoleFloorFindingsDocument document, string generated)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(generated);

        Dictionary<string, RoleFloorCommanderSnapshot> commanders = new(StringComparer.Ordinal);
        int adoptedPairs = 0;

        foreach ((string commanderName, RoleFloorFindingsCommander commander) in document.Commanders
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Dictionary<string, int> floors = new(StringComparer.Ordinal);

            foreach (string roleKey in AdoptedRoleKeys)
            {
                if (!commander.Roles.TryGetValue(roleKey, out RoleFloorFindingsRole? role))
                {
                    continue;
                }

                // Why: Postgres is the only source arm permitted to ship percentile floors, so any
                // future non-Postgres row is excluded instead of being adopted silently.
                if (!string.Equals(role.Source, "postgres", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!role.ClearsBar)
                {
                    continue;
                }

                // Why: Math.Round is deliberately not used here because .NET defaults to banker's
                // rounding, while truncation never asserts more than the measured quartile proves.
                int floor = (int)Math.Floor(role.P25);
                if (floor <= 0)
                {
                    continue;
                }

                floors.Add(roleKey, floor);
                adoptedPairs++;
            }

            if (floors.Count == 0)
            {
                continue;
            }

            commanders.Add(
                commanderName,
                new RoleFloorCommanderSnapshot
                {
                    N = commander.N,
                    Floors = floors,
                });
        }

        return new RoleFloorBaselineSnapshot
        {
            Generated = generated,
            SampleSize = document.Commanders.Count,
            AdoptedPairs = adoptedPairs,
            Commanders = commanders,
        };
    }
}

/// <summary>
/// Minimal shape of the committed research findings artifact consumed by the adoption filter.
/// </summary>
public sealed record RoleFloorFindingsDocument
{
    /// <summary>The commanders block keyed by commander name.</summary>
    [JsonPropertyName("commanders")]
    public required IReadOnlyDictionary<string, RoleFloorFindingsCommander> Commanders { get; init; }
}

/// <summary>
/// Minimal committed findings row for one commander.
/// </summary>
public sealed record RoleFloorFindingsCommander
{
    /// <summary>The commander's deduped deck sample size.</summary>
    [JsonPropertyName("n")]
    public int N { get; init; }

    /// <summary>The per-role findings keyed by role identifier.</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyDictionary<string, RoleFloorFindingsRole> Roles { get; init; } =
        new Dictionary<string, RoleFloorFindingsRole>(StringComparer.Ordinal);
}

/// <summary>
/// Minimal committed findings row for one commander-role pair.
/// </summary>
public sealed record RoleFloorFindingsRole
{
    /// <summary>The measurement source arm that produced the role-floor signal.</summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    /// <summary>The measured 25th-percentile role count.</summary>
    [JsonPropertyName("p25")]
    public double P25 { get; init; }

    /// <summary>Whether the commander-role pair cleared the Phase 2 adoption bar.</summary>
    [JsonPropertyName("clearsBar")]
    public bool ClearsBar { get; init; }
}

/// <summary>
/// Shipped commander-aware role-floor snapshot contract.
/// </summary>
public sealed record RoleFloorBaselineSnapshot
{
    /// <summary>The generation label stamped onto the snapshot.</summary>
    [JsonPropertyName("generated")]
    public required string Generated { get; init; }

    /// <summary>The count of commanders present in the input findings document.</summary>
    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; init; }

    /// <summary>The total adopted commander-role pairs in the snapshot.</summary>
    [JsonPropertyName("adoptedPairs")]
    public int AdoptedPairs { get; init; }

    /// <summary>The adopted floors keyed by commander name.</summary>
    [JsonPropertyName("commanders")]
    public required IReadOnlyDictionary<string, RoleFloorCommanderSnapshot> Commanders { get; init; }
}

/// <summary>
/// Shipped commander-level floor snapshot.
/// </summary>
public sealed record RoleFloorCommanderSnapshot
{
    /// <summary>The commander's deduped deck sample size.</summary>
    [JsonPropertyName("n")]
    public int N { get; init; }

    /// <summary>The adopted floors keyed by role identifier.</summary>
    [JsonPropertyName("floors")]
    public required IReadOnlyDictionary<string, int> Floors { get; init; }
}
