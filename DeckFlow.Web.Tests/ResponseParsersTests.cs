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
    public void ParseAnalysisResponse_PopulatesWinTurnFieldsWhenPresent()
    {
        var payload = """
            {
              "deck_profile": {
                "format": "commander",
                "commander": "Atraxa",
                "estimated_win_turn": 4,
                "can_answer_win_turn": true,
                "assessed_bracket": "Bracket 4: Optimized",
                "bracket_justification": "Wins around turn 4 with protected combo."
              }
            }
            """;

        var response = ResponseParsers.ParseAnalysisResponse(payload);

        Assert.Equal(4, response.EstimatedWinTurn);
        Assert.True(response.CanAnswerWinTurn);
        Assert.Equal("Bracket 4: Optimized", response.AssessedBracket);
        Assert.Equal("Wins around turn 4 with protected combo.", response.BracketJustification);
    }

    [Fact]
    public void ParseAnalysisResponse_LegacyDeckProfileDefaultsWinTurnFields()
    {
        var response = ResponseParsers.ParseAnalysisResponse("""{"deck_profile":{"format":"commander","commander":"Atraxa"}}""");

        Assert.Equal(0, response.EstimatedWinTurn);
        Assert.False(response.CanAnswerWinTurn);
        Assert.Equal(string.Empty, response.AssessedBracket);
        Assert.Equal(string.Empty, response.BracketJustification);
    }

    [Fact]
    public void ParseAnalysisResponse_AcceptsAssessedBracketOnlyDeckProfile()
    {
        var response = ResponseParsers.ParseAnalysisResponse("""{"deck_profile":{"assessed_bracket":"Bracket 3: Upgraded"}}""");

        Assert.Equal("Bracket 3: Upgraded", response.AssessedBracket);
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
                "estimated_win_turn": 0,
                "can_answer_win_turn": false,
                "assessed_bracket": "",
                "bracket_justification": "",
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

    [Fact]
    public void ParseAnalysisResponse_TruncatedInput_ThrowsLockedMessage()
    {
        var truncated = """{"deck_profile":{"format":"commander","commander":"Plagon""";
        var exception = Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse(truncated));
        Assert.Equal(ResponseParsers.TruncatedResponseMessage, exception.Message);
    }

    [Fact]
    public void ParseSetUpgradeResponse_TruncatedInput_ThrowsLockedMessage()
    {
        var truncated = """{"set_upgrade_report":{"sets":[{"set_code":"duskmourn","set_name":"Dusk""";
        var exception = Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseSetUpgradeResponse(truncated));
        Assert.Equal(ResponseParsers.TruncatedResponseMessage, exception.Message);
    }
}
