using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards D-06, D-08 and D-10 from source text so every admin page is covered without building models.
/// </summary>
public sealed class AdminMarkupContractTests
{
    [Fact]
    public void AdminPageViews_RenderNoH1()
    {
        var offenders = AdminViews()
            .Where(file => Regex.IsMatch(ReadView(file), @"<h1(?=\s|>)", RegexOptions.IgnoreCase))
            .Select(file => Offender(file, "<h1"));

        Assert.True(!offenders.Any(), "Admin page h1 offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AdminLayout_RendersExactlyOnePageHeaderH1()
    {
        var layout = Path.Combine(ViewsRoot, "Shared", "_AdminLayout.cshtml");
        var headings = Regex.Matches(ReadView(layout), @"<h1(?=\s|>)", RegexOptions.IgnoreCase);
        Assert.True(headings.Count == 1, $"Admin layout h1 offenders: {Offender(layout, "<h1")}");

        var openingTag = Regex.Match(ReadView(layout), @"<h1\b[^>]*>", RegexOptions.IgnoreCase).Value;
        Assert.Matches(new Regex(@"class\s*=\s*""[^""]*\badmin-page-header__title\b[^""]*""", RegexOptions.IgnoreCase), openingTag);

        var offenders = Directory.EnumerateFiles(Path.Combine(ViewsRoot, "Shared"), "_Admin*.cshtml")
            .Where(file => !Path.GetFullPath(file).Equals(Path.GetFullPath(layout), StringComparison.OrdinalIgnoreCase))
            .Where(file => Regex.IsMatch(ReadView(file), @"<h1(?=\s|>)", RegexOptions.IgnoreCase))
            .Select(file => Offender(file, "<h1"));
        Assert.True(!offenders.Any(), "Shared admin h1 offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AdminButtons_EachCarryASharedButtonClass()
    {
        var buttonPattern = new Regex(@"<button\b(?:(?!=>).)*?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var sharedClassPattern = new Regex(@"\b(admin-button|admin-tabs__tab|admin-filter-chips__chip|admin-modal__button|admin-table__sort-header)\b");
        var offenders = AdminAndSharedViews()
            .SelectMany(file => buttonPattern.Matches(ReadView(file)).Select(match => (file, match)))
            .Where(item => !sharedClassPattern.IsMatch(item.match.Value))
            .Select(item => Offender(item.file, item.match.Value));

        Assert.True(!offenders.Any(), "Admin button offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void FeedbackIndex_SuccessBanner_UsesSharedSuccessBanner()
    {
        var file = Path.Combine(ViewsRoot, "AdminFeedback", "Index.cshtml");
        var source = ReadView(file);
        var actionIndex = source.IndexOf("@actionMessage", StringComparison.Ordinal);
        Assert.True(actionIndex >= 0, "Feedback success banner offender: " + Offender(file, "@actionMessage"));

        var tagStart = source.LastIndexOf("<", actionIndex, StringComparison.Ordinal);
        var openingTag = tagStart >= 0 ? source[tagStart..source.IndexOf('>', tagStart)] : string.Empty;
        Assert.Matches(new Regex(@"class\s*=\s*""[^""]*\badmin-banner\b[^""]*\badmin-banner--success\b[^""]*""", RegexOptions.IgnoreCase), openingTag);
        Assert.Matches(new Regex(@"role\s*=\s*""status""", RegexOptions.IgnoreCase), openingTag);
    }

    private static string ViewsRoot => Path.Combine(RepositoryRoot, "DeckFlow.Web", "Views");

    private static string RepositoryRoot
    {
        get
        {
            var directory = AppContext.BaseDirectory;
            while (!Directory.Exists(Path.Combine(directory, "DeckFlow.Web")))
            {
                directory = Directory.GetParent(directory)?.FullName
                    ?? throw new DirectoryNotFoundException("Could not locate DeckFlow.Web.");
            }

            return directory;
        }
    }

    private static IEnumerable<string> AdminViews() => Directory.EnumerateFiles(ViewsRoot, "*.cshtml", SearchOption.AllDirectories)
        .Where(file => Path.GetRelativePath(ViewsRoot, file).StartsWith("Admin", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> AdminAndSharedViews() => AdminViews()
        .Concat(Directory.EnumerateFiles(Path.Combine(ViewsRoot, "Shared"), "_Admin*.cshtml"));

    private static string ReadView(string file) => Regex.Replace(Regex.Replace(File.ReadAllText(file), @"@\*.*?\*@", string.Empty, RegexOptions.Singleline), @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

    private static string Offender(string file, string token) => $"{Path.GetRelativePath(RepositoryRoot, file)}:{LineNumber(ReadView(file), token)}";

    private static int LineNumber(string source, string token)
    {
        var index = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? 1 : source[..index].Count(character => character == '\n') + 1;
    }
}
