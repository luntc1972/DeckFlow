namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Receives synchronous progress messages from content orchestration operations without introducing
/// async reordering or any direct console dependency in DeckFlow.Core.
/// </summary>
public interface IOrchestratorProgress
{
    /// <summary>
    /// Reports one progress message to the host-supplied sink.
    /// </summary>
    /// <param name="message">Progress message describing the current operation state.</param>
    void Report(string message);

    /// <summary>
    /// No-op progress sink used when the caller does not need live orchestration messages.
    /// </summary>
    public sealed class NullOrchestratorProgress : IOrchestratorProgress
    {
        /// <summary>
        /// Ignores the supplied message.
        /// </summary>
        /// <param name="message">Unused progress message.</param>
        public void Report(string message)
        {
        }
    }
}
