using DeckFlow.Web.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the Phase 66 tool registry.
/// </summary>
public static class ToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the canonical tool registry.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowTools(this IServiceCollection services)
    {
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        return services;
    }
}
