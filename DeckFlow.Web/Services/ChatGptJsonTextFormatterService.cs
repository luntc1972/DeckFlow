using System;
using System.Text.RegularExpressions;

namespace DeckFlow.Web.Services;

public static class ChatGptJsonTextFormatterService
{
    // Phase 10: shared <result>...</result> wrap directive used by all three
    // ChatGptDeck* services to ensure cross-AI parsing parity.
    internal const string ChatGptResultWrapInstruction =
        "Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.";

    // Phase 10 (post-verify hardening): Gemini ignored the JSON output requirement
    // when integration-tested 2026-05-09 — it produced thorough readable prose
    // following the A./B./C./D. format guidance and stopped without emitting the
    // <result> wrapper. Append this imperative as the absolute last instruction
    // in every Gemini variant so "do prose first" doesn't crowd out "JSON is
    // mandatory". Last-instruction-wins is the strongest lever for instruction-
    // tuned models when no API-level structured-output enforcement is available
    // (paste-into-gemini.google.com flow).
    internal const string GeminiJsonMandate =
        "MANDATORY — DO NOT SKIP: Your response MUST end with a <result>...</result> block containing a single JSON object that matches the schema above. The JSON block is REQUIRED even if you have already produced a complete readable analysis — without it your response is invalid and DeckFlow will reject the upload. Do not summarise. Do not say \"and the JSON is...\". Output the literal <result> tag, then the JSON object, then </result>. Nothing else after </result>.";

    /// <summary>
    /// Replace embedded newlines with spaces and trim. Used by request-context
    /// writers (Comparison + CedhMetaGap) to keep round-trip envelope values
    /// on a single line. ChatGptDeckPacketService has its own private variant
    /// that additionally collapses multi-line input via CollapseWhitespace —
    /// migrating those callsites is a v1.3 follow-up.
    /// </summary>
    internal static string NormalizeSingleLine(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        return value.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

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
