using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal in-memory fake for <see cref="ICreatorSourceStore"/>. Dedupes on a normalized
/// channel ref (trim + lowercase) like the real store, and records calls for assertions.
/// </summary>
internal sealed class FakeCreatorSourceStore : ICreatorSourceStore
{
    private readonly List<CreatorSource> _creators = new();
    private long _nextId = 1;

    public List<(string DisplayName, string ChannelRef)> AddCalls { get; } = new();
    public List<long> RemoveCalls { get; } = new();

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddAsync(string displayName, string channelRef, CancellationToken cancellationToken = default)
    {
        AddCalls.Add((displayName, channelRef));
        var normalized = channelRef.Trim().ToLowerInvariant();
        if (!_creators.Any(c => c.ChannelRef.Trim().ToLowerInvariant() == normalized))
        {
            _creators.Add(new CreatorSource
            {
                Id = _nextId++,
                DisplayName = displayName.Trim(),
                ChannelRef = channelRef.Trim(),
                AddedUtc = DateTimeOffset.UtcNow,
            });
        }

        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(long id, CancellationToken cancellationToken = default)
    {
        RemoveCalls.Add(id);
        var removed = _creators.RemoveAll(c => c.Id == id) > 0;
        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<CreatorSource>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CreatorSource>>(
            _creators.OrderBy(c => c.DisplayName, StringComparer.Ordinal).ThenBy(c => c.Id).ToList());

    public void Seed(params (string DisplayName, string ChannelRef)[] creators)
    {
        foreach (var (name, channelRef) in creators)
        {
            _creators.Add(new CreatorSource
            {
                Id = _nextId++,
                DisplayName = name,
                ChannelRef = channelRef,
                AddedUtc = DateTimeOffset.UtcNow,
            });
        }
    }
}
