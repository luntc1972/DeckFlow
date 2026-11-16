using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Web.Tests;

internal sealed class ThrowingCanonicalCardNameResolver : ICanonicalCardNameResolver
{
    private readonly Exception _exception;

    public ThrowingCanonicalCardNameResolver(Exception? exception = null)
    {
        _exception = exception ?? new HttpRequestException("Scryfall unavailable");
    }

    public int CallCount { get; private set; }

    public Task<IReadOnlyDictionary<string, string>> ResolveCanonicalNamesAsync(
        IReadOnlyList<string> cardNames,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        throw _exception;
    }
}
