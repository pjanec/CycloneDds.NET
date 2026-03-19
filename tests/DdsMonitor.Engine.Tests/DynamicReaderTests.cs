using System;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DynamicReaderTests
{
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

}
