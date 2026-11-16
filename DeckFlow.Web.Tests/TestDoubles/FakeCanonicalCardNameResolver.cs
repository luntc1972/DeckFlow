using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCanonicalCardNameResolver : ICanonicalCardNameResolver
{
    public Dictionary<string, string> Map { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<IReadOnlyList<string>> RequestedNames { get; } = [];
    public Exception? ThrowOnResolve { get; init; }
    public int CallCount => RequestedNames.Count;

    public Task<IReadOnlyDictionary<string, string>> ResolveCanonicalNamesAsync(
        IReadOnlyList<string> cardNames,
        CancellationToken cancellationToken = default)
    {
        RequestedNames.Add(cardNames.ToArray());
        if (ThrowOnResolve is not null)
        {
            throw ThrowOnResolve;
        }

        IReadOnlyDictionary<string, string> result = cardNames
            .Where(Map.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name, name => Map[name], StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }
}
