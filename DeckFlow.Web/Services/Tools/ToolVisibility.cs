using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Services.Tools;

/// <summary>
/// Pure helpers for evaluating tool visibility from feature flags.
/// </summary>
public static class ToolVisibility
{
    /// <summary>
    /// Returns whether the supplied tool is visible with the current flag snapshot.
    /// </summary>
    /// <param name="tool">Tool definition to evaluate.</param>
    /// <param name="cache">Feature-flag cache.</param>
    /// <returns><see langword="true" /> when the tool flag is enabled.</returns>
    public static bool IsVisible(ToolDefinition tool, IFeatureFlagCache cache)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(cache);
        return cache.IsEnabled(tool.FlagKey);
    }

    /// <summary>
    /// Groups visible tools by section, omitting empty sections and preserving registry order.
    /// </summary>
    /// <param name="tools">Tool definitions in registry order.</param>
    /// <param name="cache">Feature-flag cache.</param>
    /// <returns>Visible sections in section declaration order.</returns>
    public static IReadOnlyList<ToolSection> VisibleBySection(
        IReadOnlyList<ToolDefinition> tools,
        IFeatureFlagCache cache)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(cache);

        var results = new List<ToolSection>();

        foreach (var section in Enum.GetValues<ToolNavSection>())
        {
            var visibleTools = tools
                .Where(tool => tool.Section == section && IsVisible(tool, cache))
                .ToArray();

            if (visibleTools.Length == 0)
            {
                continue;
            }

            results.Add(new ToolSection
            {
                Section = section,
                Tools = visibleTools,
            });
        }

        return results;
    }
}
