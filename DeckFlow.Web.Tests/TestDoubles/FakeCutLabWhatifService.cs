using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Test fake for <see cref="ICutLabWhatifService"/>, shared by the JSON-API and no-JS Cut Lab
/// controller tests so both transports exercise the same double. Defaults are permissive:
/// <see cref="TryValidateSwap"/> accepts every pair and <see cref="CommitSwapAsync"/> reports
/// not-applied, so a test only configures the member it is actually driving.
/// </summary>
internal sealed class FakeCutLabWhatifService : ICutLabWhatifService
{
    public delegate bool TryValidateSwapCallback(CutLabState state, string cardOut, string cardIn, out string? error);

    /// <summary>Preview payload returned by <see cref="PreviewSwapAsync"/>; unset card names echo the request.</summary>
    public CutLabWhatifPreview Preview { get; set; } = new();

    /// <summary>Overrides the validation verdict; when null every pair is accepted.</summary>
    public TryValidateSwapCallback? TryValidateSwapHandler { get; set; }

    /// <summary>Overrides the commit outcome; when null the swap reports not-applied.</summary>
    public Func<CutLabState, string, string, CutLabWhatifCommitResult>? CommitResultFactory { get; set; }

    public Task<CutLabWhatifPreview> PreviewSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken)
        => Task.FromResult(Preview with
        {
            CardOut = Preview.CardOut == string.Empty ? cardOut : Preview.CardOut,
            CardIn = Preview.CardIn == string.Empty ? cardIn : Preview.CardIn,
        });

    public bool TryValidateSwap(CutLabState state, string cardOut, string cardIn, [NotNullWhen(false)] out string? error)
    {
        if (TryValidateSwapHandler is not null)
        {
            // Why: the handler delegate is unannotated, so normalise here to honour the
            // interface's [NotNullWhen(false)] contract without annotating every test lambda.
            bool valid = TryValidateSwapHandler(state, cardOut, cardIn, out string? handlerError);
            error = valid ? null : handlerError ?? CutLabMessages.NoChangeMessage;
            return valid;
        }

        error = null;
        return true;
    }

    public Task<CutLabWhatifCommitResult> CommitSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken)
        => Task.FromResult(CommitResultFactory?.Invoke(state, cardOut, cardIn) ?? new CutLabWhatifCommitResult
        {
            Applied = false,
            State = state,
        });
}
