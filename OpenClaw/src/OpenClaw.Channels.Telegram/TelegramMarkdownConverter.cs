using System.Text;
using System.Text.RegularExpressions;

namespace OpenClaw.Channels.Telegram;

/// <summary>
/// Converts standard Markdown to Telegram MarkdownV2 format.
/// Telegram MarkdownV2 requires escaping special characters outside of code blocks.
/// </summary>
public static partial class TelegramMarkdownConverter
{
    // Characters that must be escaped in Telegram MarkdownV2
    private const string SpecialChars = @"_*[]()~`>#+-=|{}.!";

    /// <summary>
    /// Converts standard Markdown text to Telegram MarkdownV2 format.
    /// </summary>
    public static string ToTelegramMarkdownV2(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var result = new StringBuilder();
        var i = 0;

        while (i < markdown.Length)
        {
            // Handle fenced code blocks: ```...```
            if (i + 2 < markdown.Length && markdown[i] == '`' && markdown[i + 1] == '`' && markdown[i + 2] == '`')
            {
                var endIndex = markdown.IndexOf("```", i + 3, StringComparison.Ordinal);
                if (endIndex != -1)
                {
                    // Keep code block as-is (Telegram supports ```)
                    result.Append(markdown, i, endIndex + 3 - i);
                    i = endIndex + 3;
                    continue;
                }
            }

            // Handle inline code: `...`
            if (markdown[i] == '`')
            {
                var endIndex = markdown.IndexOf('`', i + 1);
                if (endIndex != -1)
                {
                    // Keep inline code as-is
                    result.Append(markdown, i, endIndex + 1 - i);
                    i = endIndex + 1;
                    continue;
                }
            }

            // Handle bold: **text** → *text*
            if (i + 1 < markdown.Length && markdown[i] == '*' && markdown[i + 1] == '*')
            {
                var endIndex = markdown.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (endIndex != -1)
                {
                    result.Append('*');
                    var inner = markdown.Substring(i + 2, endIndex - i - 2);
                    result.Append(EscapeSpecialChars(inner, excludeBoldItalic: true));
                    result.Append('*');
                    i = endIndex + 2;
                    continue;
                }
            }

            // Handle italic: _text_ (already Telegram format, but need to avoid escaping the underscores)
            if (markdown[i] == '_' && (i == 0 || markdown[i - 1] != '\\'))
            {
                var endIndex = markdown.IndexOf('_', i + 1);
                if (endIndex != -1 && endIndex > i + 1)
                {
                    result.Append("__");
                    var inner = markdown.Substring(i + 1, endIndex - i - 1);
                    result.Append(EscapeSpecialChars(inner, excludeBoldItalic: true));
                    result.Append("__");
                    i = endIndex + 1;
                    continue;
                }
            }

            // Escape special characters outside of formatting
            if (SpecialChars.Contains(markdown[i]))
            {
                result.Append('\\');
            }

            result.Append(markdown[i]);
            i++;
        }

        return result.ToString();
    }

    private static string EscapeSpecialChars(string text, bool excludeBoldItalic = false)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (excludeBoldItalic && (c == '*' || c == '_'))
            {
                sb.Append(c);
                continue;
            }

            if (SpecialChars.Contains(c))
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Splits a message into chunks that fit within Telegram's message length limit.
    /// Tries to split at newline boundaries when possible.
    /// </summary>
    public static IReadOnlyList<string> SplitMessage(string message, int maxLength = 4096)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return [message];

        var chunks = new List<string>();
        var remaining = message.AsSpan();

        while (remaining.Length > 0)
        {
            if (remaining.Length <= maxLength)
            {
                chunks.Add(remaining.ToString());
                break;
            }

            // Try to find a good split point (newline) within the limit
            var splitAt = maxLength;
            var lastNewline = remaining[..maxLength].LastIndexOf('\n');
            if (lastNewline > maxLength / 2) // Only split at newline if it's in the second half
            {
                splitAt = lastNewline + 1;
            }

            chunks.Add(remaining[..splitAt].ToString());
            remaining = remaining[splitAt..];
        }

        return chunks;
    }
}
