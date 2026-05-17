using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Plugins.FeatureDemo;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Plugins.FeatureDemo.Tests;

/// <summary>
/// Headless DI integration test for <see cref="FeatureDemoPlugin"/> (PLA1-P8-T05).
/// Boots a minimal DI container (no Blazor renderer), loads the plugin, feeds a
/// mock sample store, and asserts that <see cref="DemoBackgroundProcessor"/>
/// processed at least one sample within 5 seconds.
/// </summary>
public sealed class HeadlessPluginIntegrationTest
{
    [Fact]
    public async Task DemoBackgroundProcessor_ProcessesAtLeastOneSample_WithinTimeout()
    {
        // ── Arrange ────────────────────────────────────────────────────────────
        var store = new FakeSampleStore(totalCount: 10);

        var services = new ServiceCollection();
        services.AddSingleton<ISampleStore>(store);

        // Let the plugin register its own services (DemoBackgroundProcessor + hosted service).
        var plugin = new FeatureDemoPlugin();
        plugin.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        var processor = provider.GetRequiredService<DemoBackgroundProcessor>();

        // ── Act ────────────────────────────────────────────────────────────────
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await processor.StartAsync(cts.Token);

        // Wait for at least one tick — the timer fires at dueTime=0,
        // so a 200 ms poll is more than enough.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (processor.ProcessedCount < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await processor.StopAsync(CancellationToken.None);

        // ── Assert ─────────────────────────────────────────────────────────────
        Assert.True(processor.ProcessedCount >= 1,
            $"Expected ProcessedCount >= 1 but got {processor.ProcessedCount}.");
    }

    [Fact]
    public void FeatureDemoPlugin_Initialize_DoesNotThrow_InHeadlessContainer()
    {
        // Build a container that mimics the engine-only headless setup
        // (no Blazor-specific services), verifying Initialize completes without exception.
        var services = new ServiceCollection();
        var store = new FakeSampleStore(totalCount: 0);
        services.AddSingleton<ISampleStore>(store);

        var plugin = new FeatureDemoPlugin();
        plugin.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // NullMonitorContext simulates a host that provides no optional features.
        var context = new NullMonitorContext();

        var ex = Record.Exception(() => plugin.Initialize(context));

        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies the PluginConfigService / PluginLoader enablement path (PLA1-DEBT-020 narrowed
    /// scope): demonstrates the exact enabled-check PluginLoader performs, then hands off to
    /// FeatureDemoPlugin.ConfigureServices the same way PluginLoader would for an enabled plugin.
    /// ISampleStore contains 10 SampleData entries with DemoPayload payloads.
    /// </summary>
    [Fact]
    public async Task DemoBackgroundProcessor_EnabledViaPluginConfigService_ProcessesAtLeastOneSample()
    {
        // ── Arrange: PluginConfigService path ─────────────────────────────────
        var pluginConfig = new PluginConfigService();   // public ctor, loads from %APPDATA%
        // Ensure "Feature Demo" is in the enabled set before checking.
        pluginConfig.EnabledPlugins.Add("Feature Demo");

        var plugin = new FeatureDemoPlugin();

        // Replicate the PluginLoader enabled check (only explicitly listed plugins are enabled):
        bool isEnabled = pluginConfig.EnabledPlugins.Contains(plugin.Name);

        Assert.True(isEnabled,
            "PluginConfigService should report 'Feature Demo' as enabled.");

        // Provide a store pre-loaded with 10 SampleData instances carrying DemoPayload.
        var store = new FakeSampleStore(totalCount: 10);

        var services = new ServiceCollection();
        services.AddSingleton<ISampleStore>(store);

        // Simulate what PluginLoader calls for an enabled plugin:
        plugin.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<DemoBackgroundProcessor>();

        // ── Act ────────────────────────────────────────────────────────────────
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await processor.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (processor.ProcessedCount < 1 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        await processor.StopAsync(CancellationToken.None);

        // ── Assert ─────────────────────────────────────────────────────────────
        Assert.True(processor.ProcessedCount >= 1,
            $"Expected ProcessedCount >= 1 but got {processor.ProcessedCount}.");
    }

    // ── Stubs ──────────────────────────────────────────────────────────────

    private sealed class FakeSampleStore : ISampleStore
    {
        private static readonly IReadOnlyList<SampleData> _samples =
            Enumerable.Range(1, 10)
                .Select(i => new SampleData
                {
                    Ordinal = i,
                    Payload = new DemoPayload { Id = i, Label = $"demo-{i}" }
                })
                .ToArray();

        private readonly int _totalCount;
        public FakeSampleStore(int totalCount) => _totalCount = totalCount;

        public IReadOnlyList<SampleData> AllSamples =>
            _totalCount > 0 ? _samples : Array.Empty<SampleData>();

        public int TotalCount => _totalCount;
        public long TotalBytesReceived => 0;
        public SampleData[] GetSamples(int startIndex) => Array.Empty<SampleData>();
        public ITopicSamples GetTopicSamples(Type topicType) => throw new NotSupportedException();
        public int GetTopicCount(Type topicType) => 0;
        public void Append(SampleData sample) { }
        public void Clear() { }
        public event Action? Cleared { add { } remove { } }
    }

    private sealed class NullMonitorContext : DdsMonitor.Engine.Plugins.IMonitorContext
    {
        public TFeature? GetFeature<TFeature>() where TFeature : class => null;
    }
}
