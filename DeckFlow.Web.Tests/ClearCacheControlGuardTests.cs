using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Regression guard for the "clear control does nothing" trap. Every clear/reset control is
/// marked <c>data-clear-cache</c> and is handled in <c>deck-sync.ts</c>, which resets the page
/// by navigating to a clean GET. It can only do that when the control carries a URL — either a
/// real <c>href</c> (anchor form) or <c>data-clear-href</c> (button form). A control with
/// neither falls through to <c>form.reset()</c>, which restores every field to its HTML
/// default; on a POST-rendered page the server has written the submitted values into those
/// defaults, so the reset is a visual no-op. Four views shipped in exactly that state.
/// </summary>
public sealed class ClearCacheControlGuardTests
{
    private static readonly Regex ClearControlPattern = new(
        @"<(a|button)\b[^>]*\bdata-clear-cache\b[^>]*>",
        RegexOptions.Compiled);

    // Why: an anchor navigates from its own href, a button from data-clear-href. The two are
    // matched separately rather than by a bare "href=" substring, which data-clear-href also
    // contains — collapsing them would make the check pass for reasons unrelated to the tag.
    private static readonly Regex NavigableUrlPattern = new(
        @"\s(data-clear-)?href=""", RegexOptions.Compiled);

    private static readonly Lazy<IReadOnlyList<(string Path, string Markup)>> Views =
        new(() => Directory
            .EnumerateFiles(FindViewsRoot(), "*.cshtml", SearchOption.AllDirectories)
            .Select(path => (path, File.ReadAllText(path)))
            .ToList());

    [Fact]
    public void EveryClearCacheControlCarriesANavigableUrl()
    {
        var offenders = new List<string>();

        foreach (var (path, markup) in Views.Value)
        {
            foreach (Match match in ClearControlPattern.Matches(markup))
            {
                if (!NavigableUrlPattern.IsMatch(match.Value))
                {
                    var line = markup[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(path)}:{line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These [data-clear-cache] controls carry neither href nor data-clear-href, so they "
            + "fall through to a form.reset() that cannot clear a POST-rendered page: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void GuardActuallyMatchesRealMarkup()
    {
        // Why: an unresolved Views root or a stale selector would make the guard above pass
        // vacuously — it would find nothing to check and report success.
        Assert.Contains(Views.Value, v => ClearControlPattern.IsMatch(v.Markup));
    }

    private static string FindViewsRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var viewsRoot = Path.Combine(current.FullName, "DeckFlow.Web", "Views");
            if (Directory.Exists(viewsRoot))
                return viewsRoot;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find DeckFlow.Web/Views from the test working directory.");
    }
}
