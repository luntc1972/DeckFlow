using System.Reflection;
using DeckFlow.Core.Content;

namespace DeckFlow.Core.Tests.Content;

public sealed class ContentKbFeatureFlagKeysTests
{
    [Fact]
    public void DirectPushGitBody_IsTheCanonicalFlagName()
    {
        var keysType = typeof(ContentKbPaths).Assembly.GetType("DeckFlow.Core.Content.ContentKbFeatureFlagKeys");

        Assert.NotNull(keysType);

        var field = keysType!.GetField("DirectPushGitBody", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal("sync.directpush-gitbody", field!.GetRawConstantValue());
    }
}
