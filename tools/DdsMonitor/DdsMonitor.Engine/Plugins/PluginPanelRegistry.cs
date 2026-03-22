namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Singleton registry that stores Blazor component types registered by plugins.
/// The application shell reads this registry to populate the "Plugin Panels" menu.
/// </summary>
public sealed class PluginPanelRegistry
{
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>Raised when a panel type is registered or removed.</summary>
    public event Action? Changed;

    /// <summary>
    /// Registers a custom Blazor panel type under the given display name.
    /// Registering a name that already exists replaces the existing entry.
    /// </summary>
    /// <param name="name">Short display name shown in the Plugin Panels menu.</param>
    /// <param name="blazorComponentType">The Blazor component type to spawn when selected.</param>
    public void RegisterPanelType(string name, Type blazorComponentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(blazorComponentType);

        lock (_sync)
        {
            _types[name] = blazorComponentType;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Returns a read-only snapshot of all registered panel types keyed by display name.
    /// </summary>
    public IReadOnlyDictionary<string, Type> RegisteredTypes
    {
        get
        {
            lock (_sync)
            {
                return new Dictionary<string, Type>(_types, StringComparer.Ordinal);
            }
        }
    }
}
