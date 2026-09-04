using System.Text.Json;
using System.Text.RegularExpressions;
using DeckFlow.Core.Integration;
using DeckFlow.Web.Models.CutLab;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Result of an EDHREC commander-theme lookup.</summary>
/// <param name="Themes">Themes returned by EDHREC.</param>
/// <param name="IsUnavailable">Whether the upstream result was unavailable.</param>
public sealed record EdhrecThemeResult(
    IReadOnlyList<CutLabCommanderTheme> Themes,
    bool IsUnavailable);

/// <summary>Loads commander themes and their cards from EDHREC's static JSON pages.</summary>
public interface IEdhrecCommanderThemeService
{
    /// <summary>Gets ordered commander themes, returning unavailable data on upstream failure.</summary>
    Task<EdhrecThemeResult> GetCommanderThemesAsync(string commanderName, CancellationToken cancellationToken = default);
    /// <summary>Gets deduplicated card names for one commander theme.</summary>
    Task<IReadOnlyList<string>> GetThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken = default);
}

/// <summary>Fail-open EDHREC theme source with bounded response parsing and memory caching.</summary>
public sealed partial class EdhrecCommanderThemeService : IEdhrecCommanderThemeService
{
    // Why: planning retained these product defaults for unobtrusive initial selection.
    internal const double PreselectMinimumShare = 0.05;
    // Why: planning retained these product defaults for unobtrusive initial selection.
    internal const int PreselectMaximumThemes = 3;
    internal const int MaxResponseBytes = 4 * 1024 * 1024;
    // Why: HttpClient bounds response bytes; this remains a defense against unexpectedly large text.
    internal const int MaxResponseCharacters = 4 * 1024 * 1024;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan UnavailableCacheDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EmptyThemeCardCacheDuration = TimeSpan.FromMinutes(5);
    // Why: transient upstream failures should retain a recently known-good disk response, but never indefinitely stale EDHREC data.
    internal static readonly TimeSpan DiskCacheFallbackMaxAge = TimeSpan.FromDays(7);
    private static readonly TimeSpan DiskCacheSweepMinimumInterval = TimeSpan.FromMinutes(5);
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ResiliencePipeline<RestResponse> _pipeline;

    private readonly IMemoryCache _memoryCache;

    private readonly ILogger<EdhrecCommanderThemeService>? _logger;

    private readonly string _cacheRoot;

    private int _cacheWriteFailureLogged;

    private long _lastCacheSweepUtcTicks;

    /// <summary>Creates an EDHREC service using the named client and resilience pipeline.</summary>
    public EdhrecCommanderThemeService(IHttpClientFactory httpClientFactory, ResiliencePipelineProvider<string> pipelineProvider, IMemoryCache memoryCache, IWebHostEnvironment environment, ILogger<EdhrecCommanderThemeService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(environment);
        _httpClientFactory = httpClientFactory;
        _pipeline = pipelineProvider.GetPipeline<RestResponse>("edhrec") ?? ResiliencePipeline<RestResponse>.Empty;
        _memoryCache = memoryCache;
        _logger = logger;
        _cacheRoot = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("MTG_DATA_DIR") ?? Path.Combine(environment.ContentRootPath, "..", "artifacts"), "edhrec-themes"));
    }

    /// <inheritdoc />
    public async Task<EdhrecThemeResult> GetCommanderThemesAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string slug = EdhrecCardLookup.Slugify(commanderName);
        string cacheKey = "cutlab:edhrec:themes:" + slug;

        if (!IsValidSlug(slug))
        {
            _logger?.LogWarning("Rejected invalid EDHREC commander slug for {CommanderName}", commanderName);
            return CacheUnavailableResult(cacheKey);
        }

        if (_memoryCache.TryGetValue<EdhrecThemeResult>(cacheKey, out EdhrecThemeResult? cached) && cached is not null)
        {
            return cached;
        }

        string? body = await FetchAsync($"commanders/{slug}.json", slug + ".json", cancellationToken).ConfigureAwait(false);

        if (body is null)
        {
            return CacheUnavailableResult(cacheKey);
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("panels", out JsonElement panels) ||
                !panels.TryGetProperty("taglinks", out JsonElement tags) ||
                tags.ValueKind != JsonValueKind.Array)
            {
                return CacheUnavailableResult(cacheKey);
            }

            List<CutLabCommanderTheme> themes = tags
                .EnumerateArray()
                .Where(tag => tag.ValueKind == JsonValueKind.Object)
                .Select(tag => new CutLabCommanderTheme
                {
                    Slug = tag.TryGetProperty("slug", out JsonElement slugElement) ? slugElement.GetString() ?? string.Empty : string.Empty,
                    DisplayName = tag.TryGetProperty("value", out JsonElement displayNameElement) ? displayNameElement.GetString() ?? string.Empty : string.Empty,
                    DeckCount = tag.TryGetProperty("count", out JsonElement deckCountElement) && deckCountElement.TryGetInt32(out int deckCount) ? deckCount : 0,
                })
                .Where(theme => IsValidSlug(theme.Slug) && !string.IsNullOrWhiteSpace(theme.DisplayName))
                .OrderByDescending(theme => theme.DeckCount)
                .ThenBy(theme => theme.Slug, StringComparer.Ordinal)
                .DistinctBy(theme => theme.Slug, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var parsed = new EdhrecThemeResult(themes, false);
            _memoryCache.Set(cacheKey, parsed, CacheDuration);
            return parsed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "EDHREC commander page had an unexpected JSON shape");
            return CacheUnavailableResult(cacheKey);
        }
    }

    private EdhrecThemeResult CacheUnavailableResult(string cacheKey)
    {
        var unavailable = new EdhrecThemeResult([], true);
        _memoryCache.Set(cacheKey, unavailable, UnavailableCacheDuration);
        return unavailable;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string commanderSlug = EdhrecCardLookup.Slugify(commanderName);

        if (!IsValidSlug(commanderSlug) || !IsValidSlug(themeSlug))
        {
            return [];
        }

        string cacheKey = $"cutlab:edhrec:themecards:{commanderSlug}:{themeSlug}";

        if (_memoryCache.TryGetValue<IReadOnlyList<string>>(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        string? body = await FetchAsync($"commanders/{commanderSlug}/{themeSlug}.json", commanderSlug + "__" + themeSlug + ".json", cancellationToken).ConfigureAwait(false);

        if (body is null)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("container", out JsonElement container) ||
                !container.TryGetProperty("json_dict", out JsonElement dictionary) ||
                !dictionary.TryGetProperty("cardlists", out JsonElement cardLists) ||
                cardLists.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            IReadOnlyList<string> parsed = cardLists
                .EnumerateArray()
                .Where(cardList => cardList.ValueKind == JsonValueKind.Array)
                .SelectMany(cardList => cardList.EnumerateArray())
                .Where(card => card.ValueKind == JsonValueKind.Object && card.TryGetProperty("name", out _))
                .Select(card => card.GetProperty("name").GetString())
                .Where(cardName => !string.IsNullOrWhiteSpace(cardName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            // Why: an empty parse can be legitimate or an upstream shape change; avoid request storms while retrying soon.
            _memoryCache.Set(cacheKey, parsed, parsed.Count > 0 ? CacheDuration : EmptyThemeCardCacheDuration);
            return parsed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "EDHREC theme page had an unexpected JSON shape");
            return [];
        }
    }

    internal static IReadOnlyList<CutLabCommanderTheme> SelectDefaultThemes(IReadOnlyList<CutLabCommanderTheme> themes)
    {
        var total = themes.Sum(x => x.DeckCount);
        return total <= 0 ? [] : themes.Where(x => (double)x.DeckCount / total >= PreselectMinimumShare).Take(PreselectMaximumThemes).ToList();
    }

    private async Task<string?> FetchAsync(string resource, string fileName, CancellationToken cancellationToken)
    {
        CacheEntry? cached = null;
        try
        {
            var cachePath = GetCachePath(fileName);
            cached = ReadCache(cachePath);
            var client = new RestClient(_httpClientFactory.CreateClient("edhrec"));
            var request = new RestRequest(resource, Method.Get);
            if (!string.IsNullOrWhiteSpace(cached?.ETag))
            {
                request.AddHeader("If-None-Match", cached.ETag);
            }

            var response = await _pipeline.ExecuteAsync(async ct => await client.ExecuteAsync(request, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

            if ((int)response.StatusCode == 304 && cached is not null)
            {
                // Why: revalidation confirms this entry remains fresh, so retain its offline fallback.
                WriteCache(cachePath, cached with { WrittenAtUtc = DateTimeOffset.UtcNow });
                return cached.Body;
            }

            if ((int)response.StatusCode == 403 && response.Content?.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger?.LogDebug("EDHREC page absent: {Resource}", resource);
                return null;
            }

            if (!response.IsSuccessful)
            {
                return GetUsableCachedBody(cached);
            }

            if (string.IsNullOrWhiteSpace(response.Content) || response.Content.Length > MaxResponseCharacters)
            {
                return null;
            }

            string? eTag = response.Headers?
                .FirstOrDefault(header => string.Equals(header.Name, "ETag", StringComparison.OrdinalIgnoreCase))?
                .Value?
                .ToString();
            WriteCache(cachePath, new CacheEntry(response.Content, eTag, DateTimeOffset.UtcNow));
            return response.Content;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "EDHREC fetch failed for {Resource}", resource);
            return GetUsableCachedBody(cached);
        }
    }

    private static string? GetUsableCachedBody(CacheEntry? cached)
        => cached is not null && DateTimeOffset.UtcNow - cached.WrittenAtUtc <= DiskCacheFallbackMaxAge ? cached.Body : null;

    private static bool IsValidSlug(string slug) => SlugPattern().IsMatch(slug);

    private string GetCachePath(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(_cacheRoot, fileName));

        if (!path.StartsWith(_cacheRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("EDHREC cache path escaped its root.");
        }

        return path;
    }

    private CacheEntry? ReadCache(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Unable to read EDHREC disk cache");
            return null;
        }
    }

    private void WriteCache(string path, CacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(_cacheRoot);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entry));
            File.Move(temporaryPath, path, overwrite: true);
            SweepExpiredCacheEntries();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (Interlocked.Exchange(ref _cacheWriteFailureLogged, 1) == 0)
            {
                _logger?.LogWarning(ex, "Unable to write EDHREC disk cache");
            }
        }
    }

    private void SweepExpiredCacheEntries()
    {
        DateTime now = DateTime.UtcNow;
        long previousSweepTicks = Interlocked.Read(ref _lastCacheSweepUtcTicks);

        if (previousSweepTicks != 0 && now.Ticks - previousSweepTicks < DiskCacheSweepMinimumInterval.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastCacheSweepUtcTicks, now.Ticks, previousSweepTicks) != previousSweepTicks)
        {
            return;
        }

        try
        {
            foreach (var cachePath in Directory.EnumerateFiles(_cacheRoot, "*.json"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(cachePath) < now - DiskCacheFallbackMaxAge)
                    {
                        File.Delete(cachePath);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger?.LogDebug(ex, "Unable to evict expired EDHREC disk cache entry");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Unable to sweep expired EDHREC disk cache entries");
        }
    }
    private sealed record CacheEntry(string Body, string? ETag, DateTimeOffset WrittenAtUtc);

    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}

/// <summary>
/// Null-object fallback used when no <see cref="IEdhrecCommanderThemeService"/> is supplied (e.g.
/// direct-construction tests). Always reports the commander-theme lookup as unavailable, matching
/// the fail-open "EDHREC could not be reached" branch.
/// </summary>
internal sealed class NullEdhrecCommanderThemeService : IEdhrecCommanderThemeService
{
    public static NullEdhrecCommanderThemeService Instance { get; } = new();

    public Task<EdhrecThemeResult> GetCommanderThemesAsync(string commanderName, CancellationToken cancellationToken = default)
        => Task.FromResult(new EdhrecThemeResult([], true));

    public Task<IReadOnlyList<string>> GetThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);
}
