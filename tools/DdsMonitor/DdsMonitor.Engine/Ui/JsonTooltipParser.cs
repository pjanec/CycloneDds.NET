using System;
using System.Text.Json;

namespace DdsMonitor.Engine;

/// <summary>
/// Parses and formats JSON strings for tooltip display.
/// </summary>
public static class JsonTooltipParser
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Returns true when the input is valid JSON and produces a formatted version.
    /// </summary>
    public static bool TryFormatJson(string? input, out string formatted)
    {
        formatted = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (!StartsWithJsonToken(trimmed))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            formatted = JsonSerializer.Serialize(document.RootElement, IndentedOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool StartsWithJsonToken(string text)
    {
        return text.StartsWith("{", StringComparison.Ordinal) ||
               text.StartsWith("[", StringComparison.Ordinal);
    }
}
