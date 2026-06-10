using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckAnalysisRequestTests
{
    [Fact]
    public void TargetAiPlatform_setter_normalizes_unknown_values_to_default()
    {
        var request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "Unknown"
        };

        Assert.Equal("ChatGPT", request.TargetAiPlatform);
    }
}
