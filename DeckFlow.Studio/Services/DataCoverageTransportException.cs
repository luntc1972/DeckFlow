namespace DeckFlow.Studio.Services;

/// <summary>Indicates that an audit download failed over SSH without exposing transport details.</summary>
public sealed class DataCoverageTransportException : Exception
{
    /// <summary>Safe operator-facing explanation for an SSH download failure.</summary>
    public const string OperatorMessage = "A /data download failed over SSH; check the SSH connection. No coverage result was recorded.";

    /// <summary>Creates the sanitized transport failure.</summary>
    public DataCoverageTransportException()
        : base(OperatorMessage)
    {
    }
}
