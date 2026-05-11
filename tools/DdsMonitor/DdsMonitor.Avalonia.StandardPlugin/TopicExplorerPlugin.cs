using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Plugin that provides the Topic Explorer panel — a live view of all registered DDS topics.
/// </summary>
public sealed class TopicExplorerPlugin : IMonitorPlugin
{
    public string Name => "TopicExplorer";
    public string Version => "1.0";

    public void ConfigureServices(IServiceCollection services) { }

    public void Initialize(IMonitorContext context)
    {
        var menuRegistry = context.GetFeature<IMenuRegistry>();
        var toolbarRegistry = context.GetFeature<IToolbarRegistry>();
        var windowManager = context.GetFeature<IWindowManager>();
        var viewRegistry = context.GetFeature<IAvaloniaViewRegistry>();

        if (windowManager is not null)
        {
            if (menuRegistry is not null)
            {
                menuRegistry.AddMenuItem("View", "Topic Explorer", () =>
                    windowManager.SpawnPanel(nameof(TopicExplorerViewModel), null));
            }

            if (toolbarRegistry is not null)
            {
                toolbarRegistry.Register(
                    id: "TopicExplorer",
                    onClick: () => windowManager.SpawnPanel(nameof(TopicExplorerViewModel), null),
                    tooltip: "Topic Explorer");
            }

            // Auto-open the Topic Explorer at startup
            windowManager.SpawnPanel(nameof(TopicExplorerViewModel), null);
        }

        if (viewRegistry is not null)
        {
            viewRegistry.Register<TopicExplorerViewModel>(vm => new TopicExplorerView { DataContext = vm });
        }
    }
}
