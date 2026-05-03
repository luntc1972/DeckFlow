using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Renders the Help hub index and individual help topic pages.</summary>
public sealed class HelpController : Controller
{
    private readonly IHelpContentService _content;

    public HelpController(IHelpContentService content) => _content = content;

    [HttpGet("/help")]
    [FeatureFlagGate("page.help.enabled",
        Title = "Help center temporarily unavailable",
        Message = "Help is offline for maintenance. Please try again in a few minutes.")]
    public IActionResult Index() => View(_content.GetAll());

    [HttpGet("/help/{slug}")]
    public IActionResult Topic(string slug)
    {
        var topic = _content.GetBySlug(slug);
        return topic is null ? NotFound() : View(topic);
    }
}
