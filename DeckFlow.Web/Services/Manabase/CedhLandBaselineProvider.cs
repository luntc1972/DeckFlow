using System.Text.Json;
using DeckFlow.Core.Manabase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Loads the committed cEDH land-baseline snapshot once and serves commander-name lookups from an
/// in-memory cache. Fail-open: a missing or corrupt file degrades to "no baseline", never an error.
/// </summary>
public interface ICedhLandBaselineProvider
{
    /// <summary>Warm-loads the baseline snapshot into memory, swallowing file/parse failures.</summary>
    void EnsureLoaded();

    /// <summary>Tries to resolve a baseline mean/sample size/standard deviation for one commander or one partner pair.</summary>
    bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n, out double sd, out string? generated);
}

/// <inheritdoc />
public sealed class CedhLandBaselineProvider : ICedhLandBaselineProvider
{
    private const string CacheKey = "manabase:cedh-land-baseline";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private int _loadFailureLogged;

    /// <summary>
    /// DI constructor — locates <c>Data/cedh-land-baseline/latest.json</c> inside the web app's
    /// content root.
    /// </summary>
    public CedhLandBaselineProvider(
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<CedhLandBaselineProvider>? logger = null)
        : this(
            Path.Combine(env.ContentRootPath, "Data", "cedh-land-baseline", "latest.json"),
            cache,
            logger)
    {
    }

    /// <summary>Test-seam constructor with an explicit baseline path.</summary>
    internal CedhLandBaselineProvider(string dataFilePath, IMemoryCache cache, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dataFilePath);
        ArgumentNullException.ThrowIfNull(cache);
        _dataFilePath = dataFilePath;
        _cache = cache;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public void EnsureLoaded()
        => GetOrLoadSnapshot();

    /// <inheritdoc />
    public bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n, out double sd, out string? generated)
    {
        mean = default;
        n = default;
        sd = default;
        generated = default;

        CedhLandBaselineSnapshot? snapshot = GetOrLoadSnapshot();
        if (snapshot is null)
        {
            return false;
        }

        generated = snapshot.Generated;

        foreach (string key in CandidateKeys(commanderNames))
        {
            if (snapshot.Commanders.TryGetValue(key, out CedhCommanderBaselineSnapshot? match))
            {
                mean = match.LandsMean;
                n = match.N;
                sd = match.LandsSd;
                return true;
            }
        }

        return false;
    }

    // Baseline keys are either a single commander name or a partner pair joined by " / " in an
    // unspecified order; try the solo name, then both partner orders. An MDFC commander name
    // (containing " // ") is a single card = a single key, so it is never split.
    private static IEnumerable<string> CandidateKeys(IReadOnlyList<string> commanderNames)
    {
        if (commanderNames.Count == 1)
        {
            yield return commanderNames[0];
        }
        else if (commanderNames.Count == 2)
        {
            yield return $"{commanderNames[0]} / {commanderNames[1]}";
            yield return $"{commanderNames[1]} / {commanderNames[0]}";
        }
    }

    private CedhLandBaselineSnapshot? GetOrLoadSnapshot()
    {
        if (_cache.TryGetValue<CacheEntry>(CacheKey, out CacheEntry? cached) && cached is not null)
        {
            return cached.Snapshot;
        }

        CedhLandBaselineSnapshot? snapshot = null;
        try
        {
            string json = File.ReadAllText(_dataFilePath);
            snapshot = JsonSerializer.Deserialize<CedhLandBaselineSnapshot>(json, JsonOptions);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            LogLoadFailureOnce(exception);
        }

        _cache.Set(CacheKey, new CacheEntry(snapshot), TimeSpan.FromHours(24));
        return snapshot;
    }

    private void LogLoadFailureOnce(Exception exception)
    {
        if (Interlocked.Exchange(ref _loadFailureLogged, 1) != 0)
        {
            return;
        }

        _logger.LogWarning(
            exception,
            "cEDH land baseline unavailable at {DataFilePath}; continuing without commander baselines.",
            _dataFilePath);
    }

    private sealed record CacheEntry(CedhLandBaselineSnapshot? Snapshot);
}
