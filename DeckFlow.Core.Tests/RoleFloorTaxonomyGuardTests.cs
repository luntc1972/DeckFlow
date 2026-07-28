using System.Reflection;
using System.Text;
using DeckFlow.CLI;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Regression tests for the role-floor taxonomy guard.
/// </summary>
// Why: a failure here means the harness TargetRoles drifted from CutLabRoleAssigner.RoleKeys, or a
// probe stopped provoking the role it was built to cover; the fix is to update the harness or probe,
// not to weaken this test.
public sealed class RoleFloorTaxonomyGuardTests
{
    public static TheoryData<ManabaseMode> Modes
    {
        get
        {
            var data = new TheoryData<ManabaseMode>();
            foreach (ManabaseMode mode in Enum.GetValues<ManabaseMode>())
            {
                data.Add(mode);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ValidateTaxonomyAgainstAssigner_AllShippedModes_ReturnsNull(ManabaseMode mode)
    {
        string? result = RoleFloorResearchCommandRunner.ValidateTaxonomyAgainstAssigner(mode);

        Assert.Null(result);
    }

    [Fact]
    public void AppendBlock_NormalizesWindowsLineEndings_WithoutDoubleCarriageReturns()
    {
        var builder = new StringBuilder();
        MethodInfo appendBlock = typeof(RoleFloorResearchCommandRunner).GetMethod("AppendBlock", BindingFlags.NonPublic | BindingFlags.Static)!;

        appendBlock.Invoke(null, new object[] { builder, "### Heading\r\nLine one\r\nLine two\r\n" });

        string emitted = builder.ToString();
        Assert.DoesNotContain("\r\r", emitted, StringComparison.Ordinal);
        Assert.Equal($"### Heading{Environment.NewLine}Line one{Environment.NewLine}Line two{Environment.NewLine}", emitted);
    }

    [Fact]
    public void BuildProtectionUnderDetectionPointer_NormalizesEmbeddedNoticeLineEndings()
    {
        MethodInfo buildPointer = typeof(RoleFloorResearchCommandRunner).GetMethod("BuildProtectionUnderDetectionPointer", BindingFlags.NonPublic | BindingFlags.Static)!;

        string pointer = (string)buildPointer.Invoke(null, Array.Empty<object>())!;

        Assert.DoesNotContain('\r', pointer);
    }
}
