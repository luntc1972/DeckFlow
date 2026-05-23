using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for <see cref="JsonTextFormatterService.ExtractJsonPayload"/> verifying that the
/// AI-response payload extraction correctly handles &lt;result&gt; wrappers, prose surroundings,
/// legacy fenced JSON, nested braces, and edge cases like dangling tags.
/// </summary>
public sealed class JsonTextFormatterServiceTests
{
    [Fact]
    public void ExtractJsonPayload_returns_input_when_whitespace()
    {
        Assert.Equal(string.Empty, JsonTextFormatterService.ExtractJsonPayload(string.Empty));
        Assert.Equal(string.Empty, JsonTextFormatterService.ExtractJsonPayload("   \n  "));
    }

    [Fact]
    public void ExtractJsonPayload_extracts_inner_when_result_tag_wraps_json()
    {
        var input = "<result>{\"key\":\"value\"}</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"key\":\"value\"}", result);
    }

    [Fact]
    public void ExtractJsonPayload_strips_prose_outside_result_tag()
    {
        var input = "Here is my analysis prose.\n\n<result>{\"deck_profile\":{}}</result>\n\nAnd commentary after.";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"deck_profile\":{}}", result);
    }

    [Fact]
    public void ExtractJsonPayload_handles_newlines_inside_result_tag_via_singleline()
    {
        var input = "<result>\n{\n  \"a\": 1,\n  \"b\": 2\n}\n</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Contains("\"a\": 1", result);
        Assert.Contains("\"b\": 2", result);
        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
    }

    [Fact]
    public void ExtractJsonPayload_first_match_wins_when_multiple_result_pairs()
    {
        var input = "<result>{\"first\":true}</result>\n<result>{\"second\":true}</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"first\":true}", result);
        Assert.DoesNotContain("second", result);
    }

    [Fact]
    public void ExtractJsonPayload_falls_through_when_only_open_tag_present()
    {
        var input = "<result>{\"only_open\":true}";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"only_open\":true}", result);
    }

    [Fact]
    public void ExtractJsonPayload_falls_through_when_only_close_tag_present()
    {
        var input = "{\"only_close\":true}</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"only_close\":true}", result);
    }

    [Fact]
    public void ExtractJsonPayload_falls_through_when_result_tag_is_empty()
    {
        var input = "<result>   </result>{\"actual\":\"json\"}";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"actual\":\"json\"}", result);
    }

    [Fact]
    public void ExtractJsonPayload_legacy_fenced_json_still_parses_without_result_tag()
    {
        var input = "Some intro.\n```json\n{\"legacy\":\"fenced\"}\n```\nTrailing prose.";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"legacy\":\"fenced\"}", result);
    }

    [Fact]
    public void ExtractJsonPayload_legacy_raw_json_still_parses_without_result_tag()
    {
        var input = "{\"raw\":\"json\"}";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"raw\":\"json\"}", result);
    }

    [Fact]
    public void ExtractJsonPayload_handles_array_payload_inside_result_tag()
    {
        var input = "<result>[{\"a\":1},{\"b\":2}]</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("[{\"a\":1},{\"b\":2}]", result);
    }

    [Fact]
    public void ExtractJsonPayload_strips_surrounding_whitespace_inside_result_tag()
    {
        var input = "<result>\n\n   {\"trimmed\":true}   \n\n</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"trimmed\":true}", result);
    }

    [Fact]
    public void ExtractJsonPayload_handles_nested_braces_inside_result_tag()
    {
        var input = "<result>{\"outer\":{\"inner\":{\"deep\":true}}}</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"outer\":{\"inner\":{\"deep\":true}}}", result);
    }

    [Fact]
    public void ExtractJsonPayload_handles_string_with_braces_inside_json()
    {
        var input = "<result>{\"text\":\"contains } and { chars\",\"valid\":true}</result>";
        var result = JsonTextFormatterService.ExtractJsonPayload(input);
        Assert.Equal("{\"text\":\"contains } and { chars\",\"valid\":true}", result);
    }
}
