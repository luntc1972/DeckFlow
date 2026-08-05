using System.Linq;
using System.Reflection;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Locks the help kill-switch onto every <see cref="HelpController"/> action. Help is not a
/// <c>ToolRegistry</c> entry, so <c>ToolRouteGateCoverageTests</c> never sees it; the index
/// carried <c>tool.help.enabled</c> while <c>/help/{slug}</c> relied only on each topic's own
/// <c>RequiresFlag</c>, leaving every ungated topic live with the switch off. Gate behaviour
/// itself (404 when disabled) is covered by <see cref="FeatureFlagGateAttributeTests"/>.
/// </summary>
public sealed class HelpControllerFlagGateTests
{
    private const string HelpFlagKey = "tool.help.enabled";

    [Fact]
    public void Every_help_action_is_gated_by_the_help_flag()
    {
        var actions = GetHelpActions();

        // No hardcoded action-name list: ToolRouteGateCoverageTests' sibling rule already fails
        // when a new help action arrives ungated. What only this test pins is the key itself,
        // since the sibling rule accepts any key.
        Assert.NotEmpty(actions);

        foreach (var method in actions)
        {
            var gate = method.GetCustomAttribute<FeatureFlagGateAttribute>();

            Assert.NotNull(gate);
            Assert.Equal(HelpFlagKey, gate!.Key);
        }
    }

    private static MethodInfo[] GetHelpActions() =>
        typeof(HelpController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();
}
