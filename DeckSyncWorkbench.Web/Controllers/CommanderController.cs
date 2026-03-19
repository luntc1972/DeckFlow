using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using DeckSyncWorkbench.Web.Models;
using DeckSyncWorkbench.Web.Services;

namespace DeckSyncWorkbench.Web.Controllers;

public sealed class CommanderController : Controller
{
    private readonly ICategoryKnowledgeStore _categoryKnowledgeStore;
    private readonly ICommanderSearchService _searchService;
    private readonly ILogger<CommanderController> _logger;

    public CommanderController(ICategoryKnowledgeStore categoryKnowledgeStore, ICommanderSearchService searchService, ILogger<CommanderController> logger)
    {
        _categoryKnowledgeStore = categoryKnowledgeStore;
        _searchService = searchService;
        _logger = logger;
    }

    [HttpGet("/commander-categories")]
    /// <summary>
    /// Renders the commander categories form.
    /// </summary>
    /// <param name="commander">Optional commander name to pre-populate.</param>
    public IActionResult Index(string? commander)
    {
        var request = new CommanderCategoryRequest { CommanderName = commander ?? string.Empty };
        return View("CommanderCategories", new CommanderCategoryViewModel { Request = request });
    }

    [HttpPost("/commander-categories")]
    [ValidateAntiForgeryToken]
    /// <summary>
    /// Looks up commander categories using the knowledge store.
    /// </summary>
    /// <param name="request">Commander category request.</param>
    public async Task<IActionResult> Index(CommanderCategoryRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CommanderName))
        {
            return View("CommanderCategories", new CommanderCategoryViewModel
            {
                Request = request ?? new CommanderCategoryRequest(),
                ErrorMessage = "Enter a commander name to see its most common Archidekt categories."
            });
        }

        var trimmed = request.CommanderName.Trim();
        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var initialDeckCount = await _categoryKnowledgeStore.GetProcessedDeckCountAsync(cancellationToken);
        try
        {
            await _categoryKnowledgeStore.EnsureHarvestFreshAsync(_logger, cancellationToken);
            var rows = await _categoryKnowledgeStore.GetCategoryRowsAsync(trimmed, boardFilter: "commander", cancellationToken);
            if (!rows.Any())
            {
                _logger.LogInformation("No cached rows found for {Commander}; importing additional Archidekt decks.", trimmed);
                await _categoryKnowledgeStore.ProcessNextDecksAsync(_logger, cancellationToken);
                rows = await _categoryKnowledgeStore.GetCategoryRowsAsync(trimmed, boardFilter: "commander", cancellationToken);
            }
            var deckCount = await _categoryKnowledgeStore.GetProcessedDeckCountAsync(cancellationToken);
            var cardTotals = await _categoryKnowledgeStore.GetCardDeckTotalsAsync(trimmed, boardFilter: "commander", cancellationToken);
            var categorySummaries = rows
                .GroupBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
                .Select(group => new CommanderCategorySummary(
                    group.Key,
                    group.Sum(row => row.Count),
                    group.Sum(row => row.DeckCount)))
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Category, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var additionalDecksFound = Math.Max(deckCount - initialDeckCount, 0);
            var viewModel = new CommanderCategoryViewModel
            {
                Request = new CommanderCategoryRequest { CommanderName = trimmed },
                CategoryRows = rows,
                CategorySummaries = categorySummaries,
                HarvestedDeckCount = deckCount,
                AdditionalDecksFound = additionalDecksFound,
                ExtendedHarvestTriggered = true,
                CardDeckTotals = cardTotals
            };
            ScheduleExtendedArchidektHarvest();
            return View("CommanderCategories", viewModel);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Commander category lookup for {Commander} timed out.", trimmed);
            return View("CommanderCategories", new CommanderCategoryViewModel
            {
                Request = new CommanderCategoryRequest { CommanderName = trimmed },
                ErrorMessage = "Category lookup timed out. Try again in a moment."
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load commander categories for {Commander}.", trimmed);
            return View("CommanderCategories", new CommanderCategoryViewModel
            {
                Request = new CommanderCategoryRequest { CommanderName = trimmed },
                ErrorMessage = "Archidekt could not be reached right now. Try again shortly."
            });
        }
    }

    [HttpGet("/commander-categories/search")]
    /// <summary>
    /// Provides a look-ahead list of commander names.
    /// </summary>
    /// <param name="query">Partial commander name.</param>
    public async Task<IActionResult> Search(string query)
    {
        var names = await _searchService.SearchAsync(query ?? string.Empty, HttpContext.RequestAborted);
        return Json(names);
    }

    private void ScheduleExtendedArchidektHarvest()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _categoryKnowledgeStore.RunCacheSweepAsync(_logger, 30 * 60);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Extended Archidekt harvest failed.");
            }
        });
    }
}
