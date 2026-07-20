using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds the compact D-12 baseline snapshot for the original Cut Lab pool.</summary>
public sealed class CutLabBaselineSnapshot
{
    private readonly ICutLabSimulationService _simulationService;

    /// <summary>Creates a new <see cref="CutLabBaselineSnapshot"/>.</summary>
    /// <param name="simulationService">Simulation service reused for the shared metric projection pipeline.</param>
    public CutLabBaselineSnapshot(ICutLabSimulationService simulationService)
    {
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
    }

    /// <summary>Builds the compact numeric baseline snapshot for the original locked pool.</summary>
    /// <param name="originalPool">Original Cut Lab pool cards.</param>
    /// <param name="playExperience">Cut Lab play-experience label used to resolve the shared manabase mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The baseline metric snapshot at full default simulation fidelity.</returns>
    public Task<CutLabMetricSnapshot> Build(
        IReadOnlyList<CutLabPoolCard> originalPool,
        string? playExperience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalPool);
        return _simulationService.BuildSnapshot(originalPool, playExperience, trialsOverride: null, cancellationToken);
    }
}
