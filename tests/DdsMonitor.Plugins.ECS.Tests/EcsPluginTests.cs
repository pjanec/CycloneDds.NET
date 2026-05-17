using System.Linq;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Plugins.ECS.Tests;

/// <summary>
/// Unit tests verifying EcsPlugin.Initialize against the PLA1-P1-T04 API (PLA1).
/// </summary>
public sealed class EcsPluginTests
{
    [Fact]
    public void Initialize_RegistersPanelsViaGetFeature()
    {
        var services = new ServiceCollection();
        var panelRegistry = new PluginPanelRegistry();
        services.AddSingleton(panelRegistry);
        using var provider = services.BuildServiceProvider();
        IMonitorContext context = new MonitorContext(provider);

        var plugin = new EcsPlugin();
        plugin.Initialize(context);

        var types = panelRegistry.RegisteredTypes;
        Assert.True(types.ContainsKey("ECS Entity Grid"), "ECS Entity Grid should be registered");
        Assert.True(types.ContainsKey("ECS Entity Detail"), "ECS Entity Detail should be registered");
        Assert.True(types.ContainsKey("ECS Settings"), "ECS Settings should be registered");
    }

    [Fact]
    public void Initialize_RegistersMenuItemsViaGetFeature()
    {
        var services = new ServiceCollection();
        var menuRegistry = new MenuRegistry();
        services.AddSingleton<IMenuRegistry>(menuRegistry);
        using var provider = services.BuildServiceProvider();
        IMonitorContext context = new MonitorContext(provider);

        var plugin = new EcsPlugin();
        plugin.Initialize(context);

        var menus = menuRegistry.GetTopLevelMenus();
        var pluginsNode = menus.FirstOrDefault(n => n.Label == "Plugins");
        Assert.NotNull(pluginsNode);
        var ecsNode = pluginsNode.Children.FirstOrDefault(n => n.Label == "ECS");
        Assert.NotNull(ecsNode);
    }

    [Fact]
    public void Initialize_DoesNotThrow_WhenContextHasNoFeatures()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        IMonitorContext context = new MonitorContext(provider);

        var plugin = new EcsPlugin();
        var ex = Record.Exception(() => plugin.Initialize(context));

        Assert.Null(ex);
    }
}
