using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Orchestration for the Pull-from-Production workflow, extracted from the <c>PullFromProd</c> page
/// code-behind (H1 god-component split). Owns the read-only prod pull (read-only prod
/// read, local git-tree body resolution, local classify) and the local-only adopt apply (content
/// upsert + approval mirror + body copy). This type performs no rendering and holds no
/// per-page UI state — the page keeps the progress log, resolution map, busy guards, cancellation,
/// and <c>StateHasChanged</c>. It NEVER writes to production. Behavior is identical to the prior
/// inline implementation.
/// </summary>
public sealed class PullFromProdCoordinator
{
    private readonly IContentSiteIndexStore _indexStore;
    private readonly IGitRepository _git;
    private readonly IProdContentReader _prodReader;
    private readonly IConfiguration _configuration;
    private readonly ContentKbOrchestratorOptions _options;
    private readonly ILogger<PullFromProdCoordinator> _logger;

    /// <summary>Creates the coordinator with the local store, git repository, read-only prod reader, config, options, and logger.</summary>
    public PullFromProdCoordinator(
        IContentSiteIndexStore indexStore,
        IGitRepository git,
        IProdContentReader prodReader,
        IConfiguration configuration,
        ContentKbOrchestratorOptions options,
        ILogger<PullFromProdCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(prodReader);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _indexStore = indexStore;
        _git = git;
        _prodReader = prodReader;
        _configuration = configuration;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the data root (parent of <c>ArtifactRoot</c>, which already carries content-kb/) and
    /// the isolated pull-staging directory under it.
    /// </summary>
    public PullPaths ResolvePaths()
    {
        var dataRoot = Path.GetDirectoryName(_options.ArtifactRoot) ?? _options.ArtifactRoot;
        var stagingRoot = Path.Combine(dataRoot, "pull-staging");
        return new PullPaths(dataRoot, stagingRoot);
    }

    /// <summary>
    /// Reads the live production content index (read-only, NO DDL), resolves each prod body from the
    /// local git tree, and classifies the result against the local store — returning only the
    /// differing entries with their per-entry artifact-available flag stamped. Sets the current
    /// stage name via <paramref name="onStage"/> (a synchronous callback so a fault reads the exact
    /// stage in flight — diagnostic copy) and emits human-readable progress lines to
    /// <paramref name="log"/>. NEVER writes to production.
    /// </summary>
    public async Task<IReadOnlyList<SyncDiffEntry>> PullAndClassifyAsync(
        string stagingRoot,
        IProgress<string> log,
        Action<string> onStage,
        CancellationToken cancellationToken)
    {
        _ = stagingRoot;

        // R1: read prod via the read-only reader — plain SELECT, NO EnsureSchemaAsync/DDL.
        onStage("read production content_site_index");
        log.Report("Reading production content_site_index...");

        // Why: the prod conn string is read ephemerally here, never materialized into DI state (D-03/D-07).
        var rawConnStr = _configuration["Studio:ProdConnectionString"] ?? string.Empty;
        var prodRows = await _prodReader.ReadAllAsync(rawConnStr, cancellationToken).ConfigureAwait(false);

        log.Report($"  {prodRows.Count} row(s) read from production.");

        onStage("resolve local repo bodies");
        log.Report($"Resolving {prodRows.Count} body/bodies from local repository...");

        var repoRoot = await _git.ResolveRepoRootAsync(Directory.GetCurrentDirectory(), cancellationToken).ConfigureAwait(false);
        var availableSet = new HashSet<string>(
            prodRows
                .Where(r =>
                {
                    if (!TryBuildContainedPath(repoRoot, r.ArtifactPath, out var repoBody))
                    {
                        log.Report("  body SKIPPED (invalid path)");
                        return false;
                    }

                    var present = File.Exists(repoBody);
                    log.Report(present
                        ? $"  body present: {r.ArtifactPath}"
                        : $"  body MISSING (run 'git pull'): {r.ArtifactPath}");
                    return present;
                })
                .Select(r => r.ArtifactPath),
            StringComparer.Ordinal);

        onStage("classify");
        log.Report("Classifying diff against local store...");

        var localRows = await _indexStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

        // Classify (omits in-sync pairs, R3), then stamp ArtifactDownloaded per entry.
        var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows)
            .Select(e => e with { ArtifactDownloaded = availableSet.Contains(e.ArtifactPath) })
            .ToList();

        log.Report($"Done — {entries.Count} differing entry/entries found. "
            + $"{availableSet.Count}/{prodRows.Count} body/bodies resolved from the local repo.");

        return entries;
    }

    /// <summary>
    /// Applies "adopt prod" resolutions to the LOCAL store only: content-columns-only upsert +
    /// approval-status mirror, then best-effort copy of the git-tree body into the live tree.
    /// Production is never modified. Reports the running per-entry result list to
    /// <paramref name="progress"/> after each entry so the page can render incrementally. The caller
    /// pre-filters <paramref name="adoptEntries"/> to entries whose resolution is "adopt prod", that
    /// are not local-only, and that carry a prod row.
    /// </summary>
    public async Task<IReadOnlyList<PullApplyRowResult>> ApplyAdoptionsAsync(
        IReadOnlyList<SyncDiffEntry> adoptEntries,
        string stagingRoot,
        string dataRoot,
        IProgress<IReadOnlyList<PullApplyRowResult>> progress,
        CancellationToken cancellationToken)
    {
        var results = new List<PullApplyRowResult>();
        var repoRoot = await _git.ResolveRepoRootAsync(Directory.GetCurrentDirectory(), cancellationToken).ConfigureAwait(false);

        foreach (var entry in adoptEntries)
        {
            // Defensive: the page pre-filters these, but never adopt a local-only / prod-less row.
            if (entry.Kind == SyncDiffKind.LocalOnly || entry.ProdRow is null)
            {
                continue;
            }

            var prodRow = entry.ProdRow;
            // The store + SetApprovalStatusAsync key on the ContentSourceType discriminator
            // ("youtube_channel"/"podcast_rss"), NOT the classifier's short "youtube"/"podcast";
            // derive it from the row so the approval mirror matches the right row.
            var keyType = prodRow.YoutubeVideoId is not null
                ? ContentSourceType.Youtube
                : ContentSourceType.Podcast;
            var keyValue = entry.NaturalKeyValue;

            try
            {
                // LOCAL-only apply: content columns + mirror prod approval_status (Q2 — reflect prod's
                // actual state, never a blind pending). is_visible/is_hidden untouched — adopting never
                // auto-publishes. Never the full-row upsert (Pitfall 3).
                await _indexStore.UpsertContentColumnsOnlyAsync(prodRow, cancellationToken).ConfigureAwait(false);
                await _indexStore.SetApprovalStatusAsync(keyType, keyValue, prodRow.ApprovalStatus, cancellationToken).ConfigureAwait(false);

                var note = "row updated; approval mirrored from prod";
                var validSource = TryBuildContainedPath(repoRoot, entry.ArtifactPath, out var repoBody);
                var validDest = TryBuildContainedPath(dataRoot, entry.ArtifactPath, out var liveDest);
                if (!validSource || !validDest)
                {
                    note = "row updated; body path invalid, not copied; approval mirrored from prod";
                }
                else if (File.Exists(repoBody))
                {
                    // Copy the git-tree body into the live tree (local only). The row upsert above
                    // is the primary effect of adopt and has already succeeded; body copy is
                    // best-effort and must NOT fail the whole entry.
                    var liveDir = Path.GetDirectoryName(liveDest);
                    if (!string.IsNullOrEmpty(liveDir))
                    {
                        Directory.CreateDirectory(liveDir);
                    }

                    File.Copy(repoBody, liveDest, overwrite: true);
                    note = "row updated + body copied from local repo; approval mirrored from prod";
                }
                else
                {
                    // R4: partial local repo — upsert + approval still applied; skip ONLY File.Copy.
                    note = "row updated; body not in local repo — run 'git pull' to sync; approval mirrored from prod";
                }

                results.Add(new PullApplyRowResult(entry.Title, entry.NaturalKeyType, keyValue, true, "Adopted", note));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Why: log the full exception to the server-side Serilog file so "see logs" is truthful
                // and the failure is diagnosable; the UI note stays sanitized — never surface ex.Message,
                // which can carry secrets/paths (D-07).
                _logger.LogError(ex, "Local apply failed for pull-from-prod entry {KeyType}:{KeyValue}.", keyType, keyValue);
                results.Add(new PullApplyRowResult(entry.Title, entry.NaturalKeyType, keyValue, false, "Failed",
                    "Local apply failed for this entry — see logs."));
            }

            progress.Report(results.ToList());
        }

        return results;
    }

    private static bool TryBuildContainedPath(string root, string artifactPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!IsSafeArtifactPath(artifactPath))
        {
            return false;
        }

        var rootFull = Path.GetFullPath(root);
        var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rootFull, artifactPath));
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    private static bool IsSafeArtifactPath(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return false;
        }

        if (Path.IsPathRooted(artifactPath)
            || IsWindowsRootedPath(artifactPath)
            || artifactPath[0] == '/'
            || artifactPath[0] == '\\')
        {
            return false;
        }

        if (!artifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = artifactPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0
            && !segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');

}

/// <summary>Data root + isolated pull-staging directory resolved for the Pull-from-Prod page.</summary>
/// <param name="DataRoot">Studio data root (parent of <c>ArtifactRoot</c>).</param>
/// <param name="StagingRoot">Legacy isolated staging directory path retained for page compatibility.</param>
public sealed record PullPaths(string DataRoot, string StagingRoot);

/// <summary>One per-entry outcome of applying a Pull-from-Prod adopt resolution to the local store.</summary>
/// <param name="Title">Entry title (display).</param>
/// <param name="KeyType">Natural key type label.</param>
/// <param name="KeyValue">Natural key value.</param>
/// <param name="Success">True when the local row upsert + approval mirror succeeded.</param>
/// <param name="Action">Short action label ("Adopted" / "Failed").</param>
/// <param name="Note">Sanitized per-entry note (never carries a secret).</param>
public sealed record PullApplyRowResult(string Title, string KeyType, string KeyValue, bool Success, string Action, string Note);
