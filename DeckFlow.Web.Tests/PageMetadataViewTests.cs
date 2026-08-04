using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// File-level SEO regression guard: every public, indexable page must set a unique
/// per-page meta description (<c>ViewData["Description"]</c>) so search engines do not
/// see the shared default description site-wide, and the shared layout must render the
/// computed <c>pageTitle</c> (not raw <c>ViewData["Title"]</c>, which left a dangling
/// "- DeckFlow" when the title was empty).
/// </summary>
public sealed class PageMetadataViewTests
{
    // The sitewide fallback in _Layout.cshtml; no page may reuse it verbatim.
    private const string DefaultDescription =
        "DeckFlow — Magic: The Gathering deck analysis for cEDH and Commander. Compare, analyze, and generate ChatGPT-ready deck prompts.";

    private static readonly (string Folder, string File)[] IndexableViews =
    {
        ("Deck", "Home.cshtml"),
        ("Deck", "DeckSync.cshtml"),
        ("Deck", "DeckConvert.cshtml"),
        ("Deck", "CardLookup.cshtml"),
        ("Deck", "MechanicLookup.cshtml"),
        ("Deck", "DeckAnalysis.cshtml"),
        ("Deck", "DeckComparison.cshtml"),
        ("Deck", "CedhMetaGap.cshtml"),
        ("Deck", "DeckPrimer.cshtml"),
        ("Deck", "SuggestCategories.cshtml"),
        ("Deck", "Manabase.cshtml"),
        ("Deck", "JudgeQuestions.cshtml"),
        ("Commander", "CommanderCategories.cshtml"),
        ("ContentKb", "Index.cshtml"),
        ("Help", "Index.cshtml"),
        ("About", "Index.cshtml"),
        ("Feedback", "Index.cshtml"),
    };

    // Matches a string-literal assignment: ViewData["Description"] = "....";
    private static readonly Regex DescriptionLiteral = new(
        "ViewData\\[\"Description\"\\]\\s*=\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"",
        RegexOptions.Compiled);

    public static TheoryData<string, string> IndexableViewData()
    {
        var data = new TheoryData<string, string>();
        foreach (var (folder, file) in IndexableViews)
        {
            data.Add(folder, file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(IndexableViewData))]
    public void IndexableView_SetsNonDefaultMetaDescription(string folder, string file)
    {
        var content = ReadView(folder, file);
        var match = DescriptionLiteral.Match(content);

        Assert.True(match.Success, $"{folder}/{file} does not set a string-literal ViewData[\"Description\"].");

        var description = match.Groups["value"].Value;
        Assert.False(string.IsNullOrWhiteSpace(description), $"{folder}/{file} has an empty meta description.");
        Assert.NotEqual(DefaultDescription, description);
    }

    [Fact]
    public void IndexableViews_AllHaveDistinctMetaDescriptions()
    {
        var descriptions = IndexableViews
            .Select(view => DescriptionLiteral.Match(ReadView(view.Folder, view.File)).Groups["value"].Value)
            .ToList();

        var duplicates = descriptions
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate meta descriptions: {string.Join(" | ", duplicates)}");
    }

    [Theory]
    [InlineData("Bracket.cshtml", "MTG Commander Bracket Checker", "Check a Commander deck against the official Magic: The Gathering bracket system and get a local classification, target-bracket gaps, and recommended cuts.")]
    [InlineData("DeckHistory.cshtml", "MTG Commander Deck Version Tracker", "Track Commander deck versions in a local history, compare any two snapshots, and get a card-by-card changelog with an AI-ready review prompt.")]
    [InlineData("CutLab.cshtml", "Cut Lab", "Trim an oversized Commander card pool with a structured cut workspace that identifies removable cards, protects locked roles, and exports your finished deck.")]
    public void UncoveredDeckViews_SetPlannedTitleAndMetaDescription(string file, string title, string description)
    {
        var content = ReadView("Deck", file);

        Assert.Contains($"ViewData[\"Title\"] = \"{title}\"", content, StringComparison.Ordinal);
        Assert.Contains($"ViewData[\"Description\"] = \"{description}\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpTopicDetail_SetsDescriptionFromSummary()
    {
        var content = ReadView("Help", "Topic.cshtml");

        Assert.Contains("ViewData[\"Description\"] = Model.Summary", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_RendersComputedPageTitle_NotRawViewData()
    {
        var content = ReadView("Shared", "_Layout.cshtml");

        // The <title> tag must use the computed pageTitle, and that computation must keep
        // its collapse-when-empty / suffix-otherwise logic so the tag matches the og/twitter
        // titles and never emits a dangling "- DeckFlow".
        Assert.Contains("<title>@pageTitle</title>", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>@ViewData[\"Title\"] - DeckFlow</title>", content, StringComparison.Ordinal);
        Assert.Contains("? \"DeckFlow\" : $\"{pageTitle} - DeckFlow\"", content, StringComparison.Ordinal);
    }

    private static string ReadView(string folder, string file)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            folder,
            file));
}
