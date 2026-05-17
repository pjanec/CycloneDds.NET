using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Default implementation of <see cref="IMonitorContext"/> backed by the host's DI container.
/// Feature resolution is delegated to <see cref="ServiceProviderServiceExtensions.GetService{T}"/>
/// so any service registered in the container is automatically available to plugins via
/// <see cref="GetFeature{TFeature}"/>.
/// An instance is created after the DI container is built and passed to every loaded plugin
/// during <see cref="IMonitorPlugin.Initialize"/>.
/// </summary>
public sealed class MonitorContext : IMonitorContext
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initialises a new <see cref="MonitorContext"/>.
    /// </summary>
    /// <param name="services">The built host service provider.</param>
    public MonitorContext(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public TFeature? GetFeature<TFeature>() where TFeature : class
        => _services.GetService<TFeature>();
}
