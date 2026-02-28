using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Tracks keyed instance lifecycles for DDS topics.
/// </summary>
public interface IInstanceStore
{
    /// <summary>
    /// Gets the instance data for the provided topic.
    /// </summary>
    ITopicInstances GetTopicInstances(Type topicType);

    /// <summary>
    /// Gets a snapshot of instances and journal data for the provided topic.
    /// </summary>
    InstanceSnapshot GetTopicSnapshot(Type topicType);

    /// <summary>
    /// Gets an observable stream of instance transition events.
    /// </summary>
    IObservable<InstanceTransitionEvent> OnInstanceChanged { get; }

    /// <summary>
    /// Processes a new sample for instance tracking.
    /// </summary>
    void ProcessSample(SampleData sample);

    /// <summary>
    /// Clears all tracked instances.
    /// </summary>
    void Clear();
}

/// <summary>
/// Provides a thread-safe snapshot of per-topic instance data.
/// </summary>
public sealed record InstanceSnapshot(int LiveCount, InstanceData[] Instances, InstanceJournalRecord[] Journal);

/// <summary>
/// Provides per-topic instance data.
/// </summary>
public interface ITopicInstances
{
    /// <summary>
    /// Gets the count of live instances.
    /// </summary>
    int LiveCount { get; }

    /// <summary>
    /// Gets the instances keyed by their DDS key values.
    /// </summary>
    IReadOnlyDictionary<InstanceKey, InstanceData> InstancesByKey { get; }

    /// <summary>
    /// Gets the instance journal records.
    /// </summary>
    IReadOnlyList<InstanceJournalRecord> Journal { get; }
}

/// <summary>
/// Represents the lifecycle state of a keyed instance.
/// </summary>
public enum InstanceState
{
    /// <summary>
    /// Instance is alive.
    /// </summary>
    Alive,

    /// <summary>
    /// Instance has been disposed.
    /// </summary>
    Disposed,

    /// <summary>
    /// Instance has no writers.
    /// </summary>
    NoWriters
}

/// <summary>
/// Represents the type of transition.
/// </summary>
public enum TransitionKind
{
    /// <summary>
    /// Instance was added or reborn.
    /// </summary>
    Added,

    /// <summary>
    /// Instance was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// Instance was removed.
    /// </summary>
    Removed
}

/// <summary>
/// Key values identifying a DDS instance.
/// </summary>
public readonly record struct InstanceKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceKey"/> struct.
    /// </summary>
    public InstanceKey(object[] values)
    {
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    /// <summary>
    /// Gets the key values.
    /// </summary>
    public object[] Values { get; }

    /// <inheritdoc />
    public bool Equals(InstanceKey other)
    {
        if (ReferenceEquals(Values, other.Values))
        {
            return true;
        }

        if (Values == null || other.Values == null)
        {
            return false;
        }

        if (Values.Length != other.Values.Length)
        {
            return false;
        }

        for (var i = 0; i < Values.Length; i++)
        {
            if (!Equals(Values[i], other.Values[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Describes a tracked instance.
/// </summary>
public sealed class InstanceData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceData"/> class.
    /// </summary>
    public InstanceData(TopicMetadata topicMetadata, InstanceKey key, SampleData creationSample, InstanceState state)
    {
        TopicMetadata = topicMetadata ?? throw new ArgumentNullException(nameof(topicMetadata));
        Key = key;
        RecentSample = creationSample ?? throw new ArgumentNullException(nameof(creationSample));
        RecentCreationSample = creationSample;
        State = state;
    }

    /// <summary>
    /// Gets the topic metadata.
    /// </summary>
    public TopicMetadata TopicMetadata { get; }

    /// <summary>
    /// Gets the instance key.
    /// </summary>
    public InstanceKey Key { get; }

    /// <summary>
    /// Gets or sets the most recent sample.
    /// </summary>
    public SampleData RecentSample { get; set; }

    /// <summary>
    /// Gets or sets the most recent creation sample.
    /// </summary>
    public SampleData RecentCreationSample { get; set; }

    /// <summary>
    /// Gets or sets the total sample count.
    /// </summary>
    public int NumSamplesTotal { get; set; }

    /// <summary>
    /// Gets or sets the number of samples since the last creation.
    /// </summary>
    public int NumSamplesRecent { get; set; }

    /// <summary>
    /// Gets or sets the instance state.
    /// </summary>
    public InstanceState State { get; set; }
}

/// <summary>
/// Records a transition in the instance journal.
/// </summary>
public sealed record InstanceJournalRecord(TransitionKind Kind, InstanceData Instance, SampleData Sample);

/// <summary>
/// Describes an instance transition event.
/// </summary>
public sealed record InstanceTransitionEvent(TransitionKind Kind, InstanceData Instance, SampleData Sample);
