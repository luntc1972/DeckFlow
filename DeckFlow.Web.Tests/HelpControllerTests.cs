using System.Collections.Generic;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="HelpController"/> covering topic listing, detail rendering, not-found
/// handling, and feature-flag-gated topic visibility.
/// </summary>
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

    private static HelpController CreateController(
        StubHelpContentService content,
        IDictionary<string, bool>? flags = null) =>
        new(content, new FakeFeatureFlagCache(flags));

    [Fact]
    public void Index_returns_view_with_all_topics()
    {
        var a = new HelpTopic("a", "Alpha", "first", 10, "<p>a</p>");
        var b = new HelpTopic("b", "Beta", "second", 20, "<p>b</p>");
        var controller = CreateController(new StubHelpContentService(a, b));

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsAssignableFrom<IReadOnlyList<HelpTopic>>(result.Model);

        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void Topic_returns_NotFound_for_unknown_slug()
    {
        var controller = CreateController(new StubHelpContentService());

        var result = controller.Topic("unknown");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Topic_returns_view_with_topic_for_known_slug()
    {
        var topic = new HelpTopic("chatgpt-analysis", "ChatGPT Analysis", "s", 10, "<h1>X</h1>");
        var controller = CreateController(new StubHelpContentService(topic));

        var result = Assert.IsType<ViewResult>(controller.Topic("chatgpt-analysis"));

        Assert.Same(topic, result.Model);
    }

    [Fact]
    public void Index_hides_topic_whose_required_flag_is_disabled()
    {
        var open = new HelpTopic("a", "Alpha", "first", 10, "<p>a</p>");
        var gated = new HelpTopic("manabase", "Mana Base", "s", 35, "<p>m</p>", "feature.manabase.enabled");
        var controller = CreateController(
            new StubHelpContentService(open, gated),
            new Dictionary<string, bool> { ["feature.manabase.enabled"] = false });

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsAssignableFrom<IReadOnlyList<HelpTopic>>(result.Model);

        Assert.DoesNotContain(model, t => t.Slug == "manabase");
        Assert.Contains(model, t => t.Slug == "a");
    }

    [Fact]
    public void Index_shows_gated_topic_when_its_flag_is_enabled()
    {
        var gated = new HelpTopic("manabase", "Mana Base", "s", 35, "<p>m</p>", "feature.manabase.enabled");
        var controller = CreateController(
            new StubHelpContentService(gated),
            new Dictionary<string, bool> { ["feature.manabase.enabled"] = true });

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsAssignableFrom<IReadOnlyList<HelpTopic>>(result.Model);

        Assert.Contains(model, t => t.Slug == "manabase");
    }

    [Fact]
    public void Topic_returns_NotFound_when_required_flag_is_disabled()
    {
        var gated = new HelpTopic("manabase", "Mana Base", "s", 35, "<p>m</p>", "feature.manabase.enabled");
        var controller = CreateController(
            new StubHelpContentService(gated),
            new Dictionary<string, bool> { ["feature.manabase.enabled"] = false });

        var result = controller.Topic("manabase");

        Assert.IsType<NotFoundResult>(result);
    }
}
