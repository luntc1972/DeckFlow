using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Registers <see cref="ContentKbOrchestrator"/> as one scoped concrete and forwards the Content KB facade plus
/// each orchestration slice interface to that same scoped instance. The host must register the required stores,
/// integration services, <see cref="Func{TResult}"/> UTC clock, and <see cref="ContentKbOrchestratorOptions"/>
/// carrying the host-resolved artifact root before calling this extension.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Content KB orchestration slice registrations backed by one scoped <see cref="ContentKbOrchestrator"/>.
    /// This method does not register stores, connection strings, paths, or options; those remain host responsibilities.
    /// </summary>
    /// <param name="services">Service collection to extend.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddContentKbOrchestrator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ContentKbOrchestrator>();
        services.AddScoped<IContentKbOrchestrator>(sp => sp.GetRequiredService<ContentKbOrchestrator>());
        services.AddScoped<IHarvestOrchestrator>(sp => sp.GetRequiredService<ContentKbOrchestrator>());
        services.AddScoped<IDistillOrchestrator>(sp => sp.GetRequiredService<ContentKbOrchestrator>());
        services.AddScoped<IContentMaintenanceOrchestrator>(sp => sp.GetRequiredService<ContentKbOrchestrator>());
        services.AddScoped<IContentSourceManager>(sp => sp.GetRequiredService<ContentKbOrchestrator>());
        services.AddScoped<IContentIndexExporter>(sp => sp.GetRequiredService<ContentKbOrchestrator>());

        return services;
    }
}
