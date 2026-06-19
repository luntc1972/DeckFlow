using DeckFlow.Core.Content;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="PublishStateDeriver"/>.
/// </summary>
public sealed class PublishStateDeriverTests
{
    private readonly PublishStateDeriver _deriver = new();

    [Theory]
    [MemberData(nameof(DeriveCases))]
    public void Derive_ReturnsExpectedState(
        DateTimeOffset? pushedToProdUtc,
        bool isVisible,
        DateTimeOffset localIndexedUtc,
        PublishState expected)
    {
        var actual = _deriver.Derive(pushedToProdUtc, isVisible, localIndexedUtc);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(PublishState.NeverPublished, "Never published")]
    [InlineData(PublishState.PushedHidden, "Pushed-hidden")]
    [InlineData(PublishState.Published, "Published")]
    [InlineData(PublishState.LocalNewer, "Local-newer")]
    public void ToDisplayString_ReturnsLockedUiVocabulary(PublishState state, string expected)
    {
        var actual = state.ToDisplayString();

        Assert.Equal(expected, actual);
    }

    public static TheoryData<DateTimeOffset?, bool, DateTimeOffset, PublishState> DeriveCases()
    {
        var pushUtc = DateTimeOffset.Parse("2023-07-28T23:50:51+00:00");
        return new TheoryData<DateTimeOffset?, bool, DateTimeOffset, PublishState>
        {
            { null, true, DateTimeOffset.Parse("2023-07-28T10:00:00+00:00"), PublishState.NeverPublished },
            { null, false, DateTimeOffset.Parse("2023-07-28T10:00:00+00:00"), PublishState.NeverPublished },
            { pushUtc, false, DateTimeOffset.Parse("2023-07-29T00:00:00+00:00"), PublishState.PushedHidden },
            { pushUtc, true, DateTimeOffset.Parse("2023-07-28T23:50:50+00:00"), PublishState.Published },
            { pushUtc, true, DateTimeOffset.Parse("2023-07-28T23:50:51+00:00"), PublishState.Published },
            { pushUtc, true, DateTimeOffset.Parse("2023-07-29T00:00:00+00:00"), PublishState.LocalNewer },
            { pushUtc, true, DateTimeOffset.Parse("2023-07-28T16:50:51-07:00"), PublishState.Published },
            { pushUtc, true, DateTimeOffset.Parse("2023-07-28T18:50:52-05:00"), PublishState.LocalNewer },
        };
    }
}
