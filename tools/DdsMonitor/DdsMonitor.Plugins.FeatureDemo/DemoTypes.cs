namespace DdsMonitor.Plugins.FeatureDemo;

/// <summary>
/// Demo payload type used to exercise <see cref="ISampleViewRegistry"/> and
/// <see cref="ITooltipProviderRegistry"/> registration in <see cref="FeatureDemoPlugin"/>.
/// </summary>
public sealed class DemoPayload
{
    /// <summary>Numeric identifier for this demo payload.</summary>
    public int Id { get; set; }

    /// <summary>A textual label shown in the custom sample view.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>A geographic coordinate field shown through the demo geo-formatter.</summary>
    public GeoCoord Location { get; set; }

    public override string ToString() => $"DemoPayload(Id={Id}, Label={Label})";
}

/// <summary>
/// Geographic coordinate type demonstrating <see cref="DdsMonitor.Engine.Ui.IValueFormatterRegistry"/>
/// registration with <see cref="DemoGeoFormatter"/>.
/// </summary>
public readonly struct GeoCoord
{
    /// <summary>Latitude in decimal degrees.</summary>
    public double Lat { get; init; }

    /// <summary>Longitude in decimal degrees.</summary>
    public double Lon { get; init; }

    public override string ToString() => $"{Lat:F6}, {Lon:F6}";
}
