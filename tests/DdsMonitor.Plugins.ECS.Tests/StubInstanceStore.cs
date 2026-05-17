using System;
using System.Collections.Generic;
using DdsMonitor.Engine;

namespace DdsMonitor.Plugins.ECS.Tests;

/// <summary>
/// Minimal IInstanceStore stub for unit-testing EntityStore without a live DDS bus.
/// Call <see cref="Raise"/> to push an <see cref="InstanceTransitionEvent"/> to all subscribers.
/// </summary>
internal sealed class StubInstanceStore : IInstanceStore
{
    private readonly SimpleObservable _observable = new();

    public IObservable<InstanceTransitionEvent> OnInstanceChanged => _observable;

    public event Action? Cleared;

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Pushes an event to all observers synchronously.</summary>
    public void Raise(InstanceTransitionEvent evt) => _observable.Push(evt);

    /// <summary>Fires <see cref="Cleared"/> to simulate a global reset.</summary>
    public void FireCleared() => Cleared?.Invoke();

    // ── Unused IInstanceStore members (not needed for unit tests) ─────────────

    public ITopicInstances GetTopicInstances(Type topicType) => throw new NotSupportedException();
    public InstanceSnapshot GetTopicSnapshot(Type topicType) => throw new NotSupportedException();
    public void ProcessSample(SampleData sample) => throw new NotSupportedException();
    public void Clear() => Cleared?.Invoke();

    // ── Minimal IObservable implementation ────────────────────────────────────

    private sealed class SimpleObservable : IObservable<InstanceTransitionEvent>
    {
        private readonly List<IObserver<InstanceTransitionEvent>> _observers = new();

        public IDisposable Subscribe(IObserver<InstanceTransitionEvent> observer)
        {
            _observers.Add(observer);
            return new Unsubscribe(_observers, observer);
        }

        public void Push(InstanceTransitionEvent evt)
        {
            foreach (var obs in _observers.ToArray())
                obs.OnNext(evt);
        }

        private sealed class Unsubscribe : IDisposable
        {
            private readonly List<IObserver<InstanceTransitionEvent>> _list;
            private readonly IObserver<InstanceTransitionEvent> _observer;
            public Unsubscribe(List<IObserver<InstanceTransitionEvent>> list, IObserver<InstanceTransitionEvent> observer)
            {
                _list = list;
                _observer = observer;
            }
            public void Dispose() => _list.Remove(_observer);
        }
    }
}

/// <summary>
/// Factory helpers that build test <see cref="InstanceTransitionEvent"/> objects
/// without requiring a live DDS participant.
/// </summary>
internal static class TestEventFactory
{
    /// <summary>
    /// Creates an Alive (Added/Updated) instance event for the given topic type,
    /// using the supplied key values.
    /// </summary>
    public static InstanceTransitionEvent AliveEvent<TTopic>(
        TransitionKind kind,
        object[] keyValues,
        object? payload = null)
    {
        var meta = new TopicMetadata(typeof(TTopic));
        var instance = new InstanceData(meta, new InstanceKey(keyValues), MakeSample(meta, payload), InstanceState.Alive);
        return new InstanceTransitionEvent(kind, instance, MakeSample(meta, payload));
    }

    /// <summary>
    /// Creates a Removed instance event for the given topic type.
    /// </summary>
    public static InstanceTransitionEvent RemovedEvent<TTopic>(object[] keyValues, object? payload = null)
    {
        var meta = new TopicMetadata(typeof(TTopic));
        var instance = new InstanceData(meta, new InstanceKey(keyValues), MakeSample(meta, payload), InstanceState.Disposed);
        return new InstanceTransitionEvent(TransitionKind.Removed, instance, MakeSample(meta, payload));
    }

    private static SampleData MakeSample(TopicMetadata meta, object? payload)
    {
        return new SampleData
        {
            Ordinal       = 0,
            Payload       = payload ?? Activator.CreateInstance(meta.TopicType)!,
            TopicMetadata = meta,
            SampleInfo    = default,
            Timestamp     = DateTime.UtcNow,
        };
    }
}
