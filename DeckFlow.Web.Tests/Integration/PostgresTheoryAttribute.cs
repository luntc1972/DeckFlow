using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>Marks a Theory that runs only when PostgreSQL integration testing is enabled.</summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        PostgresTestEnvironment.ApplySkip(this);
    }
}
