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
        PostgresTestEnvironment.ApplySkip(this);
    }
}

internal static class PostgresTestEnvironment
{
    internal static void ApplySkip(FactAttribute attribute)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS"), "1", StringComparison.Ordinal))
        {
            attribute.Skip = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 and ensure Docker is running to enable.";
        }
    }
}
