using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCanonicalCardNameResolver : ICanonicalCardNameResolver
{
    public Dictionary<string, string> Map { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<IReadOnlyList<string>> RequestedNames { get; } = [];

    public Task<IReadOnlyDictionary<string, string>> ResolveCanonicalNamesAsync(
        IReadOnlyList<string> cardNames,
        CancellationToken cancellationToken = default)
    {
        RequestedNames.Add(cardNames.ToArray());
        IReadOnlyDictionary<string, string> result = cardNames
            .Where(Map.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name, name => Map[name], StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }
}
