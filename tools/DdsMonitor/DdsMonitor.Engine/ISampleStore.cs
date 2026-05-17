using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Append-only concurrent store for DDS sample data.  All filtering, sorting, and
/// virtualized views are the responsibility of <see cref="ISampleView"/> instances
/// that each <c>SamplesPanel</c> creates independently.
/// </summary>
public interface ISampleStore
{
    /// <summary>
    /// Gets all samples in append order.
    /// </summary>
    IReadOnlyList<SampleData> AllSamples { get; }

    /// <summary>
    /// Gets the total number of samples currently in the store.
    /// Thread-safe; no allocation.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Returns all samples starting from <paramref name="startIndex"/> as a fresh
    /// array snapshot.  Used by <see cref="ISampleView"/> implementations to poll
    /// for new arrivals without holding the store lock.
    /// </summary>
    SampleData[] GetSamples(int startIndex);

    /// <summary>
    /// Gets the per-topic sample ledger.
    /// </summary>
    ITopicSamples GetTopicSamples(Type topicType);

    /// <summary>
    /// Returns the number of samples received for a specific topic without acquiring the store lock.
    /// Returns 0 if no samples have been received for that topic yet.
    /// </summary>
    int GetTopicCount(Type topicType);

    /// <summary>
    /// Appends a sample to the store.
    /// </summary>
    void Append(SampleData sample);

    /// <summary>
    /// Clears all samples.
    /// </summary>
    void Clear();

    /// <summary>
    /// Raised after all samples have been cleared.
    /// Subscribers should drop any cached references to sample data.
    /// </summary>
    event Action? Cleared;

    /// <summary>
    /// Gets the total bytes received across all topics (best-effort estimate).
    /// </summary>
    long TotalBytesReceived { get; }
}

/// <summary>
/// Provides per-topic sample access.
/// </summary>
public interface ITopicSamples
{
    /// <summary>
    /// Gets the CLR topic type.
    /// </summary>
    Type TopicType { get; }

    /// <summary>
    /// Gets the total number of samples for this topic.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Gets the topic samples in append order.
    /// </summary>
    IReadOnlyList<SampleData> Samples { get; }
}

/// <summary>
/// Sort direction for sample views.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Sort values in ascending order.
    /// </summary>
    Ascending,

    /// <summary>
    /// Sort values in descending order.
    /// </summary>
    Descending
}
