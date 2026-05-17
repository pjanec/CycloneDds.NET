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
    private const int ConcurrentTaskCount = 4;
    private const int SamplesPerTask = 1000;
    private const int ReadLoopIterations = 5000;

    [Fact]
    public void SampleStore_Append_IncrementsCount()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        Assert.Equal(SampleCount, store.TotalCount);
        Assert.Equal(SampleCount, store.AllSamples.Count);
    }

    [Fact]
    public void SampleStore_GetTopicSamples_ReturnsOnlyMatchingTopic()
    {
        var metadataA = new TopicMetadata(typeof(SampleTopic));
        var metadataB = new TopicMetadata(typeof(SimpleType));
        var store = new SampleStore();

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
    public void SampleStore_GetSamples_ReturnsSliceFromStartIndex()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        var slice = store.GetSamples(SampleCount - 10);

        Assert.Equal(10, slice.Length);
        Assert.Equal(SampleCount - 9, slice[0].Ordinal);
        Assert.Equal(SampleCount, slice[^1].Ordinal);
    }

    [Fact]
    public void SampleStore_GetSamples_EmptyWhenStartIndexEqualsCount()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
        store.Append(CreateSample(metadata, 1));

        var result = store.GetSamples(store.TotalCount);

        Assert.Empty(result);
    }

    [Fact]
    public void SampleStore_Clear_ResetsEverything()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        for (var i = 1; i <= SampleCount; i++)
        {
            store.Append(CreateSample(metadata, i));
        }

        store.Clear();

        Assert.Equal(0, store.TotalCount);
        Assert.Empty(store.AllSamples);
        Assert.Empty(store.GetSamples(0));
    }

    [Fact]
    public async Task SampleStore_ConcurrentAppendAndRead_DoesNotThrow()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();
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
                    _ = store.TotalCount;
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

    internal static SampleData CreateSample(TopicMetadata metadata, long ordinal, int sizeBytes = 0)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = new SampleTopic { Id = (int)ordinal },
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = sizeBytes
        };
    }
}

public sealed class SampleStoreBandwidthTests
{
    [Fact]
    public void TotalBytesReceived_IsZeroInitially()
    {
        var store = new SampleStore();
        Assert.Equal(0, store.TotalBytesReceived);
    }

    [Fact]
    public void TotalBytesReceived_AccumulatesAcrossAppends()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        store.Append(CreateSizedSample(metadata, 1, 100));
        store.Append(CreateSizedSample(metadata, 2, 200));
        store.Append(CreateSizedSample(metadata, 3, 50));

        Assert.Equal(350, store.TotalBytesReceived);
    }

    [Fact]
    public void TotalBytesReceived_ResetToZeroOnClear()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        store.Append(CreateSizedSample(metadata, 1, 100));
        store.Append(CreateSizedSample(metadata, 2, 200));
        Assert.Equal(300, store.TotalBytesReceived);

        store.Clear();

        Assert.Equal(0, store.TotalBytesReceived);
    }

    [Fact]
    public void TotalBytesReceived_IgnoresSamplesWithZeroSize()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var store = new SampleStore();

        store.Append(CreateSizedSample(metadata, 1, 0));
        store.Append(CreateSizedSample(metadata, 2, 0));

        Assert.Equal(0, store.TotalBytesReceived);
    }

    private static SampleData CreateSizedSample(TopicMetadata metadata, long ordinal, int sizeBytes)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = new SampleTopic { Id = (int)ordinal },
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = sizeBytes
        };
    }
}