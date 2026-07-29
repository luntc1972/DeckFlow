using System.Text.Json;

namespace DeckFlow.Core.Research;

/// <summary>
/// Limits governing how far a candidate role-floor snapshot may move from the committed one before
/// the refresh is rejected. Every property is required because a missing field must throw rather
/// than silently fall back to a default, which would let a typo disable the guard.
/// </summary>
public sealed record RoleFloorDriftThresholds
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Prior sample size at or above which a commander's disappearance is a failure.</summary>
    public required int MinEstablishedN { get; init; }

    /// <summary>Prior sample size at or above which a commander is checked for sample collapse.</summary>
    public required int MinPopulousN { get; init; }

    /// <summary>Maximum tolerated percentage drop in a populous commander's sample size.</summary>
    public required double MaxSampleDropPct { get; init; }

    /// <summary>Minimum absolute floor delta for a pair to count as a mover.</summary>
    public required int MoverThresholdFloors { get; init; }

    /// <summary>Minimum movers before the one-sidedness test is meaningful.</summary>
    public required int MinMoversForDirectionTest { get; init; }

    /// <summary>Percentage at or above which same-direction movers fail the one-sidedness rule.</summary>
    public required double MaxOneSidedPct { get; init; }

    /// <summary>Maximum tolerated percentage drop in the snapshot's adopted-pair count.</summary>
    public required double MaxAdoptedPairDropPct { get; init; }

    /// <summary>Parse thresholds from the committed configuration file contents.</summary>
    /// <param name="json">Raw JSON document.</param>
    /// <returns>The parsed thresholds.</returns>
    /// <exception cref="JsonException">
    /// The document is malformed or omits any threshold. Missing values are fatal by design
    /// because silently defaulting would disable the guard.
    /// </exception>
    public static RoleFloorDriftThresholds FromJson(string json) =>
        JsonSerializer.Deserialize<RoleFloorDriftThresholds>(json, JsonOptions)
        ?? throw new JsonException("Drift thresholds document was null.");
}

/// <summary>One reason a candidate role-floor snapshot was rejected.</summary>
public sealed record RoleFloorDriftFinding
{
    /// <summary>Name of the rule that fired.</summary>
    public required string Rule { get; init; }

    /// <summary>Commander the finding concerns, when the rule is per-commander.</summary>
    public string? Commander { get; init; }

    /// <summary>Role the finding concerns, when the rule is per-role.</summary>
    public string? Role { get; init; }

    /// <summary>Human-readable explanation including the observed value and the limit breached.</summary>
    public required string Detail { get; init; }
}

/// <summary>Outcome of comparing a candidate role-floor snapshot against the committed one.</summary>
public sealed record RoleFloorDriftVerdict
{
    /// <summary>True when no drift rule fired.</summary>
    public required bool Passed { get; init; }

    /// <summary>Every rule breach found, in rule order.</summary>
    public required IReadOnlyList<RoleFloorDriftFinding> Findings { get; init; }
}

/// <summary>
/// Compares a freshly built role-floor snapshot against the committed one and reports corruption
/// signatures without throwing on rule breaches.
/// </summary>
public static class RoleFloorBaselineDriftCheck
{
    /// <summary>Evaluate a candidate role-floor snapshot against the committed snapshot.</summary>
    /// <param name="previous">The committed snapshot being replaced.</param>
    /// <param name="candidate">The freshly built snapshot.</param>
    /// <param name="thresholds">Limits loaded from the committed thresholds file.</param>
    /// <returns>A verdict carrying every rule breach found.</returns>
    public static RoleFloorDriftVerdict Evaluate(
        RoleFloorBaselineSnapshot previous,
        RoleFloorBaselineSnapshot candidate,
        RoleFloorDriftThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(thresholds);

        List<RoleFloorDriftFinding> findings = [];
        if (previous.Commanders.Count == 0)
        {
            findings.Add(new RoleFloorDriftFinding
            {
                Rule = "EmptyPreviousSnapshot",
                Commander = null,
                Role = null,
                Detail =
                    $"The committed snapshot ({previous.Generated}) has no commanders and is treated as corrupt for drift evaluation.",
            });

            return new RoleFloorDriftVerdict
            {
                Passed = false,
                Findings = findings,
            };
        }

        AddDroppedEstablishedCommanders(previous, candidate, thresholds, findings);
        AddDroppedEstablishedRoles(previous, candidate, thresholds, findings);
        AddSampleCollapses(previous, candidate, thresholds, findings);
        AddAdoptedPairCollapse(previous, candidate, thresholds, findings);
        AddOneSidedDrift(previous, candidate, thresholds, findings);

        return new RoleFloorDriftVerdict
        {
            Passed = findings.Count == 0,
            Findings = findings,
        };
    }

    private static void AddDroppedEstablishedCommanders(
        RoleFloorBaselineSnapshot previous,
        RoleFloorBaselineSnapshot candidate,
        RoleFloorDriftThresholds thresholds,
        List<RoleFloorDriftFinding> findings)
    {
        foreach ((string name, RoleFloorCommanderSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinEstablishedN || candidate.Commanders.ContainsKey(name))
            {
                continue;
            }

            findings.Add(new RoleFloorDriftFinding
            {
                Rule = "DroppedEstablishedCommander",
                Commander = name,
                Role = null,
                Detail =
                    $"present with n={prior.N} in the committed snapshot ({previous.Generated}) but absent from the candidate; floor is n>={thresholds.MinEstablishedN}.",
            });
        }
    }

    private static void AddDroppedEstablishedRoles(
        RoleFloorBaselineSnapshot previous,
        RoleFloorBaselineSnapshot candidate,
        RoleFloorDriftThresholds thresholds,
        List<RoleFloorDriftFinding> findings)
    {
        foreach ((string name, RoleFloorCommanderSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinEstablishedN
                || !candidate.Commanders.TryGetValue(name, out RoleFloorCommanderSnapshot? current))
            {
                continue;
            }

            // Why: unlike the lands baseline, role floors have a sub-role dimension that can drop
            // independently even when the commander itself remains present.
            foreach ((string roleKey, int priorFloor) in prior.Floors)
            {
                if (current.Floors.ContainsKey(roleKey))
                {
                    continue;
                }

                findings.Add(new RoleFloorDriftFinding
                {
                    Rule = "DroppedEstablishedRole",
                    Commander = name,
                    Role = roleKey,
                    Detail =
                        $"role '{roleKey}' was present at floor {priorFloor} in the committed snapshot ({previous.Generated}) but is absent from the candidate; commander floor is n>={thresholds.MinEstablishedN}.",
                });
            }
        }
    }

    private static void AddSampleCollapses(
        RoleFloorBaselineSnapshot previous,
        RoleFloorBaselineSnapshot candidate,
        RoleFloorDriftThresholds thresholds,
        List<RoleFloorDriftFinding> findings)
    {
        foreach ((string name, RoleFloorCommanderSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinPopulousN
                || !candidate.Commanders.TryGetValue(name, out RoleFloorCommanderSnapshot? current))
            {
                continue;
            }

            double dropPct = (prior.N - current.N) / (double)prior.N * 100.0;
            if (dropPct <= thresholds.MaxSampleDropPct)
            {
                continue;
            }

            findings.Add(new RoleFloorDriftFinding
            {
                Rule = "SampleCollapse",
                Commander = name,
                Role = null,
                Detail =
                    $"sample fell {dropPct:0.0}% (n {prior.N} -> {current.N}); limit is {thresholds.MaxSampleDropPct:0.#}%.",
            });
        }
    }

    private static void AddAdoptedPairCollapse(
        RoleFloorBaselineSnapshot previous,
        RoleFloorBaselineSnapshot candidate,
        RoleFloorDriftThresholds thresholds,
        List<RoleFloorDriftFinding> findings)
    {
        if (previous.AdoptedPairs <= 0)
        {
            return;
        }

        double dropPct = (previous.AdoptedPairs - candidate.AdoptedPairs) / (double)previous.AdoptedPairs * 100.0;
        if (dropPct <= thresholds.MaxAdoptedPairDropPct)
        {
            return;
        }

        findings.Add(new RoleFloorDriftFinding
        {
            Rule = "AdoptedPairCollapse",
            Commander = null,
            Role = null,
            Detail =
                $"adopted pairs fell {dropPct:0.0}% ({previous.AdoptedPairs} -> {candidate.AdoptedPairs}); limit is {thresholds.MaxAdoptedPairDropPct:0.#}%.",
        });
    }

    private static void AddOneSidedDrift(
        RoleFloorBaselineSnapshot previous,
        RoleFloorBaselineSnapshot candidate,
        RoleFloorDriftThresholds thresholds,
        List<RoleFloorDriftFinding> findings)
    {
        int up = 0;
        int down = 0;

        foreach ((string commanderName, RoleFloorCommanderSnapshot priorCommander) in previous.Commanders)
        {
            if (!candidate.Commanders.TryGetValue(commanderName, out RoleFloorCommanderSnapshot? currentCommander))
            {
                continue;
            }

            foreach ((string roleKey, int priorFloor) in priorCommander.Floors)
            {
                if (!currentCommander.Floors.TryGetValue(roleKey, out int currentFloor))
                {
                    continue;
                }

                int delta = currentFloor - priorFloor;
                if (Math.Abs(delta) < thresholds.MoverThresholdFloors)
                {
                    continue;
                }

                if (delta > 0)
                {
                    up++;
                }
                else
                {
                    down++;
                }
            }
        }

        int movers = up + down;
        if (movers < thresholds.MinMoversForDirectionTest)
        {
            return;
        }

        double oneSidedPct = Math.Max(up, down) / (double)movers * 100.0;
        if (oneSidedPct < thresholds.MaxOneSidedPct)
        {
            return;
        }

        findings.Add(new RoleFloorDriftFinding
        {
            Rule = "OneSidedDrift",
            Commander = null,
            Role = null,
            Detail =
                $"{movers} adopted pairs moved by at least {thresholds.MoverThresholdFloors} floors and {oneSidedPct:0.0}% went the same way (up {up}, down {down}); limit is {thresholds.MaxOneSidedPct:0.#}%. Genuine drift scatters while a one-sided shift indicates systematic input corruption.",
        });
    }
}
