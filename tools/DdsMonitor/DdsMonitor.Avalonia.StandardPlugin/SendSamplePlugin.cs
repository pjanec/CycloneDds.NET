using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Plugin that provides the Send Sample panel for authoring and publishing DDS payloads.
/// Registers standard Avalonia type drawers and contributes a context menu item
/// on <see cref="SampleData"/> to clone a sample into a send panel.
/// </summary>
public sealed class SendSamplePlugin : IMonitorPlugin
{
    public string Name => "SendSample";
    public string Version => "1.0";

    public void ConfigureServices(IServiceCollection services) { }

    public void Initialize(IMonitorContext context)
    {
        var drawerRegistry = context.GetFeature<IAvaloniaTypeDrawerRegistry>();
        var contextMenuRegistry = context.GetFeature<IContextMenuRegistry>();
        var menuRegistry = context.GetFeature<IMenuRegistry>();
        var windowManager = context.GetFeature<IWindowManager>();

        if (drawerRegistry is not null)
        {
            StandardDrawerRegistrar.Register(drawerRegistry);
        }

        if (contextMenuRegistry is not null && windowManager is not null)
        {
            contextMenuRegistry.RegisterProvider<SampleData>(sample =>
            [
                new ContextMenuItem(
                    "Clone to Send",
                    null,
                    () =>
                    {
                        windowManager.SpawnPanel(
                            $"SendSample_{sample.TopicMetadata.TopicName}",
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["TopicName"] = sample.TopicMetadata.TopicName,
                            });
                        return Task.CompletedTask;
                    })
            ]);
        }

        if (menuRegistry is not null && windowManager is not null)
        {
            menuRegistry.AddMenuItem("Tools", "Send Sample", () =>
                windowManager.SpawnPanel("SendSample_Blank", null));
        }
    }
}
