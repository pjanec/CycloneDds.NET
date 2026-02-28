using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine;

/// <summary>
/// Stores DDS samples with filtering, sorting, and virtualized views.
/// </summary>
public sealed class SampleStore : ISampleStore, IDisposable
{
    private const int EmptyCount = 0;
    private const int MinIndex = 0;
    private const int MinCount = 0;
    private const int SortWorkerIntervalMs = 50;
    private const int ComparisonLess = -1;
    private const int ComparisonGreater = 1;

    private readonly object _sync = new();
    private readonly List<SampleData> _allSamples = new();
    private readonly Dictionary<Type, TopicSamples> _samplesByTopic = new();
    private readonly List<SampleData> _filteredSamples = new();
    private readonly AutoResetEvent _sortSignal = new(false);
    private readonly CancellationTokenSource _sortCancellation = new();
    private readonly Task _sortTask;

    private Func<SampleData, bool>? _filterPredicate;
    private FieldMetadata? _sortField;
    private SortDirection _sortDirection;
    private bool _sortDirty;
    private bool _disposed;

    private SampleData[] _sortedView = Array.Empty<SampleData>();
    private int _currentFilteredCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleStore"/> class.
    /// </summary>
    public SampleStore()
    {
        _sortTask = Task.Run(SortLoop);
    }

    /// <inheritdoc />
    public IReadOnlyList<SampleData> AllSamples
    {
        get
        {
            lock (_sync)
            {
                return _allSamples.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public int CurrentFilteredCount => Volatile.Read(ref _currentFilteredCount);

    /// <inheritdoc />
    public event Action? OnViewRebuilt;

    /// <inheritdoc />
    public ITopicSamples GetTopicSamples(Type topicType)
    {
        if (topicType == null)
        {
            throw new ArgumentNullException(nameof(topicType));
        }

        lock (_sync)
        {
            if (!_samplesByTopic.TryGetValue(topicType, out var samples))
            {
                samples = new TopicSamples(topicType);
                _samplesByTopic[topicType] = samples;
            }

            return samples;
        }
    }

    /// <inheritdoc />
    public void Append(SampleData sample)
    {
        if (sample == null)
        {
            throw new ArgumentNullException(nameof(sample));
        }

        var shouldSignal = false;

        lock (_sync)
        {
            ThrowIfDisposed();

            _allSamples.Add(sample);

            if (!_samplesByTopic.TryGetValue(sample.TopicMetadata.TopicType, out var topicSamples))
            {
                topicSamples = new TopicSamples(sample.TopicMetadata.TopicType);
                _samplesByTopic[sample.TopicMetadata.TopicType] = topicSamples;
            }

            topicSamples.Add(sample);

            if (_filterPredicate == null || _filterPredicate(sample))
            {
                _filteredSamples.Add(sample);
                Volatile.Write(ref _currentFilteredCount, _filteredSamples.Count);
                _sortDirty = true;
                shouldSignal = true;
            }
        }

        if (shouldSignal)
        {
            _sortSignal.Set();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            _allSamples.Clear();
            _samplesByTopic.Clear();
            _filteredSamples.Clear();
            _sortedView = Array.Empty<SampleData>();
            Volatile.Write(ref _currentFilteredCount, EmptyCount);
        }

        OnViewRebuilt?.Invoke();
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

        var view = Volatile.Read(ref _sortedView);
        if (view.Length == EmptyCount || count == EmptyCount || startIndex >= view.Length)
        {
            return ReadOnlyMemory<SampleData>.Empty;
        }

        var available = view.Length - startIndex;
        var sliceCount = Math.Min(count, available);
        return new ReadOnlyMemory<SampleData>(view, startIndex, sliceCount);
    }

    /// <inheritdoc />
    public void SetFilter(Func<SampleData, bool>? compiledFilterPredicate)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            _filterPredicate = compiledFilterPredicate;
            _filteredSamples.Clear();

            if (_filterPredicate == null)
            {
                _filteredSamples.AddRange(_allSamples);
            }
            else
            {
                foreach (var sample in _allSamples)
                {
                    if (_filterPredicate(sample))
                    {
                        _filteredSamples.Add(sample);
                    }
                }
            }

            Volatile.Write(ref _currentFilteredCount, _filteredSamples.Count);
            _sortDirty = true;
        }

        _sortSignal.Set();
    }

    /// <inheritdoc />
    public void SetSortSpec(FieldMetadata? field, SortDirection direction)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _sortField = field;
            _sortDirection = direction;
            _sortDirty = true;
        }

        _sortSignal.Set();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _sortCancellation.Cancel();
        _sortSignal.Set();

        try
        {
            _sortTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _sortCancellation.Dispose();
        _sortSignal.Dispose();
    }

    private void SortLoop()
    {
        var cancellationToken = _sortCancellation.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            _sortSignal.WaitOne(SortWorkerIntervalMs);

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            FieldMetadata? sortField;
            SortDirection direction;
            SampleData[] snapshot;

            lock (_sync)
            {
                if (!_sortDirty)
                {
                    continue;
                }

                _sortDirty = false;
                sortField = _sortField;
                direction = _sortDirection;
                snapshot = _filteredSamples.ToArray();
            }

            var sorted = SortSnapshot(snapshot, sortField, direction);
            Volatile.Write(ref _sortedView, sorted);
            Volatile.Write(ref _currentFilteredCount, sorted.Length);
            OnViewRebuilt?.Invoke();
        }
    }

    private static SampleData[] SortSnapshot(SampleData[] snapshot, FieldMetadata? sortField, SortDirection direction)
    {
        if (snapshot.Length == EmptyCount)
        {
            return snapshot;
        }

        if (sortField == null)
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

    private static object? GetSortValue(SampleData sample, FieldMetadata sortField)
    {
        var target = sortField.IsSynthetic ? sample : sample.Payload;
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SampleStore));
        }
    }

    private sealed class TopicSamples : ITopicSamples
    {
        private readonly List<SampleData> _samples = new();

        public TopicSamples(Type topicType)
        {
            TopicType = topicType ?? throw new ArgumentNullException(nameof(topicType));
        }

        public Type TopicType { get; }

        public int TotalCount => _samples.Count;

        public IReadOnlyList<SampleData> Samples => _samples;

        public void Add(SampleData sample)
        {
            _samples.Add(sample);
        }
    }
}
