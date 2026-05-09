using System;
using System.Text.RegularExpressions;

namespace DeckFlow.Web.Services;

public static class ChatGptJsonTextFormatterService
{
    // Phase 10: shared <result>...</result> wrap directive used by all three
    // ChatGptDeck* services to ensure cross-AI parsing parity.
    internal const string ChatGptResultWrapInstruction =
        "Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.";

    // Phase 10: unified <result>...</result> wrapper across ChatGPT/Claude/Gemini.
    // Lazy quantifier (.*?) ensures FIRST matching pair wins if user-pasted text
    // contains stray <result> tokens. Singleline so dot matches newlines.
    private static readonly Regex ResultTagRegex = new(
        @"<result>\s*(.*?)\s*</result>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    internal static string ExtractJsonPayload(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        // Phase 10: prefer <result>...</result> wrapper.
        // Falls through to the existing brace-finding extraction on miss so
        // legacy artifacts (no wrapper) and ChatGPT-fenced-only responses
        // keep working unchanged.
        var resultMatch = ResultTagRegex.Match(trimmed);
        if (resultMatch.Success && !string.IsNullOrWhiteSpace(resultMatch.Groups[1].Value))
        {
            trimmed = resultMatch.Groups[1].Value.Trim();
        }

        var payloadStart = FindPayloadStart(trimmed);
        if (payloadStart < 0)
        {
            return trimmed;
        }

        var payloadEnd = FindPayloadEnd(trimmed, payloadStart);
        if (payloadEnd < 0)
        {
            return trimmed[payloadStart..].Trim();
        }

        return trimmed[payloadStart..(payloadEnd + 1)].Trim();
    }

    private static int FindPayloadStart(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is '{' or '[')
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindPayloadEnd(string text, int startIndex)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = startIndex; index < text.Length; index++)
        {
            var character = text[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character is '{' or '[')
            {
                depth++;
                continue;
            }

            if (character is '}' or ']')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }
}
