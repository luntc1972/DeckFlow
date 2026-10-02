using Microsoft.Extensions.Internal;

namespace DeckFlow.Web.Tests.TestDoubles;

internal sealed class TimeProviderSystemClock(TimeProvider timeProvider) : ISystemClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
