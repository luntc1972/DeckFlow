using System.Text.Json;
using DeckFlow.Core.Bracket;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace DeckFlow.Web.Services.Bracket;

/// <summary>
/// Loads <c>bracket-data.json</c> into <see cref="IMemoryCache"/> once at startup
/// and serves the parsed <see cref="GameChangerCatalog"/> on every subsequent call.
/// Fail-closed: a missing or unparseable file throws <see cref="InvalidOperationException"/>
/// rather than caching a null (BRACKET-02 + T-76-03 mitigation).
/// </summary>
public sealed class GameChangerCatalogService : IGameChangerCatalogService
{
    private const string CacheKey = "bracket:game-changer-catalog";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// DI constructor — locates <c>bracket-data.json</c> inside the web app's
    /// <c>Data/</c> directory via <c>IWebHostEnvironment.ContentRootPath</c>.
    /// </summary>
    /// <param name="env">Web host environment (<c>ContentRootPath</c> must be set).</param>
    /// <param name="cache">Application-level memory cache.</param>
    public GameChangerCatalogService(IWebHostEnvironment env, IMemoryCache cache)
        : this(Path.Combine(env.ContentRootPath, "Data", "bracket-data.json"), cache)
    {
    }

    /// <summary>
    /// Test-seam constructor — accepts an explicit data file path so tests can point
    /// at the repo's <c>bracket-data.json</c> without standing up a full web host.
    /// </summary>
    /// <param name="dataFilePath">Absolute path to the bracket-data JSON file.</param>
    /// <param name="cache">Memory cache instance (use <c>new MemoryCache(…)</c> in tests).</param>
    internal GameChangerCatalogService(string dataFilePath, IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(dataFilePath);
        ArgumentNullException.ThrowIfNull(cache);
        _dataFilePath = dataFilePath;
        _cache = cache;
    }

    /// <inheritdoc/>
    public GameChangerCatalog GetCatalog()
    {
        if (_cache.TryGetValue<GameChangerCatalog>(CacheKey, out var cached) && cached is not null)
            return cached;

        var json = File.ReadAllText(_dataFilePath);
        var catalog = JsonSerializer.Deserialize<GameChangerCatalog>(json, JsonOptions)
            ?? throw new InvalidOperationException("bracket-data.json could not be loaded");

        _cache.Set(CacheKey, catalog, TimeSpan.FromHours(24));
        return catalog;
    }
}
