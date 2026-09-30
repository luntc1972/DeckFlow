using System.IO;

namespace DeckFlow.Web.Infrastructure;

/// <summary>Writes a deck-intake card's closing markup when a Razor rendering scope ends.</summary>
internal sealed class IntakeCardScope(TextWriter writer, string closeMarkup) : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        writer.Write(closeMarkup);
    }
}
