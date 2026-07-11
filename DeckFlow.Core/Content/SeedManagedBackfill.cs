using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Content;

/// <summary>
/// Host-agnostic seam providing the CURRENT <c>index-seed.json</c> natural-key membership used to
/// classify legacy <c>seed_managed IS NULL</c> rows (D-02). Implementations MUST return the full
/// <see cref="SeedIndexReadResult"/> from <see cref="SeedIndexFileReader.Read"/> UNCHANGED — never
/// collapsing an unavailable/unreadable/unparsable seed into an empty-but-available result
/// (Codex-HIGH / T-91-07). Deliberately synchronous: each host resolves its own seed-file path
/// (a plain file-system lookup) before delegating to the shared reader.
/// </summary>
public interface ISeedKeyMembershipSource
{
    /// <summary>
    /// Reads the current seed membership. <see cref="SeedIndexReadResult.SeedAvailable"/> MUST
    /// reflect whether the seed was genuinely present-and-parsed on this call.
    /// </summary>
    SeedIndexReadResult GetSeedMembership();
}

/// <summary>
/// One-time-per-boot deterministic backfill (D-02) that classifies every existing
/// <c>content_site_index</c> row whose <c>seed_managed</c> is currently <see langword="null"/> by
/// CURRENT seed membership: a natural key present in the seed -&gt; <see langword="true"/>
/// (seed-owned); absent -&gt; <see langword="false"/> (prod-owned). Host-agnostic: depends only on
/// <see cref="IContentSiteIndexStore"/>, an injected <see cref="ISeedKeyMembershipSource"/>, and the
/// shared <see cref="ContentNaturalKey.TryDerive"/> helper, so both the web app (prod-pointed) and
/// Studio (local <c>content-kb.db</c>) can run it at startup — mirroring
/// <see cref="ContentBodyHashBackfill"/>'s dual-host shape exactly.
/// </summary>
/// <remarks>
/// CRITICAL SAFETY GATE (Codex-HIGH / T-91-07): classification runs ONLY when
/// <see cref="SeedIndexReadResult.SeedAvailable"/> is <see langword="true"/> on this call. When the
/// seed is absent, unreadable, or fails to parse, <see cref="RunAsync"/> performs ZERO writes and
/// leaves every unclassified row <see langword="null"/> — it must NEVER classify rows
/// <see langword="false"/> against a missing seed, because
/// <see cref="IContentSiteIndexStore.SetSeedManagedIfNullAsync"/> is a null-only write and could
/// never later repair a wrongly-set <see langword="false"/>. A valid EMPTY seed
/// (<c>SeedAvailable == true</c>, zero keys) IS still a real classify pass — every unclassified row
/// correctly becomes <see langword="false"/> in that case, since none of them are in the seed.
///
/// Idempotent — a row already classified (<see langword="true"/> or <see langword="false"/>) is
/// skipped in memory and never rewritten; re-running after a full pass performs zero writes. Issues
/// no DDL and no direct SQL; every write flows through
/// <see cref="IContentSiteIndexStore.SetSeedManagedIfNullAsync"/>, a null-only UPDATE.
/// </remarks>
public sealed class SeedManagedBackfill
{
    private static readonly IReadOnlySet<string> EmptyKeys = new HashSet<string>(StringComparer.Ordinal);

    private readonly IContentSiteIndexStore _store;
    private readonly ISeedKeyMembershipSource _membership;
    private readonly ILogger<SeedManagedBackfill> _logger;

    /// <summary>
    /// Creates a new host-agnostic seed-managed backfill service.
    /// </summary>
    /// <param name="store">Content site-index store to enumerate and update.</param>
    /// <param name="membership">Host-supplied seed-membership source.</param>
    /// <param name="logger">Structured logger.</param>
    public SeedManagedBackfill(
        IContentSiteIndexStore store,
        ISeedKeyMembershipSource membership,
        ILogger<SeedManagedBackfill> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _membership = membership;
        _logger = logger;
    }

    /// <summary>
    /// Classifies every <c>seed_managed IS NULL</c> row against the current seed membership, but
    /// ONLY when the seed was genuinely present-and-parsed this run (see remarks). Never crashes
    /// host startup: a throwing membership source is caught and treated as an unavailable seed
    /// (zero writes); a row with no derivable natural key is logged and skipped.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        SeedIndexReadResult membershipResult;
        try
        {
            membershipResult = _membership.GetSeedMembership();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is never a "seed unavailable" outcome — always propagate it.
            throw;
        }
        catch (Exception ex)
        {
            // D-02: this backfill runs during host startup, so a throwing membership source
            // (unresolvable git repo root, transient I/O) must never crash the host. Treat it
            // exactly like an unavailable seed — zero writes, rows stay NULL.
            _logger.LogWarning(
                ex,
                "Content KB seed_managed backfill: membership source threw; treating the seed as unavailable this run.");
            membershipResult = new SeedIndexReadResult(false, EmptyKeys);
        }

        if (!membershipResult.SeedAvailable)
        {
            // T-91-07: the Codex-HIGH gate. Never classify against a missing/unreadable/unparsable
            // seed — a later correct seed must still be able to classify these rows, and the setter
            // is null-only so a wrongly-set false could never be repaired.
            _logger.LogWarning(
                "Content KB seed_managed backfill skipped: seed unavailable this run; unclassified rows remain NULL.");
            return;
        }

        var rows = await _store.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);

        var classifiedTrueCount = 0;
        var classifiedFalseCount = 0;
        var skippedCount = 0;

        foreach (var row in rows)
        {
            if (row.SeedManaged is not null)
            {
                continue;
            }

            if (!ContentNaturalKey.TryDerive(row, out var naturalKey))
            {
                skippedCount++;
                _logger.LogWarning(
                    "Content KB seed_managed backfill skipped row {ContentKbRowId}: no derivable natural key.",
                    row.Id);
                continue;
            }

            // Why: U+0000 NULL separator matches the shipped Codex anti-collision key format
            // (ContentNaturalKey / SeedIndexFileReader / ContentSyncDiffClassifier, SYNC-05).
            var classified = membershipResult.NaturalKeys.Contains($"{naturalKey.Type}\u0000{naturalKey.Value}");
            await _store.SetSeedManagedIfNullAsync(row.Id, classified, cancellationToken).ConfigureAwait(false);

            if (classified)
            {
                classifiedTrueCount++;
            }
            else
            {
                classifiedFalseCount++;
            }
        }

        _logger.LogInformation(
            "Content KB seed_managed backfill complete: {ClassifiedTrueCount} seed-owned, {ClassifiedFalseCount} prod-owned, {SkippedCount} skipped.",
            classifiedTrueCount,
            classifiedFalseCount,
            skippedCount);
    }
}
