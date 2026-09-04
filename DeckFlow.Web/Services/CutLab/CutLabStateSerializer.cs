using System.Text;
using System.Text.Json;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Serializes and deserializes the Cut Lab working-session state envelope.</summary>
public static class CutLabStateSerializer
{
    /// <summary>Maximum allowed UTF-8 payload size for the serialized working-session JSON.</summary>
    public const int MaxUploadBytes = 262_144;
    private const int MaxPackages = 50;
    // Why: a 150-card pool can accumulate multiple decision records per card across loop-around
    // passes, so this keeps realistic history intact while staying comfortably under the byte cap.
    private const int MaxDecisions = 500;
    private const int MaxOriginalEntries = 200;
    // Why: mirrors the decision-history headroom while remaining comfortably under the byte cap.
    private const int MaxQuantityAdjustments = 300;
    // Why: this matches the fixed strategy catalog and prevents client state from expanding role scans.
    private const int MaxGenericStrategies = 12;
    // Why: commander themes are external data, so retain ample user choice without unbounded scans.
    private const int MaxCommanderThemes = 50;
    // Why: a 150-card pool bounds any single legal copy delta.
    private const int MaxCopyDelta = 150;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>Serializes the working-session state as camelCase JSON under the size cap.</summary>
    /// <param name="state">State to serialize.</param>
    /// <returns>The serialized JSON payload.</returns>
    public static string Serialize(CutLabState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
        {
            throw new InvalidOperationException("The Cut Lab working session is too large to save.");
        }

        return json;
    }

    /// <summary>Deserializes a previously submitted working-session payload.</summary>
    /// <param name="json">Serialized payload.</param>
    /// <param name="bracketOverride">Optional bracket to use for legacy interaction-floor migration.</param>
    /// <returns>The deserialized state, or an empty state when input is blank or malformed.</returns>
    public static CutLabState Deserialize(string? json, int? bracketOverride = null)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
        {
            return new CutLabState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<CutLabState>(json, Options) ?? new CutLabState();
            state = state with
            {
                Pool = state.Pool
                    .Where(card => card is not null && !string.IsNullOrWhiteSpace(card.Name))
                    .ToArray(),
                Packages = state.Packages
                    .Where(package => package is not null && !string.IsNullOrWhiteSpace(package.Name))
                    .Take(MaxPackages)
                    .ToArray(),
                Decisions = state.Decisions
                    .Where(decision => decision is not null && !string.IsNullOrWhiteSpace(decision.CardName))
                    .Take(MaxDecisions)
                    .ToArray(),
                QuantityAdjustments = state.QuantityAdjustments
                    .Where(adjustment => adjustment is not null && !string.IsNullOrWhiteSpace(adjustment.Name))
                    .Select(adjustment => adjustment with
                    {
                        Delta = Math.Clamp(adjustment.Delta, -MaxCopyDelta, MaxCopyDelta),
                    })
                    .Take(MaxQuantityAdjustments)
                    .ToArray(),
                OriginalEntries = state.OriginalEntries
                    .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Name))
                    .Take(MaxOriginalEntries)
                    .ToArray(),
                RoleFloors = state.RoleFloors
                    .Where(floor => floor is not null && !string.IsNullOrWhiteSpace(floor.Role))
                    .ToArray(),
                Goals = state.Goals ?? new CutLabGoalSettings(),
                Intent = state.Intent with
                {
                    PlanProfile = state.Intent.PlanProfile is { } planProfile
                        ? planProfile with
                        {
                            GenericStrategies = planProfile.GenericStrategies
                                .Where(strategy => !string.IsNullOrWhiteSpace(strategy))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(MaxGenericStrategies)
                                .ToArray(),
                            CommanderThemes = planProfile.CommanderThemes
                                .Where(theme => theme is not null && !string.IsNullOrWhiteSpace(theme.Slug))
                                .DistinctBy(theme => theme.Slug, StringComparer.OrdinalIgnoreCase)
                                .Take(MaxCommanderThemes)
                                .ToArray(),
                        }
                        : null,
                },
            };

            return CutLabGoalRules.ClampGoals(CutLabFloorRules.ClampFloors(CutLabLockRules.EnforceCommanderLock(state), bracketOverride));
        }
        catch (JsonException)
        {
            return new CutLabState();
        }
    }
}
