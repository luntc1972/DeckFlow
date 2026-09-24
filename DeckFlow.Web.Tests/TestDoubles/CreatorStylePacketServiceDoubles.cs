using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services.CreatorStyle;

namespace DeckFlow.Web.Tests;

internal sealed class CountingCreatorStyleProfileStore : ICreatorStyleProfileStore
{
    public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public int ReadCount { get; private set; }
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult<CreatorStyleProfile?>(null);
    }
}

internal sealed class ThrowingCreatorDeckCacheStore : ICreatorDeckCacheStore
{
    public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class ThrowingSubmittedDeckStatsBuilder : ISubmittedDeckStatsBuilder
{
    public Task<SubmittedDeckAnalysis> BuildAsync(string deckSource, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class ThrowingCardGroundingGuard : ICardGroundingGuard
{
    public Task<CardGroundingVerdict> TryValidateAsync(string candidateName, CardGroundingDeckContext deckContext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CardGroundingBatchResult> ValidateAllAsync(IReadOnlyList<string> candidateNames, CardGroundingDeckContext deckContext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
