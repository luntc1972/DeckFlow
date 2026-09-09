using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services;
using DeckFlow.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Proves PTOOL-01 / D-04: <c>/Admin/CreatorStyle</c> is refused unauthenticated and reachable
/// authenticated, both through the direct middleware construction (mirroring
/// <see cref="BasicAuthMiddlewareTests"/>) and — for the one fact that exercises the whole chain
/// together — through the real, unmodified <see cref="Program.BuildApp"/> composition root.
/// </summary>
[Collection("AdminEnvSerial")]
public sealed class AdminCreatorStyleControllerAuthTests
{
    private const string EnvUser = "FEEDBACK_ADMIN_USER";
    private const string EnvPass = "FEEDBACK_ADMIN_PASSWORD";
    private const string CreatorStylePath = "/Admin/CreatorStyle";

    private static AdminBruteForceTrackerStore CreateStore(out string dbPath)
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"admin-creator-style-auth-test-{Guid.NewGuid():N}.db");
        return new AdminBruteForceTrackerStore(dbPath);
    }

    private static void CleanUp(string dbPath)
    {
        try
        {
            File.Delete(dbPath);
        }
        catch (IOException)
        {
            // Why: best-effort temp-file cleanup; a lingering handle must not fail the test.
        }
    }

    [Fact]
    public async Task CreatorStylePath_NoAuthHeader_Returns401WithChallenge_AndDoesNotInvokeNext()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext { Request = { Path = CreatorStylePath, Method = HttpMethods.Get } };
        var store = CreateStore(out var dbPath);
        try
        {
            var nextCalled = false;
            var middleware = new BasicAuthMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                NullLogger<BasicAuthMiddleware>.Instance,
                "DeckFlow Admin",
                store);

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Assert.Contains("Basic", context.Response.Headers["WWW-Authenticate"].ToString());
            Assert.False(nextCalled);
        }
        finally
        {
            CleanUp(dbPath);
        }
    }

    [Fact]
    public async Task CreatorStylePath_CorrectCredentials_InvokesNext_AndIsNot401()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext { Request = { Path = CreatorStylePath, Method = HttpMethods.Get } };
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
        context.Request.Headers["Authorization"] = $"Basic {encoded}";
        var store = CreateStore(out var dbPath);
        try
        {
            var nextCalled = false;
            var middleware = new BasicAuthMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                NullLogger<BasicAuthMiddleware>.Instance,
                "DeckFlow Admin",
                store);

            await middleware.InvokeAsync(context);

            Assert.True(nextCalled);
            Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }
        finally
        {
            CleanUp(dbPath);
        }
    }

    [Fact]
    public async Task CreatorStylePath_WrongPassword_Returns401()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext { Request = { Path = CreatorStylePath, Method = HttpMethods.Get } };
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
        context.Request.Headers["Authorization"] = $"Basic {encoded}";
        var store = CreateStore(out var dbPath);
        try
        {
            var middleware = new BasicAuthMiddleware(
                _ => Task.CompletedTask,
                NullLogger<BasicAuthMiddleware>.Instance,
                "DeckFlow Admin",
                store);

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }
        finally
        {
            CleanUp(dbPath);
        }
    }

    // Why (D-04): the two facts above and AdminCreatorStyleViewRenderTests / ProgramStartupTests
    // each prove one layer in isolation (middleware, controller+view, DI wiring). This fact is the
    // one that boots the real, unmodified Program.BuildApp composition root — real Program.cs
    // registrations, middleware ordering, routing, controller activation, and Razor rendering
    // together — and drives real HTTP requests at it with a plain HttpClient, on an ephemeral
    // loopback port, with no environment variable set (default environment; the DECKFLOW_CREATOR_
    // STYLE_ENABLED gate this checkpoint removed no longer applies). No Microsoft.AspNetCore.Mvc.
    // Testing / Microsoft.AspNetCore.TestHost dependency is added — see BuildApp's xmldoc.
    [Fact]
    public async Task CreatorStylePath_ThroughRealHost_UnauthenticatedRefusedAuthenticatedRenders()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");

        var app = Program.BuildApp(new[] { "--urls", "http://127.0.0.1:0" });
        try
        {
            await app.StartAsync();

            var baseAddress = app.Urls.First();
            using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

            using var unauthenticatedResponse = await client.GetAsync(CreatorStylePath);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);
            Assert.True(unauthenticatedResponse.Headers.WwwAuthenticate.Any());

            using var authenticatedRequest = new HttpRequestMessage(HttpMethod.Get, CreatorStylePath);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
            authenticatedRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
            using var authenticatedResponse = await client.SendAsync(authenticatedRequest);
            Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);

            var body = await authenticatedResponse.Content.ReadAsStringAsync();
            Assert.Contains("id=\"creator-style-slug\"", body, StringComparison.Ordinal);
            Assert.Contains("id=\"creator-style-deck-url\"", body, StringComparison.Ordinal);
            Assert.Contains("id=\"creator-style-deck-text\"", body, StringComparison.Ordinal);
            Assert.Contains(AdminCreatorStyleController.NoProfilesSeededMessage, body, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
