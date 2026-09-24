using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Render-level contract tests for the shared deck import control partial.
/// </summary>
public sealed class DeckImportControlPartialTests
{
    [Fact]
    public async Task RenderAsync_PublicUrl_RendersUrlPanelAndSelectedPublicUrlOption()
    {
        string html = await RenderAsync(new DeckImportControlModel("deck-history", "deck-history", "Paste a decklist", Source: DeckInputSource.PublicUrl));

        Assert.Contains("<select id=\"deck-history-input-source\" name=\"DeckInputSource\" data-df-select>", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("<option value=\"PublicUrl\" selected=\"selected\">Use public deck URL</option>", StringComparison.Ordinal) < html.IndexOf("<option value=\"PasteText\">Paste text</option>", StringComparison.Ordinal));
        Assert.Contains("data-sync-panel=\"deck-history-deck-url\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"field hidden\" data-sync-panel=\"deck-history-deck-url\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"field hidden\" data-sync-panel=\"deck-history-deck-text\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WithoutSource_DefaultsToPublicUrl()
    {
        string html = await RenderAsync(new DeckImportControlModel("deck-history", "deck-history", "Paste a decklist"));

        Assert.Contains("<option value=\"PublicUrl\" selected=\"selected\">Use public deck URL</option>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"field hidden\" data-sync-panel=\"deck-history-deck-text\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_PasteText_RendersTextPanelAndSelectedPasteTextOption()
    {
        string html = await RenderAsync(new DeckImportControlModel("bracket", "bracket", "Paste a decklist", Source: DeckInputSource.PasteText));

        Assert.Contains("<option value=\"PublicUrl\">Use public deck URL</option>", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"PasteText\" selected=\"selected\">Paste text</option>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"field hidden\" data-sync-panel=\"bracket-deck-url\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"field hidden\" data-sync-panel=\"bracket-deck-text\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_ValuesAndLabel_RendersEncodedValuesAndRequiredSharedMarkup()
    {
        string html = await RenderAsync(new DeckImportControlModel(
            "deck-history",
            "bracket",
            "Paste a decklist from Arena",
            "<script>url</script>",
            "<script>text</script>"));

        Assert.Contains("id=\"deck-history-deck-url\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"deck-history-deck-text\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sync-panel=\"bracket-deck-url\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sync-panel=\"bracket-deck-text\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckUrl\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckText\"", html, StringComparison.Ordinal);
        Assert.Contains("Paste a decklist from Arena", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;url&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;text&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("class=\"deckflow-bridge-hint\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-clear-cache", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(DeckImportControlModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckPacketController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "_DeckImportControl", isMainPage: false);
        Assert.True(viewResult.Success, $"View '_DeckImportControl' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View!, viewData, new TempDataDictionary(httpContext, new StubTempDataProvider()), writer, new HtmlHelperOptions());
        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(DeckPacketController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = AppContext.BaseDirectory,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = AppContext.BaseDirectory,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
