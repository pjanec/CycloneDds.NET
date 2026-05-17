using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace DdsMonitor.Engine.Tests;

public sealed class DynamicReaderTests
{
    private readonly ITestOutputHelper _output;

    public DynamicReaderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DynamicReader_CanBeConstructedViaReflection()
    {
        var metadata = new TopicMetadata(typeof(DynamicReaderMessage));
        using var participant = new DdsParticipant();

        var readerType = typeof(DynamicReader<>).MakeGenericType(metadata.TopicType);
        var instance = Activator.CreateInstance(readerType, participant, metadata, null);

        var dynamicReader = Assert.IsAssignableFrom<IDynamicReader>(instance);
        dynamicReader.Dispose();
    }

    [Fact(Timeout = 5000)]
    public async Task DynamicReader_ReceivesSample_FromDynamicWriter()
    {
        var metadata = new TopicMetadata(typeof(DynamicReaderMessage));
        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<DynamicReaderMessage>(participant, metadata);
        using var writer = new DynamicWriter<DynamicReaderMessage>(participant, metadata);

        var tcs = new TaskCompletionSource<SampleData>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.OnSampleReceived += sample => tcs.TrySetResult(sample);

        reader.Start(null);

        await Task.Delay(200);

        var message = new DynamicReaderMessage { Id = 42, Value = 7 };
        writer.Write(message);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completed);

        var received = await tcs.Task;
        var payload = Assert.IsType<DynamicReaderMessage>(received.Payload);
        Assert.Equal(42, payload.Id);
        Assert.Equal(7, payload.Value);

        reader.Stop();
    }

    [Fact(Timeout = 10000)]
    public async Task DynamicReader_PopulatesSizeBytes_UsingGeneratedNativeSizer()
    {
        var metadata = new TopicMetadata(typeof(DynamicReaderMessage));
        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<DynamicReaderMessage>(participant, metadata);
        using var writer = new DynamicWriter<DynamicReaderMessage>(participant, metadata);

        var tcs = new TaskCompletionSource<SampleData>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.OnSampleReceived += sample => tcs.TrySetResult(sample);

        reader.Start(null);

        await Task.Delay(200);

        writer.Write(new DynamicReaderMessage { Id = 1, Value = 2 });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(7000));
        Assert.Same(tcs.Task, completed);

        var received = await tcs.Task;
        // DynamicReaderMessage has two int fields; GetNativeSize must return > 0.
        Assert.True(received.SizeBytes > 0, $"Expected SizeBytes > 0 but got {received.SizeBytes}");

        reader.Stop();
    }

    /// <summary>
    /// Writes a larger burst of messages and verifies that the reader's synchronous
    /// drain loop collects *all* of them without any samples being lost.
    /// This exercises the "keep draining until queue empty" path introduced by the
    /// cpu-usage fix: without the loop, only <c>DefaultMaxSamples</c> (32) per
    /// WaitDataAsync wakeup would be consumed during a burst.
    /// Uses <see cref="DrainTestMessage"/> which is configured with KEEP_ALL + Reliable QoS
    /// so that all burst samples are buffered in the DDS reader queue.
    /// </summary>
    [Fact(Timeout = 20000)]
    public async Task DynamicReader_DrainsBurstCompletely_AllSamplesReceived()
    {
        const int burstSize = 200; // well above DefaultMaxSamples (32)

        var metadata = new TopicMetadata(typeof(DrainTestMessage));
        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<DrainTestMessage>(participant, metadata);
        using var writer = new DynamicWriter<DrainTestMessage>(participant, metadata);

        var received = new List<SampleData>();
        var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        reader.OnSampleReceived += sample =>
        {
            lock (received)
            {
                received.Add(sample);
                if (received.Count >= burstSize)
                {
                    allReceived.TrySetResult(true);
                }
            }
        };

        reader.Start(null);
        await Task.Delay(500); // let reader and writer match

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < burstSize; i++)
        {
            writer.Write(new DrainTestMessage { Id = i, Value = i * 2 });
        }

        var completed = await Task.WhenAny(allReceived.Task, Task.Delay(17000));
        sw.Stop();

        _output.WriteLine($"Received {received.Count}/{burstSize} samples in {sw.ElapsedMilliseconds} ms");

        Assert.Same(allReceived.Task, completed);
        Assert.Equal(burstSize, received.Count);

        reader.Stop();
    }

}
