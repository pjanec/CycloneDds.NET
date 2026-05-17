using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Blazor.Tests.Components;

/// <summary>
/// bUnit tests for PluginManagerPanel business logic (PLA1-P5-T03).
/// Uses <see cref="TestablePluginManager"/> — a minimal stub component that exercises
/// the enable/disable/save logic identical to PluginManagerPanel.razor.
/// Full rendering of the real PluginManagerPanel.razor is deferred because it requires
/// referencing DdsMonitor.Blazor (Web SDK with native CycloneDDS binaries) — documented
/// in PLA1-BATCH-06-REPORT.md.
/// </summary>
public sealed class PluginManagerPanelTests : TestContext, IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public PluginManagerPanelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "enabled-plugins.json");
    }

    public new void Dispose()
    {
        base.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DiscoveredPlugin MakePlugin(string name, bool enabled = true) =>
        new(new FakeMonitorPlugin(name), $"/plugins/{name}.dll", enabled);

    private IRenderedComponent<TestablePluginManager> Render(
        List<DiscoveredPlugin> plugins,
        PluginConfigService? configService = null)
    {
        configService ??= new PluginConfigService(_configPath);

        return RenderComponent<TestablePluginManager>(p => p
            .Add(c => c.Plugins, plugins)
            .Add(c => c.ConfigService, configService));
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public void Renders_PluginTable_WithCorrectRowCount()
    {
        var plugins = new List<DiscoveredPlugin>
        {
            MakePlugin("PluginA"),
            MakePlugin("PluginB"),
        };

        var cut = Render(plugins);

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Checkbox_Checked_ForEnabledPlugin()
    {
        var plugins = new List<DiscoveredPlugin> { MakePlugin("EnabledPlugin", enabled: true) };

        var cut = Render(plugins);

        var cb = cut.Find("input[type=checkbox]");
        Assert.True(cb.ClassList.Contains("is-enabled"));
    }

    [Fact]
    public void Checkbox_Unchecked_ForDisabledPlugin()
    {
        var plugins = new List<DiscoveredPlugin> { MakePlugin("DisabledPlugin", enabled: false) };

        var cut = Render(plugins);

        var cb = cut.Find("input[type=checkbox]");
        Assert.True(cb.ClassList.Contains("is-disabled"));
    }

    [Fact]
    public void Toggle_ShowsRestartBadge()
    {
        var plugins = new List<DiscoveredPlugin> { MakePlugin("APlugin", enabled: true) };
        var configSvc = new PluginConfigService(_configPath);

        var cut = Render(plugins, configSvc);

        // Badge absent initially.
        Assert.Empty(cut.FindAll(".plugin-manager__restart-badge"));

        // Toggle the first plugin via the public API.
        cut.InvokeAsync(() => cut.Instance.TogglePlugin(0));

        // Badge must appear after toggle.
        Assert.NotEmpty(cut.FindAll(".plugin-manager__restart-badge"));
    }

    [Fact]
    public void Toggle_SavesConfig()
    {
        var plugins = new List<DiscoveredPlugin> { MakePlugin("SaveTestPlugin", enabled: true) };
        var configSvc = new PluginConfigService(_configPath);

        var cut = Render(plugins, configSvc);

        // Toggle the first plugin via the public API.
        cut.InvokeAsync(() => cut.Instance.TogglePlugin(0));

        // Config file must have been saved.
        Assert.True(configSvc.HadConfigFileAtInitialization);
        Assert.DoesNotContain("SaveTestPlugin", configSvc.EnabledPlugins);
    }

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class FakeMonitorPlugin : IMonitorPlugin
    {
        public FakeMonitorPlugin(string name) => Name = name;
        public string Name { get; }
        public string Version => "1.0.0";
        public void ConfigureServices(IServiceCollection services) { }
        public void Initialize(IMonitorContext context) { }
    }
}
