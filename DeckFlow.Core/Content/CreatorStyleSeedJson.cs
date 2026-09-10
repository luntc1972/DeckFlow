using System.Text.Json;

namespace DeckFlow.Core.Content;

/// <summary>
/// The single serializer-options contract shared by the CLI export writer and the Web startup seed
/// reader for the creator-style profile and deck-cache seed files. Changing the naming policy
/// invalidates every already-committed seed file.
/// </summary>
public static class CreatorStyleSeedJson
{
    /// <summary>
    /// CamelCase, case-insensitive JSON options with indented writes, shared by
    /// <c>creator-style-index-export</c> and <c>CreatorStyleSeedLoader</c>.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
}
