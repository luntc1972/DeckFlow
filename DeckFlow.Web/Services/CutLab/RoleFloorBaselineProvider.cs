using System.Text.Json;
using DeckFlow.Core.Research;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Loads the committed commander role-floor snapshot once and serves commander-role lookups from an
/// in-memory cache. Fail-open: a missing or corrupt file degrades to "no commander data", never an
/// error, which preserves RFLR-06's byte-identical fallback behavior.
/// </summary>
public interface IRoleFloorBaselineProvider
{
    /// <summary>Warm-loads the role-floor snapshot into memory, swallowing file/parse failures.</summary>
    void EnsureLoaded();

    /// <summary>Tries to resolve a commander-specific floor for the requested role.</summary>
    /// <param name="commanderNames">The deck's commander names.</param>
    /// <param name="role">The role key to resolve.</param>
    /// <param name="floor">The resolved floor when found; otherwise 0.</param>
    /// <returns><see langword="true"/> when a commander-specific floor is available for the role.</returns>
    bool TryGetRoleFloor(IReadOnlyList<string> commanderNames, string role, out int floor);
}

/// <inheritdoc />
public sealed class RoleFloorBaselineProvider : IRoleFloorBaselineProvider
{
    private const string CacheKey = "cutlab:role-floor-baseline";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private int _loadFailureLogged;

    /// <summary>
    /// DI constructor — locates <c>Data/role-floor-baseline/latest.json</c> inside the web app's
    /// content root.
    /// </summary>
    public RoleFloorBaselineProvider(
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<RoleFloorBaselineProvider>? logger = null)
        : this(
            Path.Combine(env.ContentRootPath, "Data", "role-floor-baseline", "latest.json"),
            cache,
            logger)
    {
    }

    /// <summary>Test-seam constructor with an explicit snapshot path.</summary>
    internal RoleFloorBaselineProvider(string dataFilePath, IMemoryCache cache, ILogger? logger = null)
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
    public bool TryGetRoleFloor(IReadOnlyList<string> commanderNames, string role, out int floor)
    {
        floor = default;

        RoleFloorBaselineSnapshot? snapshot = GetOrLoadSnapshot();
        if (snapshot is null)
        {
            return false;
        }

        foreach (string key in CommanderBaselineKeys.Candidates(commanderNames))
        {
            if (!snapshot.Commanders.TryGetValue(key, out RoleFloorCommanderSnapshot? match))
            {
                continue;
            }

            foreach ((string candidateRole, int candidateFloor) in match.Floors)
            {
                if (!string.Equals(candidateRole, role, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                floor = candidateFloor;
                return true;
            }
        }

        return false;
    }

    private RoleFloorBaselineSnapshot? GetOrLoadSnapshot()
    {
        if (_cache.TryGetValue<CacheEntry>(CacheKey, out CacheEntry? cached) && cached is not null)
        {
            return cached.Snapshot;
        }

        RoleFloorBaselineSnapshot? snapshot = null;
        try
        {
            string json = File.ReadAllText(_dataFilePath);
            snapshot = JsonSerializer.Deserialize<RoleFloorBaselineSnapshot>(json, JsonOptions);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            LogLoadFailureOnce(exception);
        }

        // Why: a corrupt file must not be re-read on every request.
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
            "Role-floor baseline unavailable at {DataFilePath}; continuing without commander role floors.",
            _dataFilePath);
    }

    private sealed record CacheEntry(RoleFloorBaselineSnapshot? Snapshot);
}
