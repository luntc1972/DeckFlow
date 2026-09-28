using System.Diagnostics;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Records the currently executing mana-base analysis stage and elapsed times for completed stages.
/// </summary>
public sealed class ManabaseStageTracker
{
    private readonly object _gate = new();
    private readonly List<KeyValuePair<string, long>> _completedStages = [];
    private long _startedTimestamp;
    private string? _currentStage;

    /// <summary>Gets the stage currently in progress, if any.</summary>
    public string? CurrentStage
    {
        get { lock (_gate) { return _currentStage; } }
    }

    /// <summary>Gets completed stages and their elapsed durations in milliseconds.</summary>
    public IReadOnlyList<KeyValuePair<string, long>> CompletedStages
    {
        get { lock (_gate) { return _completedStages.ToList(); } }
    }

    /// <summary>Gets a compact summary of completed stages for diagnostic logging.</summary>
    public string CompletedStagesSummary
    {
        get { lock (_gate) { return string.Join(", ", _completedStages.Select(stage => $"{stage.Key} ({stage.Value} ms)")); } }
    }

    /// <summary>Marks a stage as in progress.</summary>
    /// <param name="stage">Diagnostic stage name.</param>
    public void StartStage(string stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        lock (_gate)
        {
            _currentStage = stage;
            _startedTimestamp = Stopwatch.GetTimestamp();
        }
    }

    /// <summary>Completes the current stage and returns its elapsed duration in milliseconds.</summary>
    public long FinishStage()
    {
        lock (_gate)
        {
            if (_currentStage is null)
            {
                return 0;
            }

            long elapsedMs = (long)Stopwatch.GetElapsedTime(_startedTimestamp).TotalMilliseconds;
            _completedStages.Add(new KeyValuePair<string, long>(_currentStage, elapsedMs));
            _currentStage = null;
            return elapsedMs;
        }
    }
}
