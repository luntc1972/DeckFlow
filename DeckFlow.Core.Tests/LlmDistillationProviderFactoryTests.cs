using DeckFlow.Core.Integration;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class LlmDistillationProviderFactoryTests
{
    [Fact]
    public void Resolve_NullOrEmpty_ReturnsOpenAiImpl()
    {
        using var httpClient = new HttpClient();

        Assert.IsType<LlmDistillationService>(LlmDistillationProviderFactory.Resolve(null, httpClient));
        Assert.IsType<LlmDistillationService>(LlmDistillationProviderFactory.Resolve("", httpClient));
    }

    [Fact]
    public void Resolve_OpenAi_ReturnsOpenAiImpl()
    {
        using var httpClient = new HttpClient();

        var service = LlmDistillationProviderFactory.Resolve("openai", httpClient);

        Assert.IsType<LlmDistillationService>(service);
    }

    [Fact]
    public void Resolve_Claude_ReturnsCliImpl()
    {
        using var httpClient = new HttpClient();

        var service = LlmDistillationProviderFactory.Resolve("claude", httpClient);

        Assert.IsType<CliLlmDistillationService>(service);
    }

    [Fact]
    public void Resolve_ClaudeUpper_ReturnsCliImpl()
    {
        using var httpClient = new HttpClient();

        var service = LlmDistillationProviderFactory.Resolve("CLAUDE", httpClient);

        Assert.IsType<CliLlmDistillationService>(service);
    }

    [Fact]
    public void Resolve_Codex_ThrowsNotSupportedPointingAt213()
    {
        using var httpClient = new HttpClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => LlmDistillationProviderFactory.Resolve("codex", httpClient));

        Assert.Contains("21.3", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_Unknown_ThrowsNotSupportedListingSupported()
    {
        using var httpClient = new HttpClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => LlmDistillationProviderFactory.Resolve("gemini", httpClient));

        Assert.Contains("openai", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("claude", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_NullHttpClient_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => LlmDistillationProviderFactory.Resolve("openai", null!));

        Assert.Equal("httpClient", exception.ParamName);
    }
}
