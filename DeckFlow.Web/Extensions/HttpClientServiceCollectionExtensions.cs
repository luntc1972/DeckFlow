using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the DeckFlow HTTP-client infrastructure.
/// Extracts the named-client + typed-client + CookieContainer wiring from Program.cs
/// so Main stays focused on application-level composition.
/// </summary>
public static class HttpClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers all named HttpClients (commander-banlist, commander-spellbook, scryfall-rest,
    /// scryfall-tagger, youtube-metadata), the <see cref="System.Net.CookieContainer"/> singleton,
    /// and the <see cref="ScryfallTaggerHttpClient"/> typed-client singletons.
    /// </summary>
    /// <remarks>
    /// Call this before <c>AddDeckFlowResiliencePipelines()</c> so the named clients exist when
    /// the Polly pipeline factory wires up its HttpClient-based pipelines.
    /// </remarks>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowHttpClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient("commander-banlist", c =>
        {
            c.BaseAddress = new Uri("https://mtgcommander.net/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
        });

        services.AddHttpClient("edhrec", c =>
        {
            c.BaseAddress = new Uri("https://json.edhrec.com/pages/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
            c.MaxResponseContentBufferSize = EdhrecCommanderThemeService.MaxResponseBytes;
            c.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient("commander-spellbook", c =>
        {
            c.BaseAddress = new Uri("https://backend.commanderspellbook.com/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
        });

        services.AddHttpClient("scryfall-rest", c =>
        {
            c.BaseAddress = new Uri("https://api.scryfall.com/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0 (+https://github.com/luntc1972/DeckFlow)");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json;q=0.9,*/*;q=0.8");
        });

        // Typed client for Tagger — automatic cookie handling via SocketsHttpHandler CookieContainer
        // per Phase 5 BUG-01 fix. The session cookie set by GET /card/{set}/{num} is replayed
        // automatically on the subsequent POST /graphql by the same handler, removing the need
        // for manual Cookie header construction (which the pre-Phase-5 manual replay path
        // failed to deliver under RestSharp 114 + redirect-disabled config — see 04-ABANDONED.md).
        // HandlerLifetime = 5 min. TaggerSessionCache TTL = 270s (30s below HandlerLifetime)
        // so the cached CSRF token expires before the underlying handler+cookie pair rotates
        // (HIGH-2 invariant — DO NOT lower the 30s margin).
        //
        // The CookieContainer is registered as a singleton so ScryfallTaggerHttpClient can
        // expose it for diagnostic logging (Tagger.SessionFetch log line {CookieCount} slot).
        // The SocketsHttpHandler factory below resolves and uses the same instance, so reads
        // through the typed wrapper reflect the live session state.
        services.AddSingleton<System.Net.CookieContainer>();
        services.AddHttpClient("scryfall-tagger", c =>
        {
            c.BaseAddress = new Uri("https://tagger.scryfall.com/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0 (+https://www.deckflow.gg)");
            // Phase 5 BUG-01 follow-up: browser-mimicking request headers to clear
            // Cloudflare's Browser Integrity Check on tagger.scryfall.com. The host
            // appears to 404 requests from Render's egress IP that lack these signals,
            // even though the same UA from a residential IP succeeds. UA stays descriptive
            // per Scryfall API-consumer guidelines and now also carries a contact URL so
            // Scryfall can reach the operator if needed.
            c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
            c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
            c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
            c.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            c.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            c.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            c.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            c.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        })
        .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
        {
            UseCookies = true,
            AllowAutoRedirect = true,
            CookieContainer = sp.GetRequiredService<System.Net.CookieContainer>(),
            // Phase 5 BUG-01 follow-up #2: we advertise Accept-Encoding: gzip,deflate,br
            // (browser-mimicking BIC bypass), so the upstream WILL compress responses.
            // Decompression must be enabled or pageResponse.Content is binary
            // garbage and TryExtractCsrfToken returns null (csrf=False in the
            // Tagger.SessionFetch log).
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                | System.Net.DecompressionMethods.Deflate
                | System.Net.DecompressionMethods.Brotli,
            PooledConnectionLifetime = TaggerSessionCache.HandlerLifetime,
        })
        .SetHandlerLifetime(TaggerSessionCache.HandlerLifetime);

        services.AddSingleton<ScryfallTaggerHttpClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient("scryfall-tagger");
            var cookies = sp.GetRequiredService<System.Net.CookieContainer>();
            return new ScryfallTaggerHttpClient(http, cookies);
        });
        services.AddSingleton<IScryfallTaggerHttpClient>(sp => sp.GetRequiredService<ScryfallTaggerHttpClient>());

        // Admin YouTube export: transient lister so each request gets a factory-managed
        // HttpClient (handler rotation) for the per-video YoutubeExplode metadata calls.
        services.AddHttpClient("youtube-metadata", c => c.Timeout = TimeSpan.FromMinutes(5));
        services.AddTransient<DeckFlow.Core.Integration.IYouTubeChannelVideoLister>(sp =>
            new DeckFlow.Core.Integration.YouTubeChannelVideoLister(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("youtube-metadata")));

        return services;
    }
}
