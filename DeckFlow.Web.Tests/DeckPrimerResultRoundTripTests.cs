using System.Text.Json;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards JSON round-tripping for deck-primer packet result records.
/// </summary>
public sealed class DeckPrimerResultRoundTripTests
{
    [Fact]
    public void DeckPrimerPacketResult_JsonRoundTrip_PreservesAllProperties()
    {
        var result = new DeckPrimerPacketResult(
            InputSummary: "input summary",
            SuggestedChatTitle: "Kinnan | Deck Primer",
            RequestContextText: "target_ai_platform: ChatGPT",
            PromptTextsByPlatform: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ChatGPT"] = "chatgpt prompt",
                ["Claude"] = "claude prompt",
                ["Gemini"] = "gemini prompt"
            },
            TimingSummary: "Total: 12 ms",
            ImportWarning: "warning",
            ResolvedCommanderName: "Kinnan, Bonder Prodigy",
            DecklistText: "1 Kinnan, Bonder Prodigy");

        var json = JsonSerializer.Serialize(result);
        var roundTripped = JsonSerializer.Deserialize<DeckPrimerPacketResult>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(result.InputSummary, roundTripped.InputSummary);
        Assert.Equal(result.SuggestedChatTitle, roundTripped.SuggestedChatTitle);
        Assert.Equal(result.RequestContextText, roundTripped.RequestContextText);
        Assert.Equal(result.TimingSummary, roundTripped.TimingSummary);
        Assert.Equal(result.ImportWarning, roundTripped.ImportWarning);
        Assert.Equal(result.ResolvedCommanderName, roundTripped.ResolvedCommanderName);
        Assert.Equal(result.DecklistText, roundTripped.DecklistText);
        Assert.Equal(result.PromptTextsByPlatform.Count, roundTripped.PromptTextsByPlatform.Count);
        Assert.Equal("chatgpt prompt", roundTripped.PromptTextsByPlatform["ChatGPT"]);
        Assert.Equal("claude prompt", roundTripped.PromptTextsByPlatform["Claude"]);
        Assert.Equal("gemini prompt", roundTripped.PromptTextsByPlatform["Gemini"]);
    }
}
