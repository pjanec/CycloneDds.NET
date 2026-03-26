using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Contract implemented by every DDS Monitor plugin.
/// Plugins are discovered by scanning the configured plugin directories at startup.
/// </summary>
public interface IMonitorPlugin
{
    /// <summary>Gets the human-readable name of the plugin.</summary>
    string Name { get; }

    /// <summary>Gets the plugin version string.</summary>
    string Version { get; }

    /// <summary>
    /// Called during host DI container setup, before the container is built.
    /// Plugins may register additional services here.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Called after the host has started.
    /// Plugins can register UI panels and menu items via
    /// <see cref="IMonitorContext.GetFeature{TFeature}"/>.
    /// </summary>
    /// <param name="context">Access point to host capabilities.</param>
    void Initialize(IMonitorContext context);
}
