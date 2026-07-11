using System.Text.RegularExpressions;
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
    private readonly IGitRepository _git;
    private readonly IContentKbOrchestrator _orchestrator;
    private readonly IProdContentReader _prodReader;
    private readonly IDeployedBodyConfirmer _confirmer;
    private readonly ILogger<DirectPushCoordinator> _logger;

    // Why: the git commit MAY carry the Render deploy-skip phrase. Render honors [skip render] /
    // [render skip] (NOT the CI-only [skip ci]) to suppress an auto-deploy on push. When
    // sync.directpush-gitbody is OFF (today's default), content is still served via the web /data
    // overlay, so the git push is durability only and must not trigger a redundant production
    // redeploy — the phrase is kept, byte-identical to before this flag existed. When the flag is
    // ON, bodies are served from git /app ONLY (SYNC-07), so the phrase is DROPPED: the push must
    // trigger a real Render redeploy for SYNC-09's hash-gated deploy-confirm step to ever succeed
    // (D-09) — keeping it under the flag would permanently strand every row awaiting confirm.
    private const string RenderSkipPhrase = "[skip render]";

    // Why: the web-DB feature flag key (D-04) — the SAME key ContentKbArtifactPathResolver /
    // ContentKbController read on the web side (90-01). Single source of truth; no duplicate
    // Studio-local flag.
    private const string DirectPushGitBodyFlagKey = "sync.directpush-gitbody";

    // Why: the fixed subject prefix of every durability commit. Shared by the commit-message template
    // AND the classifier regex so the two can never drift (refuted-but-noted dup from review).
    private const string CommitSubjectPrefix = "content: direct-push";

    // Why (review R2-2, extended for D-09): the foreign-commit guard must match ONLY the exact
    // subject shapes this stage writes — "content: direct-push {n} body|bodies to prod [skip
    // render]" (flag OFF) OR "content: direct-push {n} body|bodies to prod" with NO trailing phrase
    // (flag ON) — not merely a subject that starts with the prefix and contains the token. The
    // trailing " [skip render]" is OPTIONAL in the pattern so a flag-ON commit (this stage's own,
    // phrase-dropped) is still recognized as OUR durability commit on a later catch-up push, not
    // misclassified foreign. Built from the shared consts so it stays locked to the template. This
    // narrows accidental false-positives (e.g. "content: direct-push notes [skip render]") to
    // effectively zero; it is not an anti-tamper control (the operator owns the repo and could
    // always craft any commit — that is not the threat model).
    private static readonly Regex DurabilityCommitSubjectPattern = new(
        "^" + Regex.Escape(CommitSubjectPrefix) + @" \d+ (?:body|bodies) to prod(?: " + Regex.Escape(RenderSkipPhrase) + ")?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Creates the coordinator with the stores, uploader, configuration, KB options, git repo, orchestrator, prod flag reader, deploy-confirm poller, and an optional logger.</summary>
    public DirectPushCoordinator(
        IContentSiteIndexStore localStore,
        ISshArtifactUploader uploader,
        IProdStoreFactory prodStoreFactory,
        IStudioProdConnectionSource prodConnection,
        ContentKbOrchestratorOptions options,
        IGitRepository git,
        IContentKbOrchestrator orchestrator,
        IProdContentReader prodReader,
        IDeployedBodyConfirmer confirmer,
        ILogger<DirectPushCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localStore);
        ArgumentNullException.ThrowIfNull(uploader);
        ArgumentNullException.ThrowIfNull(prodStoreFactory);
        ArgumentNullException.ThrowIfNull(prodConnection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(prodReader);
        ArgumentNullException.ThrowIfNull(confirmer);
        _localStore = localStore;
        _uploader = uploader;
        _prodStoreFactory = prodStoreFactory;
        _prodConnection = prodConnection;
        _options = options;
        _git = git;
        _orchestrator = orchestrator;
        _prodReader = prodReader;
        _confirmer = confirmer;
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
        var rows = await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
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
        var localRows = await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);

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
    /// store — those happen only in <see cref="ConfirmAndPublishAsync"/>, after a successful
    /// deploy-confirm (SYNC-09), so a row can never go visible before its body is durably in git and
    /// deployed. Only the content-columns-only upsert runs on prod, preserving is_visible /
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
    /// resolved. Callers MUST invoke this only for rows a deploy-confirm (SYNC-09,
    /// <see cref="VerifyAndPublishAsync"/>) has already proven live at git <c>/app</c> with a
    /// matching <c>body_sha256</c> — this method performs no confirmation itself.
    /// </summary>
    public async Task ConfirmAndPublishAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);

        var prodStore = CreateProdStore();
        var keys = DeriveKeys(publishRows);
        var pushedUtc = DateTimeOffset.UtcNow;

        await prodStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
        await prodStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
        await _localStore.StampPushedToProdAsync(keys, pushedUtc, cancellationToken).ConfigureAwait(false);
        await _localStore.SetVisibilityAsync(keys, true, cancellationToken).ConfigureAwait(false);
        await _localStore.ClearAwaitingConfirmAsync(keys, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verify-then-publish stage (SYNC-09 / D-09 REVISED): for each pushed row, polls the Plan 90-07
    /// deploy-confirm endpoint (via <see cref="IDeployedBodyConfirmer"/>) for a hash match at the
    /// deployed git <c>/app</c> body, then runs <see cref="ConfirmAndPublishAsync"/> ONLY for the
    /// rows that confirmed. Rows that did not confirm within the confirmer's bounded retry budget
    /// stay content-upserted and awaiting-confirm (D-10) — never a false-positive stamp/visibility
    /// flip. A row with no <see cref="ContentSiteIndexRow.BodySha256"/> cannot be confirmed (there is
    /// nothing to match against) and is treated as not-confirmed.
    /// </summary>
    public async Task<DirectPushVerifyResult> VerifyAndPublishAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);

        // Codex-HIGH fix: the git /app deploy-confirm poll is only meaningful when
        // sync.directpush-gitbody is ON. Only the ON path drops [skip render] (see
        // CommitAndPushBodiesAsync), so only then does Render redeploy the body to /app for the hash
        // poll to ever match. With the flag definitively OFF the git push carries [skip render], /app
        // never receives the body, and polling it would 404 until the retry budget expires —
        // stranding every row awaiting-confirm forever; in that state the body is already served live
        // from the /data overlay (SCP'd in Stage 2), so publish immediately, byte-identical to pre-90
        // DirectPush.
        //
        // Codex RE-REVIEW HIGH: use the TRI-STATE read, not the fail-closed bool. Only a DEFINITIVE
        // OFF (false) takes the immediate path. An INDETERMINATE read (null — the flag DB was briefly
        // unreachable) must NOT immediate-publish: if prod were actually ON, immediate-publishing
        // would flip a row visible whose body was never redeployed to /app (the ON web resolver
        // ignores /data), a false-positive publish. Failing to the VERIFY path instead is safe in
        // both worlds — a genuinely-OFF row merely 404s the poll and strands recoverably (never a
        // false publish), while a genuinely-ON row is confirmed correctly.
        var directPushGitBodyFlag = await TryReadDirectPushGitBodyFlagAsync(cancellationToken).ConfigureAwait(false);
        if (directPushGitBodyFlag == false)
        {
            if (publishRows.Count > 0)
            {
                await ConfirmAndPublishAsync(publishRows, cancellationToken).ConfigureAwait(false);
            }

            return new DirectPushVerifyResult(publishRows, Array.Empty<ContentSiteIndexRow>());
        }

        var confirmed = new List<ContentSiteIndexRow>();
        var notConfirmed = new List<ContentSiteIndexRow>();
        foreach (var row in publishRows)
        {
            var exportRow = ContentIndexExportRow.From(row);
            var expectedHash = row.BodySha256;
            var isConfirmed = !string.IsNullOrEmpty(expectedHash)
                && await _confirmer.IsDeployedBodyConfirmedAsync(
                        exportRow.NaturalKeyType,
                        exportRow.NaturalKeyValue,
                        expectedHash,
                        cancellationToken)
                    .ConfigureAwait(false);

            (isConfirmed ? confirmed : notConfirmed).Add(row);
        }

        if (confirmed.Count > 0)
        {
            await ConfirmAndPublishAsync(confirmed, cancellationToken).ConfigureAwait(false);
        }

        return new DirectPushVerifyResult(confirmed, notConfirmed);
    }

    /// <summary>
    /// Resume support (D-10/Plan 90-06): reads locally-approved rows carrying a non-null
    /// <see cref="ContentSiteIndexRow.AwaitingConfirmUtc"/> marker — content already pushed to prod
    /// in a prior (possibly interrupted) session but not yet deploy-confirmed. These rows would
    /// otherwise reclassify as Unchanged on a fresh <see cref="ComputeDiffAsync"/> (their content
    /// signature already matches prod) and silently drop out of <see cref="DirectPushDiff.PublishRows"/>
    /// (90-RESEARCH Pitfall 4) — this method is the durable, diff-independent way to find them so the
    /// page can offer a resume/re-run-verify action. Filters in memory (no marker-column WHERE
    /// clause), avoiding the F-51-PG-01 TEXT-vs-timestamptz class entirely (Pitfall 3).
    /// </summary>
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAwaitingConfirmRowsAsync(CancellationToken cancellationToken)
    {
        var rows = await _localStore.GetApprovedRowsAsync(cancellationToken).ConfigureAwait(false);
        return rows.Where(r => r.AwaitingConfirmUtc is not null).ToList();
    }

    // Shared key derivation for WriteContentAsync/ConfirmAndPublishAsync — reuses
    // ContentIndexExportRow.From so the natural-key extraction can never diverge between the split
    // methods (Pitfall 5).
    private static IReadOnlyList<(string Type, string Value)> DeriveKeys(IReadOnlyList<ContentSiteIndexRow> rows)
        => rows
            .Select(row => ContentIndexExportRow.From(row))
            .Select(row => (Type: row.NaturalKeyType, Value: row.NaturalKeyValue))
            .ToList();

    /// <summary>
    /// Durability stage (runs LAST, after prod DB + /data are already live): re-exports + stages the
    /// approved-only seed, copies the pushed (New + Updated) bodies into the repo tree, commits the
    /// seed plus those body paths, and pushes the current branch to <c>origin</c> (the remote is
    /// fixed, not a parameter).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Re-exports <c>content-kb/seed/index-seed.json</c> through the SAME shared
    /// <see cref="IContentKbOrchestrator.ExportIndexToFileAsync"/> factory <c>PublishCoordinator</c>
    /// uses (D-08/SYNC-08) — no forked seed writer — and stages it alongside the copied bodies, so a
    /// fresh prod reseed can fully reconstruct this DirectPush'd row instead of reverting it (M2,
    /// C3). A seed-export failure surfaces as an exception; it never falls through to a silent
    /// bodies-only commit.
    /// </para>
    /// <para>
    /// The commit message carries <c>[skip render]</c> ONLY when <c>sync.directpush-gitbody</c> is
    /// OFF (today's default, byte-identical) — content is still serving live from the web /data
    /// overlay; git is durability only. When the flag is ON, the phrase is DROPPED so the push
    /// triggers a real production redeploy (D-09), which SYNC-09's hash-gated deploy-confirm step
    /// requires. Any git failure is the caller's to surface as non-fatal (content stays live).
    /// </para>
    /// </remarks>
    public async Task<DirectPushGitResult> CommitAndPushBodiesAsync(
        IReadOnlyList<ContentSiteIndexRow> publishRows,
        string dataRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishRows);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        var pushState = await GetVerifiedPushStateAsync(cancellationToken).ConfigureAwait(false);
        var repoRoot = pushState.RepoRoot;
        var branch = pushState.Branch;
        var aheadSubjects = pushState.AheadSubjects;

        // Only the pushed bodies — distinct in case two rows share an artifact path. Filter blank AND
        // whitespace paths (review F6) so a stray whitespace ArtifactPath cannot turn the durability
        // backup into a hard failure inside the containment guard.
        var artifactPaths = publishRows
            .Select(r => r.ArtifactPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Defensive (review R3-3): the publish set has rows but every ArtifactPath was blank/whitespace
        // (data corruption or an upstream mapping bug). Nothing can be backed up — refuse rather than
        // fall through to an "already in sync / git-durable" success on zero bodies.
        if (publishRows.Count > 0 && artifactPaths.Count == 0)
        {
            throw new InvalidOperationException(
                "Direct Push git stage: the publish set has rows but no usable artifact paths to commit.");
        }

        // Seed re-export (D-08/SYNC-08): the SAME shared factory PublishCoordinator calls — no forked
        // seed writer. Runs on EVERY invocation of this stage (not gated on changedCount below) so the
        // committed seed always reflects the current approved set. A failure surfaces immediately,
        // before any body is copied — never a silent bodies-only commit.
        var seedAbsPath = Path.GetFullPath(Path.Combine(repoRoot, ContentKbSeedPaths.SeedRelativePath));
        var exportResult = await _orchestrator
            .ExportIndexToFileAsync(seedAbsPath, progress: null, cancellationToken)
            .ConfigureAwait(false);
        if (!exportResult.Success)
        {
            throw new InvalidOperationException(
                $"Direct Push git stage: seed export failed - {exportResult.Message}");
        }

        var copied = await _orchestrator
            .CopyArtifactsToRepoAsync(dataRoot, repoRoot, artifactPaths, cancellationToken)
            .ConfigureAwait(false);

        // No-op gate (Codex MED): an Updated row whose DB columns changed but whose .md body is
        // byte-identical to HEAD produces nothing to commit — do NOT let StageAndCommitAsync throw
        // "nothing to commit". changedCount is also the ACCURATE committed-body count (review R3-2):
        // copied.Count includes those byte-identical bodies that git does not actually commit. This
        // count stays BODY-ONLY (never includes the seed) so the "N body|bodies" message wording and
        // DurabilityCommitSubjectPattern's \d+ group keep meaning exactly what they say.
        var changedCount = await _git.CountWorkingChangesAsync(repoRoot, copied, cancellationToken).ConfigureAwait(false);

        // Codex MED: the seed is re-exported every run (above), but a metadata-only edit
        // (retitle/retag) changes index-seed.json WITHOUT changing any .md body. changedCount is
        // body-only, so a seed-only change leaves it 0 and the row would return AlreadyInSync with
        // the modified seed uncommitted in the working tree (D-08 violated: a fresh prod reseed
        // could not reconstruct the edit). Detect a seed-only change so it is committed too. Kept
        // OUT of changedCount so the "N body|bodies" wording + DurabilityCommitSubjectPattern's \d+
        // group keep meaning body count exactly (a seed-only commit is "0 bodies", still matched).
        var seedChanged = await _git
            .CountWorkingChangesAsync(repoRoot, new[] { ContentKbSeedPaths.SeedRelativePath }, cancellationToken)
            .ConfigureAwait(false) > 0;

        string? sha = null;
        if (changedCount > 0 || seedChanged)
        {
            var noun = changedCount == 1 ? "body" : "bodies";

            // D-04/D-09: read the SAME web-DB flag the serving flip consults, through the
            // structurally read-only, fail-closed accessor — Studio never assumes ON.
            var directPushGitBodyOn = await ReadDirectPushGitBodyFlagAsync(cancellationToken).ConfigureAwait(false);
            var message = BuildDurabilityCommitMessage(changedCount, noun, directPushGitBodyOn);

            // Bodies AND the re-exported seed are staged together (D-08/SYNC-08): a fresh prod reseed
            // can now fully reconstruct this DirectPush'd content instead of reverting it. A commit
            // failure propagates before the push (nothing is left half-published on the remote).
            var stagedPaths = new List<string> { ContentKbSeedPaths.SeedRelativePath };
            stagedPaths.AddRange(copied);

            sha = await _git.StageAndCommitAsync(repoRoot, stagedPaths, message, cancellationToken).ConfigureAwait(false);
        }

        // Decide what actually gets published. aheadSubjects is resolved and all-own (foreign rejected):
        //  - committed this run (sha != null)          → push it, outcome Committed.
        //  - no commit but own durability commit(s) still unpushed (Count > 0) → catch-up push, outcome
        //    PushedExistingCommits.
        //  - no commit AND in sync (Count == 0)        → nothing to push; return AlreadyInSync WITHOUT
        //    pushing so the UI never falsely claims a push happened (review R2-3 / F4).
        if (sha is null && aheadSubjects.Count == 0)
        {
            return new DirectPushGitResult(null, branch, changedCount, DirectPushGitOutcome.AlreadyInSync);
        }

        var outcome = sha is null
            ? DirectPushGitOutcome.PushedExistingCommits
            : DirectPushGitOutcome.Committed;

        // Push is a SEPARATE recoverable step (Codex MED): if a commit landed but the push fails, the
        // bodies are already durable locally — surface the SHA (null when nothing was committed this
        // run) + branch so the operator can push by hand. A genuine cancellation must surface AS
        // cancellation, not as a push failure (review F3), so it is rethrown before the generic wrap.
        await PushOrWrapAsync(repoRoot, branch, sha, cancellationToken).ConfigureAwait(false);

        return new DirectPushGitResult(sha, branch, changedCount, outcome);
    }

    /// <summary>
    /// Resume-only recovery stage (FU-2): when direct-push git bodies are DEFINITIVELY ON and rows
    /// remain awaiting confirm after a verify pass, force a fresh Render redeploy by creating an
    /// empty durability commit and pushing it through the same safety gates as Stage 4.
    /// </summary>
    public async Task<DirectPushRedeployResult> TriggerRedeployAsync(CancellationToken cancellationToken)
    {
        var directPushGitBodyFlag = await TryReadDirectPushGitBodyFlagAsync(cancellationToken).ConfigureAwait(false);
        if (directPushGitBodyFlag is null)
        {
            return new DirectPushRedeployResult(DirectPushRedeployOutcome.Indeterminate, null, null);
        }

        if (directPushGitBodyFlag == false)
        {
            return new DirectPushRedeployResult(DirectPushRedeployOutcome.FlagNotOn, null, null);
        }

        var pushState = await GetVerifiedPushStateAsync(cancellationToken).ConfigureAwait(false);
        if (pushState.AheadSubjects.Count > 0)
        {
            return new DirectPushRedeployResult(DirectPushRedeployOutcome.BranchAheadNeedsPush, pushState.Branch, null);
        }

        var headSubject = await _git.GetHeadSubjectAsync(pushState.RepoRoot, cancellationToken).ConfigureAwait(false);
        var headIsEmpty = await _git.IsHeadCommitEmptyAsync(pushState.RepoRoot, cancellationToken).ConfigureAwait(false);
        if (headIsEmpty && IsDurabilityCommitSubject(headSubject))
        {
            return new DirectPushRedeployResult(DirectPushRedeployOutcome.AlreadyTriggered, pushState.Branch, null);
        }

        var sha = await _git
            .CommitEmptyAsync(
                pushState.RepoRoot,
                BuildDurabilityCommitMessage(0, "bodies", directPushGitBodyOn: true),
                cancellationToken)
            .ConfigureAwait(false);

        await PushOrWrapAsync(pushState.RepoRoot, pushState.Branch, sha, cancellationToken).ConfigureAwait(false);

        return new DirectPushRedeployResult(DirectPushRedeployOutcome.RedeployTriggered, pushState.Branch, sha);
    }

    // Why: our own durability commits are the only commits Stage 4 is allowed to publish. They are
    // identified by the EXACT subject shape CommitAndPushBodiesAsync writes (regex built from the same
    // consts). Anything else ahead of origin is foreign and blocks the push.
    private static bool IsDurabilityCommitSubject(string subject)
        => DurabilityCommitSubjectPattern.IsMatch(subject);

    private static string BuildDurabilityCommitMessage(int changedCount, string noun, bool directPushGitBodyOn)
        => directPushGitBodyOn
            ? $"{CommitSubjectPrefix} {changedCount} {noun} to prod"
            : $"{CommitSubjectPrefix} {changedCount} {noun} to prod {RenderSkipPhrase}";

    private async Task<DirectPushVerifiedPushState> GetVerifiedPushStateAsync(CancellationToken cancellationToken)
    {
        var repoRoot = await _git.ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory(), cancellationToken).ConfigureAwait(false);

        // Resolve the branch up front so a detached HEAD fails fast BEFORE any file copy or commit —
        // rev-parse --abbrev-ref returns the literal "HEAD" when detached, which would otherwise push
        // to a bogus refs/heads/HEAD branch (Codex LOW).
        var branch = await _git.GetCurrentBranchAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (string.Equals(branch, "HEAD", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException(
                "Direct Push requires a checked-out branch; the repository is in a detached-HEAD state.");
        }

        // Ahead-of-origin inspection (reviews F2 / R2-1 / R2-2): pushing the branch ref publishes HEAD
        // AND every ancestor not already on origin — not just our durability commit. Before pushing we
        // must PROVE the only thing published is our own durability commit(s). Read what is currently
        // ahead of origin/{branch}.
        IReadOnlyList<string>? aheadSubjects;
        try
        {
            aheadSubjects = await _git
                .GetSubjectsAheadOfRemoteAsync(repoRoot, "origin", branch, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitCommandException)
        {
            aheadSubjects = null;
        }

        // Fail CLOSED when the ahead state cannot be determined (review R2-1): a missing origin/{branch}
        // remote-tracking ref (never fetched) means we cannot prove a push would publish only our own
        // commits, so refuse to auto-push rather than risk publishing unreviewed history. Nothing has
        // been committed yet — the operator resolves this by fetching, then retrying.
        if (aheadSubjects is null)
        {
            throw new DirectPushPushBlockedException(
                branch,
                $"the branch's remote state could not be verified — origin/{branch} was not found. " +
                $"If the branch has never been pushed, run 'git push -u origin {branch}'; otherwise run " +
                "'git fetch'. Then retry");
        }

        // Foreign-commit guard: if ANY commit ahead of origin is one this stage did not author (not an
        // exact-shape durability commit), refuse — Stage 4 and FU-2 resume must never publish
        // unreviewed commits.
        var foreignAhead = aheadSubjects.Count(s => !IsDurabilityCommitSubject(s));
        if (foreignAhead > 0)
        {
            throw new DirectPushUnreviewedCommitsException(foreignAhead, branch);
        }

        return new DirectPushVerifiedPushState(repoRoot, branch, aheadSubjects);
    }

    // Builds the on-demand prod store from the ephemeral connection string (D-03) — never at DI
    // startup. Shared by the diff read and the publish write so the config key lives in one place.
    private IContentSiteIndexStore CreateProdStore()
        => _prodStoreFactory.Create(_prodConnection.ConnectionString);

    // Why (D-04): reads the SAME web-DB feature flag the serving flip consults, through the
    // structurally read-only, fail-closed IProdContentReader.ReadFlagAsync accessor (Task 1) — never
    // a duplicate Studio-local flag. Reuses the same ephemeral connection string as CreateProdStore.
    private Task<bool> ReadDirectPushGitBodyFlagAsync(CancellationToken cancellationToken)
        => _prodReader.ReadFlagAsync(
            _prodConnection.ConnectionString,
            DirectPushGitBodyFlagKey,
            cancellationToken);

    // Why (Codex re-review HIGH): the TRI-STATE twin of ReadDirectPushGitBodyFlagAsync used by the
    // publish gate. Returns null when the flag read failed (indeterminate) so VerifyAndPublishAsync
    // can fail SAFE to the /app verify path instead of a false-positive immediate publish. The
    // [skip render] decision keeps using the fail-closed bool accessor above (fail-to-OFF is correct
    // there — an uncertain read just keeps [skip render], never forces a redundant redeploy).
    private Task<bool?> TryReadDirectPushGitBodyFlagAsync(CancellationToken cancellationToken)
        => _prodReader.TryReadFlagAsync(
            _prodConnection.ConnectionString,
            DirectPushGitBodyFlagKey,
            cancellationToken);

    private async Task PushOrWrapAsync(
        string repoRoot,
        string branch,
        string? sha,
        CancellationToken cancellationToken)
    {
        try
        {
            await _git.PushAsync(repoRoot, "origin", branch, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DirectPushPushException(sha, branch, ex);
        }
    }

    private sealed record DirectPushVerifiedPushState(
        string RepoRoot,
        string Branch,
        IReadOnlyList<string> AheadSubjects);
}

/// <summary>Approved-row count and resolved data root for the DirectPush page init.</summary>
public sealed record DirectPushInitData(int ApprovedCount, string DataRoot);

/// <summary>Discriminates the outcome of the DirectPush git durability stage.</summary>
public enum DirectPushGitOutcome
{
    /// <summary>A new durability commit was created and pushed.</summary>
    Committed,

    /// <summary>Nothing new to commit, but our own previously-unpushed durability commit(s) were pushed (catch-up).</summary>
    PushedExistingCommits,

    /// <summary>Nothing to commit and the branch was already in sync with origin — no push occurred.</summary>
    AlreadyInSync,
}

/// <summary>
/// Result of the DirectPush git durability stage: the commit SHA (<see langword="null"/> when nothing
/// was committed this run), the current branch, the body count, and the outcome discriminator.
/// </summary>
public sealed record DirectPushGitResult(string? Sha, string Branch, int BodyCount, DirectPushGitOutcome Outcome);

/// <summary>Discriminates the outcome of the FU-2 resume redeploy trigger stage.</summary>
public enum DirectPushRedeployOutcome
{
    /// <summary>A new empty durability commit was created and pushed to trigger a redeploy.</summary>
    RedeployTriggered,

    /// <summary>The branch is in sync and <c>HEAD</c> is already an empty durability commit, so a redeploy is already pending.</summary>
    AlreadyTriggered,

    /// <summary>The prod flag read was definitive OFF, so no redeploy should be forced.</summary>
    FlagNotOn,

    /// <summary>The prod flag state could not be read, so the coordinator refuses to force a redeploy.</summary>
    Indeterminate,

    /// <summary>The branch already has our own durability commits ahead of origin and must be pushed/caught up before another empty commit is added.</summary>
    BranchAheadNeedsPush,
}

/// <summary>Result of <see cref="DirectPushCoordinator.TriggerRedeployAsync"/>.</summary>
public sealed record DirectPushRedeployResult(
    DirectPushRedeployOutcome Outcome,
    string? Branch,
    string? Sha);

/// <summary>
/// Thrown when the DirectPush git stage committed the bodies locally but the subsequent push failed.
/// Carries the created commit SHA and branch so the page can tell the operator exactly what landed
/// locally and how to push it by hand. The inner exception (which may carry the remote URL) is logged
/// to the sink only, never surfaced to the UI (D-07 / SC5).
/// </summary>
public sealed class DirectPushPushException : Exception
{
    /// <summary>Creates the exception with the local commit SHA (null when nothing was committed this run), the branch, and the underlying push failure.</summary>
    public DirectPushPushException(string? sha, string branch, Exception inner)
        : base("Bodies committed locally but the push to origin failed.", inner)
    {
        Sha = sha;
        Branch = branch;
    }

    /// <summary>Gets the SHA of the commit that landed locally before the push failed, or <see langword="null"/> when this run had nothing new to commit and only re-attempted the push.</summary>
    public string? Sha { get; }

    /// <summary>Gets the branch the failed push targeted.</summary>
    public string Branch { get; }
}

/// <summary>
/// Thrown when the DirectPush git stage detects commits ahead of <c>origin/{branch}</c> that were NOT
/// authored by this stage (not <c>[skip render]</c> durability commits). Pushing the branch would
/// publish those unreviewed commits, so the stage refuses. The operator must review and push (or
/// reset) them by hand first. Non-fatal: the content is already live in production.
/// </summary>
public sealed class DirectPushUnreviewedCommitsException : Exception
{
    /// <summary>Creates the exception with the count of foreign unpushed commits and the branch.</summary>
    public DirectPushUnreviewedCommitsException(int foreignCommitCount, string branch)
        : base($"{foreignCommitCount} unreviewed commit(s) ahead of origin/{branch} would be published by this push.")
    {
        ForeignCommitCount = foreignCommitCount;
        Branch = branch;
    }

    /// <summary>Gets the number of foreign (non-durability) commits ahead of the remote.</summary>
    public int ForeignCommitCount { get; }

    /// <summary>Gets the branch that would have been pushed.</summary>
    public string Branch { get; }
}

/// <summary>
/// Thrown when the DirectPush git stage cannot PROVE a push would publish only its own durability
/// commits (e.g. the <c>origin/{branch}</c> remote-tracking ref is missing), so it fails closed and
/// refuses to auto-push rather than risk publishing unverified history. Nothing is committed or
/// pushed. Non-fatal: the content is already live in production; the operator resolves the stated
/// reason (typically <c>git fetch</c>) and retries.
/// </summary>
public sealed class DirectPushPushBlockedException : Exception
{
    /// <summary>Creates the exception with the branch and the operator-facing (secret-free) reason.</summary>
    public DirectPushPushBlockedException(string branch, string reason)
        : base($"Direct Push refused to auto-push {branch}: {reason}")
    {
        Branch = branch;
        Reason = reason;
    }

    /// <summary>Gets the branch that would have been pushed.</summary>
    public string Branch { get; }

    /// <summary>Gets the operator-facing reason (contains no secrets — safe to surface in the UI).</summary>
    public string Reason { get; }
}

/// <summary>A single New/Updated row shown in the diff preview table.</summary>
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

/// <summary>
/// Result of <see cref="DirectPushCoordinator.VerifyAndPublishAsync"/>: the rows whose deployed
/// git <c>/app</c> body hash matched (and were therefore stamped + made visible) and the rows that
/// did not confirm within the bounded retry budget (still content-upserted, still awaiting-confirm).
/// </summary>
public sealed record DirectPushVerifyResult(
    IReadOnlyList<ContentSiteIndexRow> Confirmed,
    IReadOnlyList<ContentSiteIndexRow> NotConfirmed);
