using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Plugins.Bdc;

/// <summary>
/// BDC domain-entity plugin entry point.
/// Registers the aggregation engine as a singleton and exposes UI panels via
/// <see cref="IMonitorContext.PanelRegistry"/>:
/// <list type="bullet">
///   <item><description>BDC Entity Grid — live entity overview.</description></item>
///   <item><description>BDC Entity Detail — deep inspector for a single entity.</description></item>
///   <item><description>BDC Settings — runtime regex / namespace configuration.</description></item>
/// </list>
/// </summary>
public sealed class BdcPlugin : IMonitorPlugin
{
    /// <inheritdoc />
    public string Name => "BDC Domain Plugin";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Settings are singleton so they are shared between the EntityStore backend
        // and the BdcSettingsPanel frontend.
        services.AddSingleton<BdcSettings>();

        // EntityStore is a singleton background service; it subscribes to
        // IInstanceStore via constructor injection (Plugin-API-deviations.md §3.1).
        services.AddSingleton<EntityStore>();

        // BdcSettingsPersistenceService persists BdcSettings to bdc-settings.json.
        // Registered as both a named singleton AND as IHostedService so the host
        // starts it during application startup (loads saved settings before the UI
        // renders for the first time).
        services.AddSingleton<BdcSettingsPersistenceService>();
        services.AddHostedService(sp => sp.GetRequiredService<BdcSettingsPersistenceService>());

        // TimeTravelEngine provides historical state reconstruction.
        services.AddSingleton<TimeTravelEngine>();
    }

    /// <inheritdoc />
    public void Initialize(IMonitorContext context)
    {
        // Register BDC UI panels so the host WindowManager can spawn them.
        context.PanelRegistry.RegisterPanelType(
            "BDC Entity Grid",
            typeof(Components.BdcEntityGridPanel));

        context.PanelRegistry.RegisterPanelType(
            "BDC Entity Detail",
            typeof(Components.EntityDetailPanel));

        context.PanelRegistry.RegisterPanelType(
            "BDC Settings",
            typeof(Components.BdcSettingsPanel));

        // Add menu items under Plugins/BDC.
        context.MenuRegistry.AddMenuItem(
            "Plugins/BDC", "Entity Grid",
            () => { /* spawned via PanelRegistry by the host shell */ });

        context.MenuRegistry.AddMenuItem(
            "Plugins/BDC", "Settings",
            () => { /* spawned via PanelRegistry by the host shell */ });
    }
}
