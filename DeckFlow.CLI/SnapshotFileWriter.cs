using System.Text;

namespace DeckFlow.CLI;

internal static class SnapshotFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteLfFile(string path, string content)
    {
        string body = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!body.EndsWith('\n'))
        {
            body += "\n";
        }

        File.WriteAllText(path, body, Utf8NoBom);
    }
}
