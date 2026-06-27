using Microsoft.AspNetCore.Components;

namespace DeckFlow.Studio;

/// <summary>
/// Base for Studio Blazor pages. Provides the disposal-safe re-render helpers every page needs so
/// the <c>try { StateHasChanged(); } catch (ObjectDisposedException/InvalidOperationException)</c>
/// idiom is written once here instead of being copy-pasted at each progress/completion callback.
/// </summary>
public abstract class StudioComponentBase : ComponentBase
{
    /// <summary>
    /// Calls <see cref="ComponentBase.StateHasChanged"/>, swallowing the two exceptions that a
    /// re-render can throw after the circuit/component has been torn down (a progress callback firing
    /// post-dispose). Call only on the renderer's synchronization context (e.g. from a lifecycle
    /// method or inside an <see cref="ComponentBase.InvokeAsync(Action)"/> body).
    /// </summary>
    protected void SafeStateHasChanged()
    {
        try
        {
            StateHasChanged();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// Marshals a disposal-safe re-render onto the renderer's synchronization context. Use from a
    /// background continuation where you are not already on that context.
    /// </summary>
    protected Task SafeStateHasChangedAsync() => InvokeAsync(SafeStateHasChanged);
}
