using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// Reusable per-action page kill-switch (Phase 6, FLAG-05, D-17 + D-18). Applied via
/// <c>[FeatureFlagGate("page.help.enabled", Title = "Help center temporarily unavailable",
/// Message = "Help is offline for maintenance.")]</c> on any controller action. When the
/// referenced flag is off in <see cref="IFeatureFlagCache"/>, the action is short-circuited
/// and the response becomes HTTP 503 + Retry-After: 300 + a render of
/// <c>Views/Shared/_MaintenancePage.cshtml</c> bound to the operator-supplied Title/Message.
///
/// Because attribute ctors only accept compile-time constants, the cache is resolved per
/// invocation from <see cref="HttpContext.RequestServices"/> — guarantees the latest snapshot
/// is consulted, not a stale constructor-captured one (T-06-G3 mitigation).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class FeatureFlagGateAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>Dotted-namespace flag key (D-08), e.g. "page.help.enabled".</summary>
    public string Key { get; }

    /// <summary>Title rendered in the 503 page H1. Defaults to a generic copy.</summary>
    public string Title { get; init; } = "Temporarily unavailable";

    /// <summary>Body copy rendered in the 503 page paragraph. Defaults to a generic copy.</summary>
    public string Message { get; init; } = "This page is offline for maintenance. Please try again shortly.";

    /// <summary>Optional primary action label rendered on the 503 page.</summary>
    public string? PrimaryActionLabel { get; init; }

    /// <summary>Optional primary action URL rendered on the 503 page.</summary>
    public string? PrimaryActionUrl { get; init; }

    /// <summary>
    /// Constructs the gate with a required flag key. Throws <see cref="ArgumentException"/>
    /// if <paramref name="key"/> is null, empty, or whitespace.
    /// </summary>
    /// <param name="key">Dotted-namespace flag key (e.g. "page.help.enabled").</param>
    public FeatureFlagGateAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>
    /// Resolves <see cref="IFeatureFlagCache"/> from request services and either calls
    /// <paramref name="next"/> (flag on) or short-circuits with 503 + maintenance view (flag off).
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cache = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagCache>();
        if (cache.IsEnabled(Key))
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Flag off: short-circuit with 503 + maintenance view (D-17).
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        response.Headers["Retry-After"] = "300"; // 5 minutes — discourages tight-loop polling, recovers quickly on toggle (T-06-G2).

        var vm = new MaintenanceViewModel
        {
            Title = Title,
            Message = Message,
            PrimaryActionLabel = PrimaryActionLabel,
            PrimaryActionUrl = PrimaryActionUrl,
        };
        context.Result = new ViewResult
        {
            ViewName = "_MaintenancePage",
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = vm,
            },
        };
    }
}
