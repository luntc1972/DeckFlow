using DeckFlow.Web.Services.CutLab;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for Cut Lab services.
/// </summary>
public static class CutLabServiceCollectionExtensions
{
    /// <summary>
    /// Registers the process-wide Cut Lab cache services.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowCutLabServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CutLabResolvedCardCache>();
        services.AddSingleton<CutLabDeltaCache>();

        return services;
    }
}
