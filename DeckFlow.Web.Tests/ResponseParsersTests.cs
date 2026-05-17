using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for <see cref="ResponseParsers"/> covering deck-analysis and set-upgrade JSON
/// parsing with both bare and wrapped payload shapes, and error handling for empty / malformed input.
/// </summary>
public sealed class ResponseParsersTests
{
    [Fact]
    public void ParseAnalysisResponse_ThrowsForNullOrWhitespaceInput()
    {
        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse(null!));
        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse(string.Empty));
        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse("   "));
    }

    [Fact]
    public void ParseAnalysisResponse_ThrowsForValidJsonWithoutDeckProfileShape()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse("""{"foo":1}"""));

        Assert.Contains("deck_profile", exception.Message);
    }

    [Fact]
    public void ParseAnalysisResponse_AcceptsBareDeckProfilePayload()
    {
        var response = ResponseParsers.ParseAnalysisResponse("""{"format":"commander","commander":"Atraxa"}""");

        Assert.Equal("commander", response.Format);
        Assert.Equal("Atraxa", response.Commander);
    }

    [Fact]
    public void ParseAnalysisResponse_AcceptsWrappedDeckProfilePayload()
    {
        var response = ResponseParsers.ParseAnalysisResponse("""{"deck_profile":{"format":"commander","commander":"Atraxa"}}""");

        Assert.Equal("commander", response.Format);
        Assert.Equal("Atraxa", response.Commander);
    }

    [Fact]
    public void ParseAnalysisResponse_ThrowsForRecognizableButEmptyDeckProfile()
    {
        var payload = """
            {
              "deck_profile": {
                "format": "",
                "commander": null,
                "game_plan": "",
                "primary_axes": [],
                "speed": "",
                "strengths": [],
                "weaknesses": [],
                "deck_needs": [],
                "weak_slots": [],
                "synergy_tags": [],
                "question_answers": [],
                "deck_versions": []
              }
            }
            """;

        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse(payload));
    }

    [Fact]
    public void ParseSetUpgradeResponse_ThrowsForNullOrWhitespaceInput()
    {
        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseSetUpgradeResponse(null!));
        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseSetUpgradeResponse(string.Empty));
        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseSetUpgradeResponse("   "));
    }

    [Fact]
    public void ParseSetUpgradeResponse_ThrowsForValidJsonWithoutSetUpgradeShape()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseSetUpgradeResponse("""{"foo":1}"""));

        Assert.Contains("set_upgrade_report", exception.Message);
    }

    [Fact]
    public void ParseSetUpgradeResponse_AcceptsBareSetUpgradePayload()
    {
        var response = ResponseParsers.ParseSetUpgradeResponse("""{"sets":[{"set_code":"duskmourn","set_name":"Duskmourn","top_adds":[{"card":"Atraxa's Fall"}]}]}""");

        var set = Assert.Single(response.Sets);
        Assert.Equal("duskmourn", set.SetCode);
        Assert.Equal("Duskmourn", set.SetName);
        var topAdd = Assert.Single(set.TopAdds);
        Assert.Equal("Atraxa's Fall", topAdd.Card);
    }

    [Fact]
    public void ParseSetUpgradeResponse_AcceptsWrappedSetUpgradePayload()
    {
        var response = ResponseParsers.ParseSetUpgradeResponse("""{"set_upgrade_report":{"sets":[{"set_code":"duskmourn","set_name":"Duskmourn","top_adds":[{"card":"Atraxa's Fall"}]}]}}""");

        var set = Assert.Single(response.Sets);
        Assert.Equal("duskmourn", set.SetCode);
        Assert.Equal("Duskmourn", set.SetName);
        Assert.Equal("Atraxa's Fall", Assert.Single(set.TopAdds).Card);
    }

    [Fact]
    public void ParseSetUpgradeResponse_ThrowsForRecognizableButEmptySetUpgradeReport()
    {
        var payload = """
            {
              "set_upgrade_report": {
                "sets": [],
                "final_shortlist": {
                  "must_test": [],
                  "optional": [],
                  "skip": []
                }
              }
            }
            """;

        Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseSetUpgradeResponse(payload));
    }

    [Fact]
    public void ParseSetUpgradeResponse_AcceptsMeaningfulFinalShortlistMustTestEntry()
    {
        var payload = """
            {
              "final_shortlist": {
                "must_test": [
                  {
                    "card": "Atraxa's Fall"
                  }
                ]
              }
            }
            """;

        var response = ResponseParsers.ParseSetUpgradeResponse(payload);

        Assert.NotNull(response.FinalShortlist);
        var shortlist = response.FinalShortlist!;
        var mustTest = Assert.Single(shortlist.MustTest);
        Assert.Equal("Atraxa's Fall", mustTest.Card);
    }
}
