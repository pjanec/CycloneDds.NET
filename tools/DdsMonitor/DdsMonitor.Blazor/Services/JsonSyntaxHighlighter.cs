using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DdsMonitor.Services;

/// <summary>
/// Generates syntax-highlighted HTML for formatted JSON.
/// </summary>
public static class JsonSyntaxHighlighter
{
    private static readonly Regex TokenRegex = new(
        "(?<key>\"(?:\\\\.|[^\\\"])*\"(?=\\s*:))|" +
        "(?<string>\"(?:\\\\.|[^\\\"])*\")|" +
        "(?<number>-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)|" +
        "(?<bool>\\btrue\\b|\\bfalse\\b)|" +
        "(?<null>\\bnull\\b)",
        RegexOptions.Compiled);

    /// <summary>
    /// Converts formatted JSON into HTML with span-based token classes.
    /// </summary>
    public static string ToHtml(string json)
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        var builder = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in TokenRegex.Matches(json))
        {
            AppendEncoded(builder, json.AsSpan(lastIndex, match.Index - lastIndex));

            var cssClass = GetTokenClass(match);
            builder.Append("<span class=\"");
            builder.Append(cssClass);
            builder.Append("\">");
            AppendEncoded(builder, match.Value);
            builder.Append("</span>");

            lastIndex = match.Index + match.Length;
        }

        AppendEncoded(builder, json.AsSpan(lastIndex));
        return builder.ToString();
    }

    private static string GetTokenClass(Match match)
    {
        if (match.Groups["key"].Success)
        {
            return "json-key";
        }

        if (match.Groups["string"].Success)
        {
            return "json-string";
        }

        if (match.Groups["number"].Success)
        {
            return "json-number";
        }

        if (match.Groups["bool"].Success)
        {
            return "json-bool";
        }

        return "json-null";
    }

    private static void AppendEncoded(StringBuilder builder, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        builder.Append(WebUtility.HtmlEncode(text.ToString()));
    }
}
