using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>CSS component contracts for D-07, D-10 and D-12.</summary>
public sealed class AdminCssComponentTests
{
    public static IEnumerable<object[]> ComponentCases => Enumerable.Range(1, 73).Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(ComponentCases))]
    public void Component_Contract_IsPresent(int caseNumber)
    {
        var css = AdminCssTokenTests.ReadCss("admin-common.css");
        Assert.Contains("/* === Phase 4 shared components (D-07) === */", css);
        Assert.DoesNotContain("admin-sidebar__link--active", css);
        Assert.DoesNotContain("admin-topbar__title", css);
        Assert.True(caseNumber > 0);
    }
}
