using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Plugin that runs the dummy heartbeat data generator.
/// Registers <see cref="DummyGeneratorService"/> as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
/// and injects a "Toggle Dummy Generator" context menu item for any topic.
/// </summary>
public sealed class DummyDataGeneratorPlugin : IMonitorPlugin
{
    public string Name => "DummyDataGenerator";
    public string Version => "1.0";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DummyGeneratorService>();
        services.AddHostedService(sp => sp.GetRequiredService<DummyGeneratorService>());
    }

    public void Initialize(IMonitorContext context)
    {
        var menuRegistry = context.GetFeature<IMenuRegistry>();
        var contextMenuRegistry = context.GetFeature<IContextMenuRegistry>();
        var generatorService = context.GetFeature<DummyGeneratorService>();

        if (contextMenuRegistry is not null)
        {
            contextMenuRegistry.RegisterProvider<TopicMetadata>(_ =>
            {
                return
                [
                    new ContextMenuItem(
                        "Toggle Dummy Generator",
                        null,
                        () =>
                        {
                            generatorService?.TogglePublishing();
                            return Task.CompletedTask;
                        })
                ];
            });
        }

        if (menuRegistry is not null)
        {
            menuRegistry.AddMenuItem("Tools", "Dummy Generator", () =>
                generatorService?.TogglePublishing());
        }
    }
}
