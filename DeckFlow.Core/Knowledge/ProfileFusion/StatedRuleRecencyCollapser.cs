using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Knowledge.ProfileFusion;

/// <summary>
/// Collapses same-scope stated rules by recency while retaining superseded history for the ledger.
/// </summary>
public static class StatedRuleRecencyCollapser
{
    /// <summary>
    /// Collapses rules into active and superseded sets keyed by metric and condition.
    /// </summary>
    /// <param name="rules">Rules to collapse.</param>
    /// <returns>Active winners plus superseded history.</returns>
    public static RecencyCollapseResult Collapse(IReadOnlyList<StatedRuleCandidate> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
        {
            return new RecencyCollapseResult([], []);
        }

        var buckets = new Dictionary<CollapseKey, (StatedRuleCandidate Candidate, int Index)>();
        var superseded = new List<(StatedRuleCandidate Candidate, int Index)>();

        for (int index = 0; index < rules.Count; index++)
        {
            StatedRuleCandidate candidate = rules[index];
            var key = new CollapseKey(candidate.Metric, candidate.Condition ?? string.Empty);

            if (!buckets.TryGetValue(key, out var current))
            {
                buckets[key] = (candidate, index);
                continue;
            }

            if (ShouldReplace(current.Candidate, candidate))
            {
                superseded.Add((current.Candidate, current.Index));
                buckets[key] = (candidate, current.Index);
                continue;
            }

            superseded.Add((candidate, index));
        }

        return new RecencyCollapseResult(
            buckets
                .OrderBy(static pair => pair.Value.Index)
                .Select(static pair => pair.Value.Candidate)
                .ToList(),
            superseded
                .OrderBy(static item => item.Index)
                .Select(static item => item.Candidate)
                .ToList());
    }

    private static bool ShouldReplace(StatedRuleCandidate current, StatedRuleCandidate challenger)
    {
        return challenger.VideoDateUtc > current.VideoDateUtc;
    }
}

/// <summary>
/// Result of recency collapsing stated rules into active and superseded groups.
/// </summary>
/// <param name="Active">Newest active rule per metric-condition bucket.</param>
/// <param name="Superseded">Older rules retained for ledger history.</param>
public sealed record RecencyCollapseResult(
    IReadOnlyList<StatedRuleCandidate> Active,
    IReadOnlyList<StatedRuleCandidate> Superseded);

internal sealed record CollapseKey(
    string Metric,
    string Condition);
