using System.Reflection;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DeckFlow.Web.Tests.Tools;

/// <summary>
/// Proves that every controller action under a registered tool route carries the matching feature-flag gate.
/// </summary>
public sealed class ToolRouteGateCoverageTests
{
    [Fact]
    public void Every_tool_has_at_least_one_gated_action()
    {
        var toolActions = GetGatedToolActions();

        foreach (var tool in new ToolRegistry().All)
        {
            Assert.Contains(
                toolActions,
                candidate => candidate.Tool is not null && StringComparer.Ordinal.Equals(candidate.Tool.Key, tool.Key));
        }
    }

    [Fact]
    public void Every_tool_route_action_uses_the_matching_feature_flag_gate()
    {
        var failures = GetToolActions()
            .Select(action => action.Failure ?? ValidateGate(action.Tool!, action.Method, action.Path))
            .Where(failure => failure is not null)
            .ToArray();

        Assert.True(
            failures.Length == 0,
            string.Join(Environment.NewLine, failures!));
    }

    private static IReadOnlyList<ToolAction> GetGatedToolActions() =>
        GetToolActions()
            .Where(action => action.Tool is not null && action.Method.GetCustomAttribute<FeatureFlagGateAttribute>() is not null)
            .ToArray();

    private static IReadOnlyList<ToolAction> GetToolActions()
    {
        var trackedRoutes = GetTrackedRoutes()
            .OrderByDescending(candidate => candidate.Route.Length)
            .ToArray();
        var results = new List<ToolAction>();

        foreach (var method in GetTrackedControllerActionMethods(trackedRoutes))
        {
            var path = GetEffectiveRoutePath(method);
            var tool = FindLongestPrefixMatch(path, trackedRoutes);
            if (tool is null)
            {
                results.Add(new ToolAction(
                    null,
                    method,
                    path,
                    $"{method.DeclaringType!.Name}.{method.Name} ({path}) is on a tracked tool controller but does not match any registered tool route."));
                continue;
            }

            results.Add(new ToolAction(tool.Tool, method, path, null));
        }

        return results;
    }

    private static IEnumerable<TrackedRoute> GetTrackedRoutes()
    {
        foreach (var tool in new ToolRegistry().All)
        {
            yield return new TrackedRoute(tool, tool.Route);
            foreach (var route in tool.AdditionalRoutes)
            {
                yield return new TrackedRoute(tool, route);
            }
        }
    }

    private static IEnumerable<MethodInfo> GetTrackedControllerActionMethods(IReadOnlyList<TrackedRoute> trackedRoutes)
    {
        var trackedControllerTypes = typeof(DeckPacketController).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => GetActionMethods(type)
                .Select(GetEffectiveRoutePath)
                .Any(path => FindLongestPrefixMatch(path, trackedRoutes) is not null))
            .ToArray();

        foreach (var controllerType in trackedControllerTypes)
        {
            foreach (var method in GetActionMethods(controllerType))
            {
                yield return method;
            }
        }
    }

    private static MethodInfo[] GetActionMethods(Type controllerType) =>
        controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

    private static string GetEffectiveRoutePath(MethodInfo method)
    {
        var controllerRoute = method.DeclaringType!
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .FirstOrDefault(template => !string.IsNullOrWhiteSpace(template));
        var httpMethodRoute = method
            .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .FirstOrDefault(template => template is not null)
            ?? string.Empty;

        return NormalizeRoutePath(controllerRoute, httpMethodRoute);
    }

    private static TrackedRoute? FindLongestPrefixMatch(string path, IReadOnlyList<TrackedRoute> tools) =>
        tools.FirstOrDefault(tool =>
            StringComparer.Ordinal.Equals(path, tool.Route)
            || path.StartsWith(tool.Route + "/", StringComparison.Ordinal));

    private static string? ValidateGate(ToolDefinition tool, MethodInfo method, string path)
    {
        var gate = method.GetCustomAttribute<FeatureFlagGateAttribute>();
        if (gate is null)
        {
            return $"{method.DeclaringType!.Name}.{method.Name} ({path}) is missing [FeatureFlagGate(\"{tool.FlagKey}\")].";
        }

        if (!StringComparer.Ordinal.Equals(gate.Key, tool.FlagKey))
        {
            return $"{method.DeclaringType!.Name}.{method.Name} ({path}) uses gate key \"{gate.Key}\" instead of \"{tool.FlagKey}\".";
        }

        return null;
    }

    private static string NormalizeRoutePath(string? controllerRoute, string? methodRoute)
    {
        if (!string.IsNullOrWhiteSpace(methodRoute) && methodRoute![0] == '/')
        {
            return methodRoute;
        }

        var controllerPart = NormalizeRouteFragment(controllerRoute);
        var methodPart = NormalizeRouteFragment(methodRoute);
        if (string.IsNullOrEmpty(controllerPart) && string.IsNullOrEmpty(methodPart))
        {
            return "/";
        }

        if (string.IsNullOrEmpty(controllerPart))
        {
            return "/" + methodPart;
        }

        if (string.IsNullOrEmpty(methodPart))
        {
            return "/" + controllerPart;
        }

        return "/" + controllerPart + "/" + methodPart;
    }

    private static string NormalizeRouteFragment(string? routeFragment) =>
        string.IsNullOrWhiteSpace(routeFragment)
            ? string.Empty
            : routeFragment.Trim().Trim('/');

    private sealed record TrackedRoute(ToolDefinition Tool, string Route);

    private sealed record ToolAction(ToolDefinition? Tool, MethodInfo Method, string Path, string? Failure);
}
