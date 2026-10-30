using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Tests;

internal sealed class UnusedCutLabRoleEditService : ICutLabRoleEditService
{
    public Task<CutLabRoleEditResult> ApplyAsync(CutLabState state, string cardName, string? role, CutLabRoleOverrideOperation operation, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
