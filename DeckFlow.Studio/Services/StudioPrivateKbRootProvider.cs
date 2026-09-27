using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Resolves <c>DECKFLOW_KB_ROOT</c> before the optional <c>ContentKb:PrivateRoot</c> setting.
/// </summary>
public sealed class StudioPrivateKbRootProvider : IPrivateKbRootProvider
{
    private readonly string? _environmentRoot;
    private readonly string? _configuredRoot;

    /// <summary>Initializes the lazy private-root provider.</summary>
    /// <param name="environmentRoot">The environment-variable value.</param>
    /// <param name="configuredRoot">The Studio configuration fallback.</param>
    public StudioPrivateKbRootProvider(string? environmentRoot, string? configuredRoot)
    {
        _environmentRoot = environmentRoot;
        _configuredRoot = configuredRoot;
    }

    /// <inheritdoc />
    public PrivateKbRoot GetRoot() => new(string.IsNullOrWhiteSpace(_environmentRoot) ? _configuredRoot : _environmentRoot);
}
