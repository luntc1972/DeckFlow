namespace DeckFlow.Studio.Services;

/// <summary>
/// Polls the web app's authenticated deployed-body-hash endpoint (Plan 90-07,
/// <c>GET Admin/api/contentkb/deployed-body-hash</c>) by natural key until the deployed git
/// <c>/app</c> body's recomputed hash matches the expected stored hash, or a bounded retry budget
/// is exhausted (D-09 REVISED). A hash match is the ONLY correctness proof DirectPush needs before
/// stamping <c>pushed_to_prod_utc</c> and flipping <c>is_visible</c> (SYNC-09) — it defeats the
/// un-deployed-update, new-row-404, missing-body, and stale-deploy races without needing a commit
/// SHA signal.
/// </summary>
public interface IDeployedBodyConfirmer
{
    /// <summary>
    /// Polls the deployed-body-hash endpoint for the given natural key until it returns 200 with a
    /// <c>bodySha256</c> equal (ordinal) to <paramref name="expectedBodySha256"/>, or the bounded
    /// retry budget is exhausted. A 404 (body not yet deployed), a hash mismatch (stale deploy still
    /// live), or a transient failure is treated as "not yet confirmed" and retried with backoff —
    /// never a false positive, never an unbounded wait.
    /// </summary>
    /// <param name="naturalKeyType">Natural key type (e.g. <c>youtube</c> or <c>podcast</c>).</param>
    /// <param name="naturalKeyValue">Natural key value.</param>
    /// <param name="expectedBodySha256">The stored body hash the deployed body must match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the deployed body's hash matched within the retry budget;
    /// otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> IsDeployedBodyConfirmedAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string expectedBodySha256,
        CancellationToken cancellationToken);
}
