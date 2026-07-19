using System.Globalization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the creator-style critique page.
/// </summary>
public sealed class CreatorStyleController : DeckToolControllerBase
{
    private readonly ICreatorStylePacketService _packetService;
    private readonly ICreatorStyleProfileStore _profileStore;
    private readonly IContentSiteIndexStore _siteIndexStore;
    private readonly PacketSessionCache _packetCache;
    private readonly ILogger<CreatorStyleController> _logger;

    /// <summary>
    /// Creates the creator-style controller.
    /// </summary>
    public CreatorStyleController(
        ICreatorStylePacketService packetService,
        ICreatorStyleProfileStore profileStore,
        IContentSiteIndexStore siteIndexStore,
        PacketSessionCache packetCache,
        ILogger<CreatorStyleController> logger)
    {
        ArgumentNullException.ThrowIfNull(packetService);
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(siteIndexStore);
        ArgumentNullException.ThrowIfNull(packetCache);
        ArgumentNullException.ThrowIfNull(logger);

        _packetService = packetService;
        _profileStore = profileStore;
        _siteIndexStore = siteIndexStore;
        _packetCache = packetCache;
        _logger = logger;
    }

    /// <summary>
    /// Renders the creator-style form or the empty-store info state.
    /// </summary>
    [HttpGet("/creator-style")]
    [FeatureFlagGate(CreatorStylePacketService.CreatorStyleToolEnabledFlag)]
    public async Task<IActionResult> CreatorStyle()
    {
        var options = await BuildPickerOptionsAsync(HttpContext.RequestAborted);
        return View("CreatorStyle", new CreatorStyleViewModel
        {
            NoProfilesLoaded = options.Count == 0,
            AvailableCreators = options,
        });
    }

    /// <summary>
    /// Builds a creator-style critique packet for the submitted creator and deck.
    /// </summary>
    /// <param name="request">The form-bound creator-style request.</param>
    [HttpPost("/creator-style")]
    [ValidateAntiForgeryToken]
    [FeatureFlagGate(CreatorStylePacketService.CreatorStyleToolEnabledFlag)]
    public async Task<IActionResult> CreatorStyle(CreatorStyleRequest request)
    {
        request ??= new CreatorStyleRequest();

        return await RunGuardedAsync(
            request,
            "CreatorStyle",
            "Couldn't build the packet. Check your deck input and try again.",
            async token =>
            {
                CreatorStylePacketResult result;
                var cacheKey = await _packetService.TryComputeCacheKeyAsync(request, token);
                if (cacheKey is not null
                    && _packetCache.TryGet<CreatorStylePacketResult>(cacheKey, out var cached)
                    && cached is not null)
                {
                    result = cached;
                }
                else
                {
                    result = await _packetService.BuildAsync(request, token);
                }

                return View("CreatorStyle", new CreatorStyleViewModel
                {
                    Request = request,
                    Result = result,
                    AvailableCreators = await BuildPickerOptionsAsync(token),
                });
            });
    }

    private async Task<IReadOnlyList<CreatorStyleViewModel.CreatorPickerOption>> BuildPickerOptionsAsync(CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<CreatorStyleProfileSummary>> summariesTask = _profileStore.GetAllAsync(cancellationToken);
        Task<IReadOnlyList<ContentSiteIndexRow>> publishedRowsTask = _siteIndexStore.GetPublishedRowsAsync(cancellationToken);
        await Task.WhenAll(summariesTask, publishedRowsTask);

        var summaries = await summariesTask;
        if (summaries.Count == 0)
        {
            return Array.Empty<CreatorStyleViewModel.CreatorPickerOption>();
        }

        var publishedRows = await publishedRowsTask;
        var videoCountsBySlug = publishedRows
            .GroupBy(row => SlugifySourceName.Slugify(row.Source), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return summaries
            .Select(summary =>
            {
                var displayName = HumanizeSlug(summary.Slug);
                var videoCount = videoCountsBySlug.TryGetValue(summary.Slug, out var count) ? count : 0;
                return new CreatorStyleViewModel.CreatorPickerOption
                {
                    Slug = summary.Slug,
                    DisplayLabel = $"{displayName} — {summary.MinDecks} decks · {videoCount} videos",
                };
            })
            .ToArray();
    }

    private static string HumanizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace('-', ' '));
    }

    private async Task<IActionResult> RunGuardedAsync(
        CreatorStyleRequest request,
        string operation,
        string unexpectedMessage,
        Func<CancellationToken, Task<IActionResult>> body)
    {
        using var timeoutScope = CreateTimeoutScope(LookupTimeout);
        async Task<IActionResult> ErrorViewAsync(string message)
        {
            return View("CreatorStyle", new CreatorStyleViewModel
            {
                Request = request,
                AvailableCreators = await BuildPickerOptionsAsync(CancellationToken.None),
                ErrorMessage = message,
            });
        }

        try
        {
            return await body(timeoutScope.Token);
        }
        catch (OperationCanceledException) when (timeoutScope.IsCancellationRequested)
        {
            _logger.LogInformation("Creator-style {Operation} timed out.", operation);
            return await ErrorViewAsync("The deck took too long to load. Try again in a moment.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Creator-style {Operation} failed validation.", operation);
            return await ErrorViewAsync(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Creator-style {Operation} hit an upstream dependency.", operation);
            return await ErrorViewAsync(UpstreamErrorMessageBuilder.BuildScryfallMessage(exception));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Creator-style {Operation} failed unexpectedly.", operation);
            return await ErrorViewAsync(unexpectedMessage);
        }
    }
}
