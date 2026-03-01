namespace DdsMonitor.Engine;

/// <summary>
/// Configuration values for DDS Monitor hosting.
/// </summary>
public sealed class DdsSettings
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "DdsSettings";

    /// <summary>
    /// Gets the default DDS domain identifier.
    /// </summary>
    public const int DefaultDomainId = 0;

    /// <summary>
    /// Gets the default UI refresh frequency in Hz.
    /// </summary>
    public const int DefaultUiRefreshHz = 30;

    /// <summary>
    /// Gets or sets the DDS domain identifier.
    /// </summary>
    public int DomainId { get; set; } = DefaultDomainId;

    /// <summary>
    /// Gets or sets plugin directories to scan for topic metadata.
    /// </summary>
    public string[] PluginDirectories { get; set; } = new[] { "plugins" };

    /// <summary>
    /// Gets or sets the UI refresh frequency in Hz.
    /// </summary>
    public int UiRefreshHz { get; set; } = DefaultUiRefreshHz;

    /// <summary>
    /// Gets or sets a value indicating whether the monitor should generate self-send samples.
    /// </summary>
    public bool SelfSendEnabled { get; set; }

    /// <summary>
    /// Gets or sets the self-send sample rate per topic in Hz.
    /// </summary>
    public int SelfSendRateHz { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of distinct key values generated for self-send topics.
    /// </summary>
    public int SelfSendKeyCount { get; set; } = 6;
}
