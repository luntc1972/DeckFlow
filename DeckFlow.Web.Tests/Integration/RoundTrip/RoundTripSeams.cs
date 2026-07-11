using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Web.Tests.Integration.RoundTrip;

/// <summary>
/// Deterministic, network-free test doubles for the SYNC-16 round-trip harness (D-04/D-05/D-06,
/// D-02a). Every double here either canned-returns a fixed result or wraps the SAME real store
/// instance the harness constructs — no seam introduces a second hashing scheme, a live process
/// launch, or a real network/SFTP call.
/// </summary>
internal sealed class CannedLlmDistillationService : ILlmDistillationService
{
    private const string CannedSummary =
        "Canned strategy summary for the SYNC-16 round-trip harness: ramp into the game plan, " +
        "protect the payoff piece, and close through repeatable incremental value.";

    /// <inheritdoc />
    /// <remarks>Returns a FIXED body regardless of input so <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/> is deterministic across runs (D-04).</remarks>
    public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new SummaryResult(CannedSummary, new TokenUsage(120, 60)));

    /// <inheritdoc />
    /// <remarks>
    /// Why: <c>ContentKbOrchestrator.DistillVideoAsync</c> (~:1186) deletes the row when
    /// <c>Verdict=="drop"</c> — a non-drop verdict is required here or the round-trip loop this
    /// harness feeds would be vacuous (W-1).
    /// </remarks>
    public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new ClassificationResult("keep", "harness-canned keep verdict"));

    /// <inheritdoc />
    public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new ClipsResult(
            new[]
            {
                new ClipItem(30, "Canned clip one: opening sequence."),
                new ClipItem(90, "Canned clip two: mid-game pivot."),
                new ClipItem(150, "Canned clip three: closing line."),
            },
            new TokenUsage(150, 80)));

    /// <inheritdoc />
    public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromResult(new TagsResult(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new TokenUsage(40, 20)));
}

/// <summary>
/// Records every <see cref="SshUploadRequest"/> it receives and returns a success
/// <see cref="SshUploadResult"/> per entry — no real SFTP transfer (D-05).
/// </summary>
internal sealed class RecordingSshArtifactUploader : ISshArtifactUploader
{
    private readonly List<SshUploadRequest> _uploads = [];

    /// <summary>Gets every upload request recorded so far, in call order.</summary>
    public IReadOnlyList<SshUploadRequest> Uploads => _uploads;

    /// <inheritdoc />
    public Task<IReadOnlyList<SshUploadResult>> UploadArtifactsAsync(
        IReadOnlyList<SshUploadRequest> uploads,
        IProgress<SshUploadResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        _uploads.AddRange(uploads);

        var results = new List<SshUploadResult>(uploads.Count);
        foreach (var upload in uploads)
        {
            var result = new SshUploadResult(upload.LocalPath, upload.RemoteRelativePath, Success: true, FailureReason: null);
            results.Add(result);
            progress?.Report(result);
        }

        return Task.FromResult<IReadOnlyList<SshUploadResult>>(results);
    }
}

/// <summary>
/// Confirms a deploy by looking up the prod row's <c>ArtifactPath</c> by natural key, resolving it
/// under the harness's <c>/app</c> stand-in root, and recomputing
/// <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/> — the same hash surface the
/// production <c>ContentKbDeployedBodyController</c> uses (CM2/Q5). Fails CLOSED (returns
/// <see langword="false"/>) whenever the row, the resolved path, or the file is missing — never a
/// false confirm (D-06).
/// </summary>
internal sealed class AppTreeDeployedBodyConfirmer : IDeployedBodyConfirmer
{
    private readonly IContentSiteIndexStore _prodStore;
    private readonly string _appRoot;

    /// <summary>Creates a confirmer over the real prod store and the harness's <c>/app</c> root.</summary>
    /// <param name="prodStore">Prod content-site-index store (unfiltered natural-key lookup).</param>
    /// <param name="appRoot">Absolute path to the harness's deploy-copy <c>/app</c> stand-in.</param>
    public AppTreeDeployedBodyConfirmer(IContentSiteIndexStore prodStore, string appRoot)
    {
        ArgumentNullException.ThrowIfNull(prodStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(appRoot);
        _prodStore = prodStore;
        _appRoot = appRoot;
    }

    /// <inheritdoc />
    public async Task<bool> IsDeployedBodyConfirmedAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string expectedBodySha256,
        CancellationToken cancellationToken)
    {
        var row = await _prodStore.GetByNaturalKeyAsync(naturalKeyType, naturalKeyValue, cancellationToken).ConfigureAwait(false);
        if (row is null || !row.ArtifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryResolveUnderAppRoot(row.ArtifactPath, out var resolvedPath) || !File.Exists(resolvedPath))
        {
            return false;
        }

        var raw = await File.ReadAllTextAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        var actualBodySha256 = ContentSiteIndexContentSignature.ComputeBodySha256(raw);
        return string.Equals(actualBodySha256, expectedBodySha256, StringComparison.Ordinal);
    }

    // Why: mirrors ArtifactPathSafety (DeckFlow.Studio.Services, internal to that assembly — no
    // InternalsVisibleTo change here, zero production-code change this phase): reject rooted or
    // traversal paths, then verify containment under the configured /app root.
    private bool TryResolveUnderAppRoot(string artifactPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (Path.IsPathRooted(artifactPath))
        {
            return false;
        }

        var segments = artifactPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return false;
        }

        var rootFull = Path.GetFullPath(_appRoot);
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
}

/// <summary>
/// Reads all rows from the SAME real content-site-index store the harness constructs over
/// Postgres (D-02a) — the connection-string parameter is ignored, mirroring
/// <c>ReconcileFixtureDriveTests.FixtureProdReader</c>. The flag reads are a settable
/// tri-state so a test can drive <c>sync.directpush-gitbody</c> ON/OFF/indeterminate.
/// </summary>
internal sealed class FixtureProdReader : IProdContentReader
{
    private readonly IContentSiteIndexStore _store;

    /// <summary>Creates a reader over the real prod store instance.</summary>
    /// <param name="store">Real content-site-index store to delegate reads to.</param>
    public FixtureProdReader(IContentSiteIndexStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Gets or sets the settable tri-state flag value returned by the flag-read methods.</summary>
    public bool? Flag { get; set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(string connectionString, CancellationToken cancellationToken = default)
        => _store.GetAllRowsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Flag == true);

    /// <inheritdoc />
    public Task<bool?> TryReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Flag);
}

/// <summary>
/// Returns the SAME real content-site-index store instance regardless of the requested connection
/// string — mirrors <c>ReconcileFixtureDriveTests.FixtureProdStoreFactory</c> (D-02a).
/// </summary>
internal sealed class FixtureProdStoreFactory : IProdStoreFactory
{
    private readonly IContentSiteIndexStore _store;

    /// <summary>Creates a factory over the real prod store instance.</summary>
    /// <param name="store">Real content-site-index store every <see cref="Create"/> call returns.</param>
    public FixtureProdStoreFactory(IContentSiteIndexStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public IContentSiteIndexStore Create(string connectionString) => _store;
}
