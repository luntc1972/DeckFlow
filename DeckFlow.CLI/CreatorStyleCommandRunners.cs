using System.Globalization;
using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.ProfileFusion;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.CLI;

/// <summary>
/// Runners for the creator-style operator-run commands: importing hand-authored stated rules and
/// fusing them with a creator's measured profile.
/// </summary>
internal static class CreatorStyleCommandRunners
{
    /// <summary>
    /// Imports a stated-rules seed file into <c>content_stated_rules</c>. Exit codes: 0 = success
    /// with at least one rule imported; 1 = missing seed file or unhandled exception; 2 = the seed
    /// file parsed but held no rules.
    /// </summary>
    /// <param name="file">Optional path to the stated-rules seed JSON file.</param>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunCreatorStyleImportStatedAsync(FileInfo? file, FileInfo? db)
    {
        try
        {
            var seedPath = file?.FullName ?? ContentKbPaths.CreatorStatedRulesSeedRelativePath;
            if (!File.Exists(seedPath))
            {
                Console.Error.WriteLine($"Stated-rules seed file not found: {seedPath}");
                return 1;
            }

            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var json = await File.ReadAllTextAsync(seedPath).ConfigureAwait(false);
            var seed = JsonSerializer.Deserialize<Dictionary<string, List<StatedRuleCandidate>>>(
                json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? [];

            var store = new CreatorStyleStatedRuleStore(dbPath);
            var ruleCount = 0;
            var slugCount = 0;
            foreach ((var slug, var rules) in seed)
            {
                if (rules is null || rules.Count == 0)
                {
                    continue;
                }

                slugCount++;
                foreach (var rule in rules)
                {
                    await store.UpsertAsync(rule, slug).ConfigureAwait(false);
                    ruleCount++;
                }
            }

            if (ruleCount == 0)
            {
                Console.Error.WriteLine($"Stated-rules seed at {seedPath} parsed but held no rules.");
                return 2;
            }

            Console.WriteLine($"Imported {ruleCount} stated rule(s) across {slugCount} creator slug(s) from {seedPath}.");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Fuses a creator's measured profile with its stated rules and persists the fused ledger back
    /// onto the profile row. Exit codes: 0 = success with a non-empty fused ledger persisted;
    /// 1 = bad arguments or unhandled exception; 2 = ran successfully but the measured profile or
    /// stated rules were missing.
    /// </summary>
    /// <param name="slug">Creator slug to fuse.</param>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunFuseProfileAsync(string slug, FileInfo? db)
    {
        try
        {
            var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
            var profileStore = new CreatorStyleProfileStore(dbPath);
            var statedRuleStore = new CreatorStyleStatedRuleStore(dbPath);

            var profile = await profileStore.GetBySlugAsync(slug).ConfigureAwait(false);
            if (profile is null)
            {
                Console.Error.WriteLine($"No measured profile found for slug '{slug}'. Run the crawl first.");
                return 2;
            }

            if (profile.MeasuredMetrics.Count == 0)
            {
                Console.Error.WriteLine($"Measured profile for slug '{slug}' has no measured metrics.");
                return 2;
            }

            var statedRules = await statedRuleStore.GetBySlugAsync(slug).ConfigureAwait(false);
            if (statedRules.Count == 0)
            {
                Console.Error.WriteLine($"No stated rules found for slug '{slug}'. Run creator-style-import-stated first.");
                return 2;
            }

            var fusedTargets = ProfileFusionEngine.Fuse(profile.MeasuredMetrics, statedRules);
            var statedRuleRows = statedRules
                .Select(rule => new StatedRule
                {
                    Category = rule.Category,
                    TargetMetric = rule.Metric,
                    TargetValue = rule.Value,
                    TargetValueMin = rule.ValueMin,
                    TargetValueMax = rule.ValueMax,
                    Comparator = rule.Comparator,
                    Condition = rule.Condition,
                    SourceClip = rule.SourceClip,
                    Confidence = rule.Confidence,
                    VideoDateUtc = rule.VideoDateUtc,
                })
                .ToArray();

            var updatedProfile = profile with
            {
                StatedRules = statedRuleRows,
                FusedTargets = fusedTargets,
                UpdatedUtc = DateTimeOffset.UtcNow,
            };

            // TODO(115-01 RED): persist the fused ledger back onto the profile row. Left
            // unimplemented for the tracer task's RED phase per grounding correction 4 — the GREEN
            // commit adds this call.
            PrintConflictLedger(fusedTargets);
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void PrintConflictLedger(IReadOnlyList<FusedTarget> fusedTargets)
    {
        foreach (var target in fusedTargets)
        {
            var conditionSuffix = string.IsNullOrEmpty(target.Condition) ? string.Empty : $" (condition: {target.Condition})";
            Console.WriteLine($"Metric: {target.Metric}{conditionSuffix}");

            if (target.StatedMin.HasValue || target.StatedMax.HasValue)
            {
                var min = target.StatedMin?.ToString(CultureInfo.InvariantCulture) ?? "-";
                var max = target.StatedMax?.ToString(CultureInfo.InvariantCulture) ?? "-";
                Console.WriteLine($"  Stated band: {min} .. {max}");
            }

            if (target.MeasuredValue.HasValue)
            {
                Console.WriteLine($"  Measured value: {target.MeasuredValue.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            var verdictReasonSuffix = string.IsNullOrEmpty(target.VerdictReason) ? string.Empty : $" ({target.VerdictReason})";
            Console.WriteLine($"  Verdict: {target.Verdict}{verdictReasonSuffix}");

            if (target.Conflict is not null)
            {
                var delta = target.Conflict.Delta.ToString(CultureInfo.InvariantCulture);
                Console.WriteLine($"  Delta: {delta}, Winner: {target.Conflict.Winner ?? "-"}");
            }

            Console.WriteLine();
        }
    }
}
