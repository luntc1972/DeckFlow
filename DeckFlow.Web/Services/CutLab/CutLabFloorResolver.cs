using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Manabase;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Resolves the effective Cut Lab role floors for one request.</summary>
public interface ICutLabFloorResolver
{
    /// <summary>Returns one resolved floor row per Cut Lab role.</summary>
    IReadOnlyList<CutLabResolvedFloor> Resolve(
        CutLabState state,
        double commanderManaValue,
        IReadOnlyList<string> commanderNames);
}

/// <summary>Shared default floor resolver used by the page and AJAX Cut Lab paths.</summary>
public sealed class CutLabFloorResolver : ICutLabFloorResolver
{
    private readonly IManabaseBaselineProvider? _manabaseBaseline;
    private readonly ICedhLandBaselineProvider? _cedhBaseline;
    private readonly IRoleFloorBaselineProvider? _roleFloorBaseline;
    private readonly IFeatureFlagCache? _featureFlags;

    /// <summary>Creates the shared Cut Lab floor resolver.</summary>
    public CutLabFloorResolver(
        IManabaseBaselineProvider? manabaseBaseline,
        ICedhLandBaselineProvider? cedhBaseline,
        IRoleFloorBaselineProvider? roleFloorBaseline,
        IFeatureFlagCache? featureFlags)
    {
        _manabaseBaseline = manabaseBaseline;
        _cedhBaseline = cedhBaseline;
        _roleFloorBaseline = roleFloorBaseline;
        _featureFlags = featureFlags;
    }

    /// <inheritdoc />
    public IReadOnlyList<CutLabResolvedFloor> Resolve(
        CutLabState state,
        double commanderManaValue,
        IReadOnlyList<string> commanderNames)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(commanderNames);

        bool commanderFloorsEnabled = _featureFlags is { } flags
            && flags.Snapshot().TryGetValue(CutLabPageService.CommanderFloorsFlagKey, out bool enabled)
            && enabled;

        return CutLabFloorDefaults.ResolveDefaults(
            state.Intent.Bracket,
            state.Intent.PlayExperience,
            commanderManaValue,
            commanderNames,
            _manabaseBaseline,
            _cedhBaseline,
            commanderFloorsEnabled ? _roleFloorBaseline : null,
            state.RoleFloors,
            state.Intent.PlanProfile);
    }
}
