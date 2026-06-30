using DeckFlow.Core.Bracket;

namespace DeckFlow.Web.Services.Bracket;

/// <summary>
/// Loads the versioned <see cref="GameChangerCatalog"/> from <c>bracket-data.json</c>
/// and caches it in <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>.
/// The catalog is warm-loaded at application startup (BRACKET-02) so every request
/// resolves from memory without disk I/O.
/// </summary>
public interface IGameChangerCatalogService
{
    /// <summary>
    /// Returns the versioned Game Changer catalog, loading it from disk on the first
    /// call and returning the cached instance thereafter.
    /// </summary>
    GameChangerCatalog GetCatalog();
}
