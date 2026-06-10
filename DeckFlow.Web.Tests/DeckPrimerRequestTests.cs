using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for <see cref="DeckPrimerRequest"/> setter normalization and null guards.
/// </summary>
public sealed class DeckPrimerRequestTests
{
    [Fact]
    public void TargetAiPlatform_Setter_NormalizesUnknownToDefault()
    {
        var request = new DeckPrimerRequest
        {
            TargetAiPlatform = "bogus"
        };

        Assert.Equal(AiPlatform.Default.Key, request.TargetAiPlatform);
    }

    [Fact]
    public void SelectedSectionIds_NullSet_BecomesEmptyList()
    {
        var request = new DeckPrimerRequest();

        request.SelectedSectionIds = null!;

        Assert.NotNull(request.SelectedSectionIds);
        Assert.Empty(request.SelectedSectionIds);
    }
}
