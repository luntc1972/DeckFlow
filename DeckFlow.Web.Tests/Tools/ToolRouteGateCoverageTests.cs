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
    private static readonly Type[] ToolControllerTypes =
    [
        typeof(DeckPacketController),
        typeof(DeckLookupController),
        typeof(DeckPrimerController),
        typeof(DeckSyncController),
        typeof(DeckConvertController),
        typeof(JudgeQuestionsController),
        typeof(CommanderController),
        typeof(DeckCategoriesController),
        typeof(ManabaseController),
        typeof(ContentKbController),
    ];

    [Fact]
    public void Every_tool_has_at_least_one_gated_action()
    {
        var toolActions = GetGatedToolActions();

        foreach (var tool in new ToolRegistry().All)
        {
            Assert.Contains(
                toolActions,
                candidate => StringComparer.Ordinal.Equals(candidate.Tool.Key, tool.Key));
        }
    }

    [Fact]
    public void Every_tool_route_action_uses_the_matching_feature_flag_gate()
    {
        var failures = GetToolActions()
            .Select(action => ValidateGate(action.Tool, action.Method, action.Path))
            .Where(failure => failure is not null)
            .ToArray();

        Assert.True(
            failures.Length == 0,
            string.Join(Environment.NewLine, failures!));
    }

    private static IReadOnlyList<ToolAction> GetGatedToolActions() =>
        GetToolActions()
            .Where(action => action.Method.GetCustomAttribute<FeatureFlagGateAttribute>() is not null)
            .ToArray();

    private static IReadOnlyList<ToolAction> GetToolActions()
    {
        var tools = new ToolRegistry().All
            .OrderByDescending(tool => tool.Route.Length)
            .ToArray();
        var controllerToolRouteMap = BuildControllerToolRouteMap(tools);
        var results = new List<ToolAction>();

        foreach (var method in GetActionMethods())
        {
            var path = GetEffectiveRoutePath(method);
            var tool = MatchTool(method.DeclaringType!, path, tools, controllerToolRouteMap);
            if (tool is null)
            {
                continue;
            }

            results.Add(new ToolAction(tool, method, path));
        }

        return results;
    }

    private static Dictionary<Type, ToolDefinition> BuildControllerToolRouteMap(IReadOnlyList<ToolDefinition> tools)
    {
        var controllerToolRouteMap = new Dictionary<Type, ToolDefinition>();

        foreach (var controllerType in ToolControllerTypes)
        {
            var matchingTools = GetActionMethods(controllerType)
                .Select(GetEffectiveRoutePath)
                .Select(path => FindLongestPrefixMatch(path, tools))
                .Where(tool => tool is not null)
                .DistinctBy(tool => tool!.Key)
                .Cast<ToolDefinition>()
                .ToArray();

            if (matchingTools.Length == 1)
            {
                controllerToolRouteMap[controllerType] = matchingTools[0];
            }
        }

        return controllerToolRouteMap;
    }

    private static IEnumerable<MethodInfo> GetActionMethods()
    {
        foreach (var controllerType in ToolControllerTypes)
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

    private static ToolDefinition? MatchTool(
        Type controllerType,
        string path,
        IReadOnlyList<ToolDefinition> tools,
        IReadOnlyDictionary<Type, ToolDefinition> controllerToolRouteMap)
    {
        var matchedTool = FindLongestPrefixMatch(path, tools);
        if (matchedTool is not null)
        {
            return matchedTool;
        }

        if (controllerToolRouteMap.TryGetValue(controllerType, out var controllerTool))
        {
            return controllerTool;
        }

        return null;
    }

    private static ToolDefinition? FindLongestPrefixMatch(string path, IReadOnlyList<ToolDefinition> tools) =>
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

    private sealed record ToolAction(ToolDefinition Tool, MethodInfo Method, string Path);
}
