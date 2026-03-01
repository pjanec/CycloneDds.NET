using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;

namespace DdsMonitor.Engine;

/// <summary>
/// Coordinates DDS reader and writer lifecycles for dynamic topics.
/// </summary>
public interface IDdsBridge : IDisposable
{
    /// <summary>
    /// Gets the DDS participant used by the bridge.
    /// </summary>
    DdsParticipant Participant { get; }

    /// <summary>
    /// Gets the active partition used for readers and writers.
    /// </summary>
    string? CurrentPartition { get; }

    /// <summary>
    /// Subscribes to the topic and returns the active reader.
    /// </summary>
    IDynamicReader Subscribe(TopicMetadata meta);

    /// <summary>
    /// Attempts to subscribe to the topic and returns an error message on failure.
    /// </summary>
    bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? errorMessage);

    /// <summary>
    /// Unsubscribes from the topic and releases its reader.
    /// </summary>
    void Unsubscribe(TopicMetadata meta);

    /// <summary>
    /// Creates a writer for the topic.
    /// </summary>
    IDynamicWriter GetWriter(TopicMetadata meta);

    /// <summary>
    /// Changes the partition and recreates all active readers.
    /// </summary>
    void ChangePartition(string? newPartition);

    /// <summary>
    /// Gets the current map of active readers by topic type.
    /// </summary>
    IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders { get; }
}
