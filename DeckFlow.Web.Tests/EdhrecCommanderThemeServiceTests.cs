using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests default EDHREC theme selection.</summary>
public sealed class EdhrecCommanderThemeServiceTests
{
    [Fact]
    public void SelectDefaultThemes_ReturnsAtMostThree()
    {
        IReadOnlyList<CutLabCommanderTheme> themes =
        [
            new() { Slug = "a", DisplayName = "A", DeckCount = 50 },
            new() { Slug = "b", DisplayName = "B", DeckCount = 20 },
            new() { Slug = "c", DisplayName = "C", DeckCount = 15 },
            new() { Slug = "d", DisplayName = "D", DeckCount = 10 },
            new() { Slug = "e", DisplayName = "E", DeckCount = 5 },
        ];

        var selected = EdhrecCommanderThemeService.SelectDefaultThemes(themes);

        Assert.Equal(EdhrecCommanderThemeService.PreselectMaximumThemes, selected.Count);
        Assert.True(themes.Count(theme => (double)theme.DeckCount / 100 >= EdhrecCommanderThemeService.PreselectMinimumShare) > selected.Count);
    }

    [Fact]
    public void SelectDefaultThemes_ZeroTotal_ReturnsEmpty()
    {
        IReadOnlyList<CutLabCommanderTheme> themes = [new() { Slug = "a", DisplayName = "A", DeckCount = 0 }];

        Assert.Empty(EdhrecCommanderThemeService.SelectDefaultThemes(themes));
    }

    [Fact]
    public void SelectDefaultThemes_ExcludesThemesBelowMinimumShare()
    {
        IReadOnlyList<CutLabCommanderTheme> themes =
        [
            new() { Slug = "a", DisplayName = "A", DeckCount = 95 },
            new() { Slug = "b", DisplayName = "B", DeckCount = 4 },
            new() { Slug = "c", DisplayName = "C", DeckCount = 1 },
        ];

        var selected = EdhrecCommanderThemeService.SelectDefaultThemes(themes);

        Assert.All(selected, theme => Assert.True((double)theme.DeckCount / 100 >= EdhrecCommanderThemeService.PreselectMinimumShare));
        Assert.Equal(["a"], selected.Select(x => x.Slug));
    }
}
