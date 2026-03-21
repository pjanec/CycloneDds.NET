using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// xUnit collection that runs MemoryReleaseTests sequentially to avoid interference
/// from concurrent allocations in other test classes when measuring GC heap size.
/// </summary>
[CollectionDefinition("MemoryReleaseTests", DisableParallelization = true)]
public sealed class MemoryReleaseTestsCollection { }

/// <summary>
/// Proves that sample memory is actually reclaimed by the GC after
/// <see cref="SampleStore.Clear"/> (optionally with <see cref="SampleView"/> in the loop).
///
/// All tests in this class are sequential (no parallelism) to avoid skewing
/// GC.GetTotalMemory() measurements with allocations from other tests.
///
/// Test strategy
/// ─────────────
/// 1. <b>WeakReference tests</b> – deterministic proof that SampleData objects
///    are GC-collectible.  A WeakReference survives GC.Collect only if there is
///    still a live strong reference.
/// 2. <b>Memory-size tests</b> – quantitative proof that the managed heap
///    shrinks significantly (≥ 80 % of allocated bytes freed) after Clear + GC.
/// </summary>
[Collection("MemoryReleaseTests")]
public sealed class MemoryReleaseTests
{
    private readonly ITestOutputHelper _output;

    public MemoryReleaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── WeakReference tests ───────────────────────────────────────────────────

    /// <summary>
    /// Core regression test:  a <see cref="SampleData"/> that was stored in a live
    /// <see cref="SampleStore"/> must be GC-collectible immediately after
    /// <see cref="SampleStore.Clear"/> even though the store itself is still alive.
    ///
    /// Before the <c>TrimExcess</c> fix, <c>List&lt;T&gt;.Clear()</c> already null-ed the
    /// array slots (so the <em>objects</em> were collectible), but the LOH backing array
    /// remained allocated at full capacity.  This test validates that the objects ARE
    /// collectible and serves as a regression guard.
    /// </summary>
    [Fact]
    public void SampleStore_Clear_SamplesAreGcCollectible_WhileStoreRemainsAlive()
    {
        const int count = 1_000;
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();   // store stays alive for the whole test

        var weakRef = FillThenClearStore(store, metadata, count);

        ForceFullGc();

        Assert.False(weakRef.IsAlive,
            "SampleData should be GC-collectible after SampleStore.Clear() " +
            "even while the store instance itself is still alive.");

        GC.KeepAlive(store);   // prevent the JIT from collecting store before the assertion
    }

    /// <summary>
    /// End-to-end regression test: <see cref="SampleView"/> (the background worker that
    /// mirrors the store into a sorted list) must also release its internal
    /// <c>_sortedView</c> references within its next polling cycle (≤ 50 ms) after the
    /// store is cleared, allowing the GC to reclaim all SampleData objects.
    ///
    /// This proves that the "storeCleared" detection in
    /// <c>SampleView.ProcessBatch</c> works and calls both <c>Clear()</c> and
    /// <c>TrimExcess()</c> on the backing list.
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task SampleView_AfterStoreClear_SamplesAreGcCollectible()
    {
        const int count = 5_000;
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
        using var view = new SampleView(store);

        // 1. Fill store and wait for SampleView to ingest all samples.
        var weakRef = FillStore(store, metadata, count);
        await WaitForViewCountAsync(view, count, timeoutMs: 3000);

        // 2. Clear the store.  SampleView still holds all N references in _sortedView.
        store.Clear();

        // 3. Give SampleView's 50 ms timer one full cycle to detect the store clear and
        //    call _sortedView.Clear() + TrimExcess().
        await Task.Delay(200);

        // 4. Now force a full GC.  All SampleData objects should be unreachable.
        ForceFullGc();

        Assert.False(weakRef.IsAlive,
            "SampleData should be GC-collectible after SampleStore.Clear() once " +
            "SampleView has processed the store-cleared event.");

        GC.KeepAlive(store);
        GC.KeepAlive(view);
    }

    // ── Memory-size tests ─────────────────────────────────────────────────────

    /// <summary>
    /// Quantifies how much memory is freed when <see cref="SampleStore.Clear"/> is called
    /// on a store that grew to 100 000 samples.
    ///
    /// The test asserts that at least 80 % of the allocation is freed, which proves:
    /// <list type="bullet">
    ///   <item>SampleData object graphs are collected by the GC.</item>
    ///   <item><c>TrimExcess()</c> released the LOH backing array so the managed heap
    ///   size drops (not just the GC live-set).</item>
    /// </list>
    ///
    /// Note: GC.GetTotalMemory(forceFullCollection:true) triggers a blocking full GC
    /// before measuring, making the result deterministic.
    /// </summary>
    [Fact]
    public void SampleStore_Clear_ReleasesMoreThan80PctOfAllocatedMemory()
    {
        const int sampleCount = 100_000;

        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        // Establish a stable baseline (no pending GC objects from this test).
        ForceFullGc();
        var baselineBytes = GC.GetTotalMemory(forceFullCollection: false);

        // Fill the store.
        for (var i = 1; i <= sampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        var peakBytes = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedBytes = peakBytes - baselineBytes;

        // Clear and force a full collection.
        store.Clear();
        var afterBytes = GC.GetTotalMemory(forceFullCollection: true);

        var freedBytes = peakBytes - afterBytes;
        var freedPct = allocatedBytes > 0 ? freedBytes * 100.0 / allocatedBytes : 100.0;

        _output.WriteLine(
            $"Baseline : {baselineBytes / 1024.0:N1} KB\n" +
            $"Peak     : {peakBytes / 1024.0:N1} KB  (+{allocatedBytes / 1024.0:N1} KB for {sampleCount:N0} samples)\n" +
            $"After GC : {afterBytes / 1024.0:N1} KB\n" +
            $"Freed    : {freedBytes / 1024.0:N1} KB  ({freedPct:F1}% of allocated)");

        Assert.True(allocatedBytes > 0,
            "Sanity: appending 100K samples must measurably increase managed heap size.");

        Assert.True(freedPct >= 80,
            $"Expected ≥ 80% of the {allocatedBytes / 1024:N0} KB allocated to be freed " +
            $"after Clear() + GC, but only {freedPct:F1}% was freed " +
            $"({freedBytes / 1024:N0} KB / {allocatedBytes / 1024:N0} KB).");

        GC.KeepAlive(store);
    }

    /// <summary>
    /// Full pipeline test: proves the end-to-end memory release when both
    /// <see cref="SampleStore"/> and <see cref="SampleView"/> are active.
    ///
    /// Scenario mirrors a user opening the Samples panel, receiving 50 000 samples,
    /// clicking Reset, and then checking that memory returns to near-baseline.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SampleViewPipeline_AfterReset_ReleasesMoreThan80PctOfAllocatedMemory()
    {
        const int sampleCount = 50_000;

        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
        using var view = new SampleView(store);

        // Baseline
        ForceFullGc();
        var baselineBytes = GC.GetTotalMemory(forceFullCollection: false);

        // Fill + wait for SampleView to absorb everything
        for (var i = 1; i <= sampleCount; i++)
        {
            store.Append(SampleStoreTests.CreateSample(metadata, i));
        }

        await WaitForViewCountAsync(view, sampleCount, timeoutMs: 8000);

        var peakBytes = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedBytes = peakBytes - baselineBytes;

        // Reset: clear both the store and let the view detect it
        store.Clear();

        // SampleView worker fires every 50 ms; 200 ms gives it multiple chances
        // to detect storeCleared, call _sortedView.Clear() + TrimExcess(), and return
        await Task.Delay(200);

        var afterBytes = GC.GetTotalMemory(forceFullCollection: true);
        var freedBytes = peakBytes - afterBytes;
        var freedPct = allocatedBytes > 0 ? freedBytes * 100.0 / allocatedBytes : 100.0;

        _output.WriteLine(
            $"Baseline : {baselineBytes / 1024.0:N1} KB\n" +
            $"Peak     : {peakBytes / 1024.0:N1} KB  (+{allocatedBytes / 1024.0:N1} KB for {sampleCount:N0} samples)\n" +
            $"After GC : {afterBytes / 1024.0:N1} KB\n" +
            $"Freed    : {freedBytes / 1024.0:N1} KB  ({freedPct:F1}% of allocated)");

        Assert.True(allocatedBytes > 0,
            "Sanity: filling store + view must measurably increase managed heap.");

        Assert.True(freedPct >= 80,
            $"Expected ≥ 80% freed after pipeline reset + GC, but only {freedPct:F1}% was freed.");

        GC.KeepAlive(store);
        GC.KeepAlive(view);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills a store with <paramref name="count"/> samples from a separate stack frame
    /// (preventing JIT from keeping references live in the caller's frame), then clears
    /// the store.  Returns a <see cref="WeakReference"/> to the last sample added.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FillThenClearStore(SampleStore store, TopicMetadata metadata, int count)
    {
        SampleData? last = null;
        for (var i = 1; i <= count; i++)
        {
            last = SampleStoreTests.CreateSample(metadata, i);
            store.Append(last);
        }

        var weakRef = new WeakReference(last!);
        last = null;

        store.Clear();

        return weakRef;
    }

    /// <summary>
    /// Fills a store from a separate stack frame (no local references in caller).
    /// Returns a <see cref="WeakReference"/> to the last sample that was added.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FillStore(SampleStore store, TopicMetadata metadata, int count)
    {
        SampleData? last = null;
        for (var i = 1; i <= count; i++)
        {
            last = SampleStoreTests.CreateSample(metadata, i);
            store.Append(last);
        }

        var weakRef = new WeakReference(last!);
        last = null;
        return weakRef;
    }

    private static void ForceFullGc()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    private static async Task WaitForViewCountAsync(SampleView view, int expected, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (view.CurrentFilteredCount == expected) return;
            await Task.Delay(20);
        }

        Assert.Equal(expected, view.CurrentFilteredCount);
    }
}
