namespace DeckFlow.Web.Services;

/// <summary>Resolves the running application's version string for display.</summary>
public interface IVersionService
{
    /// <summary>
    /// Returns the display version for the running application.
    /// </summary>
    string GetVersion();
}
