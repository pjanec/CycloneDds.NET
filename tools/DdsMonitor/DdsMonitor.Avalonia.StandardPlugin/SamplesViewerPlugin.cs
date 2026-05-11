using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Plugin that handles spawning SamplesViewer panels on demand.
/// Subscribes to <see cref="SpawnPanelEvent"/> and delegates to <see cref="IWindowManager"/>.
/// The plugin lives for the app lifetime so the subscription token is stored but not disposed.
/// </summary>
public sealed class SamplesViewerPlugin : IMonitorPlugin
{
    public string Name => "SamplesViewer";
    public string Version => "1.0";

    // Stored for app-lifetime; intentionally not disposed (plugin outlives panels).
    private IDisposable? _spawnToken;

    public void ConfigureServices(IServiceCollection services) { }

    public void Initialize(IMonitorContext context)
    {
        var eventBroker = context.GetFeature<IEventBroker>();
        var windowManager = context.GetFeature<IWindowManager>();
        var viewRegistry = context.GetFeature<IAvaloniaViewRegistry>();

        if (eventBroker is not null && windowManager is not null)
        {
            _spawnToken = eventBroker.SubscribeOnUiThread<SpawnPanelEvent>(ev =>
            {
                if (ev.PanelTypeName != "SamplesViewer") return;
                var topicName = ev.State?.GetValueOrDefault("TopicName")?.ToString() ?? "Unknown";
                windowManager.SpawnPanel($"SamplesViewer_{topicName}", ev.State);
            });
        }

        if (viewRegistry is not null)
        {
            viewRegistry.Register<SamplesViewerViewModel>(vm => new SamplesViewerView { DataContext = vm });
        }
    }
}
