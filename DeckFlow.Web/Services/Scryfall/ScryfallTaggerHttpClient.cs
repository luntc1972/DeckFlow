using System.Net;
using System.Net.Http;

namespace DeckFlow.Web.Services;

/// <summary>
/// Typed HttpClient wrapper for tagger.scryfall.com. Phase 5 BUG-01 adds an optional
/// CookieContainer accessor so callers can read cookie state for diagnostic logging
/// without reaching into the underlying SocketsHttpHandler. The CookieContainer is
/// supplied by Program.cs at registration time (the same instance owned by the handler
/// configured with UseCookies=true), so reads here reflect live session state.
/// </summary>
public interface IScryfallTaggerHttpClient
{
    /// <summary>
    /// The underlying <see cref="HttpClient"/>, intended to be wrapped by RestSharp via
    /// <c>new RestClient(taggerHttpClient.Inner)</c> at the call site.
    /// </summary>
    HttpClient Inner { get; }

    /// <summary>
    /// CookieContainer shared with the SocketsHttpHandler primary handler (Program.cs).
    /// Reads reflect live session state for the Tagger BaseAddress.
    /// </summary>
    CookieContainer Cookies { get; }
}

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the Scryfall Tagger flow. Registered in
/// Program.cs with a primary <see cref="System.Net.Http.SocketsHttpHandler"/> configured
/// with <c>UseCookies=true</c> + <c>AllowAutoRedirect=true</c> (Phase 5 BUG-01).
/// </summary>
public sealed class ScryfallTaggerHttpClient : IScryfallTaggerHttpClient
{
    /// <summary>Back-compat single-arg ctor (creates an empty CookieContainer).</summary>
    public ScryfallTaggerHttpClient(HttpClient httpClient)
        : this(httpClient, new CookieContainer())
    {
    }

    /// <summary>Primary ctor — caller supplies the shared CookieContainer instance.</summary>
    public ScryfallTaggerHttpClient(HttpClient httpClient, CookieContainer cookies)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(cookies);
        Inner = httpClient;
        Cookies = cookies;
    }

    /// <inheritdoc />
    public HttpClient Inner { get; }

    /// <inheritdoc />
    public CookieContainer Cookies { get; }
}
