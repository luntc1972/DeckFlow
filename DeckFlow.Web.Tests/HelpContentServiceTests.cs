using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="HelpContentService"/> covering Markdown loading, caching, and not-found handling.
/// </summary>
public class HelpContentServiceTests : IDisposable
{
    private readonly string _root;

    public HelpContentServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "deckflow-help-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Write(string name, string body)
        => File.WriteAllText(Path.Combine(_root, name), body);

    [Fact]
    public void GetAll_returns_topics_ordered_by_order_then_title()
    {
        Write("b.md", "---\ntitle: Beta\nsummary: second\norder: 20\n---\n# Beta\n");
        Write("a.md", "---\ntitle: Alpha\nsummary: first\norder: 10\n---\n# Alpha\n");
        Write("c.md", "---\ntitle: Charlie\nsummary: third\norder: 10\n---\n# Charlie\n");

        var service = new HelpContentService(_root);

        var topics = service.GetAll();

        Assert.Collection(topics,
            t => Assert.Equal("Alpha", t.Title),
            t => Assert.Equal("Charlie", t.Title),
            t => Assert.Equal("Beta", t.Title));
    }

    [Fact]
    public void GetBySlug_uses_filename_without_extension_as_slug()
    {
        Write("prompt-analysis.md", "---\ntitle: Prompt Analysis\nsummary: s\norder: 10\n---\n# Prompt Analysis\n");

        var service = new HelpContentService(_root);

        var topic = service.GetBySlug("prompt-analysis");

        Assert.NotNull(topic);
        Assert.Equal("prompt-analysis", topic!.Slug);
        Assert.Equal("Prompt Analysis", topic.Title);
        Assert.Equal("s", topic.Summary);
        Assert.Equal(10, topic.Order);
    }

    [Fact]
    public void GetBySlug_returns_null_for_missing_topic()
    {
        Write("known.md", "---\ntitle: Known\nsummary: s\norder: 10\n---\n# Known\n");

        var service = new HelpContentService(_root);

        Assert.Null(service.GetBySlug("unknown"));
    }

    [Fact]
    public void GetBySlug_renders_markdown_body_to_html()
    {
        Write("sample.md", "---\ntitle: Sample\nsummary: s\norder: 10\n---\n# Heading\n\nparagraph\n");

        var service = new HelpContentService(_root);

        var topic = service.GetBySlug("sample")!;

        Assert.Contains("<h1", topic.HtmlContent);
        Assert.Contains("Heading", topic.HtmlContent);
        Assert.Contains("<p>paragraph</p>", topic.HtmlContent);
    }

    [Fact]
    public void Header_block_is_not_included_in_rendered_html()
    {
        Write("sample.md", "---\ntitle: Sample\nsummary: hidden\norder: 10\n---\n# Body\n");

        var service = new HelpContentService(_root);

        var topic = service.GetBySlug("sample")!;

        Assert.DoesNotContain("title:", topic.HtmlContent);
        Assert.DoesNotContain("hidden", topic.HtmlContent);
    }

    [Fact]
    public void Project_help_uses_category_suggestions_slug_and_title()
    {
        var helpRoot = FindProjectHelpRoot();
        var service = new HelpContentService(helpRoot);

        var topic = service.GetBySlug("category-suggestions");

        Assert.NotNull(topic);
        Assert.Equal("Commander Deck Tag Suggestions", topic!.Title);
        Assert.Contains("Commander Deck Tag Suggestions", topic.HtmlContent);
    }

    private static string FindProjectHelpRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var helpRoot = Path.Combine(current.FullName, "DeckFlow.Web", "Help");
            if (Directory.Exists(helpRoot))
                return helpRoot;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find DeckFlow.Web/Help from the test working directory.");
    }
}
