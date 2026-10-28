using System;
using System.Linq;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
using Xunit;

namespace DdsMonitor.Engine.Tests;

// Topic types shaped like the SUMO blob frames HROT publishes (sumo/vehicles, sumo/tl, sumo/geometry): a small header and one
// large C# fixed-size byte buffer. The size is the point - ddsmonitor must read these like any other topic.
[DdsTopic("LargeFixedBuffer16K")]
public unsafe partial struct LargeFixedBuffer16K
{
    public int Id;
    public fixed byte Payload[16 * 1024];
}

[DdsTopic("LargeFixedBuffer60K")]
public unsafe partial struct LargeFixedBuffer60K
{
    public int Id;
    public fixed byte Payload[60 * 1024];
}

[DdsTopic("LargeFixedBuffer64K")]
public unsafe partial struct LargeFixedBuffer64K
{
    public int Id;
    public fixed byte Payload[65536];
}

// A structural copy of HROT's sumo/vehicles frame (Sim.Replication.Dds.DdsWireFrame): keyed, explicit field ids, BestEffort/KeepLast(64),
// the 64 KiB blob in the middle and a trailing key. This is the exact shape ddsmonitor reportedly could not read.
[DdsTopic("SumoLikeWireFrame")]
[DdsQos(Reliability = DdsReliability.BestEffort, Durability = DdsDurability.Volatile, HistoryKind = DdsHistoryKind.KeepLast, HistoryDepth = 64)]
public unsafe partial struct SumoLikeWireFrame
{
    [DdsKey, DdsId(0)] public byte Kind;
    [DdsKey, DdsId(1)] public int ChunkIndex;
    [DdsId(2)] public uint Step;
    [DdsId(3)] public float Time;
    [DdsId(4)] public int ByteCount;
    [DdsId(5)] public fixed byte Payload[65536];
    [DdsKey, DdsId(6)] public uint Source;
}

/// <summary>
/// ddsmonitor reading topics whose sample holds a big fixed buffer (the SUMO blob topics: <c>fixed byte Payload[65536]</c>).
/// Reported failure: <c>DynamicReader.DrainReader</c> threw <c>InvalidProgramException</c> on the background read task - which
/// only resurfaced at shutdown - so the capture silently lacked those topics (HROT tracker CE-4018).
/// The values are built and checked through the monitor's own boxed field accessors so that this test never holds a
/// 64 KB struct in a local itself.
/// </summary>
public sealed class LargeFixedBufferTests
{
    [Fact(Timeout = 30000)]
    public Task DynamicReader_ReceivesSample_OfA16KbFixedBufferTopic() => RoundTrip<LargeFixedBuffer16K>(16 * 1024);

    [Fact(Timeout = 30000)]
    public Task DynamicReader_ReceivesSample_OfA60KbFixedBufferTopic() => RoundTrip<LargeFixedBuffer60K>(60 * 1024);

    [Fact(Timeout = 30000)]
    public Task DynamicReader_ReceivesSample_OfA64KbFixedBufferTopic_TheSumoBlobShape() => RoundTrip<LargeFixedBuffer64K>(65536);

    /// <summary>The exact SUMO blob shape (keyed, ids, QoS, trailing key): one sample must come through with its blob intact.</summary>
    [Fact(Timeout = 30000)]
    public async Task DynamicReader_ReceivesSample_OfTheSumoWireFrameShape()
    {
        var metadata = new TopicMetadata(typeof(SumoLikeWireFrame));
        var payloadField = metadata.AllFields.Single(f => f.StructuredName == "Payload");
        var chunkField = metadata.AllFields.Single(f => f.StructuredName == "ChunkIndex");

        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<SumoLikeWireFrame>(participant, metadata);
        using var writer = new DynamicWriter<SumoLikeWireFrame>(participant, metadata);
        var received = new TaskCompletionSource<SampleData>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.OnSampleReceived += sample => received.TrySetResult(sample);
        reader.Start(null);
        await Task.Delay(300);

        object boxed = Activator.CreateInstance<SumoLikeWireFrame>();
        chunkField.Setter(boxed, 3);
        var bytes = new byte[65536];
        bytes[0] = 0x11;
        bytes[65535] = 0x22;
        payloadField.Setter(boxed, bytes);
        writer.Write((SumoLikeWireFrame)boxed);

        var completed = await Task.WhenAny(received.Task, Task.Delay(10000));
        Assert.True(ReferenceEquals(received.Task, completed), "the SUMO-shaped sample never arrived");
        var got = Assert.IsType<byte[]>(payloadField.Getter((await received.Task).Payload));
        Assert.Equal(0x11, got[0]);
        Assert.Equal(0x22, got[65535]);
        reader.Stop();
    }

    /// <summary>A burst larger than one 32-sample Take batch (SUMO publishes chunk after chunk): every instance must arrive.</summary>
    [Fact(Timeout = 60000)]
    public async Task DynamicReader_ReceivesABurst_OfSumoWireFrames_AcrossSeveralDrainBatches()
    {
        const int burst = 48;
        var metadata = new TopicMetadata(typeof(SumoLikeWireFrame));
        var chunkField = metadata.AllFields.Single(f => f.StructuredName == "ChunkIndex");

        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<SumoLikeWireFrame>(participant, metadata);
        using var writer = new DynamicWriter<SumoLikeWireFrame>(participant, metadata);
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.OnSampleReceived += sample =>
        {
            seen[(int)chunkField.Getter(sample.Payload)!] = 1;
            if (seen.Count == burst) all.TrySetResult();
        };
        reader.Start(null);
        await Task.Delay(300);

        for (int i = 0; i < burst; i++)
        {
            object boxed = Activator.CreateInstance<SumoLikeWireFrame>();
            chunkField.Setter(boxed, i);
            writer.Write((SumoLikeWireFrame)boxed);
        }

        var completed = await Task.WhenAny(all.Task, Task.Delay(20000));
        Assert.True(ReferenceEquals(all.Task, completed), $"only {seen.Count} of {burst} SUMO-shaped samples arrived");
        reader.Stop();
    }

    private static async Task RoundTrip<T>(int bufferLength) where T : unmanaged
    {
        var metadata = new TopicMetadata(typeof(T));
        var idField = metadata.AllFields.Single(f => f.StructuredName == "Id");
        var payloadField = metadata.AllFields.Single(f => f.StructuredName == "Payload");
        Assert.True(payloadField.IsFixedSizeArray);
        Assert.Equal(bufferLength, payloadField.FixedArrayLength);

        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<T>(participant, metadata);
        using var writer = new DynamicWriter<T>(participant, metadata);

        var received = new TaskCompletionSource<SampleData>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.OnSampleReceived += sample => received.TrySetResult(sample);
        reader.Start(null);
        await Task.Delay(300);

        // Build the sample through boxed accessors: first and last byte marked, the rest zero.
        object boxed = Activator.CreateInstance<T>()!;
        idField.Setter(boxed, 4242);
        var bytes = new byte[bufferLength];
        bytes[0] = 0xAB;
        bytes[bufferLength - 1] = 0xCD;
        payloadField.Setter(boxed, bytes);
        writer.Write((T)boxed);

        var completed = await Task.WhenAny(received.Task, Task.Delay(10000));
        Assert.True(ReferenceEquals(received.Task, completed),
            $"No sample of the {bufferLength}-byte fixed-buffer topic arrived through the DynamicReader.");

        var sample2 = await received.Task;
        Assert.Equal(4242, idField.Getter(sample2.Payload));
        var got = Assert.IsType<byte[]>(payloadField.Getter(sample2.Payload));
        Assert.Equal(bufferLength, got.Length);
        Assert.Equal(0xAB, got[0]);
        Assert.Equal(0xCD, got[bufferLength - 1]);

        // Stop() rethrows an exception that faulted the background read task - the reported crash surfaced here, at shutdown.
        reader.Stop();
    }
}

/// <summary>
/// The capture path for the SUMO blob shape: its JSON must round-trip with the blob intact.
/// </summary>
public sealed class LargeFixedBufferJsonTests
{
    [Fact]
    public void SumoWireFrame_RoundTripsThroughTheCaptureJson()
    {
        var metadata = new TopicMetadata(typeof(SumoLikeWireFrame));
        var payloadField = metadata.AllFields.Single(f => f.StructuredName == "Payload");
        object boxed = Activator.CreateInstance<SumoLikeWireFrame>();
        var bytes = new byte[65536];
        bytes[0] = 0x11;
        bytes[65535] = 0x22;
        payloadField.Setter(boxed, bytes);

        var json = System.Text.Json.JsonSerializer.Serialize(boxed, boxed.GetType(), DdsJsonOptions.Export);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, boxed.GetType(), DdsJsonOptions.Import)!;

        var got = Assert.IsType<byte[]>(payloadField.Getter(back));
        Assert.Equal(0x11, got[0]);
        Assert.Equal(0x22, got[65535]);
    }
}
