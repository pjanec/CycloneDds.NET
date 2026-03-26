using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Plugins.ECS;

/// <summary>
/// ECS domain-entity plugin entry point.
/// Registers the aggregation engine as a singleton and exposes UI panels via
/// <see cref="IMonitorContext.GetFeature{TFeature}"/>:
/// <list type="bullet">
///   <item><description>ECS Entity Grid — live entity overview.</description></item>
///   <item><description>ECS Entity Detail — deep inspector for a single entity.</description></item>
///   <item><description>ECS Settings — runtime regex / namespace configuration.</description></item>
/// </list>
/// </summary>
public sealed class EcsPlugin : IMonitorPlugin
{
    /// <inheritdoc />
    public string Name => "ECS Domain Plugin";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Settings are singleton so they are shared between the EntityStore backend
        // and the BdcSettingsPanel frontend.
        services.AddSingleton<EcsSettings>();

        // EntityStore is a singleton background service; it subscribes to
        // IInstanceStore via constructor injection (Plugin-API-deviations.md §3.1).
        services.AddSingleton<EntityStore>();

        // EcsSettingsPersistenceService persists EcsSettings via workspace events:
        //   • WorkspaceLoadedEvent  — reads ECS settings from PluginSettings["ECS"].
        //     If the section is absent, a one-time migration from the legacy
        //     ecs-settings.json file is attempted; ecs-settings.json is the legacy
        //     migration source only and is deleted after a successful migration.
        //   • WorkspaceSavingEvent  — writes current settings back to PluginSettings["ECS"].
        // Registered as both a singleton AND as IHostedService so the host starts it
        // during application startup and the workspace-event subscriptions are put in
        // place before the UI renders for the first time.
        services.AddSingleton<EcsSettingsPersistenceService>();
        services.AddHostedService(sp => sp.GetRequiredService<EcsSettingsPersistenceService>());

        // TimeTravelEngine provides historical state reconstruction.
        services.AddSingleton<TimeTravelEngine>();
    }

    /// <inheritdoc />
    public void Initialize(IMonitorContext context)
    {
        // Register ECS UI panels so the host WindowManager can spawn them.
        context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(
            "ECS Entity Grid",
            typeof(Components.EcsEntityGridPanel));

        context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(
            "ECS Entity Detail",
            typeof(Components.EntityDetailPanel));

        context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(
            "ECS Settings",
            typeof(Components.EcsSettingsPanel));

        // Add menu items under Plugins/ECS.
        context.GetFeature<IMenuRegistry>()?.AddMenuItem(
            "Plugins/ECS", "Entity Grid",
            () => { /* spawned via PanelRegistry by the host shell */ });

        context.GetFeature<IMenuRegistry>()?.AddMenuItem(
            "Plugins/ECS", "Settings",
            () => { /* spawned via PanelRegistry by the host shell */ });
    }
}
