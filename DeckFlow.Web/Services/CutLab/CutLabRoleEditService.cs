using System.Diagnostics;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Applies role edits and rebuilds their original-pool baseline.</summary>
public interface ICutLabRoleEditService
{
    /// <summary>Applies an edit and rebuilds the baseline only when roles change.</summary>
    Task<CutLabRoleEditResult> ApplyAsync(CutLabState state, string cardName, string? role, CutLabRoleOverrideOperation operation, CancellationToken cancellationToken = default);
}

/// <summary>Rebuilds changed role-edit baselines at in-loop simulation fidelity.</summary>
public sealed class CutLabRoleEditService : ICutLabRoleEditService
{
    private readonly ICutLabSimulationService _simulationService;
    private readonly ILogger<CutLabRoleEditService> _logger;

    /// <summary>Creates a role editor with its simulation service and logger.</summary>
    public CutLabRoleEditService(ICutLabSimulationService simulationService, ILogger<CutLabRoleEditService> logger)
    {
        ArgumentNullException.ThrowIfNull(simulationService);
        ArgumentNullException.ThrowIfNull(logger);
        _simulationService = simulationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CutLabRoleEditResult> ApplyAsync(CutLabState state, string cardName, string? role, CutLabRoleOverrideOperation operation, CancellationToken cancellationToken = default)
    {
        if (!CutLabRoleOverrideRules.TryApplyEdit(state, cardName, role, operation, out CutLabState updated, out string? error))
        {
            return new() { State = state, Error = error, Changed = false };
        }
        if (CutLabRoleOverrideRules.Fingerprint(updated.RoleOverrides) == CutLabRoleOverrideRules.Fingerprint(state.RoleOverrides))
        {
            return new() { State = state, Changed = false };
        }

        var watch = Stopwatch.StartNew();
        try
        {
            CutLabSimulationResult result = await _simulationService.BuildSnapshotResult(
                updated.Pool,
                updated.Intent.PlayExperience,
                // Why: the role-edit baseline trades fidelity for latency; a fresh import keeps DefaultTrials.
                trialsOverride: ICutLabSimulationService.InLoopTrials,
                poolKey: null,
                goals: updated.Goals,
                roleOverrides: updated.RoleOverrides,
                cancellationToken: cancellationToken);
            updated = updated with
            {
                BaselineSnapshot = result.Snapshot,
                BaselineActualLands = result.ActualLands,
                BaselineTargetLands = result.TargetLands,
                Pool = CutLabSimulationResult.ApplySimulationCardData(updated.Pool, result.CastabilityByCardName),
            };
            _logger.LogInformation("Cut Lab role edit rebuilt baseline at {Trials} trials in {ElapsedMs} ms", ICutLabSimulationService.InLoopTrials, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cut Lab role edit baseline rebuild failed after {ElapsedMs} ms; saving the edit without a baseline", watch.ElapsedMilliseconds);
        }
        return new() { State = updated, Changed = true };
    }
}
