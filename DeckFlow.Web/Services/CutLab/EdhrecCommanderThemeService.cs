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
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline<RestResponse> _pipeline;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<EdhrecCommanderThemeService>? _logger;
    private readonly string _cacheRoot;
    private bool _cacheWriteFailureLogged;

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
        var slug = EdhrecCardLookup.Slugify(commanderName);
        if (!IsValidSlug(slug))
        {
            _logger?.LogWarning("Rejected invalid EDHREC commander slug for {CommanderName}", commanderName);
            return new([], true);
        }
        var cacheKey = "cutlab:edhrec:themes:" + slug;
        if (_memoryCache.TryGetValue<EdhrecThemeResult>(cacheKey, out var cached) && cached is not null) return cached;
        var body = await FetchAsync($"commanders/{slug}.json", slug + ".json", cancellationToken).ConfigureAwait(false);
        if (body is null) return new([], true);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("panels", out var panels) || !panels.TryGetProperty("taglinks", out var tags) || tags.ValueKind != JsonValueKind.Array) return new([], true);
            var result = tags.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => new CutLabCommanderTheme { Slug = x.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "", DisplayName = x.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "", DeckCount = x.TryGetProperty("count", out var c) && c.TryGetInt32(out var n) ? n : 0 }).Where(x => IsValidSlug(x.Slug) && !string.IsNullOrWhiteSpace(x.DisplayName)).OrderByDescending(x => x.DeckCount).ThenBy(x => x.Slug, StringComparer.Ordinal).ToList();
            var parsed = new EdhrecThemeResult(result, false);
            _memoryCache.Set(cacheKey, parsed, CacheDuration);
            return parsed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger?.LogWarning(ex, "EDHREC commander page had an unexpected JSON shape"); return new([], true); }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetThemeCardNamesAsync(string commanderName, string themeSlug, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var commanderSlug = EdhrecCardLookup.Slugify(commanderName);
        if (!IsValidSlug(commanderSlug) || !IsValidSlug(themeSlug)) return [];
        var body = await FetchAsync($"commanders/{commanderSlug}/{themeSlug}.json", commanderSlug + "__" + themeSlug + ".json", cancellationToken).ConfigureAwait(false);
        if (body is null) return [];
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("container", out var container) || !container.TryGetProperty("json_dict", out var dict) || !dict.TryGetProperty("cardlists", out var lists) || lists.ValueKind != JsonValueKind.Array) return [];
            return lists.EnumerateArray().SelectMany(x => x.ValueKind == JsonValueKind.Array ? x.EnumerateArray() : []).Where(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("name", out _)).Select(x => x.GetProperty("name").GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Cast<string>().ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger?.LogWarning(ex, "EDHREC theme page had an unexpected JSON shape"); return []; }
    }

    internal static IReadOnlyList<CutLabCommanderTheme> SelectDefaultThemes(IReadOnlyList<CutLabCommanderTheme> themes)
    {
        var total = themes.Sum(x => x.DeckCount);
        return total <= 0 ? [] : themes.Where(x => (double)x.DeckCount / total >= PreselectMinimumShare).Take(PreselectMaximumThemes).ToList();
    }

    private async Task<string?> FetchAsync(string resource, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = GetCachePath(fileName);
            var cached = ReadCache(cachePath);
            var client = new RestClient(_httpClientFactory.CreateClient("edhrec"));
            var request = new RestRequest(resource, Method.Get);
            if (!string.IsNullOrWhiteSpace(cached?.ETag)) request.AddHeader("If-None-Match", cached.ETag);
            var response = await _pipeline.ExecuteAsync(async ct => await client.ExecuteAsync(request, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode == 304 && cached is not null) return cached.Body;
            if ((int)response.StatusCode == 403 && response.Content?.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) == true) { _logger?.LogDebug("EDHREC page absent: {Resource}", resource); return null; }
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content) || response.Content.Length > MaxResponseBytes) return null;
            WriteCache(cachePath, new CacheEntry(response.Content, response.Headers?.FirstOrDefault(x => string.Equals(x.Name, "ETag", StringComparison.OrdinalIgnoreCase))?.Value?.ToString()));
            return response.Content;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger?.LogWarning(ex, "EDHREC fetch failed for {Resource}", resource); return null; }
    }

    private static bool IsValidSlug(string slug) => SlugPattern().IsMatch(slug);
    private string GetCachePath(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(_cacheRoot, fileName));
        if (!path.StartsWith(_cacheRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("EDHREC cache path escaped its root.");
        return path;
    }
    private CacheEntry? ReadCache(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path)) : null; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { _logger?.LogWarning(ex, "Unable to read EDHREC disk cache"); return null; }
    }
    private void WriteCache(string path, CacheEntry entry)
    {
        try { Directory.CreateDirectory(_cacheRoot); File.WriteAllText(path, JsonSerializer.Serialize(entry)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (!_cacheWriteFailureLogged) { _cacheWriteFailureLogged = true; _logger?.LogWarning(ex, "Unable to write EDHREC disk cache"); } }
    }
    private sealed record CacheEntry(string Body, string? ETag);
    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant)] private static partial Regex SlugPattern();
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
