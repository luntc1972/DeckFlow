using Xunit;

namespace DeckFlow.Web.Tests.Infrastructure;

/// <summary>
/// xUnit collection definition that serializes facts across test classes
/// mutating the process-wide FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD
/// environment variables. Disables parallel execution across BasicAuthMiddlewareTests
/// and AdminBruteForceTrackerStoreTests so their EnvScope.Set/Dispose calls do
/// not race on the shared process-global env-var state.
/// </summary>
[CollectionDefinition("AdminEnvSerial", DisableParallelization = true)]
public sealed class AdminEnvCollection
{
}
