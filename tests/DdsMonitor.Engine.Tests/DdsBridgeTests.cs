using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Channels;
using CycloneDDS.Schema;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DdsBridgeTests
{
    [Fact]
    public void DdsBridge_Subscribe_CreatesReader()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        var reader = bridge.Subscribe(metadata);

        Assert.True(bridge.ActiveReaders.TryGetValue(metadata.TopicType, out var stored));
        Assert.Same(reader, stored);
    }

    [Fact]
    public void DdsBridge_Unsubscribe_RemovesReader()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metadata);
        bridge.Unsubscribe(metadata);

        Assert.False(bridge.ActiveReaders.ContainsKey(metadata.TopicType));
    }

    [Fact]
    public void DdsBridge_ChangePartition_RecreatesReaders()
    {
        var metadataA = new TopicMetadata(typeof(SampleTopic));
        var metadataB = new TopicMetadata(typeof(SimpleType));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        var readerA = bridge.Subscribe(metadataA);
        var readerB = bridge.Subscribe(metadataB);
        var participant = bridge.Participant;

        bridge.ChangePartition("Partition-B");

        Assert.Same(participant, bridge.Participant);
        Assert.Equal(2, bridge.ActiveReaders.Count);
        Assert.Equal("Partition-B", bridge.CurrentPartition);
        Assert.True(bridge.ActiveReaders.TryGetValue(metadataA.TopicType, out var newReaderA));
        Assert.True(bridge.ActiveReaders.TryGetValue(metadataB.TopicType, out var newReaderB));
        Assert.NotSame(readerA, newReaderA);
        Assert.NotSame(readerB, newReaderB);
    }

    [Fact]
    public void DdsBridge_Subscribe_InvalidTopic_DoesNotThrow()
    {
        var invalidType = CreateInvalidTopicType();
        var metadata = new TopicMetadata(invalidType);
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        var reader = bridge.Subscribe(metadata);

        Assert.Equal(invalidType, reader.TopicType);
        Assert.False(bridge.ActiveReaders.ContainsKey(invalidType));
    }

    [Fact]
    public void DdsBridge_Subscribe_WiresOnSampleReceivedToChannel()
    {
        // Verifying the missing-link bug: after subscribing, firing the reader's
        // OnSampleReceived event must deliver the sample into the ingestion channel.
        var channel = Channel.CreateUnbounded<SampleData>();
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(channel.Writer, initialPartition: null);

        var reader = bridge.Subscribe(metadata);

        // Use reflection to get the backing delegate of the event so we can fire it
        // without requiring a real DDS sample to arrive.
        var eventField = reader.GetType().GetField(
            "OnSampleReceived",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(eventField); // event backing field must be present

        var del = (Action<SampleData>?)eventField!.GetValue(reader);
        Assert.NotNull(del); // the bridge must have attached its forwarding handler

        var sample = new SampleData { TopicMetadata = metadata };
        del!.Invoke(sample);

        Assert.True(channel.Reader.TryRead(out var received));
        Assert.Same(sample, received);
    }

    [Fact]
    public void DdsBridge_ChangePartition_RewiredReadersForwardToChannel()
    {
        // After ChangePartition all NEW readers must also be wired to the channel.
        var channel = Channel.CreateUnbounded<SampleData>();
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(channel.Writer, initialPartition: null);

        bridge.Subscribe(metadata);
        bridge.ChangePartition("PartitionX");

        var newReader = bridge.ActiveReaders[metadata.TopicType];

        var eventField = newReader.GetType().GetField(
            "OnSampleReceived",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var del = (Action<SampleData>?)eventField?.GetValue(newReader);
        Assert.NotNull(del);

        var sample = new SampleData { TopicMetadata = metadata };
        del!.Invoke(sample);

        Assert.True(channel.Reader.TryRead(out var received));
        Assert.Same(sample, received);
    }

    private static Type CreateInvalidTopicType()
    {
        var assemblyName = new AssemblyName("DdsBridgeInvalidTopicTests");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");
        var typeBuilder = moduleBuilder.DefineType(
            "InvalidTopic",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            typeof(ValueType));

        typeBuilder.DefineField("Id", typeof(int), FieldAttributes.Public);

        var attributeConstructor = typeof(DdsTopicAttribute).GetConstructor(new[] { typeof(string) });
        if (attributeConstructor == null)
        {
            throw new InvalidOperationException("Unable to locate DdsTopicAttribute constructor.");
        }

        var attribute = new CustomAttributeBuilder(attributeConstructor, new object[] { "InvalidTopic" });
        typeBuilder.SetCustomAttribute(attribute);

        return typeBuilder.CreateTypeInfo()!.AsType();
    }
}
