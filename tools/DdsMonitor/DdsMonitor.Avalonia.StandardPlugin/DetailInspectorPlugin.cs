using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Plugin that provides the Detail Inspector panel.
/// Registers a context menu provider for <see cref="SampleData"/> that allows opening
/// a linked inspector. Cross-plugin communication is via <see cref="IEventBroker"/> only.
/// </summary>
public sealed class DetailInspectorPlugin : IMonitorPlugin
{
    public string Name => "DetailInspector";
    public string Version => "1.0";

    public void ConfigureServices(IServiceCollection services) { }

    public void Initialize(IMonitorContext context)
    {
        var contextMenuRegistry = context.GetFeature<IContextMenuRegistry>();
        var windowManager = context.GetFeature<IWindowManager>();
        var viewRegistry = context.GetFeature<IAvaloniaViewRegistry>();

        if (contextMenuRegistry is not null && windowManager is not null)
        {
            contextMenuRegistry.RegisterProvider<SampleData>(sample =>
            {
                // Use topic name as a stable panel identifier fallback.
                var sourcePanelId = sample.TopicMetadata?.TopicName ?? "unknown";
                return
                [
                    new ContextMenuItem(
                        "Open Inspector",
                        null,
                        () =>
                        {
                            OpenInspector(windowManager, sample, sourcePanelId);
                            return Task.CompletedTask;
                        })
                ];
            });
        }

        if (viewRegistry is not null)
        {
            viewRegistry.Register<DetailInspectorViewModel>(vm => new DetailInspectorView { DataContext = vm });
        }
    }

    private static void OpenInspector(IWindowManager windowManager, SampleData sample, string sourcePanelId)
    {
        var state = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["IsLinked"] = true,
            ["SourcePanelId"] = sourcePanelId,
        };
        windowManager.SpawnPanel($"DetailInspector_{sourcePanelId}", state);
    }
}
