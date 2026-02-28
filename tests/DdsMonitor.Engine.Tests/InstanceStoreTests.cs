using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class InstanceStoreTests
{
    private const int InitialOrdinal = 1;
    private const int NextOrdinal = 2;

    [Fact]
    public void InstanceStore_NewKey_CreatesAliveInstance()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 1, Value = 10 }, DdsInstanceState.Alive));

        var topic = store.GetTopicInstances(metadata.TopicType);
        Assert.Equal(1, topic.LiveCount);
        Assert.Single(topic.InstancesByKey);

        var instance = Assert.Single(topic.InstancesByKey.Values);
        Assert.Equal(InstanceState.Alive, instance.State);
    }

    [Fact]
    public void InstanceStore_DisposeKey_MarksAsDead()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 2, Value = 20 }, DdsInstanceState.Alive));
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 2, Value = 20 }, DdsInstanceState.NotAliveDisposed));

        var topic = store.GetTopicInstances(metadata.TopicType);
        Assert.Equal(0, topic.LiveCount);

        var instance = Assert.Single(topic.InstancesByKey.Values);
        Assert.Equal(InstanceState.Disposed, instance.State);
    }

    [Fact]
    public void InstanceStore_RebirthKey_ResetsCounters()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 3, Value = 30 }, DdsInstanceState.Alive));
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 3, Value = 30 }, DdsInstanceState.NotAliveDisposed));
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 3, Value = 30 }, DdsInstanceState.Alive));

        var topic = store.GetTopicInstances(metadata.TopicType);
        Assert.Equal(1, topic.LiveCount);

        var instance = Assert.Single(topic.InstancesByKey.Values);
        Assert.Equal(1, instance.NumSamplesRecent);
    }

    [Fact]
    public void InstanceStore_FiresTransitionEvents()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();
        var transitions = new List<TransitionKind>();

        using var subscription = store.OnInstanceChanged.Subscribe(new ActionObserver(evt => transitions.Add(evt.Kind)));

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 4, Value = 40 }, DdsInstanceState.Alive));
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 4, Value = 41 }, DdsInstanceState.Alive));
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 4, Value = 41 }, DdsInstanceState.NotAliveDisposed));

        Assert.Equal(new[] { TransitionKind.Added, TransitionKind.Updated, TransitionKind.Removed }, transitions);
    }

    [Fact]
    public void InstanceStore_ExtractsCompositeKey()
    {
        var metadata = new TopicMetadata(typeof(InstanceCompositeKeyMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceCompositeKeyMessage { EntityId = 5, PartId = 1, Value = 10 }, DdsInstanceState.Alive));
        store.ProcessSample(CreateSample(metadata, new InstanceCompositeKeyMessage { EntityId = 5, PartId = 2, Value = 10 }, DdsInstanceState.Alive));

        var topic = store.GetTopicInstances(metadata.TopicType);
        Assert.Equal(2, topic.InstancesByKey.Count);
    }

    private static SampleData CreateSample<T>(TopicMetadata metadata, T payload, DdsInstanceState state)
    {
        return new SampleData
        {
            Ordinal = state == DdsInstanceState.Alive ? InitialOrdinal : NextOrdinal,
            Payload = payload!,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { InstanceState = state },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    private sealed class ActionObserver : IObserver<InstanceTransitionEvent>
    {
        private readonly Action<InstanceTransitionEvent> _onNext;

        public ActionObserver(Action<InstanceTransitionEvent> onNext)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(InstanceTransitionEvent value)
        {
            _onNext(value);
        }
    }
}
