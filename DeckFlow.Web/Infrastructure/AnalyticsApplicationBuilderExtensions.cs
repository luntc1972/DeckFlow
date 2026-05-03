using Microsoft.AspNetCore.Builder;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// Application builder extension for the Phase 8 analytics middleware.
/// </summary>
public static class AnalyticsApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="AnalyticsMiddleware"/> to the request pipeline.
    /// </summary>
    /// <remarks>
    /// Per D-12 placement rule: call this AFTER <c>app.UseRouting()</c> (so the endpoint
    /// is resolved and <c>HttpContext.GetEndpoint()?.DisplayName</c> is populated) and
    /// BEFORE <c>app.MapControllers()</c>.
    /// </remarks>
    /// <param name="app">Application builder to configure.</param>
    /// <returns>The same application builder so middleware registration can continue.</returns>
    public static IApplicationBuilder UseAnalyticsMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<AnalyticsMiddleware>();
    }
}
