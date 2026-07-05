using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Minimal in-memory fake for <see cref="IContentSourceStore"/> used by CreatorSources page tests
/// (P87). Supports lookup by id, enable/disable, and seeding; records SetEnabled calls for assertions.
/// </summary>
internal sealed class FakeContentSourceStore : IContentSourceStore
{
    private readonly List<ContentSource> _sources = new();
    private long _nextId = 1;

    public List<(long Id, bool IsEnabled)> SetEnabledCalls { get; } = new();

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<long> InsertSourceAsync(string sourceSlug, string displayName, string sourceType, string sourceUrl, CancellationToken cancellationToken = default)
    {
        var id = _nextId++;
        _sources.Add(new ContentSource
        {
            Id = id,
            SourceSlug = sourceSlug,
            DisplayName = displayName,
            SourceType = sourceType,
            SourceUrl = sourceUrl,
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        return Task.FromResult(id);
    }

    public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(_sources.FirstOrDefault(s => s.Id == id));

    public Task<ContentSource?> GetSourceByUrlAsync(string url, CancellationToken cancellationToken = default)
        => Task.FromResult(_sources.FirstOrDefault(s => string.Equals(s.SourceUrl, url, StringComparison.Ordinal)));

    public Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        SetEnabledCalls.Add((id, isEnabled));
        var index = _sources.FindIndex(s => s.Id == id);
        if (index >= 0)
        {
            _sources[index] = _sources[index] with { IsEnabled = isEnabled };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentSource>>(_sources.Where(s => s.IsEnabled).OrderBy(s => s.SourceSlug).ToList());

    /// <summary>Seeds a source row and returns its id.</summary>
    public long Seed(string slug, string url, bool isEnabled)
    {
        var id = _nextId++;
        _sources.Add(new ContentSource
        {
            Id = id,
            SourceSlug = slug,
            DisplayName = slug,
            SourceType = ContentSourceType.Youtube,
            SourceUrl = url,
            IsEnabled = isEnabled,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        return id;
    }
}
