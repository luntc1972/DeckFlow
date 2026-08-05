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

    /// <summary>
    /// Sibling rule: a controller that gates any action must gate all of them. Keys may differ —
    /// DeckLookupController and DeckPacketController legitimately mix several.
    ///
    /// This exists because the registry-join tests above can only catch holes on routes some
    /// ToolRegistry row already claims, and the real hole was the opposite shape: HelpController
    /// gated Index and left Topic open, so /help 404'd with the flag off while /help/{slug}
    /// stayed live. Help has no registry row, so nothing looked at it. This rule needs no
    /// declaration to work — the gated sibling is the declaration.
    /// </summary>
    [Fact]
    public void A_controller_that_gates_any_action_gates_every_action()
    {
        var failures = typeof(DeckPacketController).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .Where(static type => !MixedGatingByDesign.Contains(type.Name, StringComparer.Ordinal))
            .Select(static type => new
            {
                Type = type,
                Actions = GetActionMethods(type),
            })
            .Where(static candidate => candidate.Actions.Any(HasGate))
            .SelectMany(static candidate => candidate.Actions
                .Where(static action => !HasGate(action))
                .Select(action =>
                    $"{candidate.Type.Name}.{action.Name} has no [FeatureFlagGate] while a sibling action on the same controller does."))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every gate key belongs to a tool, or is explicitly allow-listed. An orphan key is a gate
    /// nothing else in the system knows about: no sitemap join, no admin tool row, no coverage
    /// test. tool.help.enabled was exactly that, which is why its half-applied gate survived.
    /// </summary>
    [Fact]
    public void Every_gate_key_is_owned_by_a_tool_or_explicitly_allow_listed()
    {
        var toolFlagKeys = new ToolRegistry().All.Select(static tool => tool.FlagKey);
        var known = new HashSet<string>(toolFlagKeys.Concat(NonToolGateKeys), StringComparer.Ordinal);

        var orphans = typeof(DeckPacketController).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(static type => GetActionMethods(type))
            .Select(static method => method.GetCustomAttribute<FeatureFlagGateAttribute>()?.Key)
            .Where(key => key is not null && !known.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"Gate keys owned by no tool and not allow-listed: {string.Join(", ", orphans)}");
    }

    /// <summary>
    /// Gate keys deliberately not backed by a <see cref="ToolRegistry"/> row. Help is a hub page,
    /// not a deck tool: it has no tile, no nav tab and no <c>DeckPageTab</c>, so a registry row
    /// would force presentation facts it does not have.
    /// </summary>
    private static readonly string[] NonToolGateKeys = ["tool.help.enabled"];

    /// <summary>
    /// Controllers allowed to mix gated and ungated actions. Add a name here only with a stated
    /// reason (an unauthenticated callback or health probe alongside gated actions would qualify).
    /// Empty is the expected state — no controller has needed an exemption.
    /// </summary>
    private static readonly string[] MixedGatingByDesign = [];

    private static bool HasGate(MethodInfo method) =>
        method.GetCustomAttribute<FeatureFlagGateAttribute>() is not null;

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
