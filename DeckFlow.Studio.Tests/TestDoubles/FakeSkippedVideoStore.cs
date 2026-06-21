using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal in-memory fake for <see cref="ISkippedVideoStore"/>; records calls for assertions.
/// </summary>
internal sealed class FakeSkippedVideoStore : ISkippedVideoStore
{
    private readonly HashSet<string> _skipped = new(StringComparer.Ordinal);

    public List<string> AddCalls { get; } = new();
    public List<string> RemoveCalls { get; } = new();

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddSkipAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
    {
        AddCalls.Add(youtubeVideoId);
        _skipped.Add(youtubeVideoId);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveSkipAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        RemoveCalls.Add(youtubeVideoId);
        return Task.FromResult(_skipped.Remove(youtubeVideoId));
    }

    public Task<bool> IsSkippedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        => Task.FromResult(_skipped.Contains(youtubeVideoId));

    public Task<IReadOnlyList<SkippedVideo>> ListSkippedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SkippedVideo>>(
            _skipped.Select(id => new SkippedVideo
            {
                YoutubeVideoId = id,
                Reason = null,
                SkippedUtc = DateTimeOffset.UtcNow,
            }).ToList());

    public void Seed(params string[] ids)
    {
        foreach (var id in ids)
        {
            _skipped.Add(id);
        }
    }
}
