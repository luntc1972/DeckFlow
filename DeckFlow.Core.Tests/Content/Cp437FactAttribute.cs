using DeckFlow.Core.Content;
using Xunit;

namespace DeckFlow.Core.Tests.Content;

/// <summary>
/// Marks a Fact that requires a configured private Content KB corpus.
/// </summary>
public sealed class Cp437FactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Cp437FactAttribute"/> class.
    /// </summary>
    public Cp437FactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PrivateKbRoot.EnvironmentVariableName)))
        {
            Skip = "CP437 corpus check requires DECKFLOW_KB_ROOT to point at the private Content KB root.";
        }
    }
}
