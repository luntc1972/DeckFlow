using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services.Analytics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the Phase 8 analytics subsystem. Mirrors
/// <see cref="HarvestServiceCollectionExtensions.AddDeckFlowHarvest"/>:
/// one extension call wires the buffer, store, flusher, salt accessor, and
/// scoped middleware — all with the D-14 lazy-IServiceProvider pattern to
/// prevent circular singleton DI cycles (Phase 7.1 dc66a38 errata).
/// </summary>
public static class AnalyticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phase 8 analytics services.
    /// <list type="bullet">
    /// <item><description><see cref="RequestMetricsBuffer"/> — singleton, no DI dependencies (D-08).</description></item>
    /// <item><description><see cref="IRequestMetricsStore"/> / <see cref="RequestMetricsStore"/> — singleton with lazy <see cref="IServiceProvider"/> per D-14.</description></item>
    /// <item><description><see cref="RequestMetricsFlusher"/> — registered as both singleton and <c>IHostedService</c>, mirroring <c>ArchidektCacheJobService</c>.</description></item>
    /// <item><description><see cref="AnalyticsSaltAccessor"/> — singleton; populated once at startup before the middleware handles any requests.</description></item>
    /// <item><description><see cref="AnalyticsMiddleware"/> — scoped (required by the <c>IMiddleware</c> contract).</description></item>
    /// </list>
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="env">Web host environment passed to the store's DI ctor.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowAnalytics(
        this IServiceCollection services,
        IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(env);

        // Buffer: singleton, no DI dependencies on the hot path (D-08).
        services.AddSingleton<RequestMetricsBuffer>();

        // Store: singleton with lazy IServiceProvider per D-14 (Phase 7.1 errata pattern).
        // Do NOT inject IRequestMetricsStore anywhere via constructor — use CreateScope().
        services.AddSingleton<IRequestMetricsStore>(sp => new RequestMetricsStore(
            sp.GetRequiredService<IWebHostEnvironment>(),
            sp));

        // Flusher: singleton + hosted service (mirrors HarvestScheduleCache pattern).
        // Takes IServiceProvider, not IRequestMetricsStore directly, so the DI graph has no cycle.
        services.AddSingleton<RequestMetricsFlusher>();
        services.AddHostedService(sp => sp.GetRequiredService<RequestMetricsFlusher>());

        // Salt accessor: singleton string holder populated at startup by Program.cs.
        // Middleware reads Salt on the hot path with no DB I/O (D-13 / ANLY-03).
        services.AddSingleton<AnalyticsSaltAccessor>();

        // Middleware: scoped because IMiddleware requires it (UseMiddleware<T> resolves per request).
        services.AddScoped<AnalyticsMiddleware>();

        return services;
    }
}

/// <summary>
/// Holds the resolved IP-hash salt for the analytics middleware. Populated once at startup
/// by <c>Program.cs</c> before requests are served, so the middleware never pays DB I/O on
/// the hot path (D-13 / ANLY-03). Volatile read/write prevents torn reads under the
/// unlikely cold-start race between salt population and the very first request.
/// </summary>
public sealed class AnalyticsSaltAccessor
{
    private string? _salt;

    /// <summary>Gets the resolved salt, or <c>null</c> if startup has not populated it yet.</summary>
    public string? Salt => System.Threading.Volatile.Read(ref _salt);

    /// <summary>
    /// Sets the salt. Called once at startup; subsequent calls are no-ops in practice.
    /// </summary>
    /// <param name="salt">The resolved salt string.</param>
    public void SetSalt(string salt)
    {
        ArgumentNullException.ThrowIfNull(salt);
        System.Threading.Volatile.Write(ref _salt, salt);
    }
}
