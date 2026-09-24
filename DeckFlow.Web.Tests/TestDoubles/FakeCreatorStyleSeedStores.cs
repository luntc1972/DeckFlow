using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCreatorStyleProfileSeedStore : ICreatorStyleProfileStore
{
    public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public List<CreatorStyleProfile> Upserts { get; } = [];
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult<CreatorStyleProfile?>(null);
    public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default) { Upserts.Add(profile); return Task.CompletedTask; }
}

internal sealed class FakeCreatorDeckCacheSeedStore : ICreatorDeckCacheStore
{
    public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public List<CreatorDeckCacheEntry> Upserts { get; } = [];
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>([]);
    public Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default) { Upserts.Add(entry); return Task.CompletedTask; }
}
