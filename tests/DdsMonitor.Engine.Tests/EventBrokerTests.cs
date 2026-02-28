using System;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class EventBrokerTests
{
    [Fact]
    public void EventBroker_PublishAndSubscribe_DeliversEvent()
    {
        var broker = new EventBroker();
        SampleSelectedEvent? received = null;

        using var subscription = broker.Subscribe<SampleSelectedEvent>(evt => received = evt);

        var metadata = new TopicMetadata(typeof(SampleTopic));
        var sample = CreateSample(metadata, 1, new SampleTopic { Id = 7 });
        var message = new SampleSelectedEvent("SamplesPanel.1", sample);

        broker.Publish(message);

        Assert.NotNull(received);
        Assert.Equal("SamplesPanel.1", received!.SourcePanelId);
        Assert.Same(sample, received.Sample);
    }

    [Fact]
    public void EventBroker_Dispose_StopsDelivery()
    {
        var broker = new EventBroker();
        var delivered = false;

        var subscription = broker.Subscribe<SampleSelectedEvent>(_ => delivered = true);
        subscription.Dispose();

        var metadata = new TopicMetadata(typeof(SampleTopic));
        broker.Publish(new SampleSelectedEvent("SamplesPanel.1", CreateSample(metadata, 1, new SampleTopic { Id = 1 })));

        Assert.False(delivered);
    }

    [Fact]
    public void EventBroker_MultipleSubscribers_AllReceive()
    {
        var broker = new EventBroker();
        var first = 0;
        var second = 0;

        using var subscriptionA = broker.Subscribe<SampleSelectedEvent>(_ => first++);
        using var subscriptionB = broker.Subscribe<SampleSelectedEvent>(_ => second++);

        var metadata = new TopicMetadata(typeof(SampleTopic));
        broker.Publish(new SampleSelectedEvent("SamplesPanel.2", CreateSample(metadata, 2, new SampleTopic { Id = 2 })));

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void EventBroker_DifferentEventTypes_DoNotCrossTalk()
    {
        var broker = new EventBroker();
        var sampleDelivered = 0;
        var columnDelivered = 0;

        using var sampleSubscription = broker.Subscribe<SampleSelectedEvent>(_ => sampleDelivered++);
        using var columnSubscription = broker.Subscribe<AddColumnRequestEvent>(_ => columnDelivered++);

        var metadata = new TopicMetadata(typeof(SampleTopic));
        broker.Publish(new SampleSelectedEvent("SamplesPanel.3", CreateSample(metadata, 3, new SampleTopic { Id = 3 })));

        Assert.Equal(1, sampleDelivered);
        Assert.Equal(0, columnDelivered);
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
}
