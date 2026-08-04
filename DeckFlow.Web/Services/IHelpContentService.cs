using DeckFlow.Web.Models;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Services;

/// <summary>
/// Loads and caches user-facing help topics stored as markdown files.
/// </summary>
public interface IHelpContentService
{
    /// <summary>Returns all topics ordered by their <c>order</c> header, then by title.</summary>
    IReadOnlyList<HelpTopic> GetAll();

    /// <summary>Returns a single topic by slug, or <c>null</c> when not found.</summary>
    HelpTopic? GetBySlug(string slug);

    /// <summary>Returns whether a topic is visible under the current feature-flag state.</summary>
    bool IsTopicVisible(HelpTopic topic, IFeatureFlagCache featureFlags);
}
