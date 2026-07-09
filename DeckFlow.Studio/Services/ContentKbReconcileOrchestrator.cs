using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Services;

/// <summary>
/// I/O orchestrator for the SYNC-11 reconcile dry-run (D-04): reads prod exactly once, enumerates
/// the operator's git <c>content-kb/**/*.md</c> tree, reads <c>index-seed.json</c> via the
/// availability-aware <see cref="SeedIndexFileReader.Read"/>, feeds the pure
/// <see cref="ContentKbReconcileClassifier"/>, and persists scope-tagged results to the local
/// <see cref="IContentKbReconcileStore"/> (D-05). Mirrors <see cref="GitBodyCoverageAudit"/>'s shape
/// exactly (constructor takes only the structurally read-only <see cref="IProdContentReader"/>,
/// never <c>IProdStoreFactory</c> — T-90-04 precedent) and reuses every existing I/O seam
/// (<see cref="ArtifactPathSafety"/>, <see cref="StudioRepoLocator"/>). Issues no DDL and no
/// destructive write of any kind.
/// </summary>
public sealed class ContentKbReconcileOrchestrator : IContentKbReconcileOrchestrator
{
    private readonly IProdContentReader _prodReader;
    private readonly IContentKbReconcileStore _store;
    private readonly IGitRepository _git;
    private readonly IConfiguration _configuration;
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
        IConfiguration configuration,
        ILogger<ContentKbReconcileOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(prodReader);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _prodReader = prodReader;
        _store = store;
        _git = git;
        _configuration = configuration;
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
        var rawConnStr = _configuration["Studio:ProdConnectionString"] ?? string.Empty;
        var prodRows = await _prodReader.ReadAllAsync(rawConnStr, cancellationToken).ConfigureAwait(false);

        var (existingGitBodyRelPaths, gitBodyByRelPath) = ReadGitContentTree(repoRoot);

        var seedFilePath = Path.Combine(repoRoot, "content-kb", "seed", "index-seed.json");
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

        return new ReconcileDryRunResult(seedIndex.SeedAvailable, discrepancies);
    }

    /// <summary>
    /// Enumerates every <c>.md</c> file under <c>{repoRoot}/content-kb</c>, converting each to a
    /// content-kb-relative artifact path (forward-slash normalized, so the result is byte-identical
    /// on Windows and Linux checkouts) validated by <see cref="ArtifactPathSafety.IsSafeArtifactPath"/>
    /// before being added to either collection — ONE scan builds BOTH the existing-paths set
    /// (published-orphan + file-orphan) and the body-text map (body-hash-mismatch), per 91-RESEARCH
    /// Open Question 2.
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
}
