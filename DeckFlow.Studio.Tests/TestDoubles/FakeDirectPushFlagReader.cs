using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal <see cref="IProdContentReader"/> test double for <see cref="DeckFlow.Studio.ViewModels.DirectPushCoordinator"/>'s
/// <c>ReadFlagAsync</c> dependency (90-04 Task 2 Interfaces note: "the coordinator flag fake is
/// separate") — kept apart from <see cref="FakeProdContentReader"/> (the pull-from-prod read-all
/// double, shared with <c>PullFromProdPageTests</c>) so this plan never touches that file.
/// <see cref="ReadAllAsync"/> is unused by <c>DirectPushCoordinator</c> and throws if ever called.
/// </summary>
internal sealed class FakeDirectPushFlagReader : IProdContentReader
{
    /// <summary>The canned flag value returned by <see cref="ReadFlagAsync"/>. Defaults to
    /// <see langword="false"/> (D-05: <c>sync.directpush-gitbody</c> ships OFF).</summary>
    public bool FlagValue { get; set; }

    public Task<bool> ReadFlagAsync(string connectionString, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(FlagValue);

    public Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(string connectionString, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("DirectPushCoordinator never calls ReadAllAsync.");
}
