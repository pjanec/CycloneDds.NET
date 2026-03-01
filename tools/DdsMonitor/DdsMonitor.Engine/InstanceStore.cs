using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CycloneDDS.Runtime;

namespace DdsMonitor.Engine;

/// <summary>
/// Tracks keyed DDS instance lifecycles.
/// </summary>
public sealed class InstanceStore : IInstanceStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Type, TopicInstances> _topics = new();
    private readonly TransitionObservable _observable = new();

    /// <inheritdoc />
    public IObservable<InstanceTransitionEvent> OnInstanceChanged => _observable;

    /// <inheritdoc />
    public ITopicInstances GetTopicInstances(Type topicType)
    {
        if (topicType == null)
        {
            throw new ArgumentNullException(nameof(topicType));
        }

        lock (_sync)
        {
            if (!_topics.TryGetValue(topicType, out var instances))
            {
                instances = new TopicInstances();
                _topics[topicType] = instances;
            }

            return instances;
        }
    }

    /// <inheritdoc />
    public InstanceSnapshot GetTopicSnapshot(Type topicType)
    {
        if (topicType == null)
        {
            throw new ArgumentNullException(nameof(topicType));
        }

        lock (_sync)
        {
            if (!_topics.TryGetValue(topicType, out var instances))
            {
                return new InstanceSnapshot(0, Array.Empty<InstanceData>(), Array.Empty<InstanceJournalRecord>());
            }

            return new InstanceSnapshot(
                instances.LiveCount,
                instances.InstancesByKeyInternal.Values.ToArray(),
                instances.JournalInternal.ToArray());
        }
    }

    /// <inheritdoc />
    public void ProcessSample(SampleData sample)
    {
        if (sample == null)
        {
            throw new ArgumentNullException(nameof(sample));
        }

        var metadata = sample.TopicMetadata;
        if (metadata.KeyFields.Count == 0)
        {
            return;
        }

        var key = ExtractKey(metadata, sample.Payload);
        var newState = MapInstanceState(sample.SampleInfo.InstanceState);
        InstanceTransitionEvent? transitionEvent = null;
        InstanceJournalRecord? journalRecord = null;

        lock (_sync)
        {
            if (!_topics.TryGetValue(metadata.TopicType, out var topicInstances))
            {
                topicInstances = new TopicInstances();
                _topics[metadata.TopicType] = topicInstances;
            }

            if (!topicInstances.InstancesByKeyInternal.TryGetValue(key, out var instance))
            {
                instance = new InstanceData(metadata, key, sample, newState);
                topicInstances.InstancesByKeyInternal[key] = instance;

                if (newState == InstanceState.Alive)
                {
                    topicInstances.LiveCount++;
                }

                journalRecord = new InstanceJournalRecord(TransitionKind.Added, instance, sample);
                transitionEvent = new InstanceTransitionEvent(TransitionKind.Added, instance, sample);
            }
            else
            {
                var wasAlive = instance.State == InstanceState.Alive;
                var isAlive = newState == InstanceState.Alive;

                if (wasAlive && !isAlive)
                {
                    topicInstances.LiveCount--;
                    instance.State = newState;
                    journalRecord = new InstanceJournalRecord(TransitionKind.Removed, instance, sample);
                    transitionEvent = new InstanceTransitionEvent(TransitionKind.Removed, instance, sample);
                }
                else if (!wasAlive && isAlive)
                {
                    topicInstances.LiveCount++;
                    instance.State = newState;
                    instance.RecentCreationSample = sample;
                    instance.NumSamplesRecent = 0;
                    journalRecord = new InstanceJournalRecord(TransitionKind.Added, instance, sample);
                    transitionEvent = new InstanceTransitionEvent(TransitionKind.Added, instance, sample);
                }
                else
                {
                    instance.State = newState;
                    journalRecord = new InstanceJournalRecord(TransitionKind.Updated, instance, sample);
                    transitionEvent = new InstanceTransitionEvent(TransitionKind.Updated, instance, sample);
                }
            }

            instance = topicInstances.InstancesByKeyInternal[key];
            instance.RecentSample = sample;
            instance.NumSamplesTotal++;
            instance.NumSamplesRecent++;

            topicInstances.JournalInternal.Add(journalRecord!);
        }

        _observable.Publish(transitionEvent!);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_sync)
        {
            _topics.Clear();
        }
    }

    private static InstanceKey ExtractKey(TopicMetadata metadata, object payload)
    {
        var keyFields = metadata.KeyFields;
        var values = new object[keyFields.Count];

        for (var i = 0; i < keyFields.Count; i++)
        {
            values[i] = keyFields[i].Getter(payload)!;
        }

        return new InstanceKey(values);
    }

    private static InstanceState MapInstanceState(DdsInstanceState instanceState)
    {
        return instanceState switch
        {
            DdsInstanceState.NotAliveDisposed => InstanceState.Disposed,
            DdsInstanceState.NotAliveNoWriters => InstanceState.NoWriters,
            _ => InstanceState.Alive
        };
    }

    private sealed class TopicInstances : ITopicInstances
    {
        public int LiveCount { get; set; }

        public Dictionary<InstanceKey, InstanceData> InstancesByKeyInternal { get; } = new();

        public IReadOnlyDictionary<InstanceKey, InstanceData> InstancesByKey => InstancesByKeyInternal;

        public List<InstanceJournalRecord> JournalInternal { get; } = new();

        public IReadOnlyList<InstanceJournalRecord> Journal => JournalInternal;
    }

    private sealed class TransitionObservable : IObservable<InstanceTransitionEvent>
    {
        private readonly object _sync = new();
        private readonly List<IObserver<InstanceTransitionEvent>> _observers = new();

        public IDisposable Subscribe(IObserver<InstanceTransitionEvent> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            lock (_sync)
            {
                _observers.Add(observer);
            }

            return new Subscription(this, observer);
        }

        public void Publish(InstanceTransitionEvent evt)
        {
            List<IObserver<InstanceTransitionEvent>> observers;

            lock (_sync)
            {
                observers = new List<IObserver<InstanceTransitionEvent>>(_observers);
            }

            foreach (var observer in observers)
            {
                observer.OnNext(evt);
            }
        }

        private void Unsubscribe(IObserver<InstanceTransitionEvent> observer)
        {
            lock (_sync)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly TransitionObservable _owner;
            private IObserver<InstanceTransitionEvent>? _observer;

            public Subscription(TransitionObservable owner, IObserver<InstanceTransitionEvent> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                var observer = Interlocked.Exchange(ref _observer, null);
                if (observer != null)
                {
                    _owner.Unsubscribe(observer);
                }
            }
        }
    }
}
