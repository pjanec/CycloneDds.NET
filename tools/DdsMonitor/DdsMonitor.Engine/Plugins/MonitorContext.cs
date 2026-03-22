namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Default implementation of <see cref="IMonitorContext"/> backed by singleton-lifetime services.
/// An instance is created after the DI container is built and passed to every loaded plugin
/// during <see cref="IMonitorPlugin.Initialize"/>.
/// </summary>
public sealed class MonitorContext : IMonitorContext
{
    /// <summary>
    /// Initialises a new <see cref="MonitorContext"/>.
    /// </summary>
    public MonitorContext(IMenuRegistry menuRegistry, PluginPanelRegistry panelRegistry)
    {
        MenuRegistry = menuRegistry ?? throw new ArgumentNullException(nameof(menuRegistry));
        PanelRegistry = panelRegistry ?? throw new ArgumentNullException(nameof(panelRegistry));
    }

    /// <inheritdoc />
    public IMenuRegistry MenuRegistry { get; }

    /// <inheritdoc />
    public PluginPanelRegistry PanelRegistry { get; }
}
