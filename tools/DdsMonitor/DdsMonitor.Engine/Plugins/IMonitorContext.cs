namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Provides access to optional host capabilities during plugin initialization.
/// Plugins call <see cref="GetFeature{TFeature}"/> to discover what the current host supports;
/// a <c>null</c> return indicates the feature is unavailable and the plugin should degrade
/// gracefully without throwing.
/// </summary>
public interface IMonitorContext
{
    /// <summary>
    /// Returns the requested host feature, or <c>null</c> when the host does not support it.
    /// Plugins <b>must</b> perform a null check after calling this method to ensure
    /// graceful degradation on hosts that do not provide the feature.
    /// </summary>
    /// <typeparam name="TFeature">The capability type to resolve.</typeparam>
    /// <returns>The feature instance, or <c>null</c> if the host does not provide it.</returns>
    TFeature? GetFeature<TFeature>() where TFeature : class;
}
