namespace DeckFlow.Core.Integration;

/// <summary>
/// Process launch specification for a CLI-backed LLM distillation provider.
/// </summary>
/// <param name="FileName">Executable file name.</param>
/// <param name="ArgumentList">Arguments passed with <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList" />.</param>
/// <param name="EnvelopeKind">Output envelope shape emitted by the CLI.</param>
public sealed record CliCommandSpec(
    string FileName,
    IReadOnlyList<string> ArgumentList,
    CliEnvelopeKind EnvelopeKind);

/// <summary>
/// Output envelope shape emitted by a CLI-backed LLM provider.
/// </summary>
public enum CliEnvelopeKind
{
    /// <summary>Claude JSON envelope with model text in the result field.</summary>
    ClaudeJson,

    /// <summary>Raw stdout containing model text.</summary>
    Raw
}
