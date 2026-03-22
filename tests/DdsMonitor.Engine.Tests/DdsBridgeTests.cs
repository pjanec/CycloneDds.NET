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

    // ─────────────────────────────────────────────────────────────────────────
    // BUG FIX: Explicit-unsubscribe tracking (Bug 1 — Topics window restore)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DdsBridge_Unsubscribe_AddsToExplicitlyUnsubscribedSet()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metadata);
        bridge.Unsubscribe(metadata);

        Assert.Contains(typeof(SampleTopic), bridge.ExplicitlyUnsubscribedTopicTypes);
    }

    [Fact]
    public void DdsBridge_Subscribe_ClearsExplicitlyUnsubscribedEntry()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metadata);
        bridge.Unsubscribe(metadata);

        // User re-subscribes: explicit-unsubscribe flag must be cleared.
        bridge.TrySubscribe(metadata, out _, out _);

        Assert.DoesNotContain(typeof(SampleTopic), bridge.ExplicitlyUnsubscribedTopicTypes);
    }

    [Fact]
    public void DdsBridge_Subscribe_WithoutPriorUnsubscribe_NotInExplicitSet()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metadata);

        // Normal subscribe should never place the topic in the explicit-unsubscribe set.
        Assert.DoesNotContain(typeof(SampleTopic), bridge.ExplicitlyUnsubscribedTopicTypes);
    }

    [Fact]
    public void DdsBridge_ExplicitlyUnsubscribed_PreventedByAutoSubscribe()
    {
        // Simulate the auto-subscribe logic from TopicExplorerPanel.AutoSubscribeAll:
        // topics that are in ExplicitlyUnsubscribedTopicTypes must not be re-subscribed.
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metadata);
        bridge.Unsubscribe(metadata);

        // Assert: not active after unsubscribe
        Assert.False(bridge.ActiveReaders.ContainsKey(typeof(SampleTopic)));
        Assert.Contains(typeof(SampleTopic), bridge.ExplicitlyUnsubscribedTopicTypes);

        // Simulate auto-subscribe (the fixed AutoSubscribeAll skips explicitly-unsubscribed topics).
        var registry = new TopicRegistry();
        registry.Register(metadata);

        var explicitlyUnsubscribed = bridge.ExplicitlyUnsubscribedTopicTypes;
        foreach (var topic in registry.AllTopics)
        {
            if (!explicitlyUnsubscribed.Contains(topic.TopicType))
                bridge.TrySubscribe(topic, out _, out _);
        }

        // The topic must still NOT be active because the auto-subscribe skipped it.
        Assert.False(bridge.ActiveReaders.ContainsKey(typeof(SampleTopic)),
            "Auto-subscribe must not re-subscribe explicitly unsubscribed topics.");
    }

    [Fact]
    public void DdsBridge_AutoSubscribe_SubscribesNewTopicNotInExplicitSet()
    {
        // New topics (never explicitly unsubscribed) must still be subscribed by auto-subscribe.
        var metaA = new TopicMetadata(typeof(SampleTopic));
        var metaB = new TopicMetadata(typeof(SimpleType));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metaA);
        bridge.Unsubscribe(metaA);

        // Simulate auto-subscribe for both topics (only A is explicitly unsubscribed).
        var registry = new TopicRegistry();
        registry.Register(metaA);
        registry.Register(metaB);

        var explicitlyUnsubscribed = bridge.ExplicitlyUnsubscribedTopicTypes;
        foreach (var topic in registry.AllTopics)
        {
            if (!explicitlyUnsubscribed.Contains(topic.TopicType))
                bridge.TrySubscribe(topic, out _, out _);
        }

        // Topic B (never explicitly unsubscribed) must be subscribed.
        Assert.True(bridge.ActiveReaders.ContainsKey(typeof(SimpleType)),
            "Auto-subscribe must subscribe topics that were never explicitly unsubscribed.");

        // Topic A must remain unsubscribed.
        Assert.False(bridge.ActiveReaders.ContainsKey(typeof(SampleTopic)),
            "Auto-subscribe must not re-subscribe explicitly unsubscribed topics.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BUG FIX: Sender registry created on participants (Bug 2 — Sender identity)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DdsBridge_Participant_HasSenderRegistryAfterCreation()
    {
        // DdsBridge must call EnableSenderMonitoring() on all participants so that
        // DynamicReaders can populate SampleData.Sender for live samples.
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        Assert.NotNull(bridge.Participant.SenderRegistry);
    }

    [Fact]
    public void DdsBridge_AddParticipant_NewParticipantHasSenderRegistry()
    {
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        // Add a second participant on the same domain as the default (domain 0).
        bridge.AddParticipant(0, string.Empty);

        // All participants must have a SenderRegistry for sender tracking to work.
        foreach (var participant in bridge.Participants)
        {
            Assert.NotNull(participant.SenderRegistry);
        }
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
