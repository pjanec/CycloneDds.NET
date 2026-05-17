using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using DdsMonitor.Plugins.FeatureDemo;
using DdsMonitor.Plugins.FeatureDemo.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Plugins.FeatureDemo.Tests.Components;

/// <summary>
/// bUnit tests for <see cref="DemoDashboardPanel"/> (PLA1-P8-T03).
/// </summary>
public sealed class DemoDashboardPanelTests : TestContext
{
    [Fact]
    public void DemoDashboardPanel_Renders_WithoutError()
    {
        var store = new FakeSampleStore(totalCount: 0);
        var processor = new DemoBackgroundProcessor(store);
        Services.AddSingleton(processor);

        var ex = Record.Exception(() => RenderComponent<DemoDashboardPanel>());

        Assert.Null(ex);
    }

    [Fact]
    public async Task DemoDashboardPanel_ShowsProcessedCount()
    {
        var store = new FakeSampleStore(totalCount: 5);
        var processor = new DemoBackgroundProcessor(store);

        // Start the processor — first timer tick fires at dueTime=0 so
        // ProcessedCount is updated before we render.
        await processor.StartAsync(CancellationToken.None);

        // Give the timer thread a moment to run the first tick.
        await Task.Delay(150);

        Services.AddSingleton(processor);

        var cut = RenderComponent<DemoDashboardPanel>();

        Assert.Contains("5", cut.Markup);

        await processor.StopAsync(CancellationToken.None);
        processor.Dispose();
    }

    // ── Stubs ──────────────────────────────────────────────────────────────

    private sealed class FakeSampleStore : ISampleStore
    {
        private readonly int _totalCount;

        public FakeSampleStore(int totalCount) => _totalCount = totalCount;

        public IReadOnlyList<SampleData> AllSamples => Array.Empty<SampleData>();
        public int TotalCount => _totalCount;
        public long TotalBytesReceived => 0;
        public SampleData[] GetSamples(int startIndex) => Array.Empty<SampleData>();
        public ITopicSamples GetTopicSamples(Type topicType) => throw new NotSupportedException();
        public int GetTopicCount(Type topicType) => 0;
        public void Append(SampleData sample) { }
        public void Clear() { }
        public event Action? Cleared { add { } remove { } }
    }
}
