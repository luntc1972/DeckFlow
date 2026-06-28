using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Orchestration for the Direct Push (publish-to-production) workflow, extracted from the
/// <c>DirectPush</c> page code-behind (H1 god-component split). Owns the prod read / content
/// diff / artifact upload / transactional write sequences and the pure diff classification so
/// they are unit-testable without bUnit. This type performs no rendering and holds no per-page
/// UI state — the page keeps all busy guards, error-copy mapping, logging, cancellation, and
/// <c>StateHasChanged</c>. Behavior is identical to the prior inline implementation.
/// </summary>
public sealed class DirectPushCoordinator
{
    private readonly IContentSiteIndexStore _localStore;
    private readonly ISshArtifactUploader _uploader;
    private readonly IProdStoreFactory _prodStoreFactory;
    private readonly IConfiguration _configuration;
    private readonly ContentKbOrchestratorOptions _options;

    /// <summary>Creates the coordinator with the stores, uploader, configuration, and KB options.</summary>
    public DirectPushCoordinator(
        IContentSiteIndexStore localStore,
        ISshArtifactUploader uploader,
        IProdStoreFactory prodStoreFactory,
        IConfiguration configuration,
        ContentKbOrchestratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(localStore);
        ArgumentNullException.ThrowIfNull(uploader);
        ArgumentNullException.ThrowIfNull(prodStoreFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        _localStore = localStore;
        _uploader = uploader;
        _prodStoreFactory = prodStoreFactory;
        _configuration = configuration;
        _options = options;
    }

    /// <summary>
    /// Reads the approved-row count and resolves the data root (parent of <c>ArtifactRoot</c>,
    /// which already carries the content-kb/ segment — D-01/D-03/D-10).
    /// </summary>
    public async Task<DirectPushInitData> LoadInitDataAsync(CancellationToken cancellationToken)
    {
        var rows = await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
        var dataRoot = Path.GetDirectoryName(_options.ArtifactRoot) ?? _options.ArtifactRoot;
        return new DirectPushInitData(rows.Count, dataRoot);
    }

    /// <summary>
    /// Reads local approved rows and all prod rows, then runs the content-aware classification
    /// (M2). The prod store is built on demand from the ephemeral connection string (D-03) and
    /// the read issues no DDL against prod (H3).
    /// </summary>
    public async Task<DirectPushDiff> ComputeDiffAsync(CancellationToken cancellationToken)
    {
        var localRows = await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);

        var rawConnStr = _configuration["Studio:ProdConnectionString"] ?? string.Empty;
        var prodStore = _prodStoreFactory.Create(rawConnStr);
        var prodRows = await prodStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

        return ClassifyDiff(localRows, prodRows);
    }

    /// <summary>
    /// Pure content-aware diff (M2): classifies each local row as New, Updated, or Unchanged
    /// against prod. The diff map is keyed on the FULL natural key (type + value) joined by U+0000
    /// so a prod podcast row and a local youtube row that share a value cannot collide and silently
    /// skip a publish (Codex MED data-loss fix). Unchanged rows (identical content signature) are
    /// excluded from the publish set, so they are never uploaded or written.
    /// </summary>
    public static DirectPushDiff ClassifyDiff(
        IReadOnlyList<ContentSiteIndexRow> localRows,
        IReadOnlyList<ContentSiteIndexRow> prodRows)
    {
        ArgumentNullException.ThrowIfNull(localRows);
        ArgumentNullException.ThrowIfNull(prodRows);

        var prodByKey = new Dictionary<string, ContentSiteIndexRow>(prodRows.Count, StringComparer.Ordinal);
        foreach (var r in prodRows)
        {
            var (prodKeyType, prodKeyValue) = DeriveNaturalKey(r);
            if (!string.IsNullOrEmpty(prodKeyValue))
            {
                prodByKey[$"{prodKeyType}\u0000{prodKeyValue}"] = r;
            }
        }

        int newCount = 0, updatedCount = 0, unchangedCount = 0;
        var diffRows = new List<DirectPushDiffRow>();
        var publishRows = new List<ContentSiteIndexRow>();
        foreach (var row in localRows)
        {
            var (keyType, key) = DeriveNaturalKey(row);
            if (!prodByKey.TryGetValue($"{keyType}\u0000{key}", out var prodRow))
            {
                newCount++;
                publishRows.Add(row);
                diffRows.Add(new DirectPushDiffRow(row.Title, keyType, key, true, Path.GetFileName(row.ArtifactPath)));
            }
            else if (!ContentSiteIndexContentSignature.AreContentEqual(row, prodRow))
            {
                updatedCount++;
                publishRows.Add(row);
                diffRows.Add(new DirectPushDiffRow(row.Title, keyType, key, false, Path.GetFileName(row.ArtifactPath)));
            }
            else
            {
                // Unchanged: content signature matches — skip SCP and DB write entirely.
                unchangedCount++;
            }
        }

        return new DirectPushDiff(localRows, publishRows, diffRows, newCount, updatedCount, unchangedCount);
    }

    /// <summary>
    /// Uploads the publish set's artifacts over SCP. Only New + Updated rows are uploaded (M2);
    /// Unchanged rows already have identical artifacts in prod from a prior push. Per-file results
    /// stream through <paramref name="progress"/>.
    /// </summary>
    public async Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        string dataRoot,
        IProgress<SshUploadResult> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);

        var requests = publishRows
            .Select(r => new SshUploadRequest(
                Path.GetFullPath(Path.Combine(dataRoot, r.ArtifactPath)),
                r.ArtifactPath))
            .ToList();

        return await _uploader.UploadArtifactsAsync(requests, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the publish set to prod as a single transactional batch (H4) and then stamps
    /// pushed-to-prod + visibility. Production state is written FIRST (prod batch upsert → stamp →
    /// visibility), then the local store is advanced, so a prod failure leaves the local row behind
    /// rather than over-reporting prod (PUB-01/HIGH-3). Only the content-columns-only upsert runs on
    /// prod, preserving is_visible / is_evergreen on existing rows (SC3 / D-08). Throws
    /// <see cref="ContentSiteIndexBatchUpsertException"/> (whole batch rolled back) or the underlying
    /// store exception to the caller; this method maps no error copy and writes no log.
    /// </summary>
    public async Task WritePublishAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);

        var rawConnStr = _configuration["Studio:ProdConnectionString"] ?? string.Empty;
        var prodStore = _prodStoreFactory.Create(rawConnStr);

        await prodStore.UpsertContentColumnsOnlyBatchAsync(publishRows, cancellationToken).ConfigureAwait(false);

        var keys = publishRows
            .Select(row => ContentIndexExportRow.From(row))
            .Select(row => (Type: row.NaturalKeyType, Value: row.NaturalKeyValue))
            .ToList();
        var pushedUtc = DateTimeOffset.UtcNow;

        await prodStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
        await prodStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
        await _localStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
        await _localStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
    }

    // Why: the diff loop and the push loop derive the same (display keyType, key value) pair.
    // KeyType is the local diff label ("youtube"/"podcast"), intentionally NOT the store's
    // youtube_channel/podcast_rss discriminator — matching is on the key value, not the type.
    private static (string KeyType, string KeyValue) DeriveNaturalKey(ContentSiteIndexRow row)
        => (row.YoutubeVideoId is not null ? "youtube" : "podcast",
            row.YoutubeVideoId ?? row.RssGuid ?? string.Empty);
}

/// <summary>Approved-row count and resolved data root for the DirectPush page init.</summary>
public sealed record DirectPushInitData(int ApprovedCount, string DataRoot);

/// <summary>A single New/Updated row shown in the diff preview table.</summary>
public sealed record DirectPushDiffRow(string Title, string KeyType, string KeyValue, bool IsNew, string ArtifactFile);

/// <summary>
/// Result of the content-aware diff: the approved local rows, the publish set (New + Updated only),
/// the per-row display rows, and the New/Updated/Unchanged counts.
/// </summary>
public sealed record DirectPushDiff(
    IReadOnlyList<ContentSiteIndexRow> ApprovedRows,
    IReadOnlyList<ContentSiteIndexRow> PublishRows,
    IReadOnlyList<DirectPushDiffRow> DiffRows,
    int NewCount,
    int UpdatedCount,
    int UnchangedCount);
