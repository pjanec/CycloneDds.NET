using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.AssemblyScanner;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Plugin that manages schema sources (DLL assemblies containing DDS topic types).
/// Contributes the "Tools/Schema Sources…" menu item.
/// </summary>
public sealed class WorkspaceManagerPlugin : IMonitorPlugin
{
    public string Name => "WorkspaceManager";
    public string Version => "1.0";

    public void ConfigureServices(IServiceCollection services) { }

    public void Initialize(IMonitorContext context)
    {
        var menuRegistry = context.GetFeature<IMenuRegistry>();
        var windowManager = context.GetFeature<IWindowManager>();
        var viewRegistry = context.GetFeature<IAvaloniaViewRegistry>();

        if (menuRegistry is not null && windowManager is not null)
        {
            menuRegistry.AddMenuItem("Tools", "Schema Sources\u2026", () =>
                windowManager.SpawnPanel(nameof(SchemaSourcesViewModel), null));

            menuRegistry.AddMenuItem("Tools", "Network Configuration\u2026", () =>
                windowManager.SpawnPanel("NetworkConfig", null));
        }

        if (viewRegistry is not null)
        {
            viewRegistry.Register<SchemaSourcesViewModel>(vm => new SchemaSourcesView { DataContext = vm });
            viewRegistry.Register<NetworkConfigViewModel>(vm => new NetworkConfigView { DataContext = vm });
        }
    }
}
