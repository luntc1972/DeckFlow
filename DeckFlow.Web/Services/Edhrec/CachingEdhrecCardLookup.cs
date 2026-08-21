using DeckFlow.Core.Integration;
using Microsoft.Extensions.Caching.Memory;

namespace DeckFlow.Web.Services.Edhrec;

/// <summary>
/// Caches successful EDHREC card category lookups to avoid repeated upstream requests.
/// </summary>
public sealed class CachingEdhrecCardLookup : IEdhrecCardLookup
{
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);
    // Why: this private 1,000,000-character cache stays bounded while retaining immutable categories across requests; the shared IMemoryCache has no SizeLimit and cannot evict on memory pressure.
    internal const long CacheCapacityChars = 1_000_000;

    private readonly IEdhrecCardLookup _inner;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = CacheCapacityChars });

    /// <summary>
    /// Initializes a cache over an EDHREC lookup implementation.
    /// </summary>
    /// <param name="inner">The lookup that issues cache-miss requests.</param>
    public CachingEdhrecCardLookup(IEdhrecCardLookup inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LookupCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
    {
        var cacheKey = EdhrecCardLookup.Slugify(cardName);
        if (_cache.TryGetValue(cacheKey, out string[]? cached))
        {
            return cached!.ToArray();
        }

        var categories = await _inner.LookupCategoriesAsync(cardName, cancellationToken).ConfigureAwait(false);
        if (categories.Count == 0)
        {
            return categories;
        }

        var cachedCategories = categories.ToArray();
        _cache.Set(cacheKey, cachedCategories, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            Size = Math.Max(cacheKey.Length + cachedCategories.Sum(category => category.Length), 1),
        });
        return categories;
    }
}
