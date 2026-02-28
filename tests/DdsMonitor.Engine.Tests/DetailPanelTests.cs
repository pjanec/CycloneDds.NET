using System;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DetailPanelTests
{
    [Fact]
    public async Task DetailPanel_Debounce_WaitsBeforeRender()
    {
        const int DebounceDelayMs = 50;
        const int BurstIntervalMs = 1;
        const int BurstCount = 10;
        const int WaitBufferMs = 80;

        var renderCount = 0;
        using var debouncer = new DebouncedAction(TimeSpan.FromMilliseconds(DebounceDelayMs),
            () => Interlocked.Increment(ref renderCount));

        for (var i = 0; i < BurstCount; i++)
        {
            debouncer.Trigger();
            await Task.Delay(BurstIntervalMs);
        }

        await Task.Delay(DebounceDelayMs + WaitBufferMs);

        Assert.InRange(renderCount, 1, 2);
    }
}
