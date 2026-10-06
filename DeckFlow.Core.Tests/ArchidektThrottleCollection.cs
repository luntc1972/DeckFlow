namespace DeckFlow.Core.Tests;

/// <summary>Serializes classes that share ArchidektThrottle's static state.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ArchidektThrottleCollection
{
    public const string Name = "ArchidektThrottleSerial";
}
