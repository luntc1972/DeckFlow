using System.Collections.Generic;
using System.Threading.Tasks;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models.Admin;
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
/// maintenance-page redirect, and pass-through when flags are enabled.
/// </summary>
public sealed class FeatureFlagGateAttributeTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WhenFlagDisabled_ReturnsMaintenancePageWithAction()
    {
        var attribute = new FeatureFlagGateAttribute("feature.categories.enabled")
        {
            Title = "Category suggestions temporarily unavailable",
            Message = "Category Suggestions is offline for maintenance. Category Reference remains available.",
            PrimaryActionLabel = "Open Category Reference",
            PrimaryActionUrl = "/commander-categories",
        };
        var context = CreateContext(new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["feature.categories.enabled"] = false,
        }));
        var nextCalled = false;

        await attribute.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.HttpContext.Response.StatusCode);
        Assert.Equal("300", context.HttpContext.Response.Headers["Retry-After"]);
        var view = Assert.IsType<ViewResult>(context.Result);
        Assert.Equal("_MaintenancePage", view.ViewName);
        var model = Assert.IsType<MaintenanceViewModel>(view.Model);
        Assert.Equal("Category suggestions temporarily unavailable", model.Title);
        Assert.Equal("Category Suggestions is offline for maintenance. Category Reference remains available.", model.Message);
        Assert.Equal("Open Category Reference", model.PrimaryActionLabel);
        Assert.Equal("/commander-categories", model.PrimaryActionUrl);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenFlagEnabled_ContinuesPipeline()
    {
        var attribute = new FeatureFlagGateAttribute("feature.categories.enabled");
        var context = CreateContext(new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["feature.categories.enabled"] = true,
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
