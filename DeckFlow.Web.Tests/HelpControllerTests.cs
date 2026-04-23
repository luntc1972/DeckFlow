using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

public class HelpControllerTests
{
    private sealed class StubHelpContentService : IHelpContentService
    {
        private readonly List<HelpTopic> _topics;
        public StubHelpContentService(params HelpTopic[] topics) => _topics = topics.ToList();
        public IReadOnlyList<HelpTopic> GetAll() => _topics;
        public HelpTopic? GetBySlug(string slug) =>
            _topics.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Index_returns_view_with_all_topics()
    {
        var a = new HelpTopic("a", "Alpha", "first", 10, "<p>a</p>");
        var b = new HelpTopic("b", "Beta", "second", 20, "<p>b</p>");
        var controller = new HelpController(new StubHelpContentService(a, b));

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsAssignableFrom<IReadOnlyList<HelpTopic>>(result.Model);

        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void Topic_returns_NotFound_for_unknown_slug()
    {
        var controller = new HelpController(new StubHelpContentService());

        var result = controller.Topic("unknown");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Topic_returns_view_with_topic_for_known_slug()
    {
        var topic = new HelpTopic("chatgpt-analysis", "ChatGPT Analysis", "s", 10, "<h1>X</h1>");
        var controller = new HelpController(new StubHelpContentService(topic));

        var result = Assert.IsType<ViewResult>(controller.Topic("chatgpt-analysis"));

        Assert.Same(topic, result.Model);
    }
}
