using System.Collections.Generic;
using System.Threading.Tasks;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="FeatureFlagGateAttribute"/> covering gate behaviour for disabled flags,
/// 404 short-circuiting, and pass-through when flags are enabled.
/// </summary>
public sealed class FeatureFlagGateAttributeTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WhenFlagDisabled_ReturnsNotFound()
    {
        var attribute = new FeatureFlagGateAttribute("tool.categories.enabled");
        var context = CreateContext(new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.categories.enabled"] = false,
        }));
        var nextCalled = false;

        await attribute.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled);
        var result = Assert.IsType<NotFoundResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, context.HttpContext.Response.StatusCode);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenFlagEnabled_ContinuesPipeline()
    {
        var attribute = new FeatureFlagGateAttribute("tool.categories.enabled");
        var context = CreateContext(new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.categories.enabled"] = true,
        }));
        var nextCalled = false;

        await attribute.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    private static ActionExecutingContext CreateContext(IFeatureFlagCache cache)
    {
        var services = new ServiceCollection()
            .AddSingleton(cache)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }
}
