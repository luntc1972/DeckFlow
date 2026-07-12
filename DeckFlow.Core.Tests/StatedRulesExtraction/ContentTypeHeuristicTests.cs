using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Xunit;

namespace DeckFlow.Core.Tests.StatedRulesExtraction;

public sealed class ContentTypeHeuristicTests
{
    [Fact]
    public void Classify_ZeroCardCategoryTagsWithoutGameplay_ReturnsMetaCommentary()
    {
        string contentType = ContentTypeHeuristic.Classify(
            archetypeTags: ["tempo"],
            cardCategoryTags: [],
            clips: [new ClipItem(15, "We are talking about the state of the format and creator trends.")]);

        Assert.Equal(ContentTypeHeuristic.MetaCommentary, contentType);
    }

    [Fact]
    public void Classify_ArchetypeAndCardCategoryWithoutGameplay_ReturnsDeckTech()
    {
        string contentType = ContentTypeHeuristic.Classify(
            archetypeTags: ["stax"],
            cardCategoryTags: ["ramp"],
            clips: [new ClipItem(22, "This shell wants more cheap artifacts and smoother land drops.")]);

        Assert.Equal(ContentTypeHeuristic.DeckTech, contentType);
    }

    [Fact]
    public void Classify_CardCategoryWithoutArchetypeOrGameplay_ReturnsDeckbuildingTheory()
    {
        string contentType = ContentTypeHeuristic.Classify(
            archetypeTags: [],
            cardCategoryTags: ["interaction"],
            clips: [new ClipItem(44, "You still need enough free interaction even outside one named archetype.")]);

        Assert.Equal(ContentTypeHeuristic.DeckbuildingTheory, contentType);
    }

    [Fact]
    public void Classify_TwoDistinctGameplayKeywords_ReturnsGameplay()
    {
        string contentType = ContentTypeHeuristic.Classify(
            archetypeTags: ["midrange"],
            cardCategoryTags: ["draw"],
            clips:
            [
                new ClipItem(91, "On turn three I cast the commander and set up the next line."),
                new ClipItem(118, "My opponent had to respect combat and keep blockers back."),
            ]);

        Assert.Equal(ContentTypeHeuristic.Gameplay, contentType);
    }

    [Fact]
    public void Classify_OneRepeatedGameplayKeyword_DoesNotCountAsGameplay()
    {
        string contentType = ContentTypeHeuristic.Classify(
            archetypeTags: ["control"],
            cardCategoryTags: ["interaction"],
            clips:
            [
                new ClipItem(30, "Combat matters here. Combat matters here. Combat matters here."),
                new ClipItem(65, "Combat is still important, but this clip never adds a second keyword."),
            ]);

        Assert.Equal(ContentTypeHeuristic.DeckTech, contentType);
    }
}
