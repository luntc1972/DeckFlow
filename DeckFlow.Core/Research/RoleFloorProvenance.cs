using Npgsql;

namespace DeckFlow.Core.Research;

/// <summary>
/// Pure provenance helpers for role-floor research artifacts.
/// </summary>
public static class RoleFloorProvenance
{
    /// <summary>
    /// Derives the database host display from a normalized Postgres connection string.
    /// </summary>
    /// <param name="normalizedConnectionString">The normalized Postgres connection string.</param>
    /// <returns>The host display, optionally including port and database; otherwise <c>unavailable</c>.</returns>
    public static string DescribeDatabaseHost(string? normalizedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(normalizedConnectionString))
        {
            return "unavailable";
        }

        try
        {
            // Why: this parses a live database credential and the resulting artifact is committed to
            // a public repository; CLAUDE.md classifies connection strings as secrets, so this code
            // must never fall back to the raw string or to any credential-bearing field.
            var builder = new NpgsqlConnectionStringBuilder(normalizedConnectionString);
            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                return "unavailable";
            }

            string result = builder.Host;
            if (builder.Port > 0)
            {
                result = $"{result}:{builder.Port}";
            }

            if (!string.IsNullOrWhiteSpace(builder.Database))
            {
                result = $"{result}/{builder.Database}";
            }

            return result;
        }
        catch
        {
            return "unavailable";
        }
    }

    /// <summary>
    /// Formats the harness commit SHA from git command output.
    /// </summary>
    /// <param name="exitCode">The <c>git rev-parse</c> exit code.</param>
    /// <param name="revParseStdout">The <c>git rev-parse --short HEAD</c> stdout text.</param>
    /// <param name="statusPorcelainStdout">The <c>git status --porcelain</c> stdout text.</param>
    /// <returns>The trimmed SHA, optionally with <c>-dirty</c>, or <c>unknown</c>.</returns>
    public static string FormatCommitSha(int exitCode, string? revParseStdout, string? statusPorcelainStdout)
    {
        if (exitCode != 0 || string.IsNullOrWhiteSpace(revParseStdout))
        {
            return "unknown";
        }

        string sha = revParseStdout.Trim();

        // Why: the harness may be run from a dirty tree, and emitting a clean-looking SHA when git
        // reports local changes would misstate the code state that produced the artifact.
        return string.IsNullOrWhiteSpace(statusPorcelainStdout)
            ? sha
            : $"{sha}-dirty";
    }

    /// <summary>
    /// Builds visible warnings for degraded provenance fields.
    /// </summary>
    /// <param name="databaseHost">The artifact's database host provenance field.</param>
    /// <param name="harnessCommitSha">The artifact's harness commit SHA provenance field.</param>
    /// <param name="rawDeckCount">The raw deck count captured by the run.</param>
    /// <param name="dedupedDeckCount">The deduped deck count captured by the run.</param>
    /// <returns>One warning per degraded provenance field, or an empty list when all fields resolved.</returns>
    public static IReadOnlyList<string> BuildProvenanceWarnings(
        string databaseHost,
        string harnessCommitSha,
        int rawDeckCount,
        int dedupedDeckCount)
    {
        var warnings = new List<string>();

        if (string.Equals(databaseHost, "unavailable", StringComparison.Ordinal))
        {
            if (rawDeckCount > 0 || dedupedDeckCount > 0)
            {
                warnings.Add(FormattableString.Invariant(
                    $"Provenance contradiction: the run reached the corpus ({rawDeckCount} raw decks, {dedupedDeckCount} deduped decks), but the database host could not be derived from the connection string, so this artifact cannot be traced to a specific database by its own contents."));
            }
            else
            {
                warnings.Add("Database host could not be derived from the connection string, and this run did not reach any deck rows, so the artifact cannot identify which database it was meant to query by its own contents.");
            }
        }

        if (string.Equals(harnessCommitSha, "unknown", StringComparison.Ordinal))
        {
            warnings.Add("The harness revision could not be determined, so this artifact cannot be tied to a specific code state.");
        }

        return warnings;
    }
}
