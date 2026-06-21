using System.Linq;
using System.Reflection;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Locks the feature-flag wiring on <see cref="ManabaseController"/>. The Mana Base tool is
/// gated by the <c>feature.manabase.enabled</c> kill-switch so operators can hide it from the
/// admin flags console; these reflection tests guard against the attribute being dropped or
/// the flag key drifting. Gate behaviour itself (503 + maintenance page) is covered by
/// <see cref="FeatureFlagGateAttributeTests"/>.
/// </summary>
public sealed class ManabaseControllerFlagGateTests
{
    private const string ManabaseFlagKey = "feature.manabase.enabled";

    [Fact]
    public void Every_manabase_action_is_gated_by_the_manabase_flag()
    {
        var actions = GetManabaseActions();

        // GET form + POST analyze — both must carry the gate so the page and the
        // submit handler are killed together when the flag is off.
        Assert.Equal(2, actions.Length);

        foreach (var method in actions)
        {
            var gate = method.GetCustomAttribute<FeatureFlagGateAttribute>();

            Assert.NotNull(gate);
            Assert.Equal(ManabaseFlagKey, gate!.Key);
        }
    }

    private static MethodInfo[] GetManabaseActions() =>
        typeof(ManabaseController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == nameof(ManabaseController.Manabase))
            .ToArray();
}
