namespace DeckFlow.Core.Manabase;

/// <summary>
/// Limits governing how far a candidate cEDH land baseline may move from the committed one
/// before the refresh is rejected. Every property is required: a missing field must throw
/// rather than silently fall back to a default, because a typo in the config file would
/// otherwise disable the guard entirely.
/// </summary>
public sealed record CedhDriftThresholds
{
    /// <summary>Prior sample size at or above which a commander's disappearance is a failure.</summary>
    public required int MinEstablishedN { get; init; }

    /// <summary>Prior sample size at or above which a commander is checked for sample collapse.</summary>
    public required int MinPopulousN { get; init; }

    /// <summary>Maximum tolerated percentage drop in a populous commander's sample size.</summary>
    public required double MaxSampleDropPct { get; init; }

    /// <summary>Minimum absolute change in mean lands for a commander to count as a mover.</summary>
    public required double MoverThresholdLands { get; init; }

    /// <summary>Minimum number of movers before the one-sidedness test is meaningful.</summary>
    public required int MinMoversForDirectionTest { get; init; }

    /// <summary>Maximum tolerated percentage of movers travelling in the same direction.</summary>
    public required double MaxOneSidedPct { get; init; }
}

/// <summary>One reason a candidate baseline was rejected.</summary>
public sealed record CedhDriftFinding
{
    /// <summary>Name of the rule that fired.</summary>
    public required string Rule { get; init; }

    /// <summary>Commander the finding concerns, when the rule is per-commander.</summary>
    public string? Commander { get; init; }

    /// <summary>Human-readable explanation including the observed value and the limit breached.</summary>
    public required string Detail { get; init; }
}

/// <summary>Outcome of comparing a candidate baseline against the committed one.</summary>
public sealed record CedhDriftVerdict
{
    /// <summary>True when no rule fired.</summary>
    public required bool Passed { get; init; }

    /// <summary>Every rule breach found, in rule order.</summary>
    public required IReadOnlyList<CedhDriftFinding> Findings { get; init; }
}

/// <summary>
/// Compares a freshly built cEDH land baseline against the committed one and rejects shapes that
/// indicate corrupt input rather than metagame movement.
/// </summary>
/// <remarks>
/// Calibrated against the 2026-07-27 incident, where a double-faced-card resolution bug dropped
/// 208 card names — heavily weighted toward modal-DFC lands — and produced a snapshot that
/// under-counted roughly 1.9 lands per deck while the pipeline reported success.
/// </remarks>
public static class CedhBaselineDriftCheck
{
    /// <summary>Evaluate a candidate snapshot against the previous one.</summary>
    /// <param name="previous">The committed snapshot being replaced.</param>
    /// <param name="candidate">The freshly built snapshot.</param>
    /// <param name="thresholds">Limits loaded from the committed thresholds file.</param>
    /// <returns>A verdict carrying every rule breach found.</returns>
    public static CedhDriftVerdict Evaluate(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(thresholds);

        List<CedhDriftFinding> findings = [];
        AddDroppedEstablishedCommanders(previous, candidate, thresholds, findings);
        AddSampleCollapses(previous, candidate, thresholds, findings);
        AddOneSidedDrift(previous, candidate, thresholds, findings);

        return new CedhDriftVerdict { Passed = findings.Count == 0, Findings = findings };
    }

    private static void AddDroppedEstablishedCommanders(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds,
        List<CedhDriftFinding> findings)
    {
        foreach ((string name, CedhCommanderBaselineSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinEstablishedN || candidate.Commanders.ContainsKey(name))
            {
                continue;
            }

            findings.Add(new CedhDriftFinding
            {
                Rule = "DroppedEstablishedCommander",
                Commander = name,
                Detail =
                    $"present with n={prior.N} in the committed snapshot ({previous.Generated}) but absent "
                    + $"from the candidate; floor is n>={thresholds.MinEstablishedN}.",
            });
        }
    }

    private static void AddSampleCollapses(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds,
        List<CedhDriftFinding> findings)
    {
        foreach ((string name, CedhCommanderBaselineSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinPopulousN
                || !candidate.Commanders.TryGetValue(name, out CedhCommanderBaselineSnapshot? current))
            {
                continue;
            }

            double dropPct = (prior.N - current.N) / (double)prior.N * 100.0;
            if (dropPct <= thresholds.MaxSampleDropPct)
            {
                continue;
            }

            findings.Add(new CedhDriftFinding
            {
                Rule = "SampleCollapse",
                Commander = name,
                Detail =
                    $"sample fell {dropPct:0.0}% (n {prior.N} -> {current.N}); "
                    + $"limit is {thresholds.MaxSampleDropPct:0.#}%.",
            });
        }
    }

    private static void AddOneSidedDrift(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds,
        List<CedhDriftFinding> findings)
    {
        int up = 0;
        int down = 0;

        foreach ((string name, CedhCommanderBaselineSnapshot prior) in previous.Commanders)
        {
            if (!candidate.Commanders.TryGetValue(name, out CedhCommanderBaselineSnapshot? current))
            {
                continue;
            }

            double delta = current.LandsMean - prior.LandsMean;
            if (Math.Abs(delta) < thresholds.MoverThresholdLands)
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

        findings.Add(new CedhDriftFinding
        {
            Rule = "OneSidedDrift",
            Detail =
                $"{movers} commanders moved at least {thresholds.MoverThresholdLands:0.#} lands and "
                + $"{oneSidedPct:0.0}% went the same way (up {up}, down {down}); limit is "
                + $"{thresholds.MaxOneSidedPct:0.#}%. Metagame drift scatters; a one-sided shift "
                + "indicates systematic input corruption.",
        });
    }
}
