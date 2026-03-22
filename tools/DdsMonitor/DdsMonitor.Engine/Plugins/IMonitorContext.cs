namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Provides access to host registries that plugins can populate during
/// <see cref="IMonitorPlugin.Initialize"/>.
/// </summary>
public interface IMonitorContext
{
    /// <summary>Gets the registry for adding items to the application's top menu bar.</summary>
    IMenuRegistry MenuRegistry { get; }

    /// <summary>Gets the registry for registering custom plugin UI panel types.</summary>
    PluginPanelRegistry PanelRegistry { get; }
}
