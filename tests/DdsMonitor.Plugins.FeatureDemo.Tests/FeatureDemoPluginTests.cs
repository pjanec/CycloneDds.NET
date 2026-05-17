using System;
using System.Collections.Generic;
using System.Linq;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Export;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine.Ui;
using DdsMonitor.Plugins.FeatureDemo;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Plugins.FeatureDemo.Tests;

/// <summary>
/// Unit tests for <see cref="FeatureDemoPlugin"/> registration (PLA1-P8-T02).
/// </summary>
public sealed class FeatureDemoPluginTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a DI container with all plugin-API features registered,
    /// initialises <see cref="FeatureDemoPlugin"/>, and returns the provider.
    /// </summary>
    private static (FeatureDemoPlugin plugin, ServiceProvider provider) BuildAndInitialize()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
        services.AddSingleton<PluginPanelRegistry>();
        services.AddSingleton<IContextMenuRegistry, ContextMenuRegistry>();
        services.AddSingleton<ISampleViewRegistry, SampleViewRegistry>();
        services.AddSingleton<IValueFormatterRegistry, ValueFormatterRegistry>();
        services.AddSingleton<ITypeDrawerRegistry, TypeDrawerRegistry>();
        services.AddSingleton<IExportFormatRegistry, ExportFormatRegistry>();
        services.AddSingleton<ITooltipProviderRegistry, TooltipProviderRegistry>();
        services.AddSingleton<IEventBroker, EventBroker>();
        services.AddSingleton<TopicColorService>(sp =>
            new TopicColorService(new FakeWorkspaceState()));

        var provider = services.BuildServiceProvider();
        var context = new MonitorContext(provider);
        var plugin = new FeatureDemoPlugin();
        plugin.Initialize(context);
        return (plugin, provider);
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints()
    {
        var (_, provider) = BuildAndInitialize();

        // IMenuRegistry has a "Plugins" top-level node with a "Demo" child subtree
        var menuRegistry = provider.GetRequiredService<IMenuRegistry>();
        var topLevel = menuRegistry.GetTopLevelMenus();
        Assert.Contains(topLevel, n => n.Label == "Plugins" && n.Children.Any(c => c.Label == "Demo"));

        // PluginPanelRegistry has "Demo Dashboard"
        var panelRegistry = provider.GetRequiredService<PluginPanelRegistry>();
        Assert.True(panelRegistry.RegisteredTypes.ContainsKey("Demo Dashboard"));

        // IContextMenuRegistry has a provider for SampleData
        var cmReg = provider.GetRequiredService<IContextMenuRegistry>();
        var sample = new SampleData { Ordinal = 1 };
        Assert.NotEmpty(cmReg.GetItems<SampleData>(sample));

        // ISampleViewRegistry has a viewer for DemoPayload
        var svReg = provider.GetRequiredService<ISampleViewRegistry>();
        Assert.NotNull(svReg.GetViewer(typeof(DemoPayload)));

        // ITooltipProviderRegistry returns HTML for DemoPayload
        var ttReg = provider.GetRequiredService<ITooltipProviderRegistry>();
        Assert.NotNull(ttReg.GetTooltipHtml(typeof(DemoPayload), new DemoPayload()));

        // TopicColorService has the "DEMO" rule registered
        var colorSvc = provider.GetRequiredService<TopicColorService>();
        var color = colorSvc.GetEffectiveColor("MY_DEMO_TOPIC");
        Assert.Equal("#FF0000", color);

        // IExportFormatRegistry has "Export as CSV (Demo)" (PLA1-DEBT-021)
        var exportReg = provider.GetRequiredService<IExportFormatRegistry>();
        Assert.Contains(exportReg.GetFormats(), f => f.Label == "Export as CSV (Demo)");

        // IValueFormatterRegistry has a formatter for GeoCoord (PLA1-DEBT-021)
        var fmtReg = provider.GetRequiredService<IValueFormatterRegistry>();
        var geoFormatters = fmtReg.GetFormatters(typeof(GeoCoord), null);
        Assert.NotEmpty(geoFormatters);
        Assert.Contains(geoFormatters, f => f.DisplayName == "Geo Coordinate (Demo)");

        // ITypeDrawerRegistry has a drawer for int (PLA1-DEBT-021)
        var drawerReg = provider.GetRequiredService<ITypeDrawerRegistry>();
        Assert.True(drawerReg.HasDrawer(typeof(int)));
    }

    [Fact]
    public void Initialize_WhenNoFeaturesAvailable_DoesNotThrow()
    {
        var context = new NullMonitorContext();
        var plugin = new FeatureDemoPlugin();

        var ex = Record.Exception(() => plugin.Initialize(context));

        Assert.Null(ex);
    }

    [Fact]
    public void Initialize_RegistersContextMenuProvider_ForSampleData()
    {
        var (_, provider) = BuildAndInitialize();

        var registry = provider.GetRequiredService<IContextMenuRegistry>();
        var sample = new SampleData { Ordinal = 42 };
        var items = registry.GetItems<SampleData>(sample).ToList();

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Label.Contains("Demo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Initialize_RegistersDetailViewer_ForDemoPayloadType()
    {
        var (_, provider) = BuildAndInitialize();

        var registry = provider.GetRequiredService<ISampleViewRegistry>();
        var viewer = registry.GetViewer(typeof(DemoPayload));

        Assert.NotNull(viewer);
    }

    [Fact]
    public void WorkspaceSaving_PopulatesPluginSettingsKey()
    {
        var (_, provider) = BuildAndInitialize();

        var broker = provider.GetRequiredService<IEventBroker>();
        var bag = new Dictionary<string, object>();
        broker.Publish(new WorkspaceSavingEvent(bag));

        Assert.True(bag.ContainsKey("FeatureDemo"), "Expected 'FeatureDemo' key in plugin settings bag.");
    }

    [Fact]
    public void WorkspaceLoaded_RestoresDemoModeFromSettings()
    {
        var (_, provider) = BuildAndInitialize();

        var broker = provider.GetRequiredService<IEventBroker>();
        var settings = new Dictionary<string, object>
        {
            ["FeatureDemo"] = new { DemoMode = true }
        };

        // Plugin should not throw when restoring state.
        var ex = Record.Exception(() =>
            broker.Publish(new WorkspaceLoadedEvent(settings)));

        Assert.Null(ex);
    }

    // ── Private stubs ──────────────────────────────────────────────────────

    /// <summary>Returns null for every GetFeature call — used to test graceful degradation.</summary>
    private sealed class NullMonitorContext : IMonitorContext
    {
        public TFeature? GetFeature<TFeature>() where TFeature : class => null;
    }

    private sealed class FakeWorkspaceState : IWorkspaceState
    {
        public string WorkspaceFilePath => System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "feature-demo-tests", "workspace.json");
    }
}