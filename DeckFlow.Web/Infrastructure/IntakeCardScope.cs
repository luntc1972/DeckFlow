using System.IO;

namespace DeckFlow.Web.Infrastructure;

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
