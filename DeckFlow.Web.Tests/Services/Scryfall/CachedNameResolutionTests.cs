using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests.Services.Scryfall;

/// <summary>
/// Covers shared name-resolution cache mechanics directly; TTL expiry remains <see cref="IMemoryCache"/>'s contract.
/// </summary>
public sealed class CachedNameResolutionTests
{
    [Fact]
    public void BuildCacheKey_TrimmedMixedCaseName_ConcatenatesNormalizedName()
    {
        string key = CachedNameResolution.BuildCacheKey("grounder:", "  Sol Ring  ");

        Assert.Equal("grounder:sol ring", key);
    }

    [Fact]
    public void BuildCacheKey_DifferentPrefixes_ReturnsDifferentKeys()
    {
        string first = CachedNameResolution.BuildCacheKey("grounder:", "Sol Ring");
        string second = CachedNameResolution.BuildCacheKey("search:", "Sol Ring");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task GetOrAddAsync_ColdCache_FetchesAndReturnsValue()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        int fetchCalls = 0;
        var expected = new CacheValue("first");

        CacheValue result = await GetAsync(cache, "Sol Ring", _ =>
        {
            fetchCalls++;
            return Task.FromResult(expected);
        });

        Assert.Equal(expected, result);
        Assert.Equal(1, fetchCalls);
    }

    [Fact]
    public async Task GetOrAddAsync_WarmCache_ReturnsFirstValueWithoutFetchingAgain()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        int fetchCalls = 0;
        var expected = new CacheValue("first");

        await GetAsync(cache, "Sol Ring", _ =>
        {
            fetchCalls++;
            return Task.FromResult(expected);
        });
        CacheValue result = await GetAsync(cache, "Sol Ring", _ =>
        {
            fetchCalls++;
            return Task.FromResult(new CacheValue("second"));
        });

        Assert.Equal(expected, result);
        Assert.Equal(1, fetchCalls);
    }

    [Fact]
    public async Task GetOrAddAsync_NameCaseAndWhitespaceVariants_HitSameCacheEntry()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        int fetchCalls = 0;
        var expected = new CacheValue("first");

        await GetAsync(cache, "  Sol Ring  ", _ =>
        {
            fetchCalls++;
            return Task.FromResult(expected);
        });
        CacheValue result = await GetAsync(cache, "sol ring", _ =>
        {
            fetchCalls++;
            return Task.FromResult(new CacheValue("second"));
        });

        Assert.Equal(expected, result);
        Assert.Equal(1, fetchCalls);
    }

    [Fact]
    public async Task GetOrAddAsync_NullTtl_ReturnsValueWithoutCaching()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        int fetchCalls = 0;

        CacheValue first = await GetAsync(
            cache,
            "Sol Ring",
            _ => Task.FromResult(new CacheValue($"value-{++fetchCalls}")),
            _ => null);
        CacheValue second = await GetAsync(
            cache,
            "Sol Ring",
            _ => Task.FromResult(new CacheValue($"value-{++fetchCalls}")),
            _ => null);

        Assert.NotEqual(first, second);
        Assert.Equal(2, fetchCalls);
    }

    [Fact]
    public async Task GetOrAddAsync_FetchThrows_ReturnsFailureValueWithoutCaching()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        int fetchCalls = 0;
        var exception = new InvalidOperationException("upstream");
        Exception? receivedException = null;

        CacheValue first = await CachedNameResolution.GetOrAddAsync(
            cache,
            "grounder:",
            "Sol Ring",
            _ =>
            {
                fetchCalls++;
                throw exception;
            },
            caught =>
            {
                receivedException = caught;
                return new CacheValue("failure");
            },
            _ => CachedNameResolution.PositiveCacheTtl,
            CancellationToken.None);
        CacheValue second = await CachedNameResolution.GetOrAddAsync(
            cache,
            "grounder:",
            "Sol Ring",
            _ =>
            {
                fetchCalls++;
                throw exception;
            },
            _ => new CacheValue("failure"),
            _ => CachedNameResolution.PositiveCacheTtl,
            CancellationToken.None);

        Assert.Equal("failure", first.Value);
        Assert.Equal("failure", second.Value);
        Assert.Same(exception, receivedException);
        Assert.Equal(2, fetchCalls);
    }

    [Fact]
    public async Task GetOrAddAsync_CancelledFetch_RethrowsCancellation()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CachedNameResolution.GetOrAddAsync(
            cache,
            "grounder:",
            "Sol Ring",
            token => throw new OperationCanceledException(token),
            _ => new CacheValue("failure"),
            _ => CachedNameResolution.PositiveCacheTtl,
            cancellationSource.Token));
    }

    [Fact]
    public async Task GetOrAddAsync_NullCache_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => GetAsync(null!, "Sol Ring", _ => Task.FromResult(new CacheValue("value"))));
    }

    [Fact]
    public async Task GetOrAddAsync_BlankPrefix_ThrowsArgumentException()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<ArgumentException>(() => CachedNameResolution.GetOrAddAsync(
            cache, " ", "Sol Ring", _ => Task.FromResult(new CacheValue("value")), _ => new CacheValue("failure"),
            _ => CachedNameResolution.PositiveCacheTtl, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrAddAsync_NullCandidateName_ThrowsArgumentNullException()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => CachedNameResolution.GetOrAddAsync(
            cache, "grounder:", null!, _ => Task.FromResult(new CacheValue("value")), _ => new CacheValue("failure"),
            _ => CachedNameResolution.PositiveCacheTtl, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrAddAsync_NullFetchAsync_ThrowsArgumentNullException()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => CachedNameResolution.GetOrAddAsync(
            cache, "grounder:", "Sol Ring", null!, _ => new CacheValue("failure"), _ => CachedNameResolution.PositiveCacheTtl,
            CancellationToken.None));
    }

    [Fact]
    public async Task GetOrAddAsync_NullOnFailure_ThrowsArgumentNullException()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => CachedNameResolution.GetOrAddAsync(
            cache, "grounder:", "Sol Ring", _ => Task.FromResult(new CacheValue("value")), null!,
            _ => CachedNameResolution.PositiveCacheTtl, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrAddAsync_NullSelectTtl_ThrowsArgumentNullException()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => CachedNameResolution.GetOrAddAsync(
            cache, "grounder:", "Sol Ring", _ => Task.FromResult(new CacheValue("value")), _ => new CacheValue("failure"),
            null!, CancellationToken.None));
    }

    [Fact]
    public void CacheTtls_Constants_KeepExpectedValuesAndOrdering()
    {
        Assert.Equal(TimeSpan.FromHours(24), CachedNameResolution.PositiveCacheTtl);
        Assert.Equal(TimeSpan.FromHours(1), CachedNameResolution.NegativeCacheTtl);
        Assert.True(
            CachedNameResolution.NegativeCacheTtl < CachedNameResolution.PositiveCacheTtl,
            $"NegativeCacheTtl ({CachedNameResolution.NegativeCacheTtl}) must be shorter than PositiveCacheTtl "
                + $"({CachedNameResolution.PositiveCacheTtl}) so failed lookups are retried sooner than successful ones refresh.");
    }

    private static Task<CacheValue> GetAsync(
        IMemoryCache cache,
        string candidateName,
        Func<CancellationToken, Task<CacheValue>> fetchAsync,
        Func<CacheValue, TimeSpan?>? selectTtl = null) =>
        CachedNameResolution.GetOrAddAsync(
            cache,
            "grounder:",
            candidateName,
            fetchAsync,
            _ => new CacheValue("failure"),
            selectTtl ?? (_ => CachedNameResolution.PositiveCacheTtl),
            CancellationToken.None);

    private sealed record CacheValue(string Value);
}
