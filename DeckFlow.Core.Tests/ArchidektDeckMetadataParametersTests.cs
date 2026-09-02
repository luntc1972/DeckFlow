using System.Globalization;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests;

public sealed class ArchidektDeckMetadataParametersTests
{
    [Fact]
    public void From_Metadata_MapsDialectNeutralValues()
    {
        var capturedUtc = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.FromHours(-6));
        var metadata = new ArchidektDeckMetadata(3, 1, true, capturedUtc, null, capturedUtc);

        ArchidektDeckMetadataParameters parameters = ArchidektDeckMetadataParameters.From(metadata);

        Assert.Equal(3, parameters.EdhBracket);
        Assert.Equal(1, parameters.DeckFormat);
        Assert.Equal(1, parameters.Theorycrafted);
        Assert.IsType<string>(parameters.CreatedUtc);
        Assert.Equal(capturedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), parameters.CreatedUtc);
        Assert.Null(parameters.UpdatedUtc);
        Assert.Equal(capturedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), parameters.CapturedUtc);
    }
}
