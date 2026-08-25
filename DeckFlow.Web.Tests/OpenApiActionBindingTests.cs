using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards MVC action metadata used by Swashbuckle when generating the OpenAPI document.
/// </summary>
public sealed class OpenApiActionBindingTests
{
    [Fact]
    public void Every_routed_public_controller_action_has_an_http_method_or_is_hidden_from_api_explorer()
    {
        var offenders = typeof(DeckFlow.Web.Controllers.ShellController)
            .Assembly
            .GetTypes()
            .Where(static type =>
                !type.IsAbstract &&
                typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(GetOffendingActions)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Actions with route metadata but no HTTP method must be hidden from ApiExplorer: {string.Join(", ", offenders)}");
    }

    private static IEnumerable<string> GetOffendingActions(Type controllerType)
    {
        var controllerIgnored = controllerType
            .GetCustomAttribute<ApiExplorerSettingsAttribute>()?
            .IgnoreApi == true;

        return controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(static method =>
                method.DeclaringType is not null &&
                !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes(inherit: true)
                .OfType<IRouteTemplateProvider>()
                .Any())
            .Where(method => !method.GetCustomAttributes(inherit: true)
                .OfType<IActionHttpMethodProvider>()
                .Any(provider => provider.HttpMethods is not null &&
                    provider.HttpMethods.Any(static httpMethod => !string.IsNullOrWhiteSpace(httpMethod))))
            .Where(method =>
                !controllerIgnored &&
                method.GetCustomAttribute<ApiExplorerSettingsAttribute>()?.IgnoreApi != true)
            .Select(method => $"{controllerType.Name}.{method.Name}");
    }
}
