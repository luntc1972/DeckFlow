using System.Collections.Generic;
using System.Threading.Tasks;
using DeckFlow.Web.Controllers.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for the HIGH-4 / D-22E fix: <see cref="AdminFlagsController.Toggle"/> now rejects
/// cross-origin requests via <c>SameOriginRequestValidator</c> (in addition to its existing
/// anti-forgery token), so the reused flag toggle is double-CSRF-guarded.
/// </summary>
public sealed class AdminFlagsControllerToggleTests
{
    [Fact]
    public async Task Toggle_CrossOrigin_Returns403_AndDoesNotWrite()
    {
        var store = new FakeFeatureFlagStore();
        var controller = Build(store, crossOrigin: true);

        var result = await controller.Toggle("tool.knowledge-base.enabled", enabled: true, default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        Assert.Equal(0, store.SetCallCount);
    }

    [Fact]
    public async Task Toggle_SameOrigin_UnknownKey_ReturnsBadRequest_AndDoesNotWrite()
    {
        var store = new FakeFeatureFlagStore();
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Toggle("does.not.exist", enabled: true, default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, store.SetCallCount);
    }

    [Fact]
    public async Task Toggle_SameOrigin_KnownKey_Writes_AndRedirects()
    {
        var store = new FakeFeatureFlagStore();
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Toggle("tool.knowledge-base.enabled", enabled: true, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, store.SetCallCount);
        Assert.Equal("tool.knowledge-base.enabled", store.LastSetKey);
        Assert.True(store.LastSetEnabled);
    }

    private static AdminFlagsController Build(FakeFeatureFlagStore store, bool crossOrigin)
    {
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["tool.knowledge-base.enabled"] = false });
        var controller = new AdminFlagsController(store, cache);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
        return controller;
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
