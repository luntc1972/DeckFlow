using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards D-06, D-08 and D-10 from source text so every admin page is covered without building models.
/// </summary>
public sealed class AdminMarkupContractTests
{
    private static readonly string[] RetiredAdminOnlyClasses =
    [
        "admin-sidebar__link--active", "admin-topbar__title", "admin-harvest__health", "admin-harvest__health-tile", "admin-harvest__health-badge", "admin-harvest__health-unavailable", "admin-harvest__tabs", "admin-harvest__panel", "admin-harvest__grid-idle", "admin-harvest__disclosure", "admin-harvest__streak", "admin-harvest__search-form", "admin-harvest__grid-loading", "admin-harvest__grid-error", "admin-harvest__grid-pager", "admin-harvest__grid-meta", "admin-feedback-pagination", "tools-filter", "tools-filter__chip", "tools-filter__chips", "tools-filter__count", "tools-filter__empty", "flag-filter", "flag-filter__chip", "flag-filter__chip-count", "flag-filter__chips", "flag-filter__count", "flag-filter__empty-row", "admin-tools__section", "admin-tools__core-badge", "admin-banner--warn", "admin-flags", "admin-tools", "admin-kb-toggle", "admin-kb-empty", "admin-kb-status__actions", "kb-status", "kb-status--hidden", "kb-status--local-newer", "kb-status--published", "kb-status--unpublished", "kb-filter", "kb-filter__count", "kb-filter__empty-row", "admin-hub-card", "admin-hub-card__title", "admin-hub-card__description", "admin-banner--error", "admin-range-selector", "admin-analytics", "admin-feedback", "admin-feedback-filters", "admin-feedback-filter", "admin-feedback-table", "admin-feedback-empty", "admin-feedback-detail", "admin-feedback-type", "type-badge", "detail-message", "detail-actions", "admin-notice", "admin-result", "maintenance-page", "admin-harvest__category-breakdown"
    ];

    private static readonly string[] RetiredPublicOverlapClasses =
    ["error-banner", "warning-banner", "feedback-banner", "feedback-banner--success", "kb-tag", "lede", "field", "result-panel", "danger"];

    private static readonly string[] SharedVocabulary =
    ["admin-card", "admin-card--link", "admin-card__title", "admin-card__description", "admin-card__actions", "admin-stat-grid", "admin-stat-tile", "admin-stat-tile__label", "admin-stat-tile__value", "admin-stat-tile__value--muted", "admin-tabs", "admin-tabs__tab", "admin-filter", "admin-filter-search", "admin-filter-chips", "admin-filter-chips__chip", "admin-filter-chips__count", "admin-filter__count", "admin-filter__empty-row", "admin-filter__empty", "admin-banner--success", "admin-banner--warning", "admin-banner--danger", "admin-banner--info", "admin-badge", "admin-badge--success", "admin-badge--warning", "admin-badge--danger", "admin-badge--info", "admin-badge--neutral", "admin-badge--alert", "admin-button", "admin-button--primary", "admin-button--secondary", "admin-button--danger", "admin-meta", "admin-field", "admin-pagination", "admin-empty", "admin-artifact", "admin-page-header", "admin-page-header__title", "admin-page-header__lede", "admin-page-header__actions", "admin-sidebar__group-label"];

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

    [Fact]
    public void RetiredAdminOnlyClasses_AreAbsentFromAdminViewsScriptsAndCss()
    {
        var offenders = RetiredAdminOnlyClasses.SelectMany(token => WholeTextOffenders(AdminAndScriptFiles(), token))
            .Concat(RetiredAdminOnlyClasses.SelectMany(token => CssTokenOffenders(token)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);

        Assert.True(!offenders.Any(), "Retired admin-only class offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void RetiredPublicOverlapClasses_AreAbsentFromAdminViewsAndCss()
    {
        var offenders = RetiredPublicOverlapClasses.SelectMany(token => ClassAttributeOffenders(token))
            .Concat(RetiredPublicOverlapClasses.SelectMany(token => CssTokenOffenders(token)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);

        Assert.True(!offenders.Any(), "Retired public-overlap class offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void SupersededFilterInputIdRules_AreRemoved()
    {
        var tokens = new[] { "#kb-filter-search", "#kb-creator-filter", "#flag-filter-search", "#tools-filter-search" };
        var offenders = CssRules().SelectMany(rule => tokens.Where(token => rule.selector.Contains(token, StringComparison.Ordinal)).Select(token => CssOffender(rule.file, rule.selector, token)));

        Assert.True(!offenders.Any(), "Superseded filter input rule offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AdminCssClassSelectors_EachHaveAConsumer()
    {
        var consumers = string.Join("\n", AdminAndScriptFiles().Concat(Directory.EnumerateFiles(Path.Combine(ViewsRoot, "Shared"), "_Admin*.cshtml")).Select(ReadSource));
        var dead = CssRules()
            .Where(rule => !rule.selector.TrimStart().StartsWith("@", StringComparison.Ordinal))
            .Select(rule => (rule, tokens: Regex.Matches(Regex.Replace(rule.selector, @"\[[^\]]*\]", string.Empty), @"\.(-?[_a-zA-Z][\w-]*)")
                .Select(match => match.Groups[1].Value)
                .Where(token => !SharedVocabulary.Contains(token, StringComparer.Ordinal) && !WholeTextMatch(consumers, token))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToArray()))
            .Where(item => item.tokens.Length > 0)
            .Select(item => $"{Path.GetRelativePath(RepositoryRoot, item.rule.file)}:{LineNumber(File.ReadAllText(item.rule.file), item.rule.selector.Trim())} {string.Join(" ", item.tokens)}")
            .OrderBy(value => value, StringComparer.Ordinal);

        Assert.True(!dead.Any(), "Dead CSS selector tokens: " + string.Join(", ", dead));
    }

    private static string ViewsRoot => Path.Combine(RepositoryRoot, "DeckFlow.Web", "Views");

    private static string WebRoot => Path.Combine(RepositoryRoot, "DeckFlow.Web");

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

    private static IEnumerable<string> AdminAndScriptFiles() => AdminViews()
        .Concat(Directory.EnumerateFiles(Path.Combine(WebRoot, "wwwroot", "ts"), "*.ts"));

    private static IEnumerable<(string file, string selector)> CssRules() => new[] { "admin-common.css", "admin-mobile.css" }
        .SelectMany(fileName => AdminCssTokenTests.Rules(AdminCssTokenTests.ReadCss(fileName)).Select(rule => (Path.Combine(WebRoot, "wwwroot", "css", fileName), rule.Selector)));

    private static IEnumerable<string> WholeTextOffenders(IEnumerable<string> files, string token) => files
        .Where(file => WholeTextMatch(ReadSource(file), token))
        .Select(file => $"{Offender(file, token)} {token}");

    private static IEnumerable<string> ClassAttributeOffenders(string token) => AdminViews()
        .Where(file => Regex.Matches(ReadView(file), "class\\s*=\\s*\\\"([^\\\"]*)\\\"", RegexOptions.IgnoreCase).Any(match => Regex.IsMatch(match.Groups[1].Value, $@"(?<![\w-]){Regex.Escape(token)}(?![\w-])")))
        .Select(file => Offender(file, token));

    private static IEnumerable<string> CssTokenOffenders(string token) => CssRules()
        .Where(rule => Regex.IsMatch(rule.selector, $@"\.{Regex.Escape(token)}(?![\w-])"))
        .Select(rule => CssOffender(rule.file, rule.selector, token));

    private static bool WholeTextMatch(string source, string token)
    {
        for (var index = source.IndexOf(token, StringComparison.Ordinal); index >= 0; index = source.IndexOf(token, index + token.Length, StringComparison.Ordinal))
        {
            var before = index > 0 ? source[index - 1] : '\0';
            var after = index + token.Length < source.Length ? source[index + token.Length] : '\0';
            var startsFileExtension = source.AsSpan(index + token.Length).StartsWith(".js", StringComparison.Ordinal)
                || source.AsSpan(index + token.Length).StartsWith(".ts", StringComparison.Ordinal)
                || source.AsSpan(index + token.Length).StartsWith(".css", StringComparison.Ordinal);
            if (!IsWord(before) && before is not '#' and not '/' and not '-' && !IsWord(after) && after != '-' && !startsFileExtension)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWord(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static string CssOffender(string file, string selector, string token) => $"{Path.GetRelativePath(RepositoryRoot, file)}:{LineNumber(File.ReadAllText(file), token)} {token}";

    private static string ReadView(string file) => Regex.Replace(Regex.Replace(File.ReadAllText(file), @"@\*.*?\*@", string.Empty, RegexOptions.Singleline), @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

    private static string ReadSource(string file) => file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
        ? ReadView(file)
        : Regex.Replace(Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"(?<!:)//.*$", string.Empty, RegexOptions.Multiline);

    private static string Offender(string file, string token) => $"{Path.GetRelativePath(RepositoryRoot, file)}:{LineNumber(ReadView(file), token)}";

    private static int LineNumber(string source, string token)
    {
        var index = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? 1 : source[..index].Count(character => character == '\n') + 1;
    }
}
