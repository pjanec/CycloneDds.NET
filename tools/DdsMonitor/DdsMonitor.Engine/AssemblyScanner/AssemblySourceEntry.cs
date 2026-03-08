namespace DdsMonitor.Engine.AssemblyScanner;

/// <summary>
/// Represents a user-configured external DLL assembly and the topics extracted from it.
/// </summary>
public sealed class AssemblySourceEntry
{
    /// <summary>
    /// Gets the absolute file-system path to the DLL.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of DDS topic types successfully registered from this assembly.
    /// -1 indicates the assembly has not been scanned yet (or failed to load).
    /// </summary>
    public int TopicCount { get; set; } = -1;

    /// <summary>
    /// Gets or sets a short diagnostic note (e.g. the exception message if loading failed).
    /// </summary>
    public string? LoadError { get; set; }
}
