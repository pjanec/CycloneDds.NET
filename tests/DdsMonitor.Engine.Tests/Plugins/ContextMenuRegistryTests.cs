using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine.Plugins;

namespace DdsMonitor.Engine.Tests.Plugins;

/// <summary>
/// Behaviour tests for ContextMenuRegistry (PLA1-P2-T02).
/// </summary>
public sealed class ContextMenuRegistryTests
{
    private static ContextMenuItem Item(string label) =>
        new(label, null, () => Task.CompletedTask);

    [Fact]
    public void GetItems_WhenNoProviders_ReturnsEmpty()
    {
        var registry = new ContextMenuRegistry();

        var result = registry.GetItems<SampleData>(null!);

        Assert.Empty(result);
    }

    [Fact]
    public void GetItems_ReturnsSingleProviderItems()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("A"), Item("B") });

        var result = registry.GetItems<SampleData>(null!).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetItems_ReturnsCombinedItemsFromMultipleProviders()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("A"), Item("B") });
        registry.RegisterProvider<SampleData>(_ => new[] { Item("C"), Item("D"), Item("E") });

        var result = registry.GetItems<SampleData>(null!).ToList();

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void GetItems_WhenProviderThrows_OtherProvidersStillRun()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => throw new InvalidOperationException("provider failure"));
        registry.RegisterProvider<SampleData>(_ => new[] { Item("OK") });

        List<ContextMenuItem> result = null!;
        var ex = Record.Exception(() => result = registry.GetItems<SampleData>(null!).ToList());

        Assert.Null(ex);
        Assert.Single(result);
        Assert.Equal("OK", result[0].Label);
    }

    [Fact]
    public void RegisterProvider_IsThreadSafe()
    {
        var registry = new ContextMenuRegistry();
        const int threadCount = 20;
        const int itemsPerThread = 5;

        var barrier = new Barrier(threadCount);
        var threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            registry.RegisterProvider<SampleData>(_ =>
                Enumerable.Range(0, itemsPerThread).Select(j => Item($"T{i}I{j}")));
        })).ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var result = registry.GetItems<SampleData>(null!).ToList();
        Assert.Equal(threadCount * itemsPerThread, result.Count);
    }
}
