using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ShellControllerRouteTests
{
    [Fact]
    public void Home_has_get_and_head_root_routes()
    {
        var attributes = typeof(DeckFlow.Web.Controllers.ShellController)
            .GetMethod(nameof(DeckFlow.Web.Controllers.ShellController.Home))!
            .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .ToArray();

        Assert.Contains(attributes, static attribute =>
            attribute.HttpMethods.Contains("GET") && attribute.Template == "/");
        Assert.Contains(attributes, static attribute =>
            attribute.HttpMethods.Contains("HEAD") && attribute.Template == "/");
    }
}
