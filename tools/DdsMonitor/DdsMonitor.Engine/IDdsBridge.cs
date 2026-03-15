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
    /// Gets the primary DDS participant (backward-compat shortcut to <c>Participants[0]</c>).
    /// </summary>
    DdsParticipant Participant { get; }

    /// <summary>
    /// Gets all active DDS participants managed by the bridge.
    /// </summary>
    IReadOnlyList<DdsParticipant> Participants { get; }

    /// <summary>
    /// Gets the configuration objects for all active participants, in the same order as
    /// <see cref="Participants"/>. Provides domain ID and partition name for display purposes.
    /// </summary>
    IReadOnlyList<ParticipantConfig> ParticipantConfigs { get; }

    /// <summary>
    /// Gets the active partition used for readers and writers on the primary participant.
    /// </summary>
    string? CurrentPartition { get; }

    /// <summary>
    /// Gets or sets a value indicating whether incoming samples are dropped without being
    /// written to the ingestion channel.  Existing in-flight samples complete normally.
    /// </summary>
    bool IsPaused { get; set; }

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

    /// <summary>
    /// Raised when the set of active readers changes (subscribe or unsubscribe).
    /// </summary>
    event Action? ReadersChanged;

    /// <summary>
    /// Adds a new participant for the specified domain and partition.
    /// </summary>
    void AddParticipant(uint domainId, string partitionName);

    /// <summary>
    /// Removes the participant at the specified index and disposes it.
    /// </summary>
    void RemoveParticipant(int participantIndex);

    /// <summary>
    /// Clears all sample and instance stores, resets the global ordinal counter,
    /// and disposes any per-participant reader subscriptions.
    /// </summary>
    void ResetAll();
}
