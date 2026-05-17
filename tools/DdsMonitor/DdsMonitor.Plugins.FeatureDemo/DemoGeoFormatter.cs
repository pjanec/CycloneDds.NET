using System;
using System.Collections.Generic;
using CycloneDDS.Schema.Formatting;
using DdsMonitor.Engine.Ui;

namespace DdsMonitor.Plugins.FeatureDemo;

/// <summary>
/// Demo <see cref="IValueFormatter"/> that presents <see cref="GeoCoord"/> values as
/// human-readable latitude/longitude strings in the DDS Monitor value column.
/// Registered by <see cref="FeatureDemoPlugin"/> via <see cref="IValueFormatterRegistry"/>.
/// </summary>
public sealed class DemoGeoFormatter : IValueFormatter
{
    /// <inheritdoc />
    public string DisplayName => "Geo Coordinate (Demo)";

    /// <inheritdoc />
    public float GetScore(Type type, object? value) =>
        type == typeof(GeoCoord) ? 1.0f : 0f;

    /// <inheritdoc />
    public string FormatText(object? value) =>
        value is GeoCoord g ? $"{g.Lat:F6}°N, {g.Lon:F6}°E" : string.Empty;

    /// <inheritdoc />
    public IEnumerable<FormattedToken> FormatTokens(object? value)
    {
        if (value is not GeoCoord g)
            yield break;

        yield return new FormattedToken($"{g.Lat:F6}", TokenType.Number);
        yield return new FormattedToken("°N, ", TokenType.Punctuation);
        yield return new FormattedToken($"{g.Lon:F6}", TokenType.Number);
        yield return new FormattedToken("°E", TokenType.Punctuation);
    }
}
