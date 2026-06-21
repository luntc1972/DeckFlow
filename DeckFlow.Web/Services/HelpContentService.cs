using System.Collections.Concurrent;
using DeckFlow.Web.Models;
using Markdig;
using Microsoft.AspNetCore.Hosting;

namespace DeckFlow.Web.Services;

/// <summary>
/// Loads markdown help topics from disk once and caches rendered HTML.
/// </summary>
public sealed class HelpContentService : IHelpContentService
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    private readonly string _root;
    private readonly Lazy<IReadOnlyList<HelpTopic>> _all;
    private readonly ConcurrentDictionary<string, HelpTopic> _bySlug = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes the help-content cache from the web app Help directory.
    /// </summary>
    /// <param name="environment">Web host environment used to locate the Help directory.</param>
    public HelpContentService(IWebHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "Help"))
    {
    }

    /// <summary>
    /// Initializes the help-content cache from an explicit markdown root path.
    /// </summary>
    /// <param name="rootPath">Directory containing markdown help topics.</param>
    public HelpContentService(string rootPath)
    {
        _root = rootPath;
        _all = new Lazy<IReadOnlyList<HelpTopic>>(LoadAll);
    }

    /// <inheritdoc/>
    public IReadOnlyList<HelpTopic> GetAll() => _all.Value;

    /// <inheritdoc/>
    public HelpTopic? GetBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        _ = _all.Value; // ensure load
        return _bySlug.TryGetValue(slug, out var topic) ? topic : null;
    }

    private IReadOnlyList<HelpTopic> LoadAll()
    {
        if (!Directory.Exists(_root))
            return Array.Empty<HelpTopic>();

        var topics = new List<HelpTopic>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.md", SearchOption.TopDirectoryOnly))
        {
            var slug = Path.GetFileNameWithoutExtension(path);
            var raw = File.ReadAllText(path);
            var (header, body) = ContentArtifactParser.SplitHeader(raw);
            var title = header.GetValueOrDefault("title", slug);
            var summary = header.GetValueOrDefault("summary", string.Empty);
            var order = int.TryParse(header.GetValueOrDefault("order"), out var o) ? o : int.MaxValue;
            // Optional: ties this topic to a feature flag so its help hides with the tool.
            var requiresFlag = header.GetValueOrDefault("requires_flag");
            requiresFlag = string.IsNullOrWhiteSpace(requiresFlag) ? null : requiresFlag.Trim();
            var html = Markdown.ToHtml(body, Pipeline);
            var topic = new HelpTopic(slug, title, summary, order, html, requiresFlag);
            topics.Add(topic);
            _bySlug[slug] = topic;
        }

        return topics
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

}
