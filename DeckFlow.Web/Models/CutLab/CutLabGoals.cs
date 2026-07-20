using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Models.CutLab;

/// <summary>Stable seeded defaults and supported bounds for persisted Cut Lab turn goals.</summary>
public static class CutLabGoalDefaults
{
    /// <summary>Minimum allowed goal turn after clamping untrusted persisted values.</summary>
    public const int MinGoalTurn = 1;

    /// <summary>Maximum allowed goal turn after clamping untrusted persisted values.</summary>
    public const int MaxGoalTurn = 15;

    /// <summary>Default commander cast-turn target seeded from the calibrated explosive cap.</summary>
    public const int CommanderByTurn = CedhMulliganCalibration.TurnCapExplosive;

    /// <summary>Default engine cast-turn target seeded from the calibrated early-engine cap.</summary>
    public const int EngineByTurn = CedhMulliganCalibration.TurnCapEngine;

    /// <summary>Default representative-line target seeded from the calibrated plan-line cap.</summary>
    public const int RepresentativeLineByTurn = CedhMulliganCalibration.RepresentativeLineTurnCap;
}

/// <summary>Serializable user-adjusted turn goals for the three existing Cut Lab by-turn metrics.</summary>
public sealed record CutLabGoalSettings
{
    /// <summary>Target turn by which the commander should be castable.</summary>
    public int CommanderByTurn { get; init; } = CutLabGoalDefaults.CommanderByTurn;

    /// <summary>Target turn by which an engine card should be castable.</summary>
    public int EngineByTurn { get; init; } = CutLabGoalDefaults.EngineByTurn;

    /// <summary>Target turn by which a representative plan line should be castable.</summary>
    public int RepresentativeLineByTurn { get; init; } = CutLabGoalDefaults.RepresentativeLineByTurn;
}
