using System.Globalization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Markdig;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the public Content KB browse and artifact detail pages.
/// </summary>
[Route("content-kb")]
public sealed class ContentKbController : Controller
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    private readonly IContentSiteIndexStore _store;
    private readonly ContentKbArtifactPathResolver _resolver;
    private readonly ILogger<ContentKbController> _logger;

    /// <summary>
    /// Creates the Content KB controller.
    /// </summary>
    /// <param name="store">Content site-index store.</param>
    /// <param name="resolver">Artifact path resolver.</param>
    /// <param name="logger">Logger.</param>
    public ContentKbController(
        IContentSiteIndexStore store,
        ContentKbArtifactPathResolver resolver,
        ILogger<ContentKbController> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Renders the published-only Content KB browse page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("")]
    [FeatureFlagGate("tool.knowledge-base.enabled")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var rows = await _store.GetPublishedRowsAsync(cancellationToken).ConfigureAwait(false);
        var entries = rows
            .Select(row => new ContentKbBrowseViewModel.Entry
            {
                Id = row.Id,
                Title = row.Title,
                VideoId = row.PinId!,
                Source = row.Source,
                SourceUrl = row.VideoUrl,
                DetailUrl = $"/content-kb/{row.Id}",
                Archetype = FirstTag(row.ArchetypeTags, "Uncategorized"),
                Bracket = FirstTag(row.BracketTags, "Bracket unknown"),
                CardCategory = FirstTag(row.CardCategoryTags, "Category unknown"),
                ArchetypeTags = row.ArchetypeTags,
                BracketTags = row.BracketTags,
                CardCategoryTags = row.CardCategoryTags,
            })
            .ToList();

        var model = new ContentKbBrowseViewModel
        {
            Entries = entries,
            Sources = DistinctSorted(entries.Select(entry => entry.Source)),
            Archetypes = DistinctSorted(entries.SelectMany(entry => entry.ArchetypeTags)),
            Brackets = DistinctSorted(entries.SelectMany(entry => entry.BracketTags)),
            CardCategories = DistinctSorted(entries.SelectMany(entry => entry.CardCategoryTags)),
        };
        return View(model);
    }

    /// <summary>
    /// Renders a single Content KB artifact by site-index row id.
    /// </summary>
    /// <param name="id">Content site-index row id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id:long}")]
    [FeatureFlagGate("tool.knowledge-base.enabled")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken = default)
    {
        var row = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null || !row.IsVisible)
        {
            return NotFound();
        }

        if (!row.ArtifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return NotFound();
        }

        var resolution = _resolver.TryResolveExistingArtifact(row.ArtifactPath, out var resolved);
        if (resolution == ContentKbArtifactResolution.InvalidPath)
        {
            return NotFound();
        }

        if (resolution == ContentKbArtifactResolution.MissingFile)
        {
            _logger.LogWarning("Content KB artifact file was unavailable for row {ContentKbRowId}.", row.Id);
            return View("Detail", BuildDetailModel(row, new HtmlString(string.Empty), string.Empty, artifactUnavailable: true));
        }

        var raw = await System.IO.File.ReadAllTextAsync(resolved, cancellationToken).ConfigureAwait(false);
        var (_, body) = ContentArtifactParser.SplitHeader(raw);
        var renderedHtml = new HtmlString(Markdown.ToHtml(body, Pipeline));
        return View("Detail", BuildDetailModel(row, renderedHtml, body, artifactUnavailable: false));
    }

    private static ContentKbDetailViewModel BuildDetailModel(
        ContentSiteIndexRow row,
        HtmlString renderedHtml,
        string cleanBodyText,
        bool artifactUnavailable)
        => new()
        {
            Title = row.Title,
            SourceName = row.Source,
            SourceUrl = row.VideoUrl,
            PublishedDisplay = row.PublishedUtc?.UtcDateTime.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                ?? "Publication date unknown",
            Bracket = FirstTag(row.BracketTags, "Bracket unknown"),
            Archetype = FirstTag(row.ArchetypeTags, "Uncategorized"),
            RenderedHtml = renderedHtml,
            // The page renders the raw notes; the copy button gets a standalone, framed prompt
            // (persona + task + evidence rules) so pasting it into an AI returns useful,
            // grounded advice with no extra typing. Applied here so every stored artifact
            // benefits without re-distilling. See ContentKbPromptWrapper.
            CleanBodyText = ContentKbPromptWrapper.Wrap(row.Title, row.Source, row.VideoUrl, cleanBodyText),
            ArtifactUnavailable = artifactUnavailable,
        };

    private static string FirstTag(IReadOnlyList<string> tags, string fallback)
        => tags.Count > 0 ? tags[0] : fallback;

    private static IReadOnlyList<string> DistinctSorted(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
