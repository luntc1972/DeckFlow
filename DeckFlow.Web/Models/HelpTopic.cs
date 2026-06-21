namespace DeckFlow.Web.Models;

/// <summary>
/// Parsed help topic: metadata from the markdown header plus rendered HTML body.
/// </summary>
/// <param name="Slug">URL slug (markdown file name without extension).</param>
/// <param name="Title">Display title from the header.</param>
/// <param name="Summary">One-line summary from the header.</param>
/// <param name="Order">Sort order from the header.</param>
/// <param name="HtmlContent">Rendered HTML body.</param>
/// <param name="RequiresFlag">
/// Optional feature-flag key (from the <c>requires_flag</c> header). When set, the topic is
/// hidden from the index and its detail page returns 404 while that flag is disabled — so a
/// tool's help follows the tool's own kill-switch.
/// </param>
public sealed record HelpTopic(
    string Slug,
    string Title,
    string Summary,
    int Order,
    string HtmlContent,
    string? RequiresFlag = null);
