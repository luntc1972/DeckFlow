using DeckFlow.Web.Services.Tools;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class AiPlatformCopyTests
{
    [Theory]
    [InlineData(true, "ChatGPT, Claude, or Gemini")]
    [InlineData(false, "ChatGPT or Claude")]
    public void PlatformList_ReturnsExpectedCopy(bool geminiEnabled, string expected)
    {
        Assert.Equal(expected, AiPlatformCopy.PlatformList(geminiEnabled));
    }

    [Theory]
    [InlineData(true, "ChatGPT / Claude / Gemini")]
    [InlineData(false, "ChatGPT / Claude")]
    public void PlatformSlashList_ReturnsExpectedCopy(bool geminiEnabled, string expected)
    {
        Assert.Equal(expected, AiPlatformCopy.PlatformSlashList(geminiEnabled));
    }
}
