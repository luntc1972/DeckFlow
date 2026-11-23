using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Runs a Cut Lab corpus measurement when an output directory is supplied.</summary>
public sealed class CutLabMeasurementFactAttribute : FactAttribute
{
    /// <summary>Skips the measurement in ordinary test runs.</summary>
    public CutLabMeasurementFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DECKFLOW_CUTLAB_MEASURE_OUT")))
        {
            Skip = "Set DECKFLOW_CUTLAB_MEASURE_OUT and DECKFLOW_CUTLAB_MEASURE_DATA, with "
                + "DECKFLOW_CUTLAB_MEASURE_CATEGORIES optional. Forward all supplied paths through WSLENV with /p.";
        }
    }
}
