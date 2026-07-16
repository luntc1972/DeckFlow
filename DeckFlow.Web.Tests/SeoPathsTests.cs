using DeckFlow.Web.Seo;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="SeoPaths"/> path normalization and shareable-page matching.</summary>
public sealed class SeoPathsTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/manabase", true)]
    [InlineData("/MANABASE", true)]
    [InlineData("/manabase/", true)]
    [InlineData("/bracket", true)]
    [InlineData("/help", false)]
    [InlineData("/about", false)]
    [InlineData("/help/mana-base", false)]
    [InlineData("", true)]
    public void IsShareablePage_matches_tools_and_home(string path, bool expected)
        => Assert.Equal(expected, SeoPaths.IsShareablePage(path));

    [Theory]
    [InlineData("/Manabase", "/manabase")]
    [InlineData("/help/", "/help")]
    [InlineData("/", "/")]
    [InlineData("", "/")]
    public void Normalize_lowercases_and_strips_trailing_slash(string input, string expected)
        => Assert.Equal(expected, SeoPaths.Normalize(input));
}
