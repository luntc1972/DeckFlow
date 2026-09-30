using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly IStudioProdConnectionSource _prodConnection;
    private readonly ContentKbOrchestratorOptions _options;
    private readonly IPrivateKbRootProvider _privateKbRootProvider;
    private readonly IContentKbOrchestrator _orchestrator;
    private readonly IProdContentReader _prodReader;
    private readonly CreatorSuppressionRowFilter _suppressionFilter;
    private readonly ILogger<DirectPushCoordinator> _logger;
    /// <summary>Creates the coordinator with local, production, upload, publishing, suppression, and logging dependencies.</summary>
    public DirectPushCoordinator(
        IContentSiteIndexStore localStore,
        ISshArtifactUploader uploader,
        IProdStoreFactory prodStoreFactory,
        IStudioProdConnectionSource prodConnection,
        ContentKbOrchestratorOptions options,
        IPrivateKbRootProvider privateKbRootProvider,
        IContentKbOrchestrator orchestrator,
        IProdContentReader prodReader,
        CreatorSuppressionRowFilter suppressionFilter,
        ILogger<DirectPushCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localStore);
        ArgumentNullException.ThrowIfNull(uploader);
        ArgumentNullException.ThrowIfNull(prodStoreFactory);
        ArgumentNullException.ThrowIfNull(prodConnection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(privateKbRootProvider);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(prodReader);
        _localStore = localStore;
        _uploader = uploader;
        _prodStoreFactory = prodStoreFactory;
        _prodConnection = prodConnection;
        _options = options;
        _privateKbRootProvider = privateKbRootProvider;
        _orchestrator = orchestrator;
        _prodReader = prodReader;
        _suppressionFilter = suppressionFilter;
        // Optional logger (house convention, e.g. CommanderSpellbookService): the default keeps every
        // existing construction site + test compiling while D-08 skip-warnings surface in prod.
        _logger = logger ?? NullLogger<DirectPushCoordinator>.Instance;
    }

    /// <summary>
    /// Reads the approved-row count and resolves the data root (parent of <c>ArtifactRoot</c>,
    /// which already carries the content-kb/ segment — D-01/D-03/D-10).
    /// </summary>
    public async Task<DirectPushInitData> LoadInitDataAsync(CancellationToken cancellationToken)
    {
        var rows = await _suppressionFilter.GetAllowedAsync(await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var dataRoot = Path.GetDirectoryName(_options.ArtifactRoot) ?? _options.ArtifactRoot;
        return new DirectPushInitData(rows.Count, dataRoot);
    }

    /// <summary>
    /// Reads local approved rows and all prod rows, then runs the content-aware classification
    /// (M2). The prod store is built on demand from the ephemeral connection string (D-03); because
    /// <see cref="IProdStoreFactory"/> builds it with schema-ensure disabled, the read issues no DDL
    /// against prod (H3 / D-10) — prod schema is owned by the web app's startup path.
    /// </summary>
    public async Task<DirectPushDiff> ComputeDiffAsync(CancellationToken cancellationToken)
    {
        var localRows = await _suppressionFilter.GetAllowedAsync(await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

        var prodStore = CreateProdStore();
        var prodRows = await prodStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

        return ClassifyDiff(localRows, prodRows, _logger);
    }

    /// <summary>
    /// Pure content-aware diff (M2): classifies each local row as New, Updated, or Unchanged
    /// against prod. The diff map is keyed on the FULL natural key (type + value) joined by U+0000
    /// so a prod podcast row and a local youtube row that share a value cannot collide and silently
    /// skip a publish (Codex MED data-loss fix). Both sides key through the shared
    /// <see cref="ContentNaturalKey.TryDerive"/> helper so this path can never diverge from the
    /// <see cref="ContentSyncDiffClassifier"/> (SYNC-05). Unchanged rows (identical content signature)
    /// are excluded from the publish set, so they are never uploaded or written.
    /// </summary>
    /// <param name="localRows">Approved local rows to publish.</param>
    /// <param name="prodRows">All rows currently in prod.</param>
    /// <param name="logger">Optional logger; warns on rows skipped for having no natural key (D-08).</param>
    public static DirectPushDiff ClassifyDiff(
        IReadOnlyList<ContentSiteIndexRow> localRows,
        IReadOnlyList<ContentSiteIndexRow> prodRows,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localRows);
        ArgumentNullException.ThrowIfNull(prodRows);

        var prodByKey = new Dictionary<string, ContentSiteIndexRow>(prodRows.Count, StringComparer.Ordinal);
        foreach (var r in prodRows)
        {
            if (!ContentNaturalKey.TryDerive(r, out var prodNk))
            {
                logger?.LogWarning(
                    "Skipping prod content row with no natural key (neither YouTube id nor RSS guid): {Title} [{Source}]",
                    r.Title,
                    r.Source);
                continue;
            }

            prodByKey[$"{prodNk.Type}\u0000{prodNk.Value}"] = r;
        }

        int newCount = 0, updatedCount = 0, unchangedCount = 0;
        var diffRows = new List<DirectPushDiffRow>();
        var publishRows = new List<ContentSiteIndexRow>();
        foreach (var row in localRows)
        {
            if (!ContentNaturalKey.TryDerive(row, out var localNk))
            {
                logger?.LogWarning(
                    "Skipping local content row with no natural key (neither YouTube id nor RSS guid): {Title} [{Source}]",
                    row.Title,
                    row.Source);
                continue;
            }

            var (keyType, key) = localNk;
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

        return new DirectPushDiff(publishRows, diffRows, newCount, updatedCount, unchangedCount);
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

        await EnsureDirectPushGitBodyFlagOffAsync(cancellationToken).ConfigureAwait(false);

        publishRows = await _suppressionFilter.GetAllowedAsync(publishRows, cancellationToken).ConfigureAwait(false);

        var requests = publishRows
            .Select(r => new SshUploadRequest(
                Path.GetFullPath(Path.Combine(dataRoot, r.ArtifactPath)),
                r.ArtifactPath))
            .ToList();

        return await _uploader.UploadArtifactsAsync(requests, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Content-only write stage (D-06/D-07 expand step): writes the publish set to prod as a single
    /// transactional batch (H4) and marks the pushed keys durably "awaiting confirm" in the LOCAL
    /// store (D-10). Does NOT stamp <c>pushed_to_prod_utc</c> or flip <c>is_visible</c> on either
    /// store — those happen only in <see cref="ConfirmAndPublishAsync"/>, after the approved body
    /// has been exported to the private KB root, so a row can never go visible before its body is
    /// durably available there. Only the content-columns-only upsert runs on prod, preserving is_visible /
    /// is_evergreen on existing rows (SC3 / D-08) and still mirroring approval_status into prod (D-03
    /// / the P88 approval mirror at <c>ContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync</c> —
    /// unchanged by this split). Throws <see cref="ContentSiteIndexBatchUpsertException"/> (whole
    /// batch rolled back) or the underlying store exception to the caller; this method maps no error
    /// copy and writes no log.
    /// </summary>
    public async Task WriteContentAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);

        await EnsureDirectPushGitBodyFlagOffAsync(cancellationToken).ConfigureAwait(false);

        publishRows = await _suppressionFilter.GetAllowedAsync(publishRows, cancellationToken).ConfigureAwait(false);

        var prodStore = CreateProdStore();

        // SYNC-17/D-01: every row DirectPush pushes to prod enters the seed-managed set — stamp the
        // marker true on the outgoing batch (Pitfall 4: hardcoded true, not sourced from the row).
        var stampedRows = publishRows.Select(r => r with { SeedManaged = true }).ToList();
        await prodStore.UpsertContentColumnsOnlyBatchAsync(stampedRows, cancellationToken).ConfigureAwait(false);

        var keys = DeriveKeys(publishRows);

        // D-10: durable local marker so a mid-flight push (content upserted, not yet confirmed)
        // survives a Blazor page reload and is resumable (Plan 90-06). Prod itself carries no
        // marker column — is_visible=false / pushed_to_prod_utc=null on prod already communicates
        // "not yet live"; the marker only needs to live where the operator's Studio session resumes.
        await _localStore.SetAwaitingConfirmAsync(keys, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Post-confirm publish stage (D-06/D-07 contract step): stamps <c>pushed_to_prod_utc</c> and
    /// flips <c>is_visible</c>=true for the given rows, prod-FIRST-then-local (PUB-01/HIGH-3 — the
    /// SAME ordering the pre-split <c>WritePublishAsync</c> used, preserved exactly across the
    /// split), then clears the LOCAL awaiting-confirm marker (D-10) now that the push is fully
    /// resolved. Callers MUST export approved bodies to the private KB root before invoking this
    /// method; this method performs no export itself.
    /// </summary>
    public async Task ConfirmAndPublishAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);
        await EnsureDirectPushGitBodyFlagOffAsync(cancellationToken).ConfigureAwait(false);
        publishRows = await _suppressionFilter.GetAllowedAsync(publishRows, cancellationToken).ConfigureAwait(false);

        var prodStore = CreateProdStore();
        var keys = DeriveKeys(publishRows);
        var pushedUtc = DateTimeOffset.UtcNow;

        await prodStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
        await prodStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
        await _localStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
        await _localStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
        await _localStore.ClearAwaitingConfirmAsync(keys, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Copies the approved artifacts and seed into the private KB root.</summary>
    public async Task<int> ExportBodiesToPrivateKbRootAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        string dataRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        await EnsureDirectPushGitBodyFlagOffAsync(cancellationToken).ConfigureAwait(false);
        publishRows = await _suppressionFilter.GetAllowedAsync(publishRows, cancellationToken).ConfigureAwait(false);

        var privateKbRoot = _privateKbRootProvider.GetRoot();
        var blankArtifactPathCount = publishRows.Count(row => string.IsNullOrWhiteSpace(row.ArtifactPath));
        if (blankArtifactPathCount > 0)
        {
            throw new InvalidOperationException($"{blankArtifactPathCount} row(s) have no artifact body to export");
        }
        var artifactPaths = publishRows.Select(row => row.ArtifactPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var export = await _orchestrator.ExportIndexToFileAsync(privateKbRoot.SeedFile, progress: null, cancellationToken)
            .ConfigureAwait(false);
        if (!export.Success)
        {
            throw new InvalidOperationException($"Direct Push private KB export failed - {export.Message}");
        }

        var copied = await _orchestrator.CopyArtifactsToRepoAsync(dataRoot, privateKbRoot.Root, artifactPaths, cancellationToken)
            .ConfigureAwait(false);
        return copied.Count;
    }

    private Task<bool?> TryReadDirectPushGitBodyFlagAsync(CancellationToken cancellationToken)
        => _prodReader.TryReadFlagAsync(
            _prodConnection.ConnectionString,
            "sync.directpush-gitbody",
            cancellationToken);

    private async Task EnsureDirectPushGitBodyFlagOffAsync(CancellationToken cancellationToken)
    {
        var enabled = await TryReadDirectPushGitBodyFlagAsync(cancellationToken).ConfigureAwait(false);
        if (enabled is not false)
        {
            throw new InvalidOperationException("Direct Push requires sync.directpush-gitbody to stay OFF.");
        }
    }

    /// <summary>
    /// Returns approved rows awaiting publication from an interrupted prior run, so rows already
    /// pushed in that session cannot reclassify as Unchanged while unapproved rows remain excluded.
    /// </summary>
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAwaitingConfirmRowsAsync(CancellationToken cancellationToken)
        => (await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false))
            .Where(row => row.AwaitingConfirmUtc is not null)
            .ToList();

    private IContentSiteIndexStore CreateProdStore()
        => _prodStoreFactory.Create(_prodConnection.ConnectionString);

    private static IReadOnlyList<(string Type, string Value)> DeriveKeys(IReadOnlyList<ContentSiteIndexRow> rows)
        => rows.Select(row => ContentIndexExportRow.From(row))
            .Select(row => (row.NaturalKeyType, row.NaturalKeyValue))
            .ToList();
}

/// <summary>Approved-row count and resolved data root for page initialization.</summary>
public sealed record DirectPushInitData(int ApprovedCount, string DataRoot);

/// <summary>Describes one Direct Push comparison row with its natural key, change kind, and artifact file.</summary>
public sealed record DirectPushDiffRow(string Title, string KeyType, string KeyValue, bool IsNew, string ArtifactFile);

/// <summary>
/// Result of the content-aware diff: the publish set (New + Updated only), the per-row display rows,
/// and the New/Updated/Unchanged counts.
/// </summary>
public sealed record DirectPushDiff(
    IReadOnlyList<ContentSiteIndexRow> PublishRows,
    IReadOnlyList<DirectPushDiffRow> DiffRows,
    int NewCount,
    int UpdatedCount,
    int UnchangedCount);
