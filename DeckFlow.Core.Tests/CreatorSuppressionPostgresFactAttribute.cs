using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Marks a fact requiring an explicitly enabled and configured PostgreSQL test database.
/// </summary>
public sealed class CreatorSuppressionPostgresFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatorSuppressionPostgresFactAttribute"/> class.
    /// </summary>
    public CreatorSuppressionPostgresFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 to enable.";
        }
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DECKFLOW_TEST_POSTGRES")))
        {
            Skip = "DECKFLOW_TEST_POSTGRES must provide the live Postgres test connection string.";
        }
    }
}
