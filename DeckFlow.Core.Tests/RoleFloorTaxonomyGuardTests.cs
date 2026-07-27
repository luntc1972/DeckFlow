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
}
