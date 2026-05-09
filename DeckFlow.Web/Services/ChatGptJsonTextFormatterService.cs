using System;

namespace DeckFlow.Web.Services;

public static class ChatGptJsonTextFormatterService
{
    // Phase 10: shared <result>...</result> wrap directive used by all three
    // ChatGptDeck* services to ensure cross-AI parsing parity.
    internal const string ChatGptResultWrapInstruction =
        "Wrap the entire JSON response in <result>...</result> tags so DeckFlow's parser can extract it uniformly across ChatGPT/Claude/Gemini. The existing fenced ```json code block remains as a fallback — do not remove it.";

    internal static string ExtractJsonPayload(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
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
