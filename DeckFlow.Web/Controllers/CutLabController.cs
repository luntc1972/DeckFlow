using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Cut Lab tool: load an oversized pool, preserve working-session state, and guide trimming.</summary>
public sealed class CutLabController : Controller
{
    private readonly ICutLabPageService _pageService;
    private readonly ICutLabWhatifService _whatifService;
    private readonly ICutLabExportService _exportService;
    private readonly ILogger<CutLabController> _logger;

    /// <summary>Creates the controller with its page service and logger.</summary>
    public CutLabController(
        ICutLabPageService pageService,
        ICutLabWhatifService whatifService,
        ICutLabExportService exportService,
        ILogger<CutLabController> logger)
    {
        ArgumentNullException.ThrowIfNull(pageService);
        ArgumentNullException.ThrowIfNull(whatifService);
        ArgumentNullException.ThrowIfNull(exportService);
        _pageService = pageService;
        _whatifService = whatifService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>Renders the empty Cut Lab form.</summary>
    [HttpGet("/cut-lab")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    public IActionResult Index() => CutLabView(new CutLabRequest(), null);

    /// <summary>Processes a Cut Lab intake request and re-renders the page with results.</summary>
    /// <param name="request">Form fields.</param>
    [HttpPost("/cut-lab")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Process(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        try
        {
            // Why: restore a saved scenario posted back through the main intake form as state-only data.
            if (!string.IsNullOrWhiteSpace(request.CutLabStateJson)
                && string.IsNullOrWhiteSpace(request.DeckText)
                && string.IsNullOrWhiteSpace(request.DeckUrl))
            {
                CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
                RehydrateIntakeRequestFromState(request, state);
            }
            else if (!string.IsNullOrWhiteSpace(request.CutLabStateJson))
            {
                CutLabState priorState = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
                BackfillMissingPlanFieldsFromState(request, priorState);
                if (priorState.OriginalEntries.Count > 0 && !IsRehydratedTextResubmission(request, priorState))
                {
                    // Why: a new import must not inherit old printings; migrate floors using the posted bracket.
                    request.CutLabStateJson = CutLabStateSerializer.Serialize(
                        CutLabStateSerializer.Deserialize(request.CutLabStateJson, request.Bracket) with { OriginalEntries = [] });
                }
            }

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result));
        }
        catch (Exception exception)
        {
            return HandleFallbackException(exception, request, "Cut Lab processing failed.", "Something went wrong processing the pool. Try again.");
        }
    }

    /// <summary>Applies posted plan selections and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    [HttpPost("/cut-lab/plan-apply")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> PlanApply(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            bool formHasNoPlanArchetype = Request.HasFormContentType && !Request.Form.ContainsKey("PlanArchetype");
            RehydrateIntakeRequestFromState(request, state, preservePostedPlanProfile: true, formHasNoPlanArchetype);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result, currentStepOverride: 3));
        }
        catch (Exception exception)
        {
            return HandleFallbackException(exception, request, "Cut Lab plan apply fallback failed.", CutLabMessages.NoChangeMessage);
        }
    }

    /// <summary>Applies one Cut Lab decision and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardName">Card receiving the decision.</param>
    /// <param name="decision">Decision to apply.</param>
    /// <param name="roundKey">Optional posted round key for the current decision button.</param>
    /// <param name="checkedCommanderThemesAvailable">Theme availability captured when the round was rendered; null for missing or invalid values.</param>
    [HttpPost("/cut-lab/decide")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Decide(CutLabRequest request, string cardName, CutLabDecideAction decision, string? roundKey = null, bool? checkedCommanderThemesAvailable = null)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            if (CutLabDecisionApplier.IsLockedDecision(state, cardName, decision))
            {
                return CutLabView(request, error: CutLabDecisionApplier.GetLockedDecisionMessage(cardName));
            }

            string resolvedRoundKey = DetermineRoundKey(state, cardName, decision, roundKey);
            state = CutLabDecisionApplier.Apply(state, cardName, decision, resolvedRoundKey, checkedCommanderThemesAvailable);
            RehydrateIntakeRequestFromState(request, state);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result));
        }
        catch (Exception exception)
        {
            return HandleFallbackException(exception, request, "Cut Lab decision fallback failed.", CutLabMessages.NoChangeMessage);
        }
    }

    /// <summary>Applies one quantity adjustment and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardName">Card receiving the quantity adjustment.</param>
    /// <param name="delta">Signed quantity delta to apply.</param>
    /// <param name="isAddedBasic">True when materializing a known added basic.</param>
    [HttpPost("/cut-lab/adjust")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Adjust(CutLabRequest request, string cardName, int delta, bool isAddedBasic)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        return await RunStateMutationAsync(request, "Cut Lab adjustment fallback failed.", state =>
        {
            state = CutLabAdjustmentApplier.Apply(state, cardName, delta, isAddedBasic);
            return (state, null);
        });
    }

    /// <summary>Applies a user-selected role override to one pool card.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardName">Card to update.</param>
    /// <param name="role">Role key to add or remove.</param>
    /// <param name="operation">Requested role-override operation.</param>
    [HttpPost("/cut-lab/role-override")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> RoleOverride(CutLabRequest request, string cardName, string? role, CutLabRoleOverrideOperation operation)
    {
        request ??= new CutLabRequest();
        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }
        return await RunStateMutationAsync(request, "Cut Lab role-override fallback failed.", state =>
        {
            CutLabRoleOverrideRules.TryApplyEdit(state, cardName, role, operation, out CutLabState updated, out string? error);
            return (updated, error);
        });
    }

    /// <summary>Applies a persisted primary category edit.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardName">Card display name.</param>
    /// <param name="category">Replacement category text.</param>
    /// <param name="operation">Explicit set or reset operation.</param>
    [HttpPost("/cut-lab/category-override")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> CategoryOverride(CutLabRequest request, string cardName, string? category, CutLabCategoryOverrideOperation operation)
    {
        request ??= new CutLabRequest();
        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }
        return await RunStateMutationAsync(request, "Cut Lab category-override fallback failed.", state =>
        {
            CutLabCategoryRules.TryApplyEdit(state, cardName, category, operation, out CutLabState updated, out string? error);
            return (updated, error);
        });
    }

    /// <summary>Re-surfaces prior round 1 and round 2 rejects/defers without undoing accepted cuts.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    [HttpPost("/cut-lab/restart-rounds")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> RestartRounds(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        return await RunStateMutationAsync(request, "Cut Lab restart rounds fallback failed.", state =>
        {
            state = CutLabDecisionApplier.RestartRounds(state, [CutLabCutRoundEngine.Round1Key, CutLabCutRoundEngine.Round2Key]);
            return (state, null);
        });
    }

    /// <summary>Applies posted goal turns and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    [HttpPost("/cut-lab/goals")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Goals(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        return await RunStateMutationAsync(request, "Cut Lab goals fallback failed.", state =>
        {
            state = state with
            {
                Goals = new CutLabGoalSettings
                {
                    CommanderByTurn = request.GoalCommanderByTurn ?? state.Goals.CommanderByTurn,
                    EngineByTurn = request.GoalEngineByTurn ?? state.Goals.EngineByTurn,
                    RepresentativeLineByTurn = request.GoalPlanByTurn ?? state.Goals.RepresentativeLineByTurn,
                },
            };
            state = CutLabGoalRules.ClampGoals(state);
            return (state, null);
        }, step: 4);
    }

    /// <summary>Builds the export surface and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    [HttpPost("/cut-lab/export")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Export(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            RehydrateIntakeRequestFromState(request, state);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            CutLabState exportState = result.State ?? state;
            CutLabExportView export = await _exportService.BuildExportAsync(
                exportState,
                request.PlayExperience,
                CutLabCommanderNames.Resolve(exportState),
                HttpContext.RequestAborted).ConfigureAwait(false);
            return View("CutLab", CutLabViewModel.From(request, result, export: export, currentStepOverride: 5));
        }
        catch (Exception exception)
        {
            return HandleFallbackException(exception, request, "Cut Lab export fallback failed.", CutLabMessages.NoChangeMessage);
        }
    }

    /// <summary>Previews or commits a what-if swap and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardOut">Working-list card to remove.</param>
    /// <param name="cardIn">Cut-pile card to restore.</param>
    /// <param name="intent">Preview or keep intent.</param>
    [HttpPost("/cut-lab/whatif")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Whatif(CutLabRequest request, string cardOut, string cardIn, string intent)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson)
            || string.IsNullOrWhiteSpace(cardOut)
            || string.IsNullOrWhiteSpace(cardIn)
            || !IsWhatifIntent(intent))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            if (!_whatifService.TryValidateSwap(state, cardOut, cardIn, out string? validationError))
            {
                return await RenderWhatifViewAsync(request, state, null, validationError);
            }

            if (string.Equals(intent, "preview", StringComparison.OrdinalIgnoreCase))
            {
                CutLabWhatifPreview preview = await _whatifService
                    .PreviewSwapAsync(state, cardOut, cardIn, HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                return await RenderWhatifViewAsync(request, state, preview, null);
            }

            CutLabWhatifCommitResult result = await _whatifService
                .CommitSwapAsync(state, cardOut, cardIn, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            if (!result.Applied)
            {
                return await RenderWhatifViewAsync(request, state, null, result.ErrorMessage ?? CutLabMessages.NoChangeMessage);
            }

            RehydrateIntakeRequestFromState(request, result.State);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(result.State);

            var pageResult = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, pageResult));
        }
        catch (Exception exception)
        {
            return HandleFallbackException(exception, request, "Cut Lab what-if fallback failed.", CutLabMessages.NoChangeMessage);
        }
    }

    private async Task<IActionResult> RunStateMutationAsync(
        CutLabRequest request,
        string failureLog,
        Func<CutLabState, (CutLabState State, string? Error)> mutate,
        int? step = null)
    {
        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            (state, string? error) = mutate(state);
            RehydrateIntakeRequestFromState(request, state);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(state);
            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            CutLabViewModel model = CutLabViewModel.From(request, result, currentStepOverride: step);
            return View("CutLab", error is null ? model : model with { ErrorMessage = error });
        }
        catch (Exception exception)
        {
            return HandleFallbackException(exception, request, failureLog, CutLabMessages.NoChangeMessage);
        }
    }

    private IActionResult HandleFallbackException(Exception exception, CutLabRequest request, string failureLog, string genericMessage)
    {
        if (exception is InvalidOperationException)
        {
            return CutLabView(request, error: exception.Message);
        }
        if (exception is OperationCanceledException)
        {
            return CutLabView(request, error: "The request timed out. Try again.");
        }
        _logger.LogError(exception, failureLog);
        return CutLabView(request, error: genericMessage);
    }

    private static string DetermineRoundKey(CutLabState state, string cardName, CutLabDecideAction decision, string? postedRoundKey)
    {
        if (CutLabCutRoundEngine.IsKnownRoundKey(postedRoundKey))
        {
            return postedRoundKey!;
        }

        return CutLabDecisionApplier.LatestRoundForCard(state, cardName);
    }

    private async Task<ViewResult> RenderWhatifViewAsync(
        CutLabRequest request,
        CutLabState state,
        CutLabWhatifPreview? preview,
        string? error)
    {
        RehydrateIntakeRequestFromState(request, state);
        request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

        var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
        CutLabViewModel viewModel = CutLabViewModel.From(request, result, BuildWhatifPreviewView(preview));
        return View("CutLab", string.IsNullOrWhiteSpace(error) ? viewModel : viewModel with { ErrorMessage = error });
    }

    private static CutLabWhatifPreviewView BuildWhatifPreviewView(CutLabWhatifPreview? preview)
    {
        if (preview is null)
        {
            return new CutLabWhatifPreviewView();
        }

        return new CutLabWhatifPreviewView
        {
            CardOut = preview.CardOut,
            CardIn = preview.CardIn,
            DeltaRows = CutLabViewModel.BuildCompareRows(preview.Deltas),
            HasPreview = true,
        };
    }

    private static bool IsWhatifIntent(string intent)
        => string.Equals(intent, "preview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intent, "keep", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rehydrates the request from state, because state is authoritative except for fields the caller explicitly owns.
    /// </summary>
    private static void RehydrateIntakeRequestFromState(
        CutLabRequest request,
        CutLabState state,
        bool preservePostedPlanProfile = false,
        bool formHasNoPlanArchetype = false)
    {
        if (formHasNoPlanArchetype)
        {
            request.PlanArchetype = state.Intent.PlanProfile?.Archetype;
        }

        if (!NeedsDeckInputRehydration(request, state))
        {
            return;
        }

        request.DeckInputSource = DeckInputSource.PasteText;
        request.DeckUrl = string.Empty;
        request.DeckText = BuildDeckText(state);
        request.PrimaryPlan = state.Intent.PrimaryPlan;
        request.SecondaryPlan = state.Intent.SecondaryPlan ?? string.Empty;
        if (!preservePostedPlanProfile)
        {
            request.PlanArchetype = state.Intent.PlanProfile?.Archetype;
            request.PlanStrategies = state.Intent.PlanProfile?.GenericStrategies ?? [];
            request.PlanThemes = state.Intent.PlanProfile?.CommanderThemes.Select(theme => theme.Slug).ToArray() ?? [];
        }

        request.Bracket = state.Intent.Bracket;
        request.PlayExperience = state.Intent.PlayExperience;
        request.IncludeSideboard = state.Intent.IncludeSideboard;
        request.IncludeMaybeboard = state.Intent.IncludeMaybeboard;
        // Why: preserve an explicit single-commander selection while allowing flagged partner commanders to resolve together.
        if (state.Pool.Count(card => card.IsCommander) <= 1)
        {
            request.SelectedCommander = state.Commander;
        }
    }

    /// <summary>
    /// Backfills missing plan fields from prior state, because the POST is authoritative and state is only a fallback.
    /// </summary>
    private static void BackfillMissingPlanFieldsFromState(CutLabRequest request, CutLabState priorState)
    {
        // Why: the plan panel sits outside this form (D-1's explicit-apply design), so a
        // reprocess submission (new deck text, changed bracket/experience) never carries
        // PlanStrategies/PlanThemes in its POST body. Backfill them from the carried-forward
        // state so reprocessing does not silently wipe or reset the user's checked plan.
        if (request.PlanStrategies.Count == 0 && request.PlanThemes.Count == 0)
        {
            request.PlanStrategies = priorState.Intent.PlanProfile?.GenericStrategies ?? [];
            request.PlanThemes = priorState.Intent.PlanProfile?.CommanderThemes.Select(theme => theme.Slug).ToArray() ?? [];
        }
        if (string.IsNullOrWhiteSpace(request.PlanArchetype))
        {
            request.PlanArchetype = priorState.Intent.PlanProfile?.Archetype;
        }
        if (string.IsNullOrWhiteSpace(request.PrimaryPlan))
        {
            request.PrimaryPlan = priorState.Intent.PrimaryPlan;
        }
        if (string.IsNullOrWhiteSpace(request.SecondaryPlan))
        {
            request.SecondaryPlan = priorState.Intent.SecondaryPlan ?? string.Empty;
        }
    }

    private static bool NeedsDeckInputRehydration(CutLabRequest request, CutLabState state)
        => string.IsNullOrWhiteSpace(request.DeckText)
            && string.IsNullOrWhiteSpace(request.DeckUrl)
            && state.Pool.Count > 0;

    private static bool IsRehydratedTextResubmission(CutLabRequest request, CutLabState state)
    {
        static string NormalizeText(string text)
            => string.Join("\n", text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
                .Split('\n').Select(line => line.Trim())).TrimEnd('\n');

        return string.IsNullOrWhiteSpace(request.DeckUrl)
            && string.Equals(NormalizeText(request.DeckText ?? string.Empty), NormalizeText(BuildDeckText(state)), StringComparison.Ordinal);
    }

    private static string BuildDeckText(CutLabState state)
    {
        IReadOnlyList<CutLabPoolCard> commanderCards = GetCommanderCards(state);
        IReadOnlyList<CutLabPoolCard> mainboardCards = state.Pool
            .Except(commanderCards)
            .ToArray();

        var lines = new List<string>(state.Pool.Count + 3);
        if (commanderCards.Count > 0)
        {
            lines.Add("Commander");
            lines.AddRange(commanderCards.Select(FormatDeckLine));
            lines.Add(string.Empty);
        }

        lines.Add("Deck");
        lines.AddRange(mainboardCards.Select(FormatDeckLine));
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<CutLabPoolCard> GetCommanderCards(CutLabState state)
    {
        CutLabPoolCard[] flaggedCommanders = state.Pool
            .Where(card => card.IsCommander)
            .ToArray();
        if (flaggedCommanders.Length > 0)
        {
            return flaggedCommanders;
        }

        if (!string.IsNullOrWhiteSpace(state.Commander))
        {
            CutLabPoolCard[] matchedCommander = state.Pool
                .Where(card => string.Equals(card.Name, state.Commander, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchedCommander.Length > 0)
            {
                return matchedCommander;
            }
        }

        return [];
    }

    private static string FormatDeckLine(CutLabPoolCard card) => $"{card.Quantity} {card.Name}";

    private ViewResult CutLabView(CutLabRequest request, string? error) =>
        View("CutLab", new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            Request = request,
            ErrorMessage = error,
            CutLabStateJson = request.CutLabStateJson,
        });
}
