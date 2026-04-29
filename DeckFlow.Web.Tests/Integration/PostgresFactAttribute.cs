using System;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Marks a Fact that runs only when DECKFLOW_POSTGRES_TESTS=1 is set in the environment.
/// Default test runs skip these to avoid requiring Docker.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS");
        if (!string.Equals(enabled, "1", StringComparison.Ordinal))
        {
            Skip = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 and ensure Docker is running to enable.";
        }
    }
}
