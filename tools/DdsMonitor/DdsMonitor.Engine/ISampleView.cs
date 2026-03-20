using System;

namespace DdsMonitor.Engine;

/// <summary>
/// A per-window sorted, filtered view of an <see cref="ISampleStore"/>.
/// Each <c>SamplesPanel</c> owns one instance so that filter/sort state is
/// isolated and the expensive background sorting never blocks the UI thread.
/// </summary>
public interface ISampleView : IDisposable
{
    /// <summary>
    /// Gets the number of samples that pass the current filter (volatile, updated by
    /// the background worker).
    /// </summary>
    int CurrentFilteredCount { get; }

    /// <summary>
    /// Returns a zero-allocation slice of the current sorted view for use by the
    /// Virtualize component.  For the descending-ordinal fast-path this builds a
    /// small reversed slice without reversing the entire backing array.
    /// </summary>
    ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count);

    /// <summary>
    /// Returns a point-in-time snapshot of all filtered samples in display order.
    /// Intended for infrequent operations such as file export.
    /// </summary>
    SampleData[] GetFilteredSnapshot();

    /// <summary>
    /// Replaces the active filter predicate and triggers a full background re-sort.
    /// Pass <c>null</c> to show all samples.
    /// </summary>
    void SetFilter(Func<SampleData, bool>? compiledFilterPredicate);

    /// <summary>
    /// Changes the sort column and direction, triggering a background re-sort.
    /// Pass <c>null</c> for <paramref name="field"/> to restore arrival order.
    /// </summary>
    void SetSortSpec(FieldMetadata? field, SortDirection direction);

    /// <summary>
    /// Raised on the background worker thread after each view rebuild.
    /// Callers should marshal to the UI thread before touching Blazor state.
    /// </summary>
    event Action? OnViewRebuilt;
}
