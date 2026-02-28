using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class SampleStoreTests
{
    private const int SampleCount = 100;
    private const int TopicSampleCount = 5;
    private const int FilterThreshold = 50;
    private const int SortTimeoutMs = 2000;
    private const int SliceStartIndex = 10;
    private const int SliceCount = 5;
    private const int ConcurrentTaskCount = 4;
    private const int SamplesPerTask = 1000;
    private const int ReadLoopIterations = 5000;

    [Fact]
    public void SampleStore_Append_IncrementsCount()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        Assert.Equal(SampleCount, store.AllSamples.Count);
    }

    [Fact]
    public void SampleStore_GetTopicSamples_ReturnsOnlyMatchingTopic()
    {
        var metadataA = new TopicMetadata(typeof(SampleTopic));
        var metadataB = new TopicMetadata(typeof(SimpleType));
        using var store = new SampleStore();

        for (var i = 0; i < TopicSampleCount; i++)
        {
            store.Append(CreateSample(metadataA, i + 1));
            store.Append(CreateSample(metadataB, i + 1));
        }

        var samplesA = store.GetTopicSamples(metadataA.TopicType);
        var samplesB = store.GetTopicSamples(metadataB.TopicType);

        Assert.Equal(TopicSampleCount, samplesA.TotalCount);
        Assert.Equal(TopicSampleCount, samplesB.TotalCount);
        Assert.All(samplesA.Samples, sample => Assert.Same(metadataA, sample.TopicMetadata));
        Assert.All(samplesB.Samples, sample => Assert.Same(metadataB, sample.TopicMetadata));
    }

    [Fact]
    public void SampleStore_SetFilter_ReducesFilteredCount()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        store.SetFilter(sample => sample.Ordinal > FilterThreshold);

        Assert.Equal(SampleCount - FilterThreshold, store.CurrentFilteredCount);
    }

    [Fact]
    public async Task SampleStore_SetSortSpec_SortsDescending()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();
        var ordinals = new[] { 7, 2, 9, 1, 5, 3 };

        foreach (var ordinal in ordinals)
        {
            store.Append(CreateSample(metadata, ordinal));
        }

        var sortField = CreateOrdinalSortField();
        store.SetSortSpec(sortField, SortDirection.Descending);

        var waitTask = WaitForViewRebuilt(store);
        store.Append(CreateSample(metadata, ordinals[^1] + 1));

        var completed = await Task.WhenAny(waitTask, Task.Delay(SortTimeoutMs));
        Assert.Same(waitTask, completed);

        var view = store.GetVirtualView(0, ordinals.Length + 1).ToArray();
        var ordered = view.Select(sample => sample.Ordinal).ToArray();
        var expected = ordinals.Concat(new[] { ordinals[^1] + 1 })
            .OrderByDescending(value => value)
            .Select(value => (long)value)
            .ToArray();

        Assert.Equal(expected, ordered);
    }

    [Fact]
    public async Task SampleStore_MergeSort_MergesNewArrivals()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();
        var sortField = CreateOrdinalSortField();

        store.SetSortSpec(sortField, SortDirection.Ascending);

        var initial = new[] { 10, 30, 20 };
        var initialWait = WaitForViewRebuilt(store);
        foreach (var ordinal in initial)
        {
            store.Append(CreateSample(metadata, ordinal));
        }

        var initialCompleted = await Task.WhenAny(initialWait, Task.Delay(SortTimeoutMs));
        Assert.Same(initialWait, initialCompleted);

        var initialView = store.GetVirtualView(0, initial.Length).ToArray();
        Assert.Equal(new long[] { 10, 20, 30 }, initialView.Select(sample => sample.Ordinal));

        var newArrivals = new[] { 25, 5 };
        var mergeWait = WaitForViewRebuilt(store);
        foreach (var ordinal in newArrivals)
        {
            store.Append(CreateSample(metadata, ordinal));
        }

        var mergeCompleted = await Task.WhenAny(mergeWait, Task.Delay(SortTimeoutMs));
        Assert.Same(mergeWait, mergeCompleted);

        var mergedView = store.GetVirtualView(0, initial.Length + newArrivals.Length).ToArray();
        var expected = initial.Concat(newArrivals).OrderBy(value => value).Select(value => (long)value).ToArray();
        Assert.Equal(expected, mergedView.Select(sample => sample.Ordinal));
    }

    [Fact]
    public async Task SampleStore_Clear_ResetsEverything()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnViewRebuilt += () => tcs.TrySetResult(true);

        store.Clear();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(SortTimeoutMs));
        Assert.Same(tcs.Task, completed);

        Assert.Empty(store.AllSamples);
        Assert.Equal(0, store.CurrentFilteredCount);
        Assert.Empty(store.GetVirtualView(0, 1).ToArray());
    }

    [Fact]
    public async Task SampleStore_GetVirtualView_ReturnsCorrectSlice()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();

        store.SetSortSpec(CreateOrdinalSortField(), SortDirection.Ascending);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnViewRebuilt += () => tcs.TrySetResult(true);

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(SortTimeoutMs));
        Assert.Same(tcs.Task, completed);

        var slice = store.GetVirtualView(SliceStartIndex, SliceCount).ToArray();

        Assert.Equal(SliceCount, slice.Length);
        Assert.Equal(SliceStartIndex + 1, slice[0].Ordinal);
        Assert.Equal(SliceStartIndex + SliceCount, slice[^1].Ordinal);
    }

    [Fact]
    public async Task SampleStore_ConcurrentAppendAndRead_DoesNotThrow()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var store = new SampleStore();
        var exceptions = new List<Exception>();
        var sync = new object();

        var appendTasks = new List<Task>();
        for (var taskIndex = 0; taskIndex < ConcurrentTaskCount; taskIndex++)
        {
            appendTasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < SamplesPerTask; i++)
                {
                    store.Append(CreateSample(metadata, i + 1));
                }
            }));
        }

        var readTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < ReadLoopIterations; i++)
                {
                    _ = store.CurrentFilteredCount;
                }
            }
            catch (Exception ex)
            {
                lock (sync)
                {
                    exceptions.Add(ex);
                }
            }
        });

        appendTasks.Add(readTask);

        await Task.WhenAll(appendTasks);

        Assert.Empty(exceptions);
    }

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = new SampleTopic { Id = (int)ordinal },
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

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

    private static Task WaitForViewRebuilt(SampleStore store)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler()
        {
            store.OnViewRebuilt -= Handler;
            tcs.TrySetResult(true);
        }

        store.OnViewRebuilt += Handler;
        return tcs.Task;
    }
}
