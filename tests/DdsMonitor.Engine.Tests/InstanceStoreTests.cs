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

    // ── Clear ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="InstanceStore.Clear"/> removes all tracked instances.
    /// </summary>
    [Fact]
    public void InstanceStore_Clear_RemovesAllInstances()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 10, Value = 1 }, DdsInstanceState.Alive));
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 11, Value = 2 }, DdsInstanceState.Alive));

        store.Clear();

        var topic = store.GetTopicInstances(metadata.TopicType);
        Assert.Equal(0, topic.InstancesByKey.Count);
        Assert.Equal(0, topic.LiveCount);
    }

    /// <summary>
    /// The critical regression test for the memory leak:
    /// <see cref="InstanceStore.Clear"/> MUST publish a <see cref="TransitionKind.Cleared"/>
    /// event so that observers (e.g. InstancesPanel) can drop their UI caches and allow
    /// the GC to release sample memory.
    /// Before the fix, Clear() silently emptied the dictionary without notifying observers,
    /// causing the UI cache to hold hard references to all SampleData forever.
    /// </summary>
    [Fact]
    public void InstanceStore_Clear_PublishesClearedEvent()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 20, Value = 1 }, DdsInstanceState.Alive));

        var events = new List<TransitionKind>();
        using var sub = store.OnInstanceChanged.Subscribe(new ActionObserver(e => events.Add(e.Kind)));

        store.Clear();

        Assert.Contains(TransitionKind.Cleared, events);
    }

    /// <summary>
    /// Verifies that the Cleared event has null Instance and Sample fields
    /// (observers must guard against null for this kind).
    /// </summary>
    [Fact]
    public void InstanceStore_ClearedEvent_HasNullInstanceAndSample()
    {
        var store = new InstanceStore();
        InstanceTransitionEvent? clearedEvent = null;

        using var sub = store.OnInstanceChanged.Subscribe(
            new ActionObserver(e =>
            {
                if (e.Kind == TransitionKind.Cleared) clearedEvent = e;
            }));

        store.Clear();

        Assert.NotNull(clearedEvent);
        Assert.Equal(TransitionKind.Cleared, clearedEvent!.Kind);
        Assert.Null(clearedEvent.Instance);
        Assert.Null(clearedEvent.Sample);
    }

    /// <summary>
    /// Verifies that processing new samples after a Clear works correctly —
    /// the store is back to a clean initial state.
    /// </summary>
    [Fact]
    public void InstanceStore_ProcessSampleAfterClear_WorksCorrectly()
    {
        var metadata = new TopicMetadata(typeof(InstanceKeyedMessage));
        var store = new InstanceStore();

        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 30, Value = 1 }, DdsInstanceState.Alive));
        store.Clear();

        // After clear, a new sample for the same key should create a fresh instance.
        store.ProcessSample(CreateSample(metadata, new InstanceKeyedMessage { Id = 30, Value = 2 }, DdsInstanceState.Alive));

        var topic = store.GetTopicInstances(metadata.TopicType);
        Assert.Equal(1, topic.LiveCount);
        Assert.Single(topic.InstancesByKey);
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
