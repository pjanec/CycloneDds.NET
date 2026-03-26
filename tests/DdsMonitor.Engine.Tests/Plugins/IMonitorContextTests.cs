using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Engine.Tests.Plugins;

/// <summary>
/// Tests for the IMonitorContext / MonitorContext capability-querying contract (PLA1-P1-T01).
/// </summary>
public sealed class IMonitorContextTests
{
    [Fact]
    public void GetFeature_ReturnsRegisteredService()
    {
        var services = new ServiceCollection();
        var menuRegistry = new MenuRegistry();
        services.AddSingleton<IMenuRegistry>(menuRegistry);
        using var provider = services.BuildServiceProvider();
        IMonitorContext context = new MonitorContext(provider);

        var result = context.GetFeature<IMenuRegistry>();

        Assert.NotNull(result);
        Assert.Same(menuRegistry, result);
    }

    [Fact]
    public void GetFeature_ReturnsNull_WhenServiceNotRegistered()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        IMonitorContext context = new MonitorContext(provider);

        var result = context.GetFeature<IContextMenuRegistry>();

        Assert.Null(result);
    }

    [Fact]
    public void GetFeature_WhenCalledTwice_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
        using var provider = services.BuildServiceProvider();
        IMonitorContext context = new MonitorContext(provider);

        var first = context.GetFeature<IMenuRegistry>();
        var second = context.GetFeature<IMenuRegistry>();

        Assert.Same(first, second);
    }
}
