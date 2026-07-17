using System.Text.Json;
using DeckFlow.Core.Manabase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Loads the committed community-baseline snapshot once and serves per-bracket land lookups from an
/// in-memory cache. Fail-open: a missing or corrupt file degrades to "no baseline", never an error.
/// </summary>
public interface IManabaseBaselineProvider
{
    /// <summary>Warm-loads the baseline snapshot into memory, swallowing file/parse failures.</summary>
    void EnsureLoaded();

    /// <summary>Returns the bundled baseline row for a bracket, or null if absent/unavailable.</summary>
    /// <param name="bracket">Power bracket (2-5).</param>
    ManabaseBracketBaseline? TryGetBracketBaseline(int bracket);

    /// <summary>Returns the bundled per-commander baseline row, or null if absent/unavailable.</summary>
    /// <param name="commanderNames">1 (lone) or 2 (partner pair) commander names; other counts return null.</param>
    ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames);
}

/// <inheritdoc />
public sealed class ManabaseBaselineProvider : IManabaseBaselineProvider
{
    private const string CacheKey = "manabase:community-baseline";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private int _loadFailureLogged;

    /// <summary>DI constructor — locates <c>Data/manabase-baseline/latest.json</c> in the content root.</summary>
    public ManabaseBaselineProvider(
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<ManabaseBaselineProvider>? logger = null)
        : this(
            Path.Combine(env.ContentRootPath, "Data", "manabase-baseline", "latest.json"),
            cache,
            logger)
    {
    }

    /// <summary>Test-seam constructor with an explicit baseline path.</summary>
    internal ManabaseBaselineProvider(string dataFilePath, IMemoryCache cache, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dataFilePath);
        ArgumentNullException.ThrowIfNull(cache);
        _dataFilePath = dataFilePath;
        _cache = cache;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public void EnsureLoaded() => GetOrLoadSnapshot();

    /// <inheritdoc />
    public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
    {
        ManabaseBaselineSnapshot? snapshot = GetOrLoadSnapshot()?.Snapshot;
        if (snapshot is null)
        {
            return null;
        }

        foreach (ManabaseBracketBaseline row in snapshot.Brackets)
        {
            if (row.Bracket == bracket)
            {
                // Backfill provenance from the snapshot-level source when the row omits its own
                // (Increment 1 rows share one source; Increment 2 may set per-row).
                return row.Source is null ? row with { Source = snapshot.Source } : row;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames)
    {
        ArgumentNullException.ThrowIfNull(commanderNames);

        CacheEntry? cacheEntry = GetOrLoadSnapshot();
        if (cacheEntry?.Commanders is null)
        {
            return null;
        }

        string key = commanderNames.Count switch
        {
            1 => ManabaseCommanderKey.Create(commanderNames[0]),
            2 => ManabaseCommanderKey.Create(commanderNames[0], commanderNames[1]),
            _ => string.Empty,
        };

        return key.Length > 0 && cacheEntry.Commanders.TryGetValue(key, out ManabaseCommanderBaseline? row)
            ? row
            : null;
    }

    private CacheEntry? GetOrLoadSnapshot()
    {
        if (_cache.TryGetValue<CacheEntry>(CacheKey, out CacheEntry? cached) && cached is not null)
        {
            return cached;
        }

        ManabaseBaselineSnapshot? snapshot = null;
        IReadOnlyDictionary<string, ManabaseCommanderBaseline>? commanders = null;
        try
        {
            string json = File.ReadAllText(_dataFilePath);
            snapshot = JsonSerializer.Deserialize<ManabaseBaselineSnapshot>(json, JsonOptions);
            commanders = BuildCommanders(snapshot);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            LogLoadFailureOnce(exception);
        }

        CacheEntry cacheEntry = new(snapshot, commanders);
        _cache.Set(CacheKey, cacheEntry, TimeSpan.FromHours(24));
        return cacheEntry;
    }

    private void LogLoadFailureOnce(Exception exception)
    {
        if (Interlocked.Exchange(ref _loadFailureLogged, 1) != 0)
        {
            return;
        }

        _logger.LogWarning(
            exception,
            "Manabase community baseline unavailable at {DataFilePath}; continuing without it.",
            _dataFilePath);
    }

    private static IReadOnlyDictionary<string, ManabaseCommanderBaseline>? BuildCommanders(ManabaseBaselineSnapshot? snapshot)
    {
        if (snapshot?.Commanders.Count is not > 0)
        {
            return null;
        }

        Dictionary<string, ManabaseCommanderBaseline> rows = new(StringComparer.Ordinal);
        foreach (ManabaseCommanderBaseline row in snapshot.Commanders)
        {
            string key = ManabaseCommanderKey.Create(row.Name, row.PartnerName);
            // The generator pre-dedupes, but keep the higher-deck-count row here too so lookup never
            // depends on perfect input hygiene.
            if (!rows.TryGetValue(key, out ManabaseCommanderBaseline? existing) || row.DeckCount > existing.DeckCount)
            {
                rows[key] = row;
            }
        }

        return rows;
    }

    private sealed record CacheEntry(
        ManabaseBaselineSnapshot? Snapshot,
        IReadOnlyDictionary<string, ManabaseCommanderBaseline>? Commanders);
}
