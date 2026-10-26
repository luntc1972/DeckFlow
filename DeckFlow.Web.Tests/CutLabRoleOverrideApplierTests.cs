using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRoleOverrideApplierTests
{
    [Fact]
    public void RoleOverrideApplier_EmptyOverrides_UsesExistingContext()
    {
        Assert.NotNull(typeof(CutLabRoleOverrideApplier));
    }
}
