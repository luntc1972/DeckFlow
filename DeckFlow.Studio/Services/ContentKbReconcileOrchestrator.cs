using System.Text;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Services;

/// <summary>
/// I/O orchestrator for the SYNC-11 reconcile dry-run (D-04): reads prod exactly once, enumerates
/// the operator's git <c>content-kb/**/*.md</c> tree, reads <c>index-seed.json</c> via the
/// availability-aware <see cref="SeedIndexFileReader.Read"/>, feeds the pure
/// <see cref="ContentKbReconcileClassifier"/>, persists scope-tagged results to the local
/// <see cref="IContentKbReconcileStore"/> (D-05), and writes the git-tracked D-06 human-readable
/// report. Mirrors <see cref="GitBodyCoverageAudit"/>'s shape exactly (constructor takes only the
/// structurally read-only <see cref="IProdContentReader"/>, never <c>IProdStoreFactory</c> — T-90-04
/// precedent) and reuses every existing I/O seam (<see cref="ArtifactPathSafety"/>,
/// <see cref="StudioRepoLocator"/>). Issues no DDL and no destructive write of any kind.
/// </summary>
public sealed class ContentKbReconcileOrchestrator : IContentKbReconcileOrchestrator
{
    /// <summary>
    /// Content-kb-relative path of the D-06 report this orchestrator itself writes. Excluded from
    /// the file-orphan enumeration (alongside <c>content-kb/seed/index-seed.json</c>, which is
    /// naturally excluded by the <c>*.md</c> glob) so the orchestrator's own output can never
    /// self-classify as an orphaned content body on a subsequent run — no prod row will ever claim
    /// this artifact path.
    /// </summary>
    internal const string ReportRelativePath = "content-kb/reconcile-report.md";

    private readonly IProdContentReader _prodReader;
    private readonly IContentKbReconcileStore _store;
    private readonly IGitRepository _git;
    private readonly IStudioProdConnectionSource _prodConnection;
    private readonly ILogger<ContentKbReconcileOrchestrator> _logger;

    /// <summary>
    /// Creates the orchestrator over the read-only prod reader, the local discrepancy store, the
    /// git repository adapter (repo-root resolution only — no destructive git operation is ever
    /// invoked here), configuration (for the ephemeral prod connection string, read exactly once
    /// per run and never materialized into DI state — mirrors <c>PullFromProdCoordinator</c>), and
    /// a logger.
    /// </summary>
    public ContentKbReconcileOrchestrator(
        IProdContentReader prodReader,
        IContentKbReconcileStore store,
        IGitRepository git,
        IStudioProdConnectionSource prodConnection,
        ILogger<ContentKbReconcileOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(prodReader);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(prodConnection);
        ArgumentNullException.ThrowIfNull(logger);

        _prodReader = prodReader;
        _store = store;
        _git = git;
        _prodConnection = prodConnection;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ReconcileDryRunResult> RunDryRunAsync(string scopeTag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeTag);

        var repoRoot = await _git
            .ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory(), cancellationToken)
            .ConfigureAwait(false);

        // Why (T-91-15): read prod EXACTLY ONCE per run, never per-row — the ephemeral connection
        // string is read here and never materialized into DI state (D-03/D-07 precedent).
        var rawConnStr = _prodConnection.ConnectionString;
        var prodRows = await _prodReader.ReadAllAsync(rawConnStr, cancellationToken).ConfigureAwait(false);

        var (existingGitBodyRelPaths, gitBodyByRelPath) = ReadGitContentTree(repoRoot);

        var seedFilePath = Path.Combine(repoRoot, ContentKbSeedPaths.SeedRelativePath);
        var seedIndex = SeedIndexFileReader.Read(seedFilePath, _logger);

        var discrepancies = ContentKbReconcileClassifier.Classify(
            prodRows,
            existingGitBodyRelPaths,
            seedIndex,
            gitBodyByRelPath,
            _logger);

        await _store
            .PersistRunAsync(scopeTag, discrepancies, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        var result = new ReconcileDryRunResult(seedIndex.SeedAvailable, discrepancies);
        WriteReport(repoRoot, result);
        return result;
    }

    /// <summary>
    /// Enumerates every <c>.md</c> file under <c>{repoRoot}/content-kb</c>, converting each to a
    /// content-kb-relative artifact path (forward-slash normalized, so the result is byte-identical
    /// on Windows and Linux checkouts) validated by <see cref="ArtifactPathSafety.IsSafeArtifactPath"/>
    /// before being added to either collection — ONE scan builds BOTH the existing-paths set
    /// (published-orphan + file-orphan) and the body-text map (body-hash-mismatch), per 91-RESEARCH
    /// Open Question 2. The orchestrator's own D-06 report is excluded (see
    /// <see cref="ReportRelativePath"/>).
    /// </summary>
    private static (
        IReadOnlySet<string> ExistingPaths,
        IReadOnlyDictionary<string, string> BodyByRelPath) ReadGitContentTree(string repoRoot)
    {
        var contentKbRoot = Path.Combine(repoRoot, "content-kb");
        var existingPaths = new HashSet<string>(StringComparer.Ordinal);
        var bodyByRelPath = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!Directory.Exists(contentKbRoot))
        {
            return (existingPaths, bodyByRelPath);
        }

        foreach (var fullPath in Directory.EnumerateFiles(contentKbRoot, "*.md", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(repoRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            if (string.Equals(relPath, ReportRelativePath, StringComparison.Ordinal))
            {
                continue;
            }

            // Why (T-91-14): a file->row artifact path is validated with the SAME shared guard used
            // for row->file resolution — a path that would fail containment is simply skipped, never
            // used to probe or read outside the content-kb root.
            if (!ArtifactPathSafety.IsSafeArtifactPath(relPath))
            {
                continue;
            }

            existingPaths.Add(relPath);
            bodyByRelPath[relPath] = File.ReadAllText(fullPath);
        }

        return (existingPaths, bodyByRelPath);
    }

    /// <summary>
    /// Writes the D-06 human-readable report to <see cref="ReportRelativePath"/> under
    /// <paramref name="repoRoot"/>. Layered on top of the store (D-05 remains the source of truth);
    /// this is reporting only.
    /// </summary>
    private void WriteReport(string repoRoot, ReconcileDryRunResult result)
    {
        var reportPath = Path.Combine(repoRoot, ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(reportPath, BuildReportText(result));
    }

    /// <summary>
    /// Renders <paramref name="result"/> to a sectioned Markdown report — one section per
    /// discrepancy class with its count, mining only the AppendSection SHAPE of
    /// <c>ReconciliationReporter</c> (91-RESEARCH Anti-Patterns: that type's own domain records are
    /// NOT reused). When the seed was unavailable, the seed-drift section is replaced with a
    /// prominent advisory instead of an empty list, so the operator never reads "no drift" from a
    /// run that actually skipped seed-drift detection entirely (T-91-26).
    /// </summary>
    internal static string BuildReportText(ReconcileDryRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.AppendLine("# Content KB Reconcile Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();

        AppendSection(builder, ContentKbReconcileKind.PublishedOrphan, "Published Orphans (visible+approved row, no git body)", result);
        builder.AppendLine();
        AppendSection(builder, ContentKbReconcileKind.FileOrphan, "File Orphans (git body, no matching prod row)", result);
        builder.AppendLine();

        if (result.SeedAvailable)
        {
            AppendSection(builder, ContentKbReconcileKind.SeedDrift, "Seed Drift (seed-managed row absent from seed)", result);
        }
        else
        {
            builder.AppendLine("## Seed Drift (seed-managed row absent from seed)");
            builder.AppendLine();
            builder.AppendLine(
                "  SEED UNAVAILABLE - seed-drift/removal skipped. index-seed.json could not be read "
                + "or parsed for this run, so NO seed-managed row was evaluated for drift. This is "
                + "NOT the same as \"no drift found\" - re-run once the seed file is restored.");
        }

        builder.AppendLine();
        AppendSection(builder, ContentKbReconcileKind.BodyHashMismatch, "Body Hash Mismatches (prod hash differs from computed git body hash)", result);

        return builder.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder builder, ContentKbReconcileKind kind, string header, ReconcileDryRunResult result)
    {
        var items = result.Discrepancies.Where(d => d.Kind == kind).ToList();
        builder.AppendLine($"## {header} ({items.Count})");
        builder.AppendLine();
        if (items.Count == 0)
        {
            builder.AppendLine("  none");
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"  - {item.ToDisplayLabel()}");
        }
    }
}
