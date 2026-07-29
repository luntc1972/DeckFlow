using System.Text.Json;
using System.Text.RegularExpressions;

using DeckFlow.Core.Research;

namespace DeckFlow.CLI;

/// <summary>
/// Runs the <c>role-floor-baseline</c> command: load the committed Phase 2 research findings,
/// enforce fail-closed guards, and emit the shipped commander-aware role-floor snapshot consumed
/// by the web app.
/// </summary>
internal static class RoleFloorBaselineCommandRunner
{
    private static readonly Regex GeneratedLabelRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Build the shipped commander-aware role-floor baseline snapshot.</summary>
    /// <param name="findingsPath">Path to the committed Phase 2 research findings JSON file.</param>
    /// <param name="outputDirectory">Directory to write <c>latest.json</c> into.</param>
    /// <param name="generated">Generation label in <c>YYYY-MM-DD</c> form.</param>
    /// <param name="thresholdsPath">Path to the drift-threshold configuration file.</param>
    public static Task<int> RunAsync(string findingsPath, string outputDirectory, string generated, string thresholdsPath)
    {
        if (string.IsNullOrWhiteSpace(findingsPath))
        {
            Console.Error.WriteLine("--findings is required.");
            return Task.FromResult(1);
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            Console.Error.WriteLine("--out is required.");
            return Task.FromResult(1);
        }

        if (!GeneratedLabelRegex.IsMatch(generated))
        {
            Console.Error.WriteLine("--generated must be in YYYY-MM-DD format.");
            return Task.FromResult(1);
        }

        try
        {
            if (!File.Exists(findingsPath))
            {
                Console.Error.WriteLine($"Research findings file not found at {findingsPath}.");
                return Task.FromResult(2);
            }

            RoleFloorDriftThresholds thresholds;
            try
            {
                if (!File.Exists(thresholdsPath))
                {
                    Console.Error.WriteLine($"Drift thresholds file not found at {thresholdsPath}.");
                    return Task.FromResult(1);
                }

                thresholds = RoleFloorDriftThresholds.FromJson(File.ReadAllText(thresholdsPath));
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Could not read drift inputs: {ex.Message}");
                return Task.FromResult(1);
            }

            RoleFloorFindingsDocument? document = JsonSerializer.Deserialize<RoleFloorFindingsDocument>(
                File.ReadAllText(findingsPath),
                JsonOptions);

            if (document is null)
            {
                Console.Error.WriteLine("Could not deserialize research findings.");
                return Task.FromResult(2);
            }

            List<string> nonPostgresExamples = [];
            foreach ((string commanderName, RoleFloorFindingsCommander commander) in document.Commanders)
            {
                foreach (string roleKey in RoleFloorBaseline.AdoptedRoleKeys)
                {
                    if (!commander.Roles.TryGetValue(roleKey, out RoleFloorFindingsRole? role)
                        || string.Equals(role.Source, "postgres", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Why: D-07 forbids mixing a mean-only non-Postgres arm into a shipped
                    // percentile floor, so any such row is fatal before Build runs.
                    nonPostgresExamples.Add($"{commanderName}/{roleKey}: source={role.Source}");
                }
            }

            if (nonPostgresExamples.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Found {nonPostgresExamples.Count} adopted-role row(s) with a non-postgres source; refusing to build.");
                foreach (string example in nonPostgresExamples.Take(20))
                {
                    Console.Error.WriteLine(example);
                }

                Console.Error.WriteLine(
                    "The snapshot would otherwise mix a mean-only arm into a percentile floor.");
                return Task.FromResult(1);
            }

            RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, generated);
            if (snapshot.Commanders.Count == 0)
            {
                Console.Error.WriteLine("The adoption filter kept zero commanders; nothing to write.");
                return Task.FromResult(2);
            }

            // Why: the 2026-07-27 corrupt cEDH regeneration overwrote committed artifacts that had
            // to be recovered from git, so this command reaches a verdict before creating any
            // directory or writing any file.
            string latestPath = Path.Combine(outputDirectory, "latest.json");
            if (File.Exists(latestPath))
            {
                RoleFloorBaselineSnapshot? previous;
                try
                {
                    previous = JsonSerializer.Deserialize<RoleFloorBaselineSnapshot>(
                        File.ReadAllText(latestPath),
                        JsonOptions);
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Could not read drift inputs: {ex.Message}");
                    return Task.FromResult(1);
                }

                if (previous is null)
                {
                    Console.Error.WriteLine($"Could not deserialize the committed snapshot at {latestPath}.");
                    return Task.FromResult(1);
                }

                RoleFloorDriftVerdict verdict = RoleFloorBaselineDriftCheck.Evaluate(previous, snapshot, thresholds);
                if (!verdict.Passed)
                {
                    Console.Error.WriteLine(
                        $"Drift check FAILED with {verdict.Findings.Count} finding(s); no files written.");
                    foreach (RoleFloorDriftFinding finding in verdict.Findings)
                    {
                        Console.Error.WriteLine($"  {FormatFinding(finding)}");
                    }

                    Console.Error.WriteLine(
                        "If this reflects a genuine corpus shift, retune and commit "
                        + $"{thresholdsPath}, then re-run.");
                    return Task.FromResult(1);
                }

                Console.WriteLine($"Drift check passed against {latestPath} using thresholds {thresholdsPath}.");
            }
            else
            {
                Console.WriteLine($"No committed snapshot at {latestPath}; skipping drift check (bootstrap run).");
            }

            Directory.CreateDirectory(outputDirectory);

            string snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
            SnapshotFileWriter.WriteLfFile(latestPath, snapshotJson);

            int byteSize = System.Text.Encoding.UTF8.GetByteCount(snapshotJson) + 1;
            Console.WriteLine($"Wrote {latestPath}");
            Console.WriteLine($"Commanders={snapshot.Commanders.Count}, AdoptedPairs={snapshot.AdoptedPairs}, Bytes={byteSize}");

            return Task.FromResult(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return Task.FromResult(1);
        }
    }

    private static string FormatFinding(RoleFloorDriftFinding finding)
    {
        if (finding.Commander is null && finding.Role is null)
        {
            return $"{finding.Rule}: {finding.Detail}";
        }

        return $"{finding.Rule} [{finding.Commander}/{finding.Role}]: {finding.Detail}";
    }
}
