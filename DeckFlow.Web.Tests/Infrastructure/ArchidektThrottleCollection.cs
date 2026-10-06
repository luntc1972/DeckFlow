using Xunit;

namespace DeckFlow.Web.Tests.Infrastructure;

/// <summary>
/// Serializes tests that touch ArchidektThrottle's process-wide static state.
/// </summary>
// Why: 06-02's same-named collection lives in DeckFlow.Core.Tests and xUnit collections do not cross assemblies.
[CollectionDefinition("ArchidektThrottleSerial", DisableParallelization = true)]
public sealed class ArchidektThrottleCollection
{
}
