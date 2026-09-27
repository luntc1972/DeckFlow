using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Lazily resolves the private content-KB root for Studio content operations.
/// </summary>
public interface IPrivateKbRootProvider
{
    /// <summary>Gets the configured private KB root.</summary>
    /// <returns>The resolved private KB root.</returns>
    PrivateKbRoot GetRoot();
}
