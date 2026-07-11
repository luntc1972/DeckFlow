using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Operator-action coordinator for the SYNC-11 reconcile dry-run (D-04) and the SYNC-12 gated
/// destructive Apply (D-08/D-09/91-08), mirroring <see cref="DirectPushCoordinator"/>'s
/// constructor/optional-logger shape. <see cref="RunDryRunAsync"/> delegates entirely to
/// <see cref="IContentKbReconcileOrchestrator.RunDryRunAsync"/> — the orchestrator alone owns the
/// prod read, git tree walk, seed read, classification, and persistence (91-06) — so the dry-run
/// path issues no destructive write of its own and does not read <c>sync.reconcile</c> (the dry-run
/// is flag-independent; only <see cref="ApplyRemovalsAsync"/> is flag-gated). Also exposes a thin
/// pass-through to <see cref="IContentKbReconcileStore.GetOpenAsync"/> for the page to render
/// previously-persisted open discrepancies without re-running the dry-run.
/// </summary>
public sealed class ReconcileCoordinator
{
    /// <summary>The scope tag used when the operator has not specified one — a whole-catalog run.</summary>
    public const string FullScopeTag = "full";

    // Why (D-10): the web-DB feature flag key that gates ONLY the destructive Apply — the SAME
    // convention DirectPushCoordinator.DirectPushGitBodyFlagKey follows. Single source of truth;
    // no duplicate Studio-local flag.
    private const string ReconcileFlagKey = "sync.reconcile";

    private readonly IContentKbReconcileOrchestrator _orchestrator;
    private readonly IContentKbReconcileStore _store;
    private readonly IProdStoreFactory _prodStoreFactory;
    private readonly IProdContentReader _prodReader;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReconcileCoordinator> _logger;

    /// <summary>
    /// Creates the coordinator over the reconcile orchestrator, the local discrepancy store, the
    /// on-demand prod store factory (Apply's write path), the tri-state prod flag reader (Apply's
    /// gate), configuration (the ephemeral prod connection string), and an optional logger.
    /// </summary>
    public ReconcileCoordinator(
        IContentKbReconcileOrchestrator orchestrator,
        IContentKbReconcileStore store,
        IProdStoreFactory prodStoreFactory,
        IProdContentReader prodReader,
        IConfiguration configuration,
        ILogger<ReconcileCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(prodStoreFactory);
        ArgumentNullException.ThrowIfNull(prodReader);
        ArgumentNullException.ThrowIfNull(configuration);
        _orchestrator = orchestrator;
        _store = store;
        _prodStoreFactory = prodStoreFactory;
        _prodReader = prodReader;
        _configuration = configuration;
        // Optional logger (house convention, e.g. DirectPushCoordinator): the default keeps every
        // existing construction site + test compiling.
        _logger = logger ?? NullLogger<ReconcileCoordinator>.Instance;
    }

    /// <summary>
    /// Runs one read-only reconcile dry-run pass for <paramref name="scopeTag"/> and returns the
    /// orchestrator's <see cref="ReconcileDryRunResult"/> unchanged — including
    /// <see cref="ReconcileDryRunResult.SeedAvailable"/>, surfaced intact so the page can render the
    /// "seed unavailable" notice instead of reading an empty seed-drift group as "no drift"
    /// (T-91-28). Performs no visibility change and no prod DDL; the underlying orchestrator issues
    /// only prod reads, a git-tree walk, and a local-store persist (91-06).
    /// </summary>
    /// <param name="scopeTag">What this run examines; defaults to <see cref="FullScopeTag"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ReconcileDryRunResult> RunDryRunAsync(
        string scopeTag = FullScopeTag,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting reconcile dry-run for scope {ScopeTag}.", scopeTag);
        return _orchestrator.RunDryRunAsync(scopeTag, cancellationToken);
    }

    /// <summary>
    /// Pass-through to <see cref="IContentKbReconcileStore.GetOpenAsync"/> — every currently-open
    /// (unresolved) persisted discrepancy, optionally filtered to one scope tag.
    /// </summary>
    /// <param name="scopeTag">When non-null, restricts results to this scope tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<StoredReconcileDiscrepancy>> GetOpenDiscrepanciesAsync(
        string? scopeTag = null,
        CancellationToken cancellationToken = default)
        => _store.GetOpenAsync(scopeTag, cancellationToken);

    /// <summary>
    /// SYNC-12 gated destructive Apply (D-08/D-09): soft-hides the still-present
    /// <see cref="ContentKbReconcileKind.SeedDrift"/> discrepancies in <paramref name="reviewedRemovalDiscrepancyIds"/>
    /// - and ONLY those - after re-validating them fresh. The reviewed set MUST already be scoped to
    /// seed-drift (removal) discrepancy IDs; the other three classes (published-orphan, file-orphan,
    /// body-hash-mismatch) are detection-only and never enter this method's comparisons.
    /// </summary>
    /// <remarks>
    /// Gate order (each gate independently refuses with zero writes before the next runs):
    /// <list type="number">
    /// <item>
    /// <b>Flag gate.</b> Reads <c>sync.reconcile</c> via the tri-state
    /// <see cref="IProdContentReader.TryReadFlagAsync"/>. Only a confirmed <see langword="true"/>
    /// proceeds - both a definitive <see langword="false"/> AND an indeterminate
    /// <see langword="null"/> refuse (fail-safe-to-REFUSE; the destructive-write inverse of
    /// <see cref="DirectPushCoordinator.VerifyAndPublishAsync"/>'s fail-safe-to-VERIFY tri-state use).
    /// </item>
    /// <item>
    /// <b>Seed-availability gate (Codex BLOCK / T-91-27).</b> Re-runs the reconcile diff FRESH via
    /// <see cref="IContentKbReconcileOrchestrator.RunDryRunAsync"/> - never the persisted dry-run
    /// snapshot - and refuses on the RAW <see cref="ReconcileDryRunResult.SeedAvailable"/> flag
    /// (straight from <c>SeedIndexFileReader.Read</c>, not derived from the discrepancy list) BEFORE
    /// any stale-check or hide. This closes the mass-hide-via-unavailable-seed hole even if a future
    /// classifier change wrongly emitted seed-drift against an unavailable seed.
    /// </item>
    /// <item>
    /// <b>Stale-check.</b> Filters the fresh result to <see cref="ContentKbReconcileKind.SeedDrift"/>
    /// and compares that ID set against <paramref name="reviewedRemovalDiscrepancyIds"/> by set
    /// equality. Any difference (prod/seed moved since the dry-run) refuses with zero writes.
    /// </item>
    /// <item>
    /// <b>Seed-managed re-check (T-91-20 defense-in-depth).</b> Re-reads prod rows fresh via the
    /// on-demand prod store and pre-filters to keys whose CURRENT prod row has
    /// <see cref="ContentSiteIndexRow.SeedManaged"/> == <see langword="true"/>. This is the FIRST of two
    /// ownership gates; it does not rely solely on the classifier's own seed-managed gate.
    /// </item>
    /// </list>
    /// The hide itself uses <see cref="IContentSiteIndexStore.HideSeedManagedAsync(IReadOnlyList{ValueTuple{string,string}},CancellationToken)"/>,
    /// whose SQL carries <c>AND seed_managed = TRUE</c> so the ownership re-check is ATOMIC with the write
    /// (SECOND gate, closing the Codex 91-08 TOCTOU: a prod-owned row is structurally impossible to hide
    /// through this method even if its marker flips between the fresh read and the write). It never
    /// hand-rolls SQL and never touches a timestamp column (Pitfall 5 / F-51-PG-01).
    /// </remarks>
    /// <param name="reviewedRemovalDiscrepancyIds">
    /// The seed-drift-only discrepancy IDs the operator reviewed and approved for removal. Passing a
    /// non-seed-drift ID here can never match the fresh comparison set, so it can only ever cause a
    /// stale-reject, never a hide.
    /// </param>
    /// <param name="scopeTag">Scope tag for the fresh re-run; defaults to <see cref="FullScopeTag"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ReconcileApplyResult> ApplyRemovalsAsync(
        IReadOnlySet<string> reviewedRemovalDiscrepancyIds,
        string scopeTag = FullScopeTag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reviewedRemovalDiscrepancyIds);

        var flag = await TryReadReconcileFlagAsync(cancellationToken).ConfigureAwait(false);
        if (flag != true)
        {
            _logger.LogWarning(
                "Reconcile Apply refused: sync.reconcile did not read as confirmed-true (value: {FlagValue}).",
                flag);
            return ReconcileApplyResult.Refused(ReconcileApplyRefusalReason.FlagNotEnabled);
        }

        _logger.LogInformation("Reconcile Apply: re-running the diff fresh for scope {ScopeTag}.", scopeTag);
        var fresh = await _orchestrator.RunDryRunAsync(scopeTag, cancellationToken).ConfigureAwait(false);

        // Why (Codex BLOCK / T-91-27): refuse on the RAW SeedAvailable flag, independent of the
        // discrepancy list, BEFORE the stale-check and before any hide - belt-and-suspenders even
        // though a correct classifier already emits zero seed-drift on an unavailable seed.
        if (!fresh.SeedAvailable)
        {
            _logger.LogWarning("Reconcile Apply refused: the freshly-read seed is unavailable.");
            return ReconcileApplyResult.Refused(ReconcileApplyRefusalReason.SeedUnavailable);
        }

        var freshRemovals = fresh.Discrepancies
            .Where(d => d.Kind == ContentKbReconcileKind.SeedDrift)
            .ToList();
        var freshRemovalIds = freshRemovals.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        if (!freshRemovalIds.SetEquals(reviewedRemovalDiscrepancyIds))
        {
            _logger.LogWarning(
                "Reconcile Apply refused: stale reviewed-removal set (fresh {FreshCount} vs reviewed {ReviewedCount}).",
                freshRemovalIds.Count,
                reviewedRemovalDiscrepancyIds.Count);
            return ReconcileApplyResult.Refused(ReconcileApplyRefusalReason.StaleReviewSet);
        }

        if (freshRemovals.Count == 0)
        {
            return ReconcileApplyResult.Applied(0);
        }

        var prodStore = CreateProdStore();
        var prodRows = await prodStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
        var seedManagedByKey = new Dictionary<string, bool?>(StringComparer.Ordinal);
        foreach (var row in prodRows)
        {
            if (ContentNaturalKey.TryDerive(row, out var naturalKey))
            {
                seedManagedByKey[CompositeKey(naturalKey.Type, naturalKey.Value)] = row.SeedManaged;
            }
        }

        var keysToHide = new List<(string Type, string Value)>();
        foreach (var discrepancy in freshRemovals)
        {
            if (discrepancy.NaturalKeyType is null || discrepancy.NaturalKeyValue is null)
            {
                continue;
            }

            var compositeKey = CompositeKey(discrepancy.NaturalKeyType, discrepancy.NaturalKeyValue);

            // Why (T-91-20 SYNC-17 invariant): re-check seed_managed against the FRESH prod row
            // itself, not merely the discrepancy's Kind — a prod-owned row must never be hidden even
            // if a future classifier regression ever emitted seed-drift for one.
            if (seedManagedByKey.TryGetValue(compositeKey, out var seedManaged) && seedManaged == true)
            {
                keysToHide.Add((discrepancy.NaturalKeyType, discrepancy.NaturalKeyValue));
            }
            else
            {
                _logger.LogWarning(
                    "Reconcile Apply skipping {Type}:{Value} — not confirmed seed_managed=true on the fresh prod read.",
                    discrepancy.NaturalKeyType,
                    discrepancy.NaturalKeyValue);
            }
        }

        if (keysToHide.Count == 0)
        {
            return ReconcileApplyResult.Applied(0);
        }

        // Why (Codex 91-08 HIGH): the destructive write is HideSeedManagedAsync, whose SQL carries
        // AND seed_managed = TRUE — so the ownership re-check is atomic with the hide, not merely the
        // in-memory pre-filter above. A row whose marker flipped to false/null in the TOCTOU window
        // between GetAllRowsAsync and this write is protected by the predicate and left visible.
        var hiddenCount = await prodStore
            .HideSeedManagedAsync(keysToHide, cancellationToken)
            .ConfigureAwait(false);

        if (hiddenCount == keysToHide.Count)
        {
            // Every requested key was still seed_managed=true at write time, so each was genuinely hidden.
            foreach (var (type, value) in keysToHide)
            {
                _logger.LogInformation("Reconcile Apply soft-hid {Type}:{Value}.", type, value);
            }
        }
        else
        {
            // Why (Codex 91-08 LOW): a shortfall means the SQL ownership predicate protected some keys at
            // write time (their marker flipped since the snapshot). HideSeedManagedAsync returns only a
            // count, not which keys, so we must NOT name any individual key as hidden here — doing so
            // would overstate the audit trail for a key that was actually left visible.
            _logger.LogWarning(
                "Reconcile Apply hid {HiddenCount} of {RequestedCount} keys — the remainder no longer read " +
                "seed_managed=true at write time and were protected by the ownership predicate.",
                hiddenCount,
                keysToHide.Count);
        }

        return ReconcileApplyResult.Applied(hiddenCount);
    }

    // Builds the on-demand prod store from the ephemeral connection string (D-03) — never at DI
    // startup. Mirrors DirectPushCoordinator.CreateProdStore exactly.
    private IContentSiteIndexStore CreateProdStore()
        => _prodStoreFactory.Create(_configuration["Studio:ProdConnectionString"] ?? string.Empty);

    // Why (D-10): the TRI-STATE twin of DirectPushCoordinator's flag helpers, reused verbatim for
    // the destructive Apply gate — both a definitive false AND an indeterminate null refuse here
    // (unlike DirectPush's publish-immediate short-circuit, which only fails safe on false).
    private Task<bool?> TryReadReconcileFlagAsync(CancellationToken cancellationToken)
        => _prodReader.TryReadFlagAsync(
            _configuration["Studio:ProdConnectionString"] ?? string.Empty,
            ReconcileFlagKey,
            cancellationToken);

    private static string CompositeKey(string type, string value) => $"{type}\u0000{value}";
}

/// <summary>Discriminates why <see cref="ReconcileCoordinator.ApplyRemovalsAsync"/> refused to hide anything.</summary>
public enum ReconcileApplyRefusalReason
{
    /// <summary><c>sync.reconcile</c> did not read as a confirmed <see langword="true"/> (false or indeterminate).</summary>
    FlagNotEnabled,

    /// <summary>The freshly-read seed was unavailable (<see cref="ReconcileDryRunResult.SeedAvailable"/> == <see langword="false"/>).</summary>
    SeedUnavailable,

    /// <summary>The fresh seed-drift removal ID set differs from the reviewed-removal set supplied by the caller.</summary>
    StaleReviewSet,
}

/// <summary>
/// Result of <see cref="ReconcileCoordinator.ApplyRemovalsAsync"/>: either a refusal (zero writes,
/// <see cref="RefusalReason"/> set, <see cref="HiddenCount"/> is <see langword="null"/>) or an
/// applied outcome (<see cref="HiddenCount"/> set, <see cref="RefusalReason"/> is <see langword="null"/>).
/// </summary>
public sealed record ReconcileApplyResult
{
    /// <summary><see langword="true"/> when the apply proceeded (possibly hiding zero rows); <see langword="false"/> on any refusal.</summary>
    public bool WasApplied => RefusalReason is null;

    /// <summary>Count of rows soft-hidden; <see langword="null"/> when <see cref="WasApplied"/> is <see langword="false"/>.</summary>
    public int? HiddenCount { get; init; }

    /// <summary>Why the apply refused; <see langword="null"/> when <see cref="WasApplied"/> is <see langword="true"/>.</summary>
    public ReconcileApplyRefusalReason? RefusalReason { get; init; }

    /// <summary>Builds an applied result carrying the number of rows soft-hidden.</summary>
    public static ReconcileApplyResult Applied(int hiddenCount) => new() { HiddenCount = hiddenCount };

    /// <summary>Builds a refused result carrying the reason; no visibility write occurred.</summary>
    public static ReconcileApplyResult Refused(ReconcileApplyRefusalReason reason) => new() { RefusalReason = reason };
}
