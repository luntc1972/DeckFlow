using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// Reusable per-action page kill-switch (Phase 6, FLAG-05, D-17 + D-18). Applied via
/// <c>[FeatureFlagGate("tool.help.enabled")]</c> on any controller action. When the referenced
/// flag is off in <see cref="IFeatureFlagCache"/>, the action is short-circuited and the
/// response becomes HTTP 404 Not Found.
///
/// Because attribute ctors only accept compile-time constants, the cache is resolved per
/// invocation from <see cref="HttpContext.RequestServices"/> — guarantees the latest snapshot
/// is consulted, not a stale constructor-captured one (T-06-G3 mitigation).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class FeatureFlagGateAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>Dotted-namespace flag key (D-08), e.g. "tool.help.enabled".</summary>
    public string Key { get; }

    /// <summary>
    /// Constructs the gate with a required flag key. Throws <see cref="ArgumentException"/>
    /// if <paramref name="key"/> is null, empty, or whitespace.
    /// </summary>
    /// <param name="key">Dotted-namespace flag key (e.g. "tool.help.enabled").</param>
    public FeatureFlagGateAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>
    /// Resolves <see cref="IFeatureFlagCache"/> from request services and either calls
    /// <paramref name="next"/> (flag on) or short-circuits with 404 Not Found (flag off).
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cache = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagCache>();
        if (cache.IsEnabled(Key))
        {
            await next().ConfigureAwait(false);
            return;
        }

        context.Result = new NotFoundResult();
    }
}
