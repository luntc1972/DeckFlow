using System.Diagnostics;
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
/// Renders an MVC Razor view with the minimal host services required by view-render tests.
/// </summary>
internal static class RazorViewRenderer
{
    internal static async Task<string> RenderAsync(
        object model,
        Type applicationPartType,
        string controllerName,
        IRouter? router = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment(applicationPartType));
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddControllersWithViews().AddApplicationPart(applicationPartType.Assembly);

        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        var routeData = new RouteData(new RouteValueDictionary(new Dictionary<string, object?>
        {
            ["controller"] = controllerName,
        }));

        if (router is not null)
        {
            routeData.Routers.Add(router);
        }

        var action = new ActionContext(context, routeData, new ActionDescriptor());
        var result = provider.GetRequiredService<IRazorViewEngine>().FindView(action, "Index", isMainPage: false);
        Assert.True(result.Success);

        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };
        await using var writer = new StringWriter();
        await result.View!.RenderAsync(new ViewContext(
            action,
            result.View,
            data,
            new TempDataDictionary(context, new StubTempDataProvider()),
            writer,
            new HtmlHelperOptions()));
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment(Type applicationPartType) => new TestWebHostEnvironment
    {
        ApplicationName = applicationPartType.Assembly.GetName().Name ?? "DeckFlow.Web",
        ContentRootPath = AppContext.BaseDirectory,
        ContentRootFileProvider = new NullFileProvider(),
        EnvironmentName = Environments.Development,
        WebRootPath = AppContext.BaseDirectory,
        WebRootFileProvider = new NullFileProvider(),
    };

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
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
