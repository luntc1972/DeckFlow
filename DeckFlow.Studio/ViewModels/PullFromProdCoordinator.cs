using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Orchestration for the Pull-from-Production workflow, extracted from the <c>PullFromProd</c> page
/// code-behind (H1 god-component split). Owns the read-only prod pull (staging prep, read-only prod
/// read, SCP artifact download, local classify) and the local-only adopt apply (content upsert +
/// approval mirror + staged-artifact promotion). This type performs no rendering and holds no
/// per-page UI state — the page keeps the progress log, resolution map, busy guards, cancellation,
/// and <c>StateHasChanged</c>. It NEVER writes to production. Behavior is identical to the prior
/// inline implementation.
/// </summary>
public sealed class PullFromProdCoordinator
{
    private readonly IContentSiteIndexStore _indexStore;
    private readonly ISshArtifactDownloader _sshDownloader;
    private readonly IProdContentReader _prodReader;
    private readonly IConfiguration _configuration;
    private readonly ContentKbOrchestratorOptions _options;
    private readonly ILogger<PullFromProdCoordinator> _logger;

    /// <summary>Creates the coordinator with the local store, SSH downloader, read-only prod reader, config, options, and logger.</summary>
    public PullFromProdCoordinator(
        IContentSiteIndexStore indexStore,
        ISshArtifactDownloader sshDownloader,
        IProdContentReader prodReader,
        IConfiguration configuration,
        ContentKbOrchestratorOptions options,
        ILogger<PullFromProdCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(sshDownloader);
        ArgumentNullException.ThrowIfNull(prodReader);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _indexStore = indexStore;
        _sshDownloader = sshDownloader;
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
    /// Reads the live production content index (read-only, NO DDL), downloads each prod artifact into
    /// an isolated staging area, and classifies the result against the local store — returning only
    /// the differing entries with their per-entry artifact-downloaded flag stamped. Emits the current
    /// stage name to <paramref name="onStage"/> (for diagnostic copy) and human-readable progress
    /// lines to <paramref name="log"/>. NEVER writes to production.
    /// </summary>
    public async Task<IReadOnlyList<SyncDiffEntry>> PullAndClassifyAsync(
        string stagingRoot,
        IProgress<string> log,
        IProgress<string> onStage,
        CancellationToken cancellationToken)
    {
        // Wipe + recreate staging so a partial prior pull never promotes stale files. Staging is
        // isolated from the live content-kb/ tree (Pitfall 4).
        onStage.Report("prepare staging");
        log.Report("Preparing staging area...");

        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        Directory.CreateDirectory(stagingRoot);

        // R1: read prod via the read-only reader — plain SELECT, NO EnsureSchemaAsync/DDL.
        onStage.Report("read production content_site_index");
        log.Report("Reading production content_site_index...");

        // Why: the prod conn string is read ephemerally here, never materialized into DI state (D-03/D-07).
        var rawConnStr = _configuration["Studio:ProdConnectionString"] ?? string.Empty;
        var prodRows = await _prodReader.ReadAllAsync(rawConnStr, cancellationToken).ConfigureAwait(false);

        log.Report($"  {prodRows.Count} row(s) read from production.");

        onStage.Report("download artifacts");
        log.Report($"Downloading {prodRows.Count} artifact(s)...");

        // Download each prod artifact into staging (remote + local both traversal-guarded inside the
        // downloader). RemoteRelativePath == LocalRelativePath == ArtifactPath.
        var requests = prodRows
            .Select(r => new SshDownloadRequest(r.ArtifactPath, r.ArtifactPath))
            .ToList();

        // Why: per-artifact progress is kept to only RemoteRelativePath + Success + sanitized
        // FailureReason — NEVER LocalPath or ex.Message (D-07 / T-62-04).
        var artifactProgress = new Progress<SshDownloadResult>(result =>
            log.Report(BuildArtifactLine(result)));

        var downloadResults = await _sshDownloader
            .DownloadArtifactsAsync(requests, stagingRoot, artifactProgress, cancellationToken)
            .ConfigureAwait(false);

        var downloadedSet = new HashSet<string>(
            downloadResults.Where(r => r.Success).Select(r => r.RemoteRelativePath),
            StringComparer.Ordinal);

        onStage.Report("classify");
        log.Report("Classifying diff against local store...");

        var localRows = await _indexStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

        // Classify (omits in-sync pairs, R3), then stamp ArtifactDownloaded per entry.
        var entries = ContentSyncDiffClassifier.Classify(prodRows, localRows)
            .Select(e => e with { ArtifactDownloaded = downloadedSet.Contains(e.ArtifactPath) })
            .ToList();

        log.Report($"Done — {entries.Count} differing entry/entries found. "
            + $"{downloadedSet.Count}/{requests.Count} artifact(s) downloaded.");

        return entries;
    }

    /// <summary>
    /// Applies "adopt prod" resolutions to the LOCAL store only: content-columns-only upsert +
    /// approval-status mirror, then best-effort promotion of the staged artifact into the live tree.
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
                if (entry.ArtifactDownloaded)
                {
                    // Promote the staged artifact into the live tree (local only). The row upsert above
                    // is the primary effect of adopt and has already succeeded; artifact promotion is
                    // best-effort and must NOT fail the whole entry.
                    var stagedPath = Path.Combine(stagingRoot, entry.ArtifactPath);
                    var liveDest = Path.Combine(dataRoot, entry.ArtifactPath);

                    if (File.Exists(stagedPath))
                    {
                        var liveDir = Path.GetDirectoryName(liveDest);
                        if (!string.IsNullOrEmpty(liveDir))
                        {
                            Directory.CreateDirectory(liveDir);
                        }

                        File.Move(stagedPath, liveDest, overwrite: true);
                        note = "row updated + artifact promoted; approval mirrored from prod";
                    }
                    else if (File.Exists(liveDest))
                    {
                        // Staged source already consumed (e.g. a prior apply moved it) but the artifact
                        // is already present locally — adopt is still complete.
                        note = "row updated; artifact already present locally; approval mirrored from prod";
                    }
                    else
                    {
                        // Marked downloaded but neither staged nor live copy exists — promote is
                        // skipped, row adopt still stands (do not fail the entry).
                        _logger.LogWarning(
                            "Pull-from-prod adopt: artifact marked downloaded but no staged or live file for {KeyType}:{KeyValue}; promoted skipped.",
                            keyType, keyValue);
                        note = "row updated; artifact file missing, not promoted; approval mirrored from prod";
                    }
                }
                else
                {
                    // R4: partial pull — upsert + approval still applied; skip ONLY File.Move.
                    note = "row updated; artifact not promoted (not downloaded)";
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

    private static string BuildArtifactLine(SshDownloadResult result) =>
        result.Success
            ? $"  downloaded {result.RemoteRelativePath}"
            : $"  not downloaded: {result.RemoteRelativePath}"
              + (string.IsNullOrEmpty(result.FailureReason)
                  ? string.Empty
                  : $" — {result.FailureReason}");
}

/// <summary>Data root + isolated pull-staging directory resolved for the Pull-from-Prod page.</summary>
/// <param name="DataRoot">Studio data root (parent of <c>ArtifactRoot</c>).</param>
/// <param name="StagingRoot">Isolated directory pulled artifacts are downloaded into before adopt.</param>
public sealed record PullPaths(string DataRoot, string StagingRoot);

/// <summary>One per-entry outcome of applying a Pull-from-Prod adopt resolution to the local store.</summary>
/// <param name="Title">Entry title (display).</param>
/// <param name="KeyType">Natural key type label.</param>
/// <param name="KeyValue">Natural key value.</param>
/// <param name="Success">True when the local row upsert + approval mirror succeeded.</param>
/// <param name="Action">Short action label ("Adopted" / "Failed").</param>
/// <param name="Note">Sanitized per-entry note (never carries a secret).</param>
public sealed record PullApplyRowResult(string Title, string KeyType, string KeyValue, bool Success, string Action, string Note);
