using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides access to historical samples with filtering and sorting.
/// </summary>
public interface ISampleStore
{
    /// <summary>
    /// Gets all samples in append order.
    /// </summary>
    IReadOnlyList<SampleData> AllSamples { get; }

    /// <summary>
    /// Gets the per-topic sample ledger.
    /// </summary>
    ITopicSamples GetTopicSamples(Type topicType);

    /// <summary>
    /// Appends a sample to the store.
    /// </summary>
    void Append(SampleData sample);

    /// <summary>
    /// Clears all samples and views.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the count of samples passing the current filter.
    /// </summary>
    int CurrentFilteredCount { get; }

    /// <summary>
    /// Gets a virtualized slice of the current sorted view.
    /// </summary>
    ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count);

    /// <summary>
    /// Sets the active filter predicate.
    /// </summary>
    void SetFilter(Func<SampleData, bool>? compiledFilterPredicate);

    /// <summary>
    /// Sets the active sort field and direction.
    /// </summary>
    void SetSortSpec(FieldMetadata? field, SortDirection direction);

    /// <summary>
    /// Raised whenever the sorted view is rebuilt.
    /// </summary>
    event Action? OnViewRebuilt;
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
