using System.Reflection;
using System.Reflection.Emit;
using CycloneDDS.Schema;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DdsBridgeTests
{
    [Fact]
    public void DdsBridge_Subscribe_CreatesReader()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge();

        var reader = bridge.Subscribe(metadata);

        Assert.True(bridge.ActiveReaders.TryGetValue(metadata.TopicType, out var stored));
        Assert.Same(reader, stored);
    }

    [Fact]
    public void DdsBridge_Unsubscribe_RemovesReader()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge();

        bridge.Subscribe(metadata);
        bridge.Unsubscribe(metadata);

        Assert.False(bridge.ActiveReaders.ContainsKey(metadata.TopicType));
    }

    [Fact]
    public void DdsBridge_ChangePartition_RecreatesReaders()
    {
        var metadataA = new TopicMetadata(typeof(SampleTopic));
        var metadataB = new TopicMetadata(typeof(SimpleType));
        using var bridge = new DdsBridge();

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
        using var bridge = new DdsBridge();

        var reader = bridge.Subscribe(metadata);

        Assert.Equal(invalidType, reader.TopicType);
        Assert.False(bridge.ActiveReaders.ContainsKey(invalidType));
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
