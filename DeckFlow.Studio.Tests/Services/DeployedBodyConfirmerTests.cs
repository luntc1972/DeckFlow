using System.Net;
using System.Text;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using RichardSzalay.MockHttp;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="DeployedBodyConfirmer"/> — the bounded hash-match poll of the Plan
/// 90-07 deployed-body-hash endpoint (SYNC-09 / D-09 REVISED). All HTTP is stubbed via
/// <see cref="MockHttpMessageHandler"/>; no live network call is ever made.
/// </summary>
public sealed class DeployedBodyConfirmerTests
{
    private const string BaseUrl = "https://deckflow.example.test";
    private const string ExpectedHash = "abc123hash";

    private static IConfiguration BuildConfiguration(
        string? baseUrl = BaseUrl,
        string? user = "admin",
        string? password = "secret")
    {
        var dict = new Dictionary<string, string?>();
        if (baseUrl is not null)
        {
            dict["Studio:PublicSiteBaseUrl"] = baseUrl;
        }

        if (user is not null)
        {
            dict["Studio:AdminUser"] = user;
        }

        if (password is not null)
        {
            dict["Studio:AdminPassword"] = password;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task IsDeployedBodyConfirmedAsync_MatchingHash_ReturnsTrue_SendsBasicAuthAndNaturalKey()
    {
        var mockHttp = new MockHttpMessageHandler();
        var route = mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/Admin/api/contentkb/deployed-body-hash*")
            .With(req =>
                req.Headers.Authorization is { Scheme: "Basic" } auth
                && auth.Parameter == Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"))
                && req.RequestUri!.Query.Contains("naturalKeyType=youtube", StringComparison.OrdinalIgnoreCase)
                && req.RequestUri!.Query.Contains("naturalKeyValue=abc", StringComparison.OrdinalIgnoreCase))
            .Respond(HttpStatusCode.OK, "application/json", $"{{\"bodySha256\":\"{ExpectedHash}\"}}");

        var confirmer = new DeployedBodyConfirmer(
            mockHttp.ToHttpClient(), BuildConfiguration(), maxAttempts: 3, retryDelay: TimeSpan.FromMilliseconds(1));

        var result = await confirmer.IsDeployedBodyConfirmedAsync(
            "youtube", "abc", ExpectedHash, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, mockHttp.GetMatchCount(route));
    }

    [Fact]
    public async Task IsDeployedBodyConfirmedAsync_HashMismatch_RetriesThenReturnsFalse()
    {
        var mockHttp = new MockHttpMessageHandler();
        var route = mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/Admin/api/contentkb/deployed-body-hash*")
            .Respond(HttpStatusCode.OK, "application/json", "{\"bodySha256\":\"old-stale-hash\"}");

        var confirmer = new DeployedBodyConfirmer(
            mockHttp.ToHttpClient(), BuildConfiguration(), maxAttempts: 3, retryDelay: TimeSpan.FromMilliseconds(1));

        var result = await confirmer.IsDeployedBodyConfirmedAsync(
            "youtube", "abc", ExpectedHash, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(3, mockHttp.GetMatchCount(route));
    }

    [Fact]
    public async Task IsDeployedBodyConfirmedAsync_404_RetriesThenReturnsFalse()
    {
        var mockHttp = new MockHttpMessageHandler();
        var route = mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/Admin/api/contentkb/deployed-body-hash*")
            .Respond(HttpStatusCode.NotFound);

        var confirmer = new DeployedBodyConfirmer(
            mockHttp.ToHttpClient(), BuildConfiguration(), maxAttempts: 3, retryDelay: TimeSpan.FromMilliseconds(1));

        var result = await confirmer.IsDeployedBodyConfirmedAsync(
            "youtube", "abc", ExpectedHash, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(3, mockHttp.GetMatchCount(route));
    }

    [Fact]
    public async Task IsDeployedBodyConfirmedAsync_MismatchThenMatch_ConfirmsOnRetry()
    {
        // Simulates a redeploy landing between two poll attempts — the confirmer must keep polling
        // past a mismatch (not treat it as a hard failure) and succeed once the hash matches.
        var mockHttp = new MockHttpMessageHandler();
        var callCount = 0;
        var route = mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/Admin/api/contentkb/deployed-body-hash*")
            .Respond(_ =>
            {
                callCount++;
                var hash = callCount < 2 ? "old-stale-hash" : ExpectedHash;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"bodySha256\":\"{hash}\"}}", Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            });

        var confirmer = new DeployedBodyConfirmer(
            mockHttp.ToHttpClient(), BuildConfiguration(), maxAttempts: 3, retryDelay: TimeSpan.FromMilliseconds(1));

        var result = await confirmer.IsDeployedBodyConfirmedAsync(
            "youtube", "abc", ExpectedHash, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, callCount);
        Assert.Equal(2, mockHttp.GetMatchCount(route));
    }

    [Theory]
    [InlineData(null, "admin", "secret")]
    [InlineData(BaseUrl, null, "secret")]
    [InlineData(BaseUrl, "admin", null)]
    public async Task IsDeployedBodyConfirmedAsync_MissingConfig_ReturnsFalse_NoHttpCall(
        string? baseUrl, string? user, string? password)
    {
        var mockHttp = new MockHttpMessageHandler();
        var route = mockHttp.When("*").Respond(HttpStatusCode.OK, "application/json", "{}");

        var confirmer = new DeployedBodyConfirmer(
            mockHttp.ToHttpClient(),
            BuildConfiguration(baseUrl, user, password),
            maxAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(1));

        var result = await confirmer.IsDeployedBodyConfirmedAsync(
            "youtube", "abc", ExpectedHash, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, mockHttp.GetMatchCount(route));
    }
}
