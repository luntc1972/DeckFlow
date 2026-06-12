using Microsoft.AspNetCore.Mvc.Razor;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Appends the shared /Views/Deck folder as a fallback for split deck-tool controllers.
/// </summary>
public sealed class DeckViewLocationExpander : IViewLocationExpander
{
    /// <summary>
    /// Populates values that contribute to the view-location cache key.
    /// </summary>
    /// <param name="context">The current view-expander context.</param>
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        // Why: view selection does not vary by custom expander state, so no cache-key contribution is needed.
    }

    /// <summary>
    /// Expands the set of candidate view locations for MVC view resolution.
    /// </summary>
    /// <param name="context">The current view-expander context.</param>
    /// <param name="viewLocations">The default MVC view search locations.</param>
    /// <returns>The original search locations followed by the shared Deck view fallbacks.</returns>
    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        // Why: append for every controller so existing controller-specific views still win and /Views/Deck stays a simple fallback.
        return viewLocations.Concat(new[]
        {
            "/Views/Deck/{0}.cshtml",
            "/Views/Deck/{0}{1}.cshtml",
        });
    }
}
