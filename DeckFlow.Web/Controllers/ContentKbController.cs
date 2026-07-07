using System.Globalization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
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
    private readonly IFeatureFlagCache _flagCache;
    private readonly ILogger<ContentKbController> _logger;

    /// <summary>
    /// Creates the Content KB controller.
    /// </summary>
    /// <param name="store">Content site-index store.</param>
    /// <param name="resolver">Artifact path resolver.</param>
    /// <param name="flagCache">
    /// Feature-flag cache consulted for <c>sync.directpush-gitbody</c> so a missing-body
    /// resolution returns a real 404 under the flag instead of the legacy 200 shell.
    /// </param>
    /// <param name="logger">Logger.</param>
    public ContentKbController(
        IContentSiteIndexStore store,
        ContentKbArtifactPathResolver resolver,
        IFeatureFlagCache flagCache,
        ILogger<ContentKbController> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _resolver = resolver;
        _flagCache = flagCache;
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
        // Why: the public detail route reads through the approval-filtered store method so a drifted
        // visible-but-pending row 404s (D-04 / Codex HIGH); GetByIdAsync stays unfiltered for admin/Studio.
        var row = await _store.GetPublishedByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
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

        // Why: recompute the on-disk body hash via the ONE shared helper (which itself calls
        // SplitHeader over `raw`) so the render-side hash and the publish-side hash are provably
        // comparable (D-01). On mismatch OR a legacy null/absent stored hash, log a structured
        // warning naming the row but keep serving the body — fail-open this phase (D-05); a future
        // phase may tighten this to fail-closed once the backfill (89-06) guarantees coverage.
        var computedHash = ContentSiteIndexContentSignature.ComputeBodySha256(raw);
        if (row.BodySha256 is null || !string.Equals(row.BodySha256, computedHash, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Content KB body hash mismatch for row {ContentKbRowId}: stored={StoredHash} computed={ComputedHash}",
                row.Id,
                row.BodySha256 ?? "(none)",
                computedHash);
        }

        var renderedHtml = new HtmlString(Markdown.ToHtml(body, Pipeline));

        // Prefer the baked sibling prompt (written at distill time) when present; otherwise
        // reconstruct it from the notes so pre-bake artifacts still copy a framed, paste-ready
        // prompt. Both paths yield identical output for the same notes. See ContentKbPromptResolver.
        var copyPrompt = await ResolveCopyPromptAsync(row, raw, cancellationToken).ConfigureAwait(false);
        return View("Detail", BuildDetailModel(row, renderedHtml, copyPrompt, artifactUnavailable: false));
    }

    // Reads the sibling {id}.prompt.md when it resolves, then delegates to the resolver which
    // returns the baked prompt or reconstructs one from the notes.
    private async Task<string> ResolveCopyPromptAsync(
        ContentSiteIndexRow row,
        string notesRaw,
        CancellationToken cancellationToken)
    {
        string? bakedPrompt = null;
        var promptPath = ContentKbPromptResolver.PromptPathFor(row.ArtifactPath);
        if (promptPath is not null
            && _resolver.TryResolveExistingArtifact(promptPath, out var promptResolved) == ContentKbArtifactResolution.Resolved)
        {
            bakedPrompt = await System.IO.File.ReadAllTextAsync(promptResolved, cancellationToken).ConfigureAwait(false);
        }

        return ContentKbPromptResolver.BuildOrReconstruct(
            bakedPrompt, notesRaw, row.Title, row.Source, row.VideoUrl) ?? string.Empty;
    }

    private static ContentKbDetailViewModel BuildDetailModel(
        ContentSiteIndexRow row,
        HtmlString renderedHtml,
        string copyPrompt,
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
            // The page renders the raw notes; the copy button gets the standalone, framed prompt
            // (persona + task + evidence rules) resolved by ResolveCopyPromptAsync — the baked
            // sibling when present, else reconstructed from the notes. See ContentKbPromptResolver.
            CleanBodyText = copyPrompt,
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
