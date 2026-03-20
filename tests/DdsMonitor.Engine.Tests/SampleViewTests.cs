using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for <see cref="SampleView"/> – the per-window background view that replaces
/// the former in-store sort loop.
/// </summary>
public sealed class SampleViewTests
{
    private const int SortTimeoutMs = 3000;
    private const int SliceStartIndex = 10;
    private const int SliceCount = 5;
    private const int FilterThreshold = 50;
    private const int SampleCount = 100;

    // ── Filter ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_SetFilter_ReducesFilteredCount()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);
        view.SetFilter(s => s.Ordinal > FilterThreshold);

        await WaitForViewCountAsync(view, SampleCount - FilterThreshold);

        Assert.Equal(SampleCount - FilterThreshold, view.CurrentFilteredCount);
    }

    [Fact]
    public async Task SampleView_SetFilter_NullShowsAll()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);
        view.SetFilter(s => s.Ordinal > FilterThreshold); // narrow first
        await WaitForViewCountAsync(view, SampleCount - FilterThreshold);

        view.SetFilter(null); // clear filter
        await WaitForViewCountAsync(view, SampleCount);

        Assert.Equal(SampleCount, view.CurrentFilteredCount);
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_SetSortSpec_SortsDescending()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
        var ordinals = new[] { 7, 2, 9, 1, 5, 3 };

        foreach (var ord in ordinals)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, ord));
        }

        using var view = new SampleView(store);
        var sortField = CreateGenericSortField(); // non-fast-path name → general sort exercised
        view.SetSortSpec(sortField, SortDirection.Descending);

        await WaitForViewCountAsync(view, ordinals.Length);

        var viewData = view.GetVirtualView(0, ordinals.Length).ToArray();
        var expected = ordinals.OrderByDescending(x => x).Select(x => (long)x).ToArray();
        Assert.Equal(expected, viewData.Select(s => s.Ordinal));
    }

    [Fact]
    public async Task SampleView_MergeSort_MergesNewArrivals()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
        var sortField = CreateGenericSortField();

        using var view = new SampleView(store);
        view.SetSortSpec(sortField, SortDirection.Ascending); // non-fast-path name → merge-sort logic exercised

        var initial = new[] { 10, 30, 20 };
        foreach (var ord in initial)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, ord));
        }

        await WaitForViewCountAsync(view, initial.Length);

        var initialView = view.GetVirtualView(0, initial.Length).ToArray();
        Assert.Equal(new long[] { 10, 20, 30 }, initialView.Select(s => s.Ordinal));

        var newArrivals = new[] { 25, 5 };
        foreach (var ord in newArrivals)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, ord));
        }

        await WaitForViewCountAsync(view, initial.Length + newArrivals.Length);

        var mergedView = view.GetVirtualView(0, initial.Length + newArrivals.Length).ToArray();
        var expected = initial.Concat(newArrivals).OrderBy(x => x).Select(x => (long)x).ToArray();
        Assert.Equal(expected, mergedView.Select(s => s.Ordinal));
    }

    // ── Ordinal fast-path ─────────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_NullSortField_IncrementsWithoutSort()
    {
        // When no sort field is set the view should simply append in arrival order.
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);

        await WaitForViewCountAsync(view, SampleCount);

        var all = view.GetVirtualView(0, SampleCount).ToArray();
        Assert.Equal(SampleCount, all.Length);
        // Data arrives in ordinal order, so without a sort spec the view should
        // reflect arrival (ascending) order.
        for (var i = 0; i < all.Length; i++)
        {
            Assert.Equal(i + 1, all[i].Ordinal);
        }
    }

    [Fact]
    public async Task SampleView_OrdinalDescending_FastPath_ReadsFromTail()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);
        var sortField = CreateOrdinalSortField();
        view.SetSortSpec(sortField, SortDirection.Descending);

        await WaitForViewCountAsync(view, SampleCount);

        // Index 0 should be the newest (highest ordinal) sample.
        var first = view.GetVirtualView(0, 1).Span[0];
        var last = view.GetVirtualView(SampleCount - 1, 1).Span[0];

        Assert.Equal(SampleCount, first.Ordinal);
        Assert.Equal(1, last.Ordinal);
    }

    // ── Virtualized slice ─────────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_GetVirtualView_ReturnsCorrectSlice()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
        var sortField = CreateOrdinalSortField();

        using var view = new SampleView(store);
        view.SetSortSpec(sortField, SortDirection.Ascending);

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        await WaitForViewCountAsync(view, SampleCount);

        var slice = view.GetVirtualView(SliceStartIndex, SliceCount).ToArray();

        Assert.Equal(SliceCount, slice.Length);
        Assert.Equal(SliceStartIndex + 1, slice[0].Ordinal);
        Assert.Equal(SliceStartIndex + SliceCount, slice[^1].Ordinal);
    }

    // ── Clear detection ───────────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_StoreCleared_ResetsView()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);
        await WaitForViewCountAsync(view, SampleCount);

        // Simulate external store clear; no direct notification to view —
        // the view must detect the count drop on its next polling cycle.
        store.Clear();

        await WaitForViewCountAsync(view, 0);

        Assert.Equal(0, view.CurrentFilteredCount);
        Assert.True(view.GetVirtualView(0, 1).IsEmpty);
    }

    // ── GetFilteredSnapshot ───────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_GetFilteredSnapshot_AscendingOrderMatchesView()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);
        await WaitForViewCountAsync(view, SampleCount);

        var snapshot = view.GetFilteredSnapshot();
        var virtualAll = view.GetVirtualView(0, SampleCount).ToArray();

        Assert.Equal(SampleCount, snapshot.Length);
        Assert.Equal(virtualAll.Select(s => s.Ordinal), snapshot.Select(s => s.Ordinal));
    }

    [Fact]
    public async Task SampleView_GetFilteredSnapshot_DescendingOrdinalIsReversed()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        using var view = new SampleView(store);
        view.SetSortSpec(CreateOrdinalSortField(), SortDirection.Descending);
        await WaitForViewCountAsync(view, SampleCount);

        var snapshot = view.GetFilteredSnapshot();

        Assert.Equal(SampleCount, snapshot.Length);
        Assert.Equal(SampleCount, snapshot[0].Ordinal);  // newest first
        Assert.Equal(1L, snapshot[^1].Ordinal);           // oldest last
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void SampleView_Dispose_CanBeCalledMultipleTimes()
    {
        var store = new SampleStore();
        var view = new SampleView(store);
        view.Dispose();
        view.Dispose(); // should not throw
    }

    // ── Sort cancellation ─────────────────────────────────────────────────────

    [Fact]
    public async Task SampleView_SortSpec_NewRequestOverridesOld()
    {
        // Verify that the latest sort spec wins, even when applied quickly.
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        var sortField = CreateOrdinalSortField();
        using var view = new SampleView(store);

        view.SetSortSpec(sortField, SortDirection.Ascending);
        view.SetSortSpec(sortField, SortDirection.Descending); // override immediately

        await WaitForViewCountAsync(view, SampleCount);

        // The final state should reflect descending order.
        var first = view.GetVirtualView(0, 1).Span[0];
        Assert.Equal(SampleCount, first.Ordinal);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Sort field named "Ordinal" – triggers the ordinal fast-path in SampleView.</summary>
    private static FieldMetadata CreateOrdinalSortField()
    {
        return new FieldMetadata(
            "Ordinal",
            "Ordinal",
            typeof(long),
            sample => ((SampleData)sample).Ordinal,
            (_, __) => { },
            true);
    }

    /// <summary>Sort field that reads the Ordinal value but uses a non-fast-path name,
    /// so that general sort/merge logic is exercised even when ordinals are non-sequential.</summary>
    private static FieldMetadata CreateGenericSortField()
    {
        return new FieldMetadata(
            "SortKey",
            "SortKey",
            typeof(long),
            sample => ((SampleData)sample).Ordinal,
            (_, __) => { },
            true);
    }

    private static async Task WaitForViewCountAsync(SampleView view, int expectedCount)
    {
        if (view.CurrentFilteredCount == expectedCount || view.GetVirtualView(0, Math.Max(1, expectedCount)).Length == expectedCount)
        {
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler()
        {
            var count = view.CurrentFilteredCount;
            if (count == expectedCount || (expectedCount == 0 && count == 0))
            {
                view.OnViewRebuilt -= Handler;
                tcs.TrySetResult(true);
            }
        }

        view.OnViewRebuilt += Handler;

        // Re-check after subscribing to avoid race condition.
        if (view.CurrentFilteredCount == expectedCount)
        {
            view.OnViewRebuilt -= Handler;
            return;
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(SortTimeoutMs));
        view.OnViewRebuilt -= Handler;

        Assert.Same(tcs.Task, completed);
    }
}
