namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Represents an <see cref="IMonitorPlugin"/> that has been discovered during startup,
/// along with its load-time metadata and enabled state.
/// </summary>
public sealed class DiscoveredPlugin
{
    /// <summary>Gets the loaded plugin instance.</summary>
    public IMonitorPlugin Instance { get; }

    /// <summary>Gets the absolute path of the assembly from which this plugin was loaded.</summary>
    public string AssemblyPath { get; }

    /// <summary>Gets or sets whether this plugin is enabled and should be activated.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Initialises a new <see cref="DiscoveredPlugin"/>.
    /// </summary>
    public DiscoveredPlugin(IMonitorPlugin instance, string assemblyPath, bool isEnabled)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        AssemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
        IsEnabled = isEnabled;
    }
}
