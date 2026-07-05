using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal in-memory fake for <see cref="ICreatorSourceStore"/>. Dedupes on a normalized
/// channel ref (trim + lowercase) like the real store, mirrors the provisional slug the real
/// store computes on add, supports the P87 link, and records calls for assertions.
/// </summary>
internal sealed class FakeCreatorSourceStore : ICreatorSourceStore
{
    private readonly List<CreatorSource> _creators = new();
    private long _nextId = 1;

    public List<(string DisplayName, string ChannelRef)> AddCalls { get; } = new();
    public List<long> RemoveCalls { get; } = new();
    public List<(long CreatorId, long ContentSourceId, string Slug)> LinkCalls { get; } = new();

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AddAsync(string displayName, string channelRef, CancellationToken cancellationToken = default)
    {
        AddCalls.Add((displayName, channelRef));
        var normalized = CreatorSourceStore.NormalizeChannelRef(channelRef);
        if (!_creators.Any(c => CreatorSourceStore.NormalizeChannelRef(c.ChannelRef) == normalized))
        {
            _creators.Add(new CreatorSource
            {
                Id = _nextId++,
                DisplayName = displayName.Trim(),
                ChannelRef = channelRef.Trim(),
                AddedUtc = DateTimeOffset.UtcNow,
                SourceSlug = SlugifySourceName.Slugify(displayName.Trim()),
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

    public Task<CreatorSource?> GetByNormalizedRefAsync(string normalizedChannelRef, CancellationToken cancellationToken = default)
        => Task.FromResult(_creators.FirstOrDefault(
            c => CreatorSourceStore.NormalizeChannelRef(c.ChannelRef) == normalizedChannelRef));

    public Task LinkContentSourceAsync(long creatorId, long contentSourceId, string canonicalSlug, CancellationToken cancellationToken = default)
    {
        LinkCalls.Add((creatorId, contentSourceId, canonicalSlug));
        var index = _creators.FindIndex(c => c.Id == creatorId);
        if (index >= 0)
        {
            _creators[index] = _creators[index] with { ContentSourceId = contentSourceId, SourceSlug = canonicalSlug };
        }

        return Task.CompletedTask;
    }

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
                SourceSlug = SlugifySourceName.Slugify(name),
            });
        }
    }

    /// <summary>Seeds a single fully-specified creator row (P87 — for link/status assertions).</summary>
    public CreatorSource SeedLinked(string displayName, string channelRef, string? slug, long? contentSourceId)
    {
        var creator = new CreatorSource
        {
            Id = _nextId++,
            DisplayName = displayName,
            ChannelRef = channelRef,
            AddedUtc = DateTimeOffset.UtcNow,
            SourceSlug = slug,
            ContentSourceId = contentSourceId,
        };
        _creators.Add(creator);
        return creator;
    }
}
