namespace DeckFlow.Studio;

/// <summary>
/// Base for Studio Blazor pages that own a single per-page <see cref="CancellationTokenSource"/>
/// created on construction and cancelled/disposed when the page is torn down. Centralizes the
/// <c>_cts = new()</c> field and the identical <c>Dispose()</c> that every such page previously
/// duplicated. Pages needing extra teardown override <see cref="Dispose"/> and call
/// <c>base.Dispose()</c>.
/// </summary>
public abstract class StudioCancellableComponentBase : StudioComponentBase, IDisposable
{
    /// <summary>
    /// The page-lifetime cancellation source. Pass <c>Cts.Token</c> to async work so navigating away
    /// cancels in-flight operations.
    /// </summary>
    protected CancellationTokenSource Cts { get; } = new();

    /// <summary>Cancels and disposes <see cref="Cts"/>. Override and call <c>base.Dispose()</c> for extra teardown.</summary>
    public virtual void Dispose()
    {
        Cts.Cancel();
        Cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
