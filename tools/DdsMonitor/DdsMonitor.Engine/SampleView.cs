using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine;

/// <summary>
/// Per-window background view over an <see cref="ISampleStore"/>.
/// Polls the store every 50 ms for new arrivals, applies the active filter and sort
/// spec off the UI thread, and atomically publishes the result for zero-allocation
/// virtualized reads.
///
/// <para>
/// Fast paths (no <c>Array.Sort</c> required):
/// <list type="bullet">
///   <item>Null sort field or Ordinal/Timestamp ascending – data arrives in ordinal
///   order so new items are simply appended.</item>
///   <item>Ordinal/Timestamp descending – the backing array stays ascending; the
///   descending slice is built by reading from the tail, avoiding an O(N) array
///   reversal.</item>
/// </list>
/// </para>
///
/// <para>
/// Sort cancellation: each call to <see cref="SetFilter"/> or
/// <see cref="SetSortSpec"/> increments an internal version counter.  If the
/// counter changes while a general <c>Array.Sort</c> is running, its result is
/// discarded and the worker immediately restarts with the new spec.
/// </para>
/// </summary>
public sealed class SampleView : ISampleView
{
    private const int WorkerIntervalMs = 50;
    private const int EmptyCount = 0;
    private const int MinIndex = 0;
    private const int MinCount = 0;
    private const int ComparisonLess = -1;
    private const int ComparisonGreater = 1;

    private readonly ISampleStore _store;
    private readonly object _sync = new();
    private readonly object _viewLock = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _workerTask;
    private int _disposed;

    // Written by the background worker; read by UI and background threads under _viewLock.
    private readonly List<SampleData> _sortedView = new();
    private int _currentFilteredCount;

    // Tracks how many samples from the store have been incorporated into _sortedView.
    private int _lastProcessedCount;

    // True when _sortedView is ascending-ordinal and GetVirtualView should read from tail.
    private volatile bool _isDescendingOrdinalFastPath;

    // Incremented whenever filter or sort spec changes; used to detect stale sort results.
    private int _sortOpVersion;

    // Protected by _sync.
    private Func<SampleData, bool>? _filterPredicate;
    private FieldMetadata? _sortField;
    private SortDirection _sortDirection;
    private bool _sortDirty;
    private bool _requiresFullSort;

    /// <inheritdoc />
    public int CurrentFilteredCount => Volatile.Read(ref _currentFilteredCount);

    /// <inheritdoc />
    public event Action? OnViewRebuilt;

    /// <summary>
    /// Creates a new <see cref="SampleView"/> backed by the given <paramref name="store"/>
    /// and immediately starts the background worker.
    /// </summary>
    public SampleView(ISampleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _workerTask = Task.Run(ViewWorkerLoop);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count)
    {
        if (startIndex < MinIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (count < MinCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        lock (_viewLock)
        {
            var total = _sortedView.Count;

            if (total == EmptyCount || count == EmptyCount || startIndex >= total)
            {
                return ReadOnlyMemory<SampleData>.Empty;
            }

            // Descending-ordinal fast path: read from the tail without reversing the entire list.
            if (_isDescendingOrdinalFastPath)
            {
                var reverseEndIndex = total - 1 - startIndex;
                if (reverseEndIndex < 0)
                {
                    return ReadOnlyMemory<SampleData>.Empty;
                }

                var actualCount = Math.Min(count, reverseEndIndex + 1);
                var result = new SampleData[actualCount];
                for (var i = 0; i < actualCount; i++)
                {
                    result[i] = _sortedView[reverseEndIndex - i];
                }

                return new ReadOnlyMemory<SampleData>(result);
            }

            var available = total - startIndex;
            var finalCount = Math.Min(count, available);
            var slice = new SampleData[finalCount];
            _sortedView.CopyTo(startIndex, slice, 0, finalCount);
            return new ReadOnlyMemory<SampleData>(slice);
        }
    }

    /// <inheritdoc />
    public SampleData[] GetFilteredSnapshot()
    {
        lock (_viewLock)
        {
            if (_sortedView.Count == EmptyCount)
            {
                return Array.Empty<SampleData>();
            }

            if (_isDescendingOrdinalFastPath)
            {
                var reversed = new SampleData[_sortedView.Count];
                for (var i = 0; i < _sortedView.Count; i++)
                {
                    reversed[i] = _sortedView[_sortedView.Count - 1 - i];
                }

                return reversed;
            }

            return _sortedView.ToArray();
        }
    }

    /// <inheritdoc />
    public void SetFilter(Func<SampleData, bool>? compiledFilterPredicate)
    {
        lock (_sync)
        {
            _filterPredicate = compiledFilterPredicate;
            _sortDirty = true;
            _requiresFullSort = true;
            Interlocked.Increment(ref _sortOpVersion);
        }
    }

    /// <inheritdoc />
    public void SetSortSpec(FieldMetadata? field, SortDirection direction)
    {
        lock (_sync)
        {
            _sortField = field;
            _sortDirection = direction;
            _sortDirty = true;
            _requiresFullSort = true;
            Interlocked.Increment(ref _sortOpVersion);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _disposeCts.Cancel();

        try
        {
            _workerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _disposeCts.Dispose();
    }

    private async Task ViewWorkerLoop()
    {
        while (!_disposeCts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(WorkerIntervalMs, _disposeCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            ProcessBatch();
        }
    }

    private void ProcessBatch()
    {
        var currentStoreCount = _store.TotalCount;

        // Detect store clear: if TotalCount dropped we must start from scratch.
        var storeCleared = currentStoreCount < _lastProcessedCount;
        if (storeCleared)
        {
            _lastProcessedCount = 0;
        }

        // Fetch new arrivals since the last processed index.
        SampleData[] newArrivals = Array.Empty<SampleData>();
        if (currentStoreCount > _lastProcessedCount)
        {
            newArrivals = _store.GetSamples(_lastProcessedCount);
        }

        // Capture sort/filter state under lock and decide what work to do.
        FieldMetadata? sortField;
        SortDirection direction;
        Func<SampleData, bool>? filterPredicate;
        bool fullSort;
        int opVersion;

        lock (_sync)
        {
            var hasWork = _sortDirty || newArrivals.Length > 0 || storeCleared;
            if (!hasWork)
            {
                return;
            }

            sortField = _sortField;
            direction = _sortDirection;
            filterPredicate = _filterPredicate;
            fullSort = _requiresFullSort || storeCleared;
            opVersion = Volatile.Read(ref _sortOpVersion);

            _sortDirty = false;
            _requiresFullSort = false;
        }

        if (fullSort)
        {
            // Re-filter all samples from the beginning of the store.
            var allData = _store.GetSamples(0);
            _lastProcessedCount = allData.Length;

            var filtered = FilterAll(allData, filterPredicate);
            var isOrdinalFastPath = IsOrdinalOrTimestampField(sortField);

            if (isOrdinalFastPath)
            {
                lock (_viewLock)
                {
                    _sortedView.Clear();
                    // When the store was cleared and the new view is empty, release
                    // the backing array so the GC can reclaim the LOH segment.
                    if (storeCleared && filtered.Length == 0)
                    {
                        _sortedView.TrimExcess();
                    }

                    _sortedView.AddRange(filtered);
                    _isDescendingOrdinalFastPath = direction == SortDirection.Descending;
                    Volatile.Write(ref _currentFilteredCount, _sortedView.Count);
                }
            }
            else
            {
                // General sort – bail if the user already changed the spec (version check).
                if (Volatile.Read(ref _sortOpVersion) != opVersion)
                {
                    return;
                }

                var newView = SortSnapshot(filtered, sortField!, direction);

                if (Volatile.Read(ref _sortOpVersion) != opVersion)
                {
                    return;
                }

                lock (_viewLock)
                {
                    _sortedView.Clear();
                    if (storeCleared && newView.Length == 0)
                    {
                        _sortedView.TrimExcess();
                    }

                    _sortedView.AddRange(newView);
                    _isDescendingOrdinalFastPath = false;
                    Volatile.Write(ref _currentFilteredCount, _sortedView.Count);
                }
            }

            OnViewRebuilt?.Invoke();
        }
        else if (newArrivals.Length > 0)
        {
            // Incremental update: filter and merge only the new arrivals.
            _lastProcessedCount += newArrivals.Length;

            var filteredNew = FilterAll(newArrivals, filterPredicate);
            if (filteredNew.Length == EmptyCount)
            {
                return;
            }

            var isOrdinalFastPath = IsOrdinalOrTimestampField(sortField);

            lock (_viewLock)
            {
                if (isOrdinalFastPath)
                {
                    // Fast path: O(new_items) append – no O(N) copy of history.
                    _sortedView.AddRange(filteredNew);
                    _isDescendingOrdinalFastPath = direction == SortDirection.Descending;
                }
                else
                {
                    // Merge-sort: build merged result then swap in.
                    var existingSnapshot = _sortedView.ToArray();
                    var merged = MergeSorted(existingSnapshot, filteredNew, sortField!, direction);
                    _sortedView.Clear();
                    _sortedView.AddRange(merged);
                    _isDescendingOrdinalFastPath = false;
                }

                Volatile.Write(ref _currentFilteredCount, _sortedView.Count);
            }

            OnViewRebuilt?.Invoke();
        }
    }

    private static bool IsOrdinalOrTimestampField(FieldMetadata? field)
    {
        return field == null
            || string.Equals(field.StructuredName, "Ordinal", StringComparison.Ordinal)
            || string.Equals(field.StructuredName, "Timestamp", StringComparison.Ordinal);
    }

    private static SampleData[] FilterAll(SampleData[] data, Func<SampleData, bool>? predicate)
    {
        if (predicate == null)
        {
            return data;
        }

        var result = new List<SampleData>(data.Length);
        foreach (var sample in data)
        {
            if (predicate(sample))
            {
                result.Add(sample);
            }
        }

        return result.Count == data.Length ? data : result.ToArray();
    }

    private static SampleData[] SortSnapshot(SampleData[] snapshot, FieldMetadata sortField, SortDirection direction)
    {
        if (snapshot.Length == EmptyCount)
        {
            return snapshot;
        }

        Array.Sort(snapshot, (left, right) =>
        {
            var leftValue = GetSortValue(left, sortField);
            var rightValue = GetSortValue(right, sortField);
            var comparison = CompareValues(leftValue, rightValue);
            return direction == SortDirection.Descending ? -comparison : comparison;
        });

        return snapshot;
    }

    private static SampleData[] MergeSorted(
        SampleData[] existing,
        SampleData[] newItems,
        FieldMetadata sortField,
        SortDirection direction)
    {
        if (newItems.Length == EmptyCount)
        {
            return existing;
        }

        if (existing.Length == EmptyCount)
        {
            return SortSnapshot(newItems, sortField, direction);
        }

        var sortedNew = SortSnapshot(newItems, sortField, direction);
        var merged = new SampleData[existing.Length + sortedNew.Length];

        var li = 0;
        var ri = 0;
        var mi = 0;

        while (li < existing.Length && ri < sortedNew.Length)
        {
            var comp = CompareSamples(existing[li], sortedNew[ri], sortField, direction);
            if (comp <= EmptyCount)
            {
                merged[mi++] = existing[li++];
            }
            else
            {
                merged[mi++] = sortedNew[ri++];
            }
        }

        if (li < existing.Length)
        {
            Array.Copy(existing, li, merged, mi, existing.Length - li);
        }
        else if (ri < sortedNew.Length)
        {
            Array.Copy(sortedNew, ri, merged, mi, sortedNew.Length - ri);
        }

        return merged;
    }

    private static int CompareSamples(
        SampleData left,
        SampleData right,
        FieldMetadata sortField,
        SortDirection direction)
    {
        var leftValue = GetSortValue(left, sortField);
        var rightValue = GetSortValue(right, sortField);
        var comparison = CompareValues(leftValue, rightValue);
        return direction == SortDirection.Descending ? -comparison : comparison;
    }

    private static object? GetSortValue(SampleData sample, FieldMetadata sortField)
    {
        var target = sortField.IsSynthetic ? (object)sample : sample.Payload;
        return sortField.Getter(target!);
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return EmptyCount;
        }

        if (left == null)
        {
            return ComparisonLess;
        }

        if (right == null)
        {
            return ComparisonGreater;
        }

        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }
}
