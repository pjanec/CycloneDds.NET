using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DdsIngestionServiceTests
{
    private const int SampleCount = 10;
    private const int WaitTimeoutMs = 1000;

    [Fact]
    public async Task IngestionService_ProcessesSamplesFromChannel()
    {
        var channel = Channel.CreateUnbounded<SampleData>();
        using var sampleStore = new SampleStore();
        var instanceStore = new InstanceStore();
        var service = new DdsIngestionService(channel.Reader, sampleStore, instanceStore);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        var metadata = new TopicMetadata(typeof(SampleTopic));
        for (var i = 0; i < SampleCount; i++)
        {
            await channel.Writer.WriteAsync(CreateSample(metadata, i + 1, new SampleTopic { Id = i }));
        }

        await WaitForConditionAsync(() => sampleStore.AllSamples.Count == SampleCount, WaitTimeoutMs);
        Assert.Equal(SampleCount, sampleStore.AllSamples.Count);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IngestionService_RoutesKeyedSamplesToInstanceStore()
    {
        var channel = Channel.CreateUnbounded<SampleData>();
        using var sampleStore = new SampleStore();
        var instanceStore = new TrackingInstanceStore();
        var service = new DdsIngestionService(channel.Reader, sampleStore, instanceStore);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var sample = CreateSample(metadata, 1, new InstanceKeyedMessage { Id = 1, Value = 10 }, DdsInstanceState.Alive);
        await channel.Writer.WriteAsync(sample);

        await WaitForConditionAsync(() => instanceStore.ProcessedCount > 0, WaitTimeoutMs);
        Assert.Equal(1, instanceStore.ProcessedCount);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IngestionService_StopsGracefullyOnCancellation()
    {
        var channel = Channel.CreateUnbounded<SampleData>();
        using var sampleStore = new SampleStore();
        var instanceStore = new InstanceStore();
        var service = new DdsIngestionService(channel.Reader, sampleStore, instanceStore);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        cts.Cancel();
        var stopTask = service.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stopTask, Task.Delay(WaitTimeoutMs));

        Assert.Same(stopTask, completed);
    }

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal, SampleTopic payload)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { InstanceState = DdsInstanceState.Alive },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal, InstanceKeyedMessage payload, DdsInstanceState state)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { InstanceState = state },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs)
    {
        var stopAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < stopAt)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class TrackingInstanceStore : IInstanceStore
    {
        public int ProcessedCount { get; private set; }

        public IObservable<InstanceTransitionEvent> OnInstanceChanged => new EmptyObservable();

        public ITopicInstances GetTopicInstances(Type topicType) => throw new NotSupportedException();

        public void ProcessSample(SampleData sample)
        {
            ProcessedCount++;
        }

        public void Clear()
        {
        }

        private sealed class EmptyObservable : IObservable<InstanceTransitionEvent>
        {
            public IDisposable Subscribe(IObserver<InstanceTransitionEvent> observer)
            {
                return new EmptySubscription();
            }

            private sealed class EmptySubscription : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
