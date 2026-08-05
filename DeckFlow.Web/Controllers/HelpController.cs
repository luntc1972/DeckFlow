using System.Collections.Generic;
using System.Linq;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Renders the Help hub index and individual help topic pages.</summary>
public sealed class HelpController : Controller
{
    private readonly IHelpContentService _content;
    private readonly IFeatureFlagCache _flags;

    /// <summary>
    /// Creates the help controller.
    /// </summary>
    public HelpController(IHelpContentService content, IFeatureFlagCache flags)
    {
        _content = content;
        _flags = flags;
    }

    /// <summary>
    /// Renders the help topic index. Topics tied to a disabled feature flag are hidden so a
    /// tool's help disappears alongside the tool itself.
    /// </summary>
    [HttpGet("/help")]
    [FeatureFlagGate("tool.help.enabled")]
    public IActionResult Index() => View(VisibleTopics());

    /// <summary>
    /// Renders a single help topic by slug. A topic gated by a disabled flag returns 404,
    /// matching its tool's kill-switch. The help-wide gate applies first: turning off
    /// "tool.help.enabled" must take every topic down with the index, not just the index.
    /// </summary>
    /// <param name="slug">Help topic slug.</param>
    [HttpGet("/help/{slug}")]
    [FeatureFlagGate("tool.help.enabled")]
    public IActionResult Topic(string slug)
    {
        var topic = _content.GetBySlug(slug);
        return topic is null || !_content.IsTopicVisible(topic, _flags) ? NotFound() : View(topic);
    }

    private IReadOnlyList<HelpTopic> VisibleTopics() =>
        _content.GetAll().Where(topic => _content.IsTopicVisible(topic, _flags)).ToList();
}
