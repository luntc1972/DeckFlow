using Microsoft.AspNetCore.Builder;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// Application builder extensions for client-abort handling.
/// </summary>
public static class ClientAbortApplicationBuilderExtensions
{
    /// <summary>
    /// Adds client-abort handling to the request pipeline.
    /// </summary>
    /// <param name="app">Application builder to configure.</param>
    /// <returns>The same application builder so middleware registration can continue.</returns>
    public static IApplicationBuilder UseClientAbortHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ClientAbortMiddleware>();
    }
}
