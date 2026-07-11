using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Deterministic <see cref="IDeployedBodyConfirmer"/> test double (SYNC-09/D-09 REVISED): lets
/// <c>DirectPushCoordinator</c> ORDERING be tested without live HTTP. Defaults to
/// <see cref="ConfirmedResult"/> = <see langword="true"/> so tests unrelated to
/// <c>VerifyAndPublishAsync</c> (and bUnit page tests, which do not yet wire that stage — Plan
/// 90-06) are unaffected. Shared between <c>DirectPushCoordinatorTests</c> and
/// <c>DirectPushPageTests</c>, mirroring <see cref="FakeDirectPushFlagReader"/>'s placement.
/// </summary>
internal sealed class FakeDeployedBodyConfirmer : IDeployedBodyConfirmer
{
    /// <summary>The canned confirm result returned by <see cref="IsDeployedBodyConfirmedAsync"/>.</summary>
    public bool ConfirmedResult { get; set; } = true;

    /// <summary>Records every call's natural key + expected hash for assertion.</summary>
    public List<(string Type, string Value, string ExpectedHash)> Calls { get; } = new();

    public Task<bool> IsDeployedBodyConfirmedAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string expectedBodySha256,
        CancellationToken cancellationToken)
    {
        Calls.Add((naturalKeyType, naturalKeyValue, expectedBodySha256));
        return Task.FromResult(ConfirmedResult);
    }
}
