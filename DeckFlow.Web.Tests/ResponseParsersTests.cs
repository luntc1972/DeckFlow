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
    public void ParseAnalysisResponse_ObjectShapedStrengthsWeaknessesDeckNeeds_CoercesToStrings()
    {
        var payload = """
            {
              "deck_profile": {
                "format": "commander",
                "commander": "Sokka, Tenacious Tactician",
                "game_plan": "Build a board of tactical value creatures, reuse ETB triggers, and snowball combat advantage through disciplined attacks.",
                "primary_axes": ["combat", "tempo", "blink"],
                "speed": "midrange",
                "strengths": [
                  { "name": "Efficient curve", "description": "The deck starts developing pressure on turns 1 through 3 without giving up interaction." },
                  { "name": "Card velocity", "description": "Repeated ETB value lets the deck see more cards than a typical Boros shell." },
                  { "name": "Combat leverage", "description": "Attack-step triggers convert small board edges into real pressure." },
                  { "name": "Spot removal", "description": "Cheap answers keep problem blockers and combo creatures off the table." },
                  { "name": "Resilient threats", "description": "Several creatures generate value immediately, softening the impact of sweepers." },
                  { "name": "Political flexibility", "description": "The deck can pressure the leader while still holding up reactive tools." }
                ],
                "weaknesses": [
                  { "name": "Stack interaction", "description": "It cannot consistently stop fast combo once mana is untapped." },
                  { "name": "Mana recovery", "description": "After multiple wraths the deck can take too long to rebuild colored sources." },
                  { "name": "Card quality", "description": "Some low-impact role-players become poor draws once the game goes long." },
                  { "name": "Closing speed", "description": "The deck sometimes stabilizes without turning that position into a fast win." }
                ],
                "deck_needs": [
                  { "need": "More burst draw", "description": "Add effects that reload after the first board wipe." },
                  { "need": "Cheaper protection", "description": "Protect key engines without spending a full turn cycle." },
                  { "need": "Higher impact finishers", "description": "Convert tempo advantages into shorter clocks." },
                  { "need": "Extra land smoothing", "description": "Reduce the number of awkward two-color opening hands." }
                ],
                "weak_slots": [
                  { "card": "Firemantle Adept", "reason": "Too small an effect for four mana." },
                  { "card": "Tactician's Pike", "reason": "Win-more equipment that doesn't help from behind." }
                ],
                "synergy_tags": ["blink", "tokens", "attack-triggers"],
                "question_answers": [
                  { "question_number": 1, "question": "How does the deck usually win?", "answer": "By turning repeated attack triggers into overwhelming combat value.", "basis": "The creature suite is dense with ETB and attack payoffs." },
                  { "question_number": 2, "question": "What does it struggle with?", "answer": "Fast combo and repeated sweepers.", "basis": "Most interaction is permanent-based and rebuilding takes time." }
                ]
              }
            }
            """;

        var response = ResponseParsers.ParseAnalysisResponse(payload);

        Assert.Equal(6, response.Strengths.Count);
        Assert.Equal(4, response.Weaknesses.Count);
        Assert.Equal(4, response.DeckNeeds.Count);
        Assert.Contains("Efficient curve: The deck starts developing pressure on turns 1 through 3 without giving up interaction.", response.Strengths);
    }

    [Fact]
    public void ParseAnalysisResponse_LegacyStringShaped_StillParses()
    {
        var payload = """
            {
              "deck_profile": {
                "format": "commander",
                "commander": "Sokka, Tenacious Tactician",
                "strengths": [
                  "Efficient curve",
                  "Card velocity",
                  "Combat leverage",
                  "Spot removal",
                  "Resilient threats",
                  "Political flexibility"
                ],
                "weaknesses": [
                  "Stack interaction",
                  "Mana recovery",
                  "Card quality",
                  "Closing speed"
                ],
                "deck_needs": [
                  "More burst draw",
                  "Cheaper protection",
                  "Higher impact finishers",
                  "Extra land smoothing"
                ]
              }
            }
            """;

        var response = ResponseParsers.ParseAnalysisResponse(payload);

        Assert.Equal(6, response.Strengths.Count);
        Assert.Equal(4, response.Weaknesses.Count);
        Assert.Equal(4, response.DeckNeeds.Count);
        Assert.Equal("Efficient curve", response.Strengths[0]);
    }

    [Fact]
    public void ParseAnalysisResponse_MalformedDeckProfile_ThrowsInvalidOperationNotJsonException()
    {
        var payload = """
            {
              "deck_profile": {
                "format": "commander",
                "commander": "Sokka, Tenacious Tactician",
                "strengths": [
                  { "name": "Efficient curve", "description": "Good early development." },
                  7
                ]
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ResponseParsers.ParseAnalysisResponse(payload));

        Assert.Equal("The submitted AI response did not contain a valid deck_profile payload.", exception.Message);
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
    public void ParseSetUpgradeResponse_DeserializesCardTextForTopAddsAndShortlist()
    {
        var payload = """
            {
              "set_upgrade_report": {
                "sets": [
                  {
                    "set_code": "duskmourn",
                    "set_name": "Duskmourn",
                    "top_adds": [
                      { "card": "Atraxa's Fall", "card_text": "Destroy target creature with flying. 1/1" }
                    ]
                  }
                ],
                "final_shortlist": {
                  "must_test": [
                    { "card": "Overlord of the Mistmoors", "card_text": "Flying, trample. When this enters, create two 2/1 tokens." }
                  ]
                }
              }
            }
            """;

        var response = ResponseParsers.ParseSetUpgradeResponse(payload);

        Assert.Equal("Destroy target creature with flying. 1/1", Assert.Single(response.Sets).TopAdds[0].CardText);
        Assert.Equal("Flying, trample. When this enters, create two 2/1 tokens.", response.FinalShortlist!.MustTest[0].CardText);
    }

    [Fact]
    public void ParseSetUpgradeResponse_DefaultsCardTextToEmptyWhenAbsent()
    {
        var response = ResponseParsers.ParseSetUpgradeResponse("""{"sets":[{"set_code":"duskmourn","set_name":"Duskmourn","top_adds":[{"card":"Atraxa's Fall"}]}]}""");

        Assert.Equal(string.Empty, Assert.Single(response.Sets).TopAdds[0].CardText);
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
