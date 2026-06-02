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

    public HelpContentService(IWebHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "Help"))
    {
    }

    public HelpContentService(string rootPath)
    {
        _root = rootPath;
        _all = new Lazy<IReadOnlyList<HelpTopic>>(LoadAll);
    }

    public IReadOnlyList<HelpTopic> GetAll() => _all.Value;

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
            var html = Markdown.ToHtml(body, Pipeline);
            var topic = new HelpTopic(slug, title, summary, order, html);
            topics.Add(topic);
            _bySlug[slug] = topic;
        }

        return topics
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

}
