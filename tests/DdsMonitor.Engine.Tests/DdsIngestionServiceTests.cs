using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace DdsMonitor.Engine.Tests;

public sealed class DdsIngestionServiceTests
{
    private const int SampleCount = 10;
    private const int WaitTimeoutMs = 1000;

    private readonly ITestOutputHelper _output;

    public DdsIngestionServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task IngestionService_ProcessesSamplesFromChannel()
    {
        var channel = Channel.CreateUnbounded<SampleData>();
        var sampleStore = new SampleStore();
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
        var sampleStore = new SampleStore();
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
        var sampleStore = new SampleStore();
        var instanceStore = new InstanceStore();
        var service = new DdsIngestionService(channel.Reader, sampleStore, instanceStore);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        cts.Cancel();
        var stopTask = service.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stopTask, Task.Delay(WaitTimeoutMs));

        Assert.Same(stopTask, completed);
    }

    /// <summary>
    /// Verifies that a large pre-filled burst (100 000 samples written before the service
    /// starts) is fully drained in a single tight synchronous loop — all samples must be
    /// present in the store within a generous but bounded time window.
    /// This test acts as a performance regression gate: if the implementation falls back
    /// to per-sample async overhead it will either time-out or be measurably slower.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task IngestionService_ProcessesHighVolumeBurst_Correctly()
    {
        const int burstSize = 100_000;
        var channel = Channel.CreateUnbounded<SampleData>();
        var sampleStore = new SampleStore();
        var instanceStore = new InstanceStore();
        var service = new DdsIngestionService(channel.Reader, sampleStore, instanceStore);

        var metadata = new TopicMetadata(typeof(SampleTopic));

        // Write all samples before starting the service so they arrive as a single burst.
        for (var i = 0; i < burstSize; i++)
        {
            channel.Writer.TryWrite(CreateSample(metadata, i + 1, new SampleTopic { Id = i }));
        }

        using var cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();
        await service.StartAsync(cts.Token);

        await WaitForConditionAsync(() => sampleStore.AllSamples.Count == burstSize, timeoutMs: 8000);
        sw.Stop();

        _output.WriteLine($"Ingested {burstSize:N0} burst samples in {sw.ElapsedMilliseconds} ms");

        Assert.Equal(burstSize, sampleStore.AllSamples.Count);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Measures sustained throughput: samples are streamed continuously while the service
    /// is already running.  Reports samples/second to the test output so regressions are
    /// visible in CI logs without hard-coding a threshold that would be fragile across
    /// machines.  Still asserts that all samples are eventually consumed.
    /// </summary>
    [Fact(Timeout = 15000)]
    public async Task IngestionService_SustainedThroughput_AllSamplesConsumed()
    {
        const int burstSize = 50_000;
        var channel = Channel.CreateUnbounded<SampleData>();
        var sampleStore = new SampleStore();
        var instanceStore = new InstanceStore();
        var service = new DdsIngestionService(channel.Reader, sampleStore, instanceStore);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        var metadata = new TopicMetadata(typeof(SampleTopic));

        // Write samples while the service is already running (sustained stream).
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < burstSize; i++)
        {
            channel.Writer.TryWrite(CreateSample(metadata, i + 1, new SampleTopic { Id = i }));
        }

        await WaitForConditionAsync(() => sampleStore.AllSamples.Count == burstSize, timeoutMs: 12000);
        sw.Stop();

        var throughput = burstSize / (sw.Elapsed.TotalSeconds > 0 ? sw.Elapsed.TotalSeconds : 1);
        _output.WriteLine($"Sustained throughput: {throughput:N0} samples/s ({burstSize:N0} samples in {sw.ElapsedMilliseconds} ms)");

        Assert.Equal(burstSize, sampleStore.AllSamples.Count);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
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

        public event Action? Cleared;

        public ITopicInstances GetTopicInstances(Type topicType) => throw new NotSupportedException();

        public InstanceSnapshot GetTopicSnapshot(Type topicType) => throw new NotSupportedException();

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
