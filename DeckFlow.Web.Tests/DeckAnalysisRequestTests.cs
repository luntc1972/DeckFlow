using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckAnalysisRequestTests
{
    [Fact]
    public void Selection_list_setters_treat_null_as_empty_lists()
    {
        var request = new DeckAnalysisRequest
        {
            PinnedVideoIds = null!,
            FollowedCreators = null!
        };

        Assert.Empty(request.PinnedVideoIds);
        Assert.Empty(request.FollowedCreators);
    }
}
